using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Fila de cuadritos con los estados que tiene un enemigo (quemado, congelado,
// sangrado...), justo debajo de su barra de vida. Solo iconos, sin barras. El
// que esta a punto de acabar parpadea rapido. La usan la barra pequena de los
// enemigos y la grande de los jefes.
public class FilaEstados : MonoBehaviour
{
    private EnemyHealth salud;
    private EstadosEnemigo estados;
    private float lado, espacio;
    private bool centrada;
    private readonly List<(Image marco, Image icono)> cuadros = new List<(Image, Image)>();

    // "lado": tamano de cada cuadro en las unidades del lienzo. "centrada": en el
    // centro bajo la barra (enemigos) o pegada a la izquierda (jefes).
    public static FilaEstados Crear(RectTransform padre, EnemyHealth salud, float lado, float espacio, float y, bool centrada)
    {
        GameObject go = new GameObject("FilaEstados", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(centrada ? 0.5f : 0f, 0f);
        r.anchorMax = r.anchorMin;
        r.pivot = new Vector2(centrada ? 0.5f : 0f, 1f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(lado, lado);
        FilaEstados f = go.AddComponent<FilaEstados>();
        f.salud = salud;
        f.lado = lado;
        f.espacio = espacio;
        f.centrada = centrada;
        return f;
    }

    // Hay algun estado activo (la barra del enemigo no se desvanece mientras tanto).
    public bool HayEstados { get; private set; }

    private void LateUpdate()
    {
        if (estados == null && salud != null) estados = salud.GetComponent<EstadosEnemigo>();
        List<EstadosEnemigo.Icono> lista = estados != null ? estados.Activos() : null;
        int n = lista != null ? lista.Count : 0;
        HayEstados = n > 0;
        RecursosRPG r = RecursosRPG.Get();

        for (int i = 0; i < Mathf.Max(n, cuadros.Count); i++)
        {
            if (i >= cuadros.Count) cuadros.Add(NuevoCuadro());
            var (marco, icono) = cuadros[i];
            bool usar = i < n;
            marco.gameObject.SetActive(usar);
            if (!usar) continue;
            EstadosEnemigo.Icono e = lista[i];
            icono.sprite = r.Icono(e.clave);
            icono.enabled = icono.sprite != null;
            float a = EstadosEnemigo.Parpadeo(e.acabando);
            marco.color = new Color(0.72f, 0.58f, 0.36f, 0.9f * a);
            icono.color = new Color(1f, 1f, 1f, a);
            float x = centrada ? (i - (n - 1) * 0.5f) * (lado + espacio) : i * (lado + espacio);
            RectTransform rt = marco.rectTransform;
            rt.anchoredPosition = new Vector2(centrada ? x : x + lado * 0.5f, -lado * 0.5f);
        }
    }

    private (Image, Image) NuevoCuadro()
    {
        Image marco = new GameObject("Cuadro", typeof(RectTransform)).AddComponent<Image>();
        marco.transform.SetParent(transform, false);
        marco.raycastTarget = false;
        RectTransform rm = marco.rectTransform;
        rm.anchorMin = rm.anchorMax = new Vector2(centrada ? 0.5f : 0f, 1f);
        rm.sizeDelta = new Vector2(lado, lado);

        Image fondo = new GameObject("Fondo", typeof(RectTransform)).AddComponent<Image>();
        fondo.transform.SetParent(marco.transform, false);
        fondo.color = new Color(0.04f, 0.03f, 0.04f, 0.92f);
        fondo.raycastTarget = false;
        EstiloMenu.Estirar(fondo.rectTransform, lado * 0.07f);

        Image icono = new GameObject("Icono", typeof(RectTransform)).AddComponent<Image>();
        icono.transform.SetParent(marco.transform, false);
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        EstiloMenu.Estirar(icono.rectTransform, lado * 0.12f);
        return (marco, icono);
    }
}
