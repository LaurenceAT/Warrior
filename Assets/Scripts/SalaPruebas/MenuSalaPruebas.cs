#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Sala de pruebas (SOLO Editor): el menu F1, con el estilo de los menus del juego
// (EstiloMenu) y navegacion por capas (CapasMenu): clic o Enter entra, clic
// derecho o Esc vuelve un paso. Va a la izquierda, entre el HUD de arriba y la
// barra del jefe (se recoloca solo para no taparlos). Mientras esta abierto el
// juego sigue, pero el jugador no recibe teclas (Cinematica.Activa).
[DefaultExecutionOrder(1000)]
public class MenuSalaPruebas : MonoBehaviour
{
    private enum Pantalla { Principal, Invocar, Jugador, Jefe, Ataques, Tiempo, Ver, Terreno }

    public static bool Abierto { get; private set; }
    public static MenuSalaPruebas Actual { get; private set; }

    private SalaPruebas sala;
    private AjustesSalaPruebas A => sala.Ajustes;
    private CanvasGroup grupo;
    private RectTransform lienzo, panel, contenido;
    private TextMeshProUGUI titulo, aviso;
    private ScrollRect scroll;
    private readonly CapasMenu capas = new CapasMenu();
    private Pantalla pantalla;
    private int elegida;      // entrada del catalogo elegida
    private bool modoBucle;
    private float finAviso;
    private readonly List<Button> botones = new List<Button>();

    public RectTransform Panel => panel;

    private void Start()
    {
        sala = GetComponent<SalaPruebas>();
        Actual = this;
        Montar();
        Cerrar();
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        if (Abierto) Cinematica.Activa = false;
        Abierto = false;
    }

    // ------------------------------------------------------------------ Montaje

    private void Montar()
    {
        GameObject go = new GameObject("MenuSalaPruebas");
        go.transform.SetParent(transform, false);
        grupo = EstiloMenu.Lienzo(go, 60);
        // Sin velo: el combate se sigue viendo.
        Transform velo = go.transform.Find("Velo");
        if (velo != null) velo.gameObject.SetActive(false);
        lienzo = (RectTransform)go.transform;

        Image filo = EstiloMenu.Caja("Panel", go.transform, EstiloMenu.Filo);
        panel = filo.rectTransform;
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        Image fondo = EstiloMenu.Caja("Fondo", filo.transform, EstiloMenu.Fondo);
        EstiloMenu.Estirar(fondo.rectTransform, 2f);
        RectTransform interior = fondo.rectTransform;

        titulo = EstiloMenu.Texto("", interior, A.tamanoTexto + 4f, EstiloMenu.Titulo);
        titulo.fontStyle = FontStyles.Bold;
        EstiloMenu.Arriba(titulo.rectTransform, -8f, A.tamanoTexto + 14f, 12f);
        EstiloMenu.Separador(interior, -(A.tamanoTexto + 26f), 0.8f);

        aviso = EstiloMenu.Texto("", interior, A.tamanoTexto - 4f, EstiloMenu.Malo);
        aviso.textWrappingMode = TextWrappingModes.Normal;
        RectTransform ra = aviso.rectTransform;
        ra.anchorMin = new Vector2(0f, 0f);
        ra.anchorMax = new Vector2(1f, 0f);
        ra.pivot = new Vector2(0.5f, 0f);
        ra.offsetMin = new Vector2(14f, 8f);
        ra.offsetMax = new Vector2(-14f, 8f + A.tamanoTexto * 2.6f);

        // Zona con desplazamiento: las listas largas (ataques, catalogo) caben.
        GameObject vista = new GameObject("Vista", typeof(RectTransform), typeof(RectMask2D));
        vista.transform.SetParent(interior, false);
        RectTransform rv = (RectTransform)vista.transform;
        rv.anchorMin = Vector2.zero;
        rv.anchorMax = Vector2.one;
        rv.offsetMin = new Vector2(8f, 12f + A.tamanoTexto * 2.6f);
        rv.offsetMax = new Vector2(-8f, -(A.tamanoTexto + 34f));
        Image fondoVista = vista.AddComponent<Image>();
        fondoVista.color = new Color(0f, 0f, 0f, 0.001f);

        GameObject c = new GameObject("Contenido", typeof(RectTransform));
        c.transform.SetParent(vista.transform, false);
        contenido = (RectTransform)c.transform;
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vl = c.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2f;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = vista.AddComponent<ScrollRect>();
        scroll.viewport = rv;
        scroll.content = contenido;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    // ------------------------------------------------------------------ Abrir y cerrar

    public void Abrir()
    {
        Abierto = true;
        grupo.alpha = 1f;
        grupo.interactable = true;
        grupo.blocksRaycasts = true;
        capas.Vaciar();
        Mostrar(Pantalla.Principal);
        SonidoMenu.Abrir();
    }

    public void Cerrar()
    {
        if (Abierto) Cinematica.Activa = false;
        Abierto = false;
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.f1Key.wasPressedThisFrame)
        {
            if (Abierto) Cerrar(); else Abrir();
            return;
        }
        if (!Abierto) return;
        Cinematica.Activa = true;
        if (CapasMenu.PulsadoAtras() && !capas.Atras()) Cerrar();
        if (Time.unscaledTime > finAviso && aviso.text != "") aviso.text = "";
    }

