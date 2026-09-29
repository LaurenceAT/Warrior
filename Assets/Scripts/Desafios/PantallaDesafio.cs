using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Desafio completado": sale un momento despues de vencer al jefe, con el tiempo
// total, las muertes y si es tu mejor tiempo. Un boton vuelve al menu de Desafios.
public class PantallaDesafio : MonoBehaviour
{
    private static PantallaDesafio instancia;
    public static bool Abierta => instancia != null && instancia.abierta;

    private CanvasGroup grupo;
    private TextMeshProUGUI subtitulo, datos, susurro;
    private Button volver;
    private bool abierta;

    public static void Mostrar(FichaJefe f, bool dificil, float segundos, int muertes, bool record, string lineaExtra = null)
    {
        if (instancia == null) instancia = Crear();
        instancia.StartCoroutine(instancia.Entrar(f, dificil, segundos, muertes, record, lineaExtra));
    }

    private IEnumerator Entrar(FichaJefe f, bool dificil, float segundos, int muertes, bool record, string lineaExtra)
    {
        // Primero la muerte del jefe.
        yield return new WaitForSecondsRealtime(3.5f);
        abierta = true;

        subtitulo.text = $"{f.nombre}  ·  {(dificil ? "<color=#e0776c>Difícil</color>" : "Normal")}";
        datos.text = $"Tiempo  <b>{Globales.Tiempo(segundos)}</b>" + (record ? "   <color=#9fe0a0>¡Mejor tiempo!</color>" : "") +
                     $"\nMuertes  <b>{muertes}</b>";
        grupo.interactable = grupo.blocksRaycasts = true;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = t / 0.6f;
            yield return null;
        }
        grupo.alpha = 1f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(volver.gameObject);

        // Un jefe secreto acaba de despertar: una linea discreta que aparece sola,
        // con un sonido inquietante.
        susurro.text = lineaExtra ?? "";
        if (string.IsNullOrEmpty(lineaExtra)) yield break;
        yield return new WaitForSecondsRealtime(1.2f);
        Sonido.Reproducir("secreto_inquietante", 0.8f);
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
        {
            susurro.alpha = Mathf.Clamp01(t / 2f) * 0.85f;
            yield return null;
        }
    }

    private void Update()
    {
        if (abierta && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(volver.gameObject);
    }

    private static PantallaDesafio Crear()
    {
        GameObject go = new GameObject("PantallaDesafio");
        PantallaDesafio p = go.AddComponent<PantallaDesafio>();
        p.grupo = EstiloMenu.Lienzo(go, 110);
        RectTransform panel = EstiloMenu.Panel(go.transform, "DESAFÍO COMPLETADO", new Vector2(900f, 520f));
        p.subtitulo = EstiloMenu.Texto("", panel, 30, EstiloMenu.TextoElegido);
        EstiloMenu.Arriba(p.subtitulo.rectTransform, -120f, 40f);
        p.datos = EstiloMenu.Texto("", panel, 32, EstiloMenu.TextoElegido);
        p.datos.lineSpacing = 16f;
        p.datos.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        p.datos.rectTransform.sizeDelta = new Vector2(800f, 140f);
        p.susurro = EstiloMenu.Texto("", panel, 24, new Color(0.7f, 0.85f, 0.72f));
        p.susurro.fontStyle = FontStyles.Italic;
        p.susurro.alpha = 0f;
        p.susurro.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        p.susurro.rectTransform.sizeDelta = new Vector2(800f, 40f);
        VerticalLayoutGroup col = EstiloMenu.Columna(panel, 180f, 180f, 380f, 40f, 0f);
        p.volver = EstiloMenu.Opcion(col.transform, "Volver a Desafíos", Desafio.Salir, 64f, 30f);
        // Activo pero invisible (alfa 0): asi puede arrancar su entrada.
        p.grupo.interactable = p.grupo.blocksRaycasts = false;
        return p;
    }
}
