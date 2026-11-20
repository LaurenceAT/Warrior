using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Los controles de pantalla para la version de movil.
//
// Joystick abajo a la izquierda (mover y, empujandolo al fondo, correr) y los
// botones abajo a la derecha. No cambia nada del jugador: los botones escriben
// en GatherInput igual que lo haria el teclado, asi que PlayerControler ni se
// entera de por donde le llegan las ordenes.
//
// Se dibuja solo cuando ModoMovil.Activo (un movil de verdad, o el interruptor
// del menu Warrior > Movil para verlo en el Editor). Donde va cada cosa esta en
// la ficha Resources/AjustesTactiles.
//
// Lee los dedos directamente del Input System (no con botones de interfaz) para
// poder llevar varios dedos a la vez: mover con el izquierdo mientras el derecho
// ataca, que es lo normal en un movil.
//
// Un dedo que cae donde no hay ningun circulo mueve la camara (CamaraDinamica.
// MirarLibre), como en muchos juegos de movil: al soltarlo vuelve sola.
public class ControlesTactiles : MonoBehaviour
{
    public static ControlesTactiles Instancia { get; private set; }

    // El boton de pausa: GameManager lo lee como si fuera Esc.
    private static bool pausaPedida;
    public static bool ConsumirPausa()
    {
        if (!pausaPedida) return false;
        pausaPedida = false;
        return true;
    }

    // ------------------------------------------------------------------ Arranque

