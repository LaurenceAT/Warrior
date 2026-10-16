using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Guia de controles la primera vez que se entra a cualquier desafio: las
// tarjetas "esenciales" de GuiaControles, en pequeno, sobre el juego oscurecido.
// "Anterior", "Siguiente" / "Empezar" y "Saltar" (Esc y Tab tambien saltan).
// Mientras se ve, el juego esta en pausa (el combate no empieza y el tiempo del
// desafio no corre) y la pausa normal no se abre. Al terminarla o saltarla se
// pone la marca global "guia_controles_vista" (globales.json): ya no vuelve a
// salir (ni al reiniciar, ni al morir, ni en otro desafio). Verla desde
// "Controles y guia" no cambia la marca.
public class GuiaDesafio : MonoBehaviour
{
    public const string Marca = "guia_controles_vista";
#if UNITY_EDITOR
    // Las pruebas automaticas (linea de comandos) no la ven, salvo la suya (PruebaControles).
    public const string ClaveSinGuia = "Warrior.PruebasSinGuiaDesafio";
#endif

    private static GuiaDesafio instancia;
    // Tambien el fotograma en que se cierra: el Esc que la salta no abre la pausa.
    public static bool Abierta => (instancia != null && instancia.abierta) || cerradaEnFrame == Time.frameCount;
    private static int cerradaEnFrame = -1;
    public static int Paso => instancia != null ? instancia.paso : -1;
    public static int Total => instancia != null ? instancia.tarjetas.Count : 0;

    private List<GuiaControles.Tarjeta> tarjetas;
    private TarjetaControl tarjeta;
    private TextMeshProUGUI contador;
    private Button anterior, siguiente, saltar;
    private CanvasGroup grupo;
    private bool abierta;
    private int paso;
    private float escalaPrevia = 1f;
    private int abiertaEnFrame;

    public static bool Pendiente => !Globales.Marca(Marca);

    // La lanza ModoDesafio en cuanto el jugador esta en la hoguera y la carga
    // ha terminado. No hace nada si ya se vio.
    public static void MostrarSiToca()
    {
        if (!Pendiente || instancia != null) return;
#if UNITY_EDITOR
        if (Application.isBatchMode && UnityEditor.SessionState.GetBool(ClaveSinGuia, false)) return;
#endif
        GuiaControles g = GuiaControles.Get();
        if (g == null) return;
        List<GuiaControles.Tarjeta> l = g.Esenciales();
        if (l.Count == 0) return;
        instancia = Crear(g, l);
    }

    private static GuiaDesafio Crear(GuiaControles g, List<GuiaControles.Tarjeta> l)
    {
        GameObject go = new GameObject("GuiaDesafio");
        GuiaDesafio d = go.AddComponent<GuiaDesafio>();
        d.tarjetas = l;
        d.grupo = EstiloMenu.Lienzo(go, 150);

        RectTransform panel = EstiloMenu.Panel(go.transform, "", g.tamanoPequena, g.posicionPequena);
        // La tarjeta ocupa el panel menos la fila de botones.
        RectTransform zona = new GameObject("Tarjeta", typeof(RectTransform)).GetComponent<RectTransform>();
        zona.SetParent(panel, false);
        zona.anchorMin = Vector2.zero; zona.anchorMax = Vector2.one;
        zona.offsetMin = new Vector2(0f, 84f); zona.offsetMax = new Vector2(0f, -6f);
        float escala = Mathf.Clamp(g.tamanoPequena.x / 980f, 0.55f, 1f);
        d.tarjeta = TarjetaControl.Crear(zona, escala);

        // Abajo: contador y botones.
        d.contador = EstiloMenu.Texto("", panel, 22f, EstiloMenu.TextoApagado);
        RectTransform rc = d.contador.rectTransform;
        rc.anchorMin = rc.anchorMax = new Vector2(0f, 0f);
        rc.pivot = new Vector2(0f, 0f);
        rc.sizeDelta = new Vector2(120f, 56f);
        rc.anchoredPosition = new Vector2(22f, 14f);

        RectTransform fila = new GameObject("Botones", typeof(RectTransform)).GetComponent<RectTransform>();
        fila.SetParent(panel, false);
        fila.anchorMin = new Vector2(0f, 0f); fila.anchorMax = new Vector2(1f, 0f);
        fila.pivot = new Vector2(0.5f, 0f);
        fila.offsetMin = new Vector2(140f, 14f); fila.offsetMax = new Vector2(-16f, 70f);
        HorizontalLayoutGroup h = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        d.saltar = EstiloMenu.Opcion(fila, "Saltar", d.Terminar, 56f, 24f);
        d.anterior = EstiloMenu.Opcion(fila, "Anterior", d.Anterior, 56f, 24f);
        d.siguiente = EstiloMenu.Opcion(fila, "Siguiente", d.Siguiente, 56f, 24f);
        d.Abrir();
        return d;
    }

    private void Abrir()
    {
        abierta = true;
        abiertaEnFrame = Time.frameCount;
        escalaPrevia = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        grupo.alpha = 1f;
        grupo.interactable = true;
        grupo.blocksRaycasts = true;
        SonidoMenu.Abrir();
        Ir(0);
    }

    private void Ir(int i)
    {
        paso = Mathf.Clamp(i, 0, tarjetas.Count - 1);
        tarjeta.Mostrar(tarjetas[paso]);
        contador.text = $"{paso + 1} de {tarjetas.Count}";
        anterior.GetComponent<OpcionEstilo>().PonerActiva(paso > 0);
        bool ultima = paso == tarjetas.Count - 1;
        siguiente.GetComponentInChildren<TextMeshProUGUI>().text = ultima ? "Empezar" : "Siguiente";
        SonidoMenu.Silenciar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(siguiente.gameObject);
    }

    public void Siguiente()
    {
        if (paso >= tarjetas.Count - 1) { Terminar(); return; }
        Ir(paso + 1);
    }

    public void Anterior()
    {
        if (paso > 0) Ir(paso - 1);
    }

    // Saltar o Empezar: se marca como vista y se vuelve al juego.
    public void Terminar()
    {
        if (!abierta) return;
        abierta = false;
        cerradaEnFrame = Time.frameCount;
        Globales.PonerMarca(Marca, true);
        Time.timeScale = escalaPrevia;
        LimpiarEntradas();
        instancia = null;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!abierta) return;
        // Lo que se pulse aqui no cuenta en el juego (golpe, frasco, hoguera...).
        LimpiarEntradas();
        if (Time.frameCount == abiertaEnFrame) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        if ((k != null && (k.escapeKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame))
        {
            SonidoMenu.Cancelar();
            Terminar();
            return;
        }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(siguiente.gameObject);
    }

    // Borra lo pulsado mientras habia un menu encima (tambien lo usa la pausa).
    public static void LimpiarEntradas()
    {
        foreach (GatherInput gi in FindObjectsByType<GatherInput>(FindObjectsSortMode.None))
        {
            gi.IsAttacking = gi.IsJumping = gi.IsDodging = gi.IsHealing = gi.IsManaPotion = false;
            gi.IsInteracting = gi.IsTogglingWeapon = gi.IsKicking = gi.IsShooting = gi.IsPotionWheel = false;
        }
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
        if (abierta) Time.timeScale = escalaPrevia;
    }

    // Para las pruebas.
    public static TarjetaControl TarjetaActual => instancia != null ? instancia.tarjeta : null;
    public static RectTransform PanelActual => instancia != null ? (RectTransform)instancia.tarjeta.transform.parent.parent : null;
}
