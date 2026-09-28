using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Todo lo que se usa con la F (hogueras, cofres...). Cada objeto se apunta en la
// lista al activarse; el player mira cual tiene mas cerca y al pulsar F lo usa.
public interface IInteractuable
{
    Vector2 PuntoInteraccion { get; }
    float RadioInteraccion { get; }
    // Si ahora mismo se puede usar (un cofre ya abierto no).
    bool PuedeInteractuar { get; }
    // Lo que se lee sobre el objeto: "Presiona F para ...".
    string TextoInteraccion { get; }
    void Interactuar(PlayerControler p);
}

public static class Interacciones
{
    private static readonly List<IInteractuable> lista = new List<IInteractuable>();

    // El que el player tiene a tiro ahora mismo (lo actualiza el player).
    public static IInteractuable Actual { get; private set; }

    public static void Registrar(IInteractuable i) { if (i != null && !lista.Contains(i)) lista.Add(i); }
    public static void Quitar(IInteractuable i) { lista.Remove(i); if (Actual == i) Actual = null; }

    public static IInteractuable Buscar(Vector2 pos)
    {
        IInteractuable mejor = null;
        float mejorD = float.MaxValue;
        for (int n = lista.Count - 1; n >= 0; n--)
        {
            IInteractuable i = lista[n];
            if (i == null || (i is Object o && o == null)) { lista.RemoveAt(n); continue; }
            if (!i.PuedeInteractuar) continue;
            float d = Vector2.Distance(pos, i.PuntoInteraccion);
            if (d <= i.RadioInteraccion && d < mejorD) { mejor = i; mejorD = d; }
        }
        Actual = mejor;
        return mejor;
    }

    public static void Limpiar() => Actual = null;
}

// Cartel flotante "Presiona F para ..." sobre un objeto. Aparece con un fundido
// cuando ese objeto es el que el player tiene a tiro.
public class AvisoInteraccion : MonoBehaviour
{
    private TextMeshPro texto;
    private IInteractuable duenio;
    private float alfa;

    public static AvisoInteraccion Crear(Transform padre, IInteractuable duenio, float altura)
    {
        GameObject go = new GameObject("AvisoF");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(0f, altura, 0f);
        AvisoInteraccion a = go.AddComponent<AvisoInteraccion>();
        a.duenio = duenio;
        a.texto = go.AddComponent<TextMeshPro>();
        a.texto.fontSize = 2.2f;
        a.texto.alignment = TextAlignmentOptions.Center;
        a.texto.enableWordWrapping = false;
        a.texto.color = new Color(1f, 0.9f, 0.85f, 0f);
        a.texto.outlineWidth = 0.2f;
        a.texto.outlineColor = new Color(0f, 0f, 0f, 1f);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "VFX";
        mr.sortingOrder = 30;
        return a;
    }

    // Texto fijo durante un momento (por ejemplo "Has descansado").
    private string forzado;
    private float finForzado;
    public void Forzar(string t, float segundos) { forzado = t; finForzado = Time.time + segundos; }

    private void LateUpdate()
    {
        bool activo = Interacciones.Actual == duenio && duenio.PuedeInteractuar;
        bool forzando = Time.time < finForzado;
        alfa = Mathf.MoveTowards(alfa, activo || forzando ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        if (texto == null) return;
        string t = forzando ? forzado : duenio.TextoInteraccion;
        if (texto.text != t) texto.text = t;
        Color c = texto.color;
        c.a = alfa;
        texto.color = c;
        // El padre puede estar volteado con la escala: el texto siempre derecho.
        Vector3 e = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(Mathf.Sign(e.x) / Mathf.Max(0.01f, Mathf.Abs(e.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(e.y)), 1f);
    }
}