    // Al cargar cualquier escena con jugador, si estamos en modo movil se monta.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arranque()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        Montar();
    }

    private static void AlCargarEscena(Scene e, LoadSceneMode m) => Montar();

    // Tambien la llama el menu del Editor para encenderlos sin recargar escena.
    public static void Montar()
    {
        if (!ModoMovil.Activo) { Quitar(); return; }
        if (Instancia != null) return;
        if (Object.FindFirstObjectByType<GatherInput>() == null) return;
        GameObject go = new GameObject("ControlesTactiles");
        Instancia = go.AddComponent<ControlesTactiles>();
    }

    public static void Quitar()
    {
        if (Instancia == null) return;
        Destroy(Instancia.gameObject);
        Instancia = null;
    }

    // ------------------------------------------------------------------ Piezas

    // Un boton en pantalla: su sitio, su dibujo y si lo esta pulsando un dedo.
    private class Pieza
    {
        public AjustesTactiles.Boton ficha;
        public RectTransform rect;
        public Image fondo, filo;
        public Graphic marca;        // el icono, o el texto si no hay icono
        public Graphic aviso;        // el "!" del boton de atacar/usar
        public int dedo = SinDedo;
        public float brillo;
        public bool Pulsado => dedo != SinDedo;
    }

    private const int SinDedo = int.MinValue;

    private AjustesTactiles aj;
    private GatherInput entrada;
    private Canvas lienzo;
    private RectTransform raiz;
    private readonly List<Pieza> piezas = new List<Pieza>();

    // Joystick.
    private RectTransform jBase, jPomo;
    private Image jBaseImg, jPomoImg;
    private int dedoJoystick = SinDedo;
    private Vector2 centroJoystick;     // en pixeles de pantalla
    private Vector2 direccion;
    private float brilloJoystick;

    // La camara: el dedo que arrastra por donde no hay ningun circulo.
    private int dedoCamara = SinDedo;
    private Vector2 camaraInicio;
    private bool camaraMovida;

    // Los dedos de este fotograma y del anterior (para saber cuales son nuevos).
    private struct Dedo { public int id; public Vector2 pos; }
    private readonly List<Dedo> dedos = new List<Dedo>();
    private readonly HashSet<int> dedosAntes = new HashSet<int>();
    private readonly HashSet<int> dedosAhora = new HashSet<int>();

    private void Awake()
    {
        Instancia = this;
        aj = AjustesTactiles.Cargar();
        entrada = Object.FindFirstObjectByType<GatherInput>();
        Construir();
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // ------------------------------------------------------------------ Dibujo

    private void Construir()
    {
        lienzo = gameObject.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por encima del HUD y de la barra del jefe (55), por debajo de los
        // menus (90 y mas), que tienen que taparlos.
        lienzo.sortingOrder = 60;

        CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;

        raiz = new GameObject("Controles", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(transform, false);
        EstiloMenu.Estirar(raiz);

        ConstruirJoystick();
        foreach (AjustesTactiles.Boton b in aj.botones)
        {
            if (b == null || !b.activo) continue;
            piezas.Add(ConstruirBoton(b));
        }
        AplicarZonaSegura();
    }

    private void ConstruirJoystick()
    {
        jBaseImg = EstiloMenu.Caja("JoystickBase", raiz, Color.white);
        jBase = jBaseImg.rectTransform;
        jBaseImg.sprite = DibujosCazadora.Anillo();
        jBase.anchorMin = jBase.anchorMax = Vector2.zero;
        jBase.pivot = new Vector2(0.5f, 0.5f);
        jBase.sizeDelta = new Vector2(aj.tamanoBase, aj.tamanoBase);
        jBase.anchoredPosition = aj.centroJoystick;

        jPomoImg = EstiloMenu.Caja("JoystickPomo", jBase, aj.colorFilo);
        jPomo = jPomoImg.rectTransform;
        jPomoImg.sprite = DibujosCazadora.Circulo();
        jPomo.anchorMin = jPomo.anchorMax = jPomo.pivot = new Vector2(0.5f, 0.5f);
        jPomo.sizeDelta = new Vector2(aj.tamanoPomo, aj.tamanoPomo);
        jPomo.anchoredPosition = Vector2.zero;
    }

    private Pieza ConstruirBoton(AjustesTactiles.Boton b)
    {
        Pieza p = new Pieza { ficha = b };

        Image fondo = EstiloMenu.Caja("Boton " + b.accion, raiz, aj.colorBoton);
        p.fondo = fondo;
        p.rect = fondo.rectTransform;
        fondo.sprite = DibujosCazadora.Circulo();
        p.rect.sizeDelta = new Vector2(b.tamano, b.tamano);
        Anclar(p.rect, b.anclaje, b.posicion);

        Image filo = EstiloMenu.Caja("Filo", p.rect, aj.colorFilo);
        p.filo = filo;
        filo.sprite = DibujosCazadora.Anillo();
        EstiloMenu.Estirar(filo.rectTransform);

        Sprite icono = string.IsNullOrEmpty(b.icono) ? null : Icono(b.icono);
        if (icono != null)
        {
            Image img = EstiloMenu.Caja("Icono", p.rect, aj.colorIcono);
            img.sprite = icono;
            img.preserveAspect = true;
            EstiloMenu.Estirar(img.rectTransform, b.tamano * 0.26f);
            p.marca = img;
        }
        else
        {
            string texto = string.IsNullOrEmpty(b.etiqueta) ? Nombre(b.accion) : b.etiqueta;
            TextMeshProUGUI t = EstiloMenu.Texto(texto, p.rect, b.tamano * 0.22f, aj.colorIcono);
            EstiloMenu.Estirar(t.rectTransform, 6f);
            p.marca = t;
        }

        // El boton grande tambien sirve para usar hogueras, cofres y estatuas:
        // cuando hay algo a tiro cambia la espada por un "!".
        if (b.accion == AjustesTactiles.Accion.Atacar)
        {
            TextMeshProUGUI ex = EstiloMenu.Texto("!", p.rect, b.tamano * 0.55f, new Color(1f, 0.85f, 0.45f));
            ex.fontStyle = FontStyles.Bold;
            EstiloMenu.Estirar(ex.rectTransform, 6f);
            ex.gameObject.SetActive(false);
            p.aviso = ex;
        }
        return p;
    }

    // Coloca un rect a "pos" pixeles de la esquina que diga el anclaje.
    private static void Anclar(RectTransform r, AjustesTactiles.Esquina e, Vector2 pos)
    {
        switch (e)
        {
            case AjustesTactiles.Esquina.AbajoIzquierda:
                r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
                r.anchoredPosition = new Vector2(pos.x, pos.y);
                break;
            case AjustesTactiles.Esquina.AbajoDerecha:
                r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
                r.anchoredPosition = new Vector2(-pos.x, pos.y);
                break;
            case AjustesTactiles.Esquina.ArribaIzquierda:
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(pos.x, -pos.y);
                break;
            case AjustesTactiles.Esquina.ArribaDerecha:
                r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
                r.anchoredPosition = new Vector2(-pos.x, -pos.y);
                break;
            default:
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.anchoredPosition = new Vector2(pos.x, pos.y);
                break;
        }
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    // Mete todo dentro de la zona segura (muesca y esquinas redondeadas).
    private void AplicarZonaSegura()
    {
        if (!aj.zonaSegura) return;
        Rect s = Screen.safeArea;
        float escala = Mathf.Max(0.01f, lienzo.scaleFactor);
        float izq = s.xMin / escala;
        float abajo = s.yMin / escala;
        float der = (Screen.width - s.xMax) / escala;
        float arriba = (Screen.height - s.yMax) / escala;
        raiz.offsetMin = new Vector2(izq, abajo);
        raiz.offsetMax = new Vector2(-der, -arriba);
    }

    // ------------------------------------------------------------------ Cada fotograma

    // Al girar el movil (o al cambiar el escalado) hay que rehacer la zona segura.
    private Vector2Int pantallaAntes;

    private void Update()
    {
        if (pantallaAntes.x != Screen.width || pantallaAntes.y != Screen.height)
        {
            pantallaAntes = new Vector2Int(Screen.width, Screen.height);
            AplicarZonaSegura();
        }
        bool jugando = Jugando();
        raiz.gameObject.SetActive(jugando);
        if (!jugando)
        {
            Soltar();
            if (entrada != null && mandabaAlgo) entrada.AplicarTactil(Vector2.zero, false, false);
            mandabaAlgo = false;
            return;
        }

        LeerDedos();
        RepartirDedos();
        Mandar();
        Pintar();
    }

    // Los controles desaparecen con cualquier menu abierto: no se puede jugar y
    // ademas taparian los botones del menu.
    private bool Jugando()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return false;
        if (PantallaMuerte.Activa || PantallaCarga.Cargando || Cinematica.Activa) return false;
        if (RuedaImbuir.Abierta || MenuHoguera.Abierto || CuadroPista.Abierto) return false;
        if (MenuPausa.LibroAbierto || TiendaTotem.Abierta || PantallaDesafio.Abierta) return false;
        return true;
    }

    // Que dedos hay apoyados ahora mismo. En el Editor, el raton cuenta como un
    // dedo (id -1) para poder probar la disposicion sin un movil.
    private void LeerDedos()
    {
        dedos.Clear();
        dedosAhora.Clear();

        Touchscreen ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (TouchControl t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                int id = t.touchId.ReadValue();
                dedos.Add(new Dedo { id = id, pos = t.position.ReadValue() });
                dedosAhora.Add(id);
            }
        }

        Mouse m = Mouse.current;
        if (m != null && m.leftButton.isPressed)
        {
            dedos.Add(new Dedo { id = -1, pos = m.position.ReadValue() });
            dedosAhora.Add(-1);
        }
    }

    // Quien se queda con cada dedo: los que ya tenian uno lo mantienen hasta que
    // se levante, y los dedos nuevos buscan boton (o el joystick).
    private void RepartirDedos()
    {
        // 1. Los que ya estaban: si el dedo se ha levantado, se suelta.
        foreach (Pieza p in piezas)
            if (p.Pulsado && !dedosAhora.Contains(p.dedo)) p.dedo = SinDedo;
        if (dedoJoystick != SinDedo && !dedosAhora.Contains(dedoJoystick)) SoltarJoystick();
        if (dedoCamara != SinDedo && !dedosAhora.Contains(dedoCamara)) SoltarCamara();

        // 2. Los dedos nuevos.
        foreach (Dedo d in dedos)
        {
            if (dedosAntes.Contains(d.id)) continue;      // no es nuevo
            if (Ocupado(d.id)) continue;

            Pieza tocada = PiezaEn(d.pos);
            if (tocada != null)
            {
                tocada.dedo = d.id;
                if (!tocada.ficha.mantener) Disparar(tocada.ficha.accion);
                continue;
            }
            if (dedoJoystick == SinDedo && EnZonaJoystick(d.pos)) { CogerJoystick(d.id, d.pos); continue; }

            // Por donde no hay ningun circulo: ese dedo mueve la camara.
            if (aj.camaraConDedo && dedoCamara == SinDedo)
            {
                dedoCamara = d.id;
                camaraInicio = d.pos;
                camaraMovida = false;
            }
        }

        // 3. El joystick sigue al dedo que lo tenga.
        if (dedoJoystick != SinDedo)
        {
            foreach (Dedo d in dedos)
                if (d.id == dedoJoystick) { MoverJoystick(d.pos); break; }
        }

        // 4. Y la camara, al suyo.
        if (dedoCamara != SinDedo)
        {
            foreach (Dedo d in dedos)
                if (d.id == dedoCamara) { MoverCamara(d.pos); break; }
        }

        dedosAntes.Clear();
        foreach (int id in dedosAhora) dedosAntes.Add(id);
    }

    private bool Ocupado(int id)
    {
        if (dedoJoystick == id || dedoCamara == id) return true;
        foreach (Pieza p in piezas) if (p.dedo == id) return true;
        return false;
    }

    // El boton (redondo) que haya bajo ese punto de la pantalla.
    private Pieza PiezaEn(Vector2 pantalla)
    {
        Pieza mejor = null;
        float mejorDist = float.MaxValue;
        foreach (Pieza p in piezas)
        {
            Vector2 centro = RectTransformUtility.WorldToScreenPoint(null, p.rect.position);
            float radio = p.rect.rect.width * 0.5f * lienzo.scaleFactor;
            float dist = Vector2.Distance(centro, pantalla);
            if (dist > radio || dist >= mejorDist) continue;
            mejor = p;
            mejorDist = dist;
        }
        return mejor;
    }

    private bool EnZonaJoystick(Vector2 pantalla)
    {
        // Con el joystick fijo solo se coge tocando su circulo (un poco agrandado):
        // asi el resto de la pantalla queda libre para mover la camara.
        if (!aj.seguirDedo)
        {
            float radio = aj.tamanoBase * 0.5f * aj.margenJoystick * lienzo.scaleFactor;
            return Vector2.Distance(pantalla, CentroFijoJoystick()) <= radio;
        }
        Rect z = aj.zonaJoystick;
        Vector2 f = new Vector2(pantalla.x / Mathf.Max(1f, Screen.width), pantalla.y / Mathf.Max(1f, Screen.height));
        return f.x >= z.xMin && f.x <= z.xMax && f.y >= z.yMin && f.y <= z.yMax;
    }

    // ------------------------------------------------------------------ Camara

    // Arrastrando por donde no hay botones, la camara se asoma hacia ese lado
    // (como en muchos juegos de movil). Al soltar vuelve sola.
    private void MoverCamara(Vector2 pantalla)
    {
        Vector2 d = pantalla - camaraInicio;
        if (!camaraMovida)
        {
            if (d.magnitude < aj.arrastreMinimo) return;
            camaraMovida = true;
        }
        float x = d.x / Mathf.Max(1f, Screen.width * 0.5f) * aj.alcanceCamara;
        float y = aj.camaraVertical ? d.y / Mathf.Max(1f, Screen.height * 0.5f) * aj.alcanceCamara : 0f;
        CamaraDinamica.MirarLibre(new Vector2(x, y));
    }

    private void SoltarCamara()
    {
        dedoCamara = SinDedo;
        if (camaraMovida) CamaraDinamica.MirarLibre(Vector2.zero);
        camaraMovida = false;
    }

    // ------------------------------------------------------------------ Joystick

    private void CogerJoystick(int id, Vector2 pantalla)
    {
        dedoJoystick = id;
        // Con "seguirDedo" el joystick aparece donde pongas el dedo: no hay que
        // buscar un circulo a ciegas, que es lo que peor sienta en un movil.
        centroJoystick = aj.seguirDedo ? pantalla : CentroFijoJoystick();
        if (aj.seguirDedo) jBase.anchoredPosition = EnLienzo(centroJoystick);
        MoverJoystick(pantalla);
    }

    private void MoverJoystick(Vector2 pantalla)
    {
        float radio = aj.tamanoBase * 0.5f * lienzo.scaleFactor;
        Vector2 v = (pantalla - centroJoystick) / Mathf.Max(1f, radio);
        float largo = v.magnitude;
        if (largo > 1f) v /= largo;

        float zm = aj.zonaMuerta;
        direccion = largo <= zm ? Vector2.zero : v.normalized * Mathf.InverseLerp(zm, 1f, Mathf.Min(largo, 1f));
        jPomo.anchoredPosition = v * (aj.tamanoBase * 0.5f - aj.tamanoPomo * 0.25f);
    }

    private void SoltarJoystick()
    {
        dedoJoystick = SinDedo;
        direccion = Vector2.zero;
        if (jPomo != null) jPomo.anchoredPosition = Vector2.zero;
        if (jBase != null) jBase.anchoredPosition = aj.centroJoystick;
    }

    private Vector2 CentroFijoJoystick()
    {
        float escala = Mathf.Max(0.01f, lienzo.scaleFactor);
        Vector2 local = aj.centroJoystick + raiz.offsetMin;
        return local * escala;
    }

    // De pixeles de pantalla a las coordenadas del lienzo (1920x1080).
    private Vector2 EnLienzo(Vector2 pantalla) => pantalla / Mathf.Max(0.01f, lienzo.scaleFactor) - raiz.offsetMin;

    // ------------------------------------------------------------------ Mandar al jugador

    // Los toques sueltos: exactamente lo que harian las teclas.
    private void Disparar(AjustesTactiles.Accion a)
    {
        if (entrada == null) entrada = Object.FindFirstObjectByType<GatherInput>();
        if (a == AjustesTactiles.Accion.Camara) { CamaraCombate.Alternar(); return; }
        if (a == AjustesTactiles.Accion.Pausa) { pausaPedida = true; return; }
        if (entrada == null) return;
        switch (a)
        {
            // El boton grande es "atacar o usar": si hay una hoguera, un cofre o
            // una estatua a tiro y estas en el suelo, los usa; si no, ataca.
            case AjustesTactiles.Accion.Atacar:
                if (HayAlgoQueUsar()) entrada.IsInteracting = true;
                else entrada.IsAttacking = true;
                break;
            case AjustesTactiles.Accion.Saltar: entrada.IsJumping = true; break;
            case AjustesTactiles.Accion.Esquivar: entrada.IsDodging = true; break;
            case AjustesTactiles.Accion.Bloquear: entrada.IsKicking = true; break;
            case AjustesTactiles.Accion.FrascoSangre: entrada.IsHealing = true; break;
            case AjustesTactiles.Accion.FrascoMana: entrada.IsManaPotion = true; break;
            case AjustesTactiles.Accion.Interactuar: entrada.IsInteracting = true; break;
            case AjustesTactiles.Accion.Imbuir: entrada.IsTogglingWeapon = true; break;
        }
    }

    private bool HayAlgoQueUsar()
    {
        if (entrada == null) return false;
        return Interacciones.Buscar(entrada.transform.position) != null;
    }

    // Si no hay ningun dedo puesto no se manda nada, para no pisar al teclado:
    // asi en el Editor se puede probar la disposicion y seguir jugando con WASD.
    // Al levantar el dedo se manda un cero una ultima vez (si no, el personaje se
    // quedaria andando solo).
    private bool mandabaAlgo;

    private void Mandar()
    {
        if (entrada == null) return;
        bool bloquear = Mantenido(AjustesTactiles.Accion.Bloquear);
        bool correr = Mantenido(AjustesTactiles.Accion.Correr)
                      || (dedoJoystick != SinDedo && direccion.magnitude >= aj.empujeParaCorrer);
        bool algo = dedoJoystick != SinDedo || correr || bloquear;
        if (algo || mandabaAlgo) entrada.AplicarTactil(direccion, correr, bloquear);
        mandabaAlgo = algo;
    }

    private bool Mantenido(AjustesTactiles.Accion a)
    {
        foreach (Pieza p in piezas)
            if (p.ficha.accion == a && p.ficha.mantener && p.Pulsado) return true;
        return false;
    }

    private void Soltar()
    {
        foreach (Pieza p in piezas) p.dedo = SinDedo;
        SoltarJoystick();
        SoltarCamara();
        dedosAntes.Clear();
    }

    // ------------------------------------------------------------------ Como se ven

    private float pulsoUsar;

    private void Pintar()
    {
        float paso = aj.suavizado <= 0f ? 1f : Time.unscaledDeltaTime / aj.suavizado;
        // Lo que el player tiene a tiro ahora mismo (lo busca el propio player).
        bool usar = Interacciones.Actual != null;
        pulsoUsar += Time.unscaledDeltaTime * 4f;
        foreach (Pieza p in piezas)
        {
            p.brillo = Mathf.MoveTowards(p.brillo, p.Pulsado ? 1f : 0f, paso);
            Opacidad(p.fondo, p.filo, p.marca, p.brillo);
            float crece = 1f + 0.08f * p.brillo;
            p.rect.localScale = new Vector3(crece, crece, 1f);
            if (p.aviso != null) PintarAviso(p, usar);
        }
        brilloJoystick = Mathf.MoveTowards(brilloJoystick, dedoJoystick != SinDedo ? 1f : 0f, paso);
        Opacidad(jBaseImg, jPomoImg, null, brilloJoystick);
    }

    // Con algo a tiro: se esconde la espada, sale el "!" latiendo y el aro se
    // pone dorado. Sin nada a tiro, todo vuelve a como estaba.
    private void PintarAviso(Pieza p, bool usar)
    {
        if (p.aviso.gameObject.activeSelf != usar) p.aviso.gameObject.SetActive(usar);
        if (p.marca != null && p.marca.gameObject.activeSelf == usar) p.marca.gameObject.SetActive(!usar);

        Color filo = usar ? new Color(1f, 0.82f, 0.38f) : aj.colorFilo;
        p.filo.color = new Color(filo.r, filo.g, filo.b, p.filo.color.a);
        if (!usar) return;

        float alfa = Mathf.Lerp(aj.opacidad, aj.opacidadPulsado, p.brillo);
        Alfa(p.aviso, Mathf.Clamp01(alfa + 0.3f) * (0.75f + 0.25f * Mathf.Sin(pulsoUsar)));
    }

    private void Opacidad(Graphic a, Graphic b, Graphic c, float brillo)
    {
        float alfa = Mathf.Lerp(aj.opacidad, aj.opacidadPulsado, brillo);
        Alfa(a, alfa);
        Alfa(b, alfa);
        Alfa(c, alfa);
    }

    private static void Alfa(Graphic g, float alfa)
    {
        if (g == null) return;
        Color col = g.color;
        col.a = alfa;
        g.color = col;
    }

    // El icono de RecursosRPG, si esa clave existe.
    private static Sprite Icono(string clave)
    {
        RecursosRPG r = RecursosRPG.Get();
        return r != null ? r.Icono(clave) : null;
    }

    private static string Nombre(AjustesTactiles.Accion a)
    {
        switch (a)
        {
            case AjustesTactiles.Accion.Atacar: return "ATACAR";
            case AjustesTactiles.Accion.Saltar: return "SALTAR";
            case AjustesTactiles.Accion.Esquivar: return "ESQUIVA";
            case AjustesTactiles.Accion.Bloquear: return "GUARDIA";
            case AjustesTactiles.Accion.FrascoSangre: return "VIDA";
            case AjustesTactiles.Accion.FrascoMana: return "MANA";
            case AjustesTactiles.Accion.Interactuar: return "USAR";
            case AjustesTactiles.Accion.Imbuir: return "IMBUIR";
            case AjustesTactiles.Accion.Camara: return "FIJAR";
            case AjustesTactiles.Accion.Pausa: return "II";
            case AjustesTactiles.Accion.Correr: return "CORRER";
            default: return a.ToString().ToUpperInvariant();
        }
    }
}