    private void LateUpdate()
    {
        if (!Abierto) return;
        Cursor.visible = true;
        Colocar();
    }

    // ------------------------------------------------------------------ Sitio en pantalla

    // Entre lo mas bajo del HUD de arriba a la izquierda y lo mas alto de la barra
    // del jefe (con el hueco de las frases de la Cazadora encima).
    private void Colocar()
    {
        Vector2 tam = lienzo.rect.size;
        float arriba = tam.y - A.margen;
        PlayerHud hud = FindFirstObjectByType<PlayerHud>();
        if (hud != null)
            foreach (Graphic g in hud.GetComponentsInChildren<Graphic>())
            {
                if (!g.enabled || g.canvasRenderer.GetInheritedAlpha() < 0.01f) continue;
                arriba = Mathf.Min(arriba, Abajo(g.rectTransform) - A.margen * 0.5f);
            }
        float abajo = A.margen;
        BarraJefe barra = FindFirstObjectByType<BarraJefe>();
        if (barra != null && barra.Marco != null && barra.Marco.gameObject.activeInHierarchy)
            abajo = Mathf.Max(abajo, ArribaDe(barra.Marco) + AjustesInterfaz.Get().separacionSubtitulo + A.margen);
        float alto = Mathf.Max(160f, arriba - abajo);
        panel.anchoredPosition = new Vector2(A.margen, abajo);
        panel.sizeDelta = new Vector2(A.anchoMenu, alto);
    }

    private float Abajo(RectTransform r) => Esquinas(r).Min(p => p.y);
    private float ArribaDe(RectTransform r) => Esquinas(r).Max(p => p.y);

    // Esquinas de un RectTransform de otro lienzo, en las coordenadas de este
    // (origen abajo a la izquierda).
    private IEnumerable<Vector2> Esquinas(RectTransform r)
    {
        Vector3[] e = new Vector3[4];
        r.GetWorldCorners(e);
        Vector2 medio = lienzo.rect.size * 0.5f;
        return e.Select(p => (Vector2)lienzo.InverseTransformPoint(p) + medio);
    }

    // ------------------------------------------------------------------ Pantallas

    private void Entrar(Pantalla p)
    {
        Pantalla volver = pantalla;
        capas.Entrar(() => Mostrar(volver));
        Mostrar(p);
    }

