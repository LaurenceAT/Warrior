using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Los logros: se miran sus condiciones (FichaLogro) cuando pasa algo en el
// juego y, si se cumplen, se guardan para siempre (Globales) y sale un aviso
// pequeno en una esquina.
//   - Llegar a un nivel y derrotar a un jefe cuentan solo en la partida normal.
//   - Completar desafios, solo dentro de un desafio.
public static class Logros
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded += (s, m) => AlCargarEscena(s.name);
        ArenaJefe.AlVencer += () => { if (!Desafio.Activo) Comprobar(FichaLogro.Condicion.DerrotarJefeEnPartida, SceneManager.GetActiveScene().name); };
        PlayerControler.AlMorir += () => { if (!Desafio.Activo) Comprobar(FichaLogro.Condicion.PrimeraMuerte, null); };
        AlCargarEscena(SceneManager.GetActiveScene().name);
    }

    private static void AlCargarEscena(string escena)
    {
        if (Desafio.Activo) return;
        Comprobar(FichaLogro.Condicion.LlegarAEscena, escena);
    }

    public static void Comprobar(FichaLogro.Condicion c, string parametro)
    {
        foreach (FichaLogro l in FichaLogro.Todos())
            if (l.condicion == c && (string.IsNullOrEmpty(l.parametro) || l.parametro == parametro))
                Desbloquear(l);
    }

    public static void Desbloquear(FichaLogro l)
    {
        if (l == null || !Globales.DarLogro(l.id)) return;
        AvisoLogro.Mostrar(l);
    }

    public static int Desbloqueados(FichaLogro.Seccion? s = null) =>
        FichaLogro.Visibles().Count(l => (s == null || l.seccion == s) && Globales.TieneLogro(l.id));

    public static int Total(FichaLogro.Seccion? s = null) => FichaLogro.Visibles().Count(l => s == null || l.seccion == s);
}

// El aviso de "Logro desbloqueado": arriba a la derecha, entra, se queda un
// momento y se va. No para el juego. Si salen varios, van en cola.
public class AvisoLogro : MonoBehaviour
{
    private static AvisoLogro instancia;
    private readonly Queue<FichaLogro> cola = new Queue<FichaLogro>();
    private RectTransform caja;
    private CanvasGroup grupo;
    private Image icono;
    private TextMeshProUGUI nombre;
    private bool mostrando;

    public static void Mostrar(FichaLogro l)
    {
        if (instancia == null) instancia = Crear();
        instancia.cola.Enqueue(l);
        if (!instancia.mostrando) instancia.StartCoroutine(instancia.Siguiente());
    }

    private IEnumerator Siguiente()
    {
        mostrando = true;
        while (cola.Count > 0)
        {
            FichaLogro l = cola.Dequeue();
            icono.sprite = l.icono;
            icono.enabled = l.icono != null;
            nombre.text = l.nombre;
            Sonido.Reproducir("objeto_obtenido", 0.7f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float u = t / 0.35f;
                grupo.alpha = u;
                caja.anchoredPosition = new Vector2(Mathf.Lerp(80f, 0f, u), -40f);
                yield return null;
            }
            grupo.alpha = 1f;
            caja.anchoredPosition = new Vector2(0f, -40f);
            yield return new WaitForSecondsRealtime(3.2f);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                grupo.alpha = 1f - t / 0.4f;
                yield return null;
            }
            grupo.alpha = 0f;
        }
        mostrando = false;
    }

    private static AvisoLogro Crear()
    {
        GameObject go = new GameObject("AvisoLogro");
        DontDestroyOnLoad(go);
        AvisoLogro a = go.AddComponent<AvisoLogro>();
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 150;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        a.grupo = go.AddComponent<CanvasGroup>();
        a.grupo.alpha = 0f;
        a.grupo.blocksRaycasts = false;

        Image filo = EstiloMenu.Caja("Aviso", go.transform, EstiloMenu.Filo);
        a.caja = filo.rectTransform;
        a.caja.anchorMin = a.caja.anchorMax = a.caja.pivot = new Vector2(1f, 1f);
        a.caja.sizeDelta = new Vector2(460f, 104f);
        a.caja.anchoredPosition = new Vector2(0f, -40f);
        Image fondo = EstiloMenu.Caja("Fondo", filo.transform, EstiloMenu.Fondo);
        EstiloMenu.Estirar(fondo.rectTransform, 2f);
        // Un margen con el borde de la pantalla.
        a.caja.anchoredPosition = new Vector2(-30f, -40f);

        a.icono = EstiloMenu.Caja("Icono", fondo.transform, Color.white);
        a.icono.preserveAspect = true;
        RectTransform ri = a.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(72f, 72f);
        ri.anchoredPosition = new Vector2(16f, 0f);

        TextMeshProUGUI titulo = EstiloMenu.Texto("LOGRO DESBLOQUEADO", fondo.transform, 18, new Color(0.78f, 0.66f, 0.46f));
        titulo.characterSpacing = 6f;
        titulo.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform rt = titulo.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(104f, 4f); rt.offsetMax = new Vector2(-12f, -12f);

        a.nombre = EstiloMenu.Texto("", fondo.transform, 28, EstiloMenu.Titulo);
        a.nombre.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rn = a.nombre.rectTransform;
        rn.anchorMin = new Vector2(0f, 0f); rn.anchorMax = new Vector2(1f, 0.5f);
        rn.offsetMin = new Vector2(104f, 10f); rn.offsetMax = new Vector2(-12f, -2f);
        return a;
    }
}
