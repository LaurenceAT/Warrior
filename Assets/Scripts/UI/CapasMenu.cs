using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Navegacion por capas de un menu (Desafios, Bestiario, Logros, libro de pistas):
//   - Pasar el raton (o moverse con el teclado) solo RESALTA.
//   - Clic (o Enter) SELECCIONA: la opcion queda marcada hasta volver atras.
//   - Atras (clic derecho, Esc o el boton este del mando): un paso cada vez.
// Cada vez que se entra en una capa se apunta como volver de ella; Atras()
// deshace la ultima. Sin capas, Atras() devuelve false y el menu que lo usa
// hace lo de siempre (volver al menu principal, cerrar la pausa...).
public class CapasMenu
{
    private readonly List<System.Action> pila = new List<System.Action>();

    public int Profundidad => pila.Count;

    public void Entrar(System.Action volver) => pila.Add(volver);

    public bool Atras(bool sonido = true)
    {
        if (pila.Count == 0) return false;
        System.Action volver = pila[pila.Count - 1];
        pila.RemoveAt(pila.Count - 1);
        if (sonido) SonidoMenu.Atras();
        volver?.Invoke();
        return true;
    }

    public void Vaciar() => pila.Clear();

    // Se pidio volver atras este fotograma. El clic derecho es opcional: en la
    // partida es el bloqueo, asi que la pausa no lo usa para cerrarse.
    public static bool PulsadoAtras(bool clicDerecho = true)
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (clicDerecho && m != null && m.rightButton.wasPressedThisFrame)
               || (g != null && g.buttonEast.wasPressedThisFrame);
    }
}

// Opciones de las que solo una queda marcada (la seleccionada con clic o Enter).
public class GrupoMarcado
{
    private readonly List<OpcionEstilo> opciones = new List<OpcionEstilo>();
    public OpcionEstilo Actual { get; private set; }

    public void Anadir(OpcionEstilo o)
    {
        if (o == null) return;
        o.animar = true;
        opciones.Add(o);
    }

    public void Marcar(OpcionEstilo o)
    {
        Actual = o;
        foreach (OpcionEstilo x in opciones) if (x != null) x.PonerMarcada(x == o);
    }

    public void Desmarcar() => Marcar(null);

    public void Vaciar()
    {
        opciones.Clear();
        Actual = null;
    }
}

// Transiciones cortas de los menus (tiempo real: funcionan con el juego parado).
public static class TransicionMenu
{
    // El panel entra deslizandose un poco desde un lado y apareciendo.
    public static IEnumerator Entrar(RectTransform r, CanvasGroup g, float desdeX = 36f, float dura = 0.22f)
    {
        if (r == null) yield break;
        Vector2 fin = r.anchoredPosition;
        for (float t = 0f; t < dura; t += Time.unscaledDeltaTime)
        {
            float u = 1f - Mathf.Pow(1f - t / dura, 3f);
            r.anchoredPosition = fin + new Vector2(desdeX * (1f - u), 0f);
            if (g != null) g.alpha = u;
            yield return null;
        }
        r.anchoredPosition = fin;
        if (g != null) g.alpha = 1f;
    }

    public static IEnumerator Aparecer(CanvasGroup g, float dura = 0.18f)
    {
        if (g == null) yield break;
        for (float t = 0f; t < dura; t += Time.unscaledDeltaTime) { g.alpha = t / dura; yield return null; }
        g.alpha = 1f;
    }

    public static IEnumerator Pulso(RectTransform r, float cuanto = 0.05f, float dura = 0.3f)
    {
        if (r == null) yield break;
        for (float t = 0f; t < dura; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * (1f + cuanto * Mathf.Sin(t / dura * Mathf.PI));
            yield return null;
        }
        r.localScale = Vector3.one;
    }
}

// Punto de "nuevo" (algo recien descubierto que aun no se ha visto). Late
// despacio para que se vea sin molestar.
public class PuntoNuevo : MonoBehaviour
{
    private Image img;
    private float fase;

    public static PuntoNuevo Crear(Transform padre, Vector2 ancla, Vector2 posicion, float tamano = 14f)
    {
        Image i = EstiloMenu.Caja("Nuevo", padre, new Color(1f, 0.72f, 0.3f, 1f));
        i.sprite = RuedaImbuir.Circulo();
        RectTransform r = i.rectTransform;
        r.anchorMin = r.anchorMax = ancla;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(tamano, tamano);
        r.anchoredPosition = posicion;
        PuntoNuevo p = i.gameObject.AddComponent<PuntoNuevo>();
        p.img = i;
        p.fase = Random.value * 6f;
        i.gameObject.SetActive(false);
        return p;
    }

    public void Poner(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private void Update()
    {
        float k = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f + fase);
        img.color = new Color(1f, 0.72f, 0.3f, k);
    }
}