    private void Mostrar(Pantalla p, int seleccion = 0)
    {
        pantalla = p;
        foreach (Transform t in contenido) Destroy(t.gameObject);
        botones.Clear();
        switch (p)
        {
            case Pantalla.Principal:
                titulo.text = "SALA DE PRUEBAS";
                Opcion("Invocar…", () => Entrar(Pantalla.Invocar));
                Opcion("Jugador…", () => Entrar(Pantalla.Jugador));
                Opcion("Jefe…", () => Entrar(Pantalla.Jefe));
                Opcion("Ataques…", () => Entrar(Pantalla.Ataques));
                Opcion("Tiempo y dificultad…", () => Entrar(Pantalla.Tiempo));
                Opcion("Ver…", () => Entrar(Pantalla.Ver));
                Opcion("Terreno…", () => Entrar(Pantalla.Terreno));
                Opcion("Cerrar (F1)", Cerrar);
                break;

            case Pantalla.Invocar:
                titulo.text = "INVOCAR";
                List<CatalogoPruebas.Entrada> lista = Lista;
                CatalogoPruebas.Entrada e = Elegida;
                Opcion(e != null ? "Invocar: " + e.nombre : "Invocar", () => { sala.Invocar(Elegida); AvisoTerreno(Elegida); Rehacer(); });
                Opcion("Reaparecer", () => { sala.Reaparecer(); Rehacer(); }, sala.Invocado != null);
                Opcion("Limpiar", () => { sala.Limpiar(); Rehacer(); }, sala.Invocado != null);
                Rotulo("Jefes");
                for (int i = 0; i < lista.Count; i++) if (lista[i].EsJefe) Entrada(i, lista[i]);
                Rotulo("Enemigos");
                for (int i = 0; i < lista.Count; i++) if (!lista[i].EsJefe) Entrada(i, lista[i]);
                break;

            case Pantalla.Jugador:
                titulo.text = "JUGADOR";
                Opcion("Invencible: " + SiNo(sala.Invencible), () => { sala.PonerInvencible(!sala.Invencible); Rehacer(); });
                Opcion("Vida infinita: " + SiNo(sala.VidaInfinita), () => { sala.VidaInfinita = !sala.VidaInfinita; Rehacer(); });
                Opcion("Estamina infinita: " + SiNo(sala.EstaminaInfinita), () => { sala.EstaminaInfinita = !sala.EstaminaInfinita; Rehacer(); });
                Opcion("Volver al centro y restaurar", () => sala.AlCentro());
                break;

            case Pantalla.Jefe:
                titulo.text = "JEFE";
                bool hay = sala.Salud != null;
                bool jefe = sala.Jefe != null;
                Opcion("Matar de un golpe", () => { sala.Matar(); Rehacer(); }, hay);
                Opcion($"Bajar vida ({A.bajarVida:P0})", () => sala.BajarVida(), hay);
                if (sala.Cazadora != null)
                {
                    Opcion("Saltar a la fase 2", () => sala.SaltarAFase(2));
                    Opcion("Saltar a la fase 3", () => sala.SaltarAFase(3));
                }
                else Opcion("Saltar a la fase 2", () => { sala.SaltarAFase(2); Rehacer(); }, jefe && sala.FaseJefe < 2);
                Opcion("Empezar en fase 2: " + SiNo(sala.EmpezarEnFase2), () => { sala.EmpezarEnFase2 = !sala.EmpezarEnFase2; Rehacer(); });
                if (!hay) Rotulo("Invoca algo primero.");
                break;

            case Pantalla.Ataques:
                titulo.text = "ATAQUES";
                if (sala.Jefe == null) { Rotulo("Invoca un jefe primero."); break; }
                Opcion("Bucle: " + SiNo(modoBucle) + (string.IsNullOrEmpty(sala.AtaqueBucle) ? "" : "  (" + sala.AtaqueBucle + ")"), () =>
                {
                    modoBucle = !modoBucle;
                    if (!modoBucle) sala.Bucle(null);
                    Rehacer();
                });
                Rotulo(modoBucle ? $"Clic: repetir en bucle (pausa extra {A.pausaBucle:0.#} s)" : "Clic: lanzar una vez");
                foreach (string a in sala.Jefe.AtaquesPrueba)
                {
                    string ataque = a;
                    Opcion(ataque, () =>
                    {
                        if (modoBucle) { sala.Bucle(ataque); Rehacer(); }
                        else sala.LanzarAtaque(ataque);
                    }, true, modoBucle && sala.AtaqueBucle == ataque);
                }
                break;

            case Pantalla.Tiempo:
                titulo.text = "TIEMPO Y DIFICULTAD";
                foreach (float v in new[] { 1f, 0.5f, 0.25f })
                {
                    float vel = v;
                    Opcion("Velocidad x" + vel, () => { sala.PonerVelocidad(vel); Rehacer(); }, true, Mathf.Approximately(sala.Velocidad, vel));
                }
                Opcion("Normal", () => { sala.PonerDificil(false); Rehacer(); }, true, !sala.Dificil);
                Opcion("Difícil", () => { sala.PonerDificil(true); Avisar("Difícil: la vida del jefe cambia al reaparecer.", false); Rehacer(); }, true, sala.Dificil);
                break;

            case Pantalla.Ver:
                titulo.text = "VER";
                Opcion("Cajas de golpe: " + SiNo(sala.VerCajas), () => { sala.PonerVerCajas(!sala.VerCajas); Rehacer(); });
                Opcion("Estado actual: " + SiNo(sala.VerEstado), () => { sala.VerEstado = !sala.VerEstado; Rehacer(); });
                Opcion("Cuenta atrás: " + SiNo(sala.VerCuenta), () => { sala.VerCuenta = !sala.VerCuenta; Rehacer(); });
                break;

            case Pantalla.Terreno:
                titulo.text = "TERRENO";
                TerrenoPruebas t = sala.Terreno;
                for (int i = 0; i < t.Cantidad; i++)
                {
                    int n = i;
                    Opcion(t.Nombre(n) + ": " + (t.Puesta(n) ? "puesta" : "quitada"), () =>
                    {
                        if (t.Puesta(n)) t.Quitar(n);
                        else { string choque = t.Poner(n); if (choque != null) Avisar(choque); }
                        Rehacer();
                    }, true, t.Puesta(n));
                }
                Opcion("Poner todas", () => { List<string> avisos = t.PonerTodas(); if (avisos.Count > 0) Avisar(string.Join(" ", avisos)); Rehacer(); });
                Opcion("Limpiar terreno", () => { t.Limpiar(); Rehacer(); }, t.Puestas > 0);
                break;
        }
        scroll.verticalNormalizedPosition = 1f;
        Seleccionar(seleccion);
    }

    // Vuelve a pintar la pantalla actual sin perder la opcion resaltada.
    private void Rehacer()
    {
        GameObject actual = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int i = botones.FindIndex(b => b != null && b.gameObject == actual);
        Mostrar(pantalla, Mathf.Max(0, i));
    }

    private void Seleccionar(int i)
    {
        if (EventSystem.current == null || botones.Count == 0) return;
        EventSystem.current.SetSelectedGameObject(botones[Mathf.Clamp(i, 0, botones.Count - 1)].gameObject);
    }

    // ------------------------------------------------------------------ Piezas

    private List<CatalogoPruebas.Entrada> Lista => sala.Catalogo != null
        ? sala.Catalogo.entradas.Where(x => x != null && x.prefab != null).ToList() : new List<CatalogoPruebas.Entrada>();

    private CatalogoPruebas.Entrada Elegida
    {
        get { List<CatalogoPruebas.Entrada> l = Lista; return l.Count == 0 ? null : l[Mathf.Clamp(elegida, 0, l.Count - 1)]; }
    }

    private void Entrada(int i, CatalogoPruebas.Entrada e)
    {
        Opcion(e.nombre, () =>
        {
            elegida = i;
            if (e.asumeSueloPlano && sala.Terreno.Puestas > 0) AvisoTerreno(e);
            Rehacer();
        }, true, i == elegida);
    }

    private void AvisoTerreno(CatalogoPruebas.Entrada e)
    {
        if (e == null || !e.asumeSueloPlano) return;
        Avisar(string.IsNullOrEmpty(e.avisoTerreno) ? e.nombre + " supone suelo plano: con plataformas, algunos ataques pueden fallar." : e.avisoTerreno);
    }

    private Button Opcion(string texto, UnityEngine.Events.UnityAction accion, bool activa = true, bool marcada = false)
    {
        Button b = EstiloMenu.Opcion(contenido, texto, () => { if (activa) accion(); else SonidoMenu.Error(); },
                                     A.altoOpcion, A.tamanoTexto, null, TextAlignmentOptions.Left);
        OpcionEstilo o = b.GetComponent<OpcionEstilo>();
        o.animar = true;
        o.PonerActiva(activa);
        o.PonerMarcada(marcada);
        botones.Add(b);
        return b;
    }

    private void Rotulo(string texto)
    {
        TextMeshProUGUI t = EstiloMenu.Texto(texto, contenido, A.tamanoTexto - 4f, EstiloMenu.TextoApagado);
        t.alignment = TextAlignmentOptions.Left;
        t.margin = new Vector4(16f, 0f, 8f, 0f);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = A.altoOpcion * 0.8f;
    }

    private void Avisar(string texto, bool error = true)
    {
        aviso.text = texto;
        aviso.color = error ? EstiloMenu.Malo : EstiloMenu.TextoNormal;
        finAviso = Time.unscaledTime + 5f;
        if (error) SonidoMenu.Error();
    }

    private static string SiNo(bool si) => si ? "SÍ" : "NO";

    // Para las pruebas automaticas: el ultimo aviso.
    public string UltimoAviso => aviso != null ? aviso.text : "";
}
#endif
