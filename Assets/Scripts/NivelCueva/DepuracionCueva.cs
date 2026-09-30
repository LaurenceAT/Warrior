using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// Herramientas de prueba de la cueva nueva. SOLO en el Editor (en la version
// final no hacen nada). F8 abre el panel:
//   - Teletransporte a cada zona y a cada hoguera.
//   - Ver las zonas de muerte (rojo), los suelos falsos (naranja), las zonas
//     oscuras (morado) y el derrumbe del atajo (verde).
//   - Encender o apagar la oscuridad.
public class DepuracionCueva : MonoBehaviour
{
    [System.Serializable]
    public class Punto
    {
        public string nombre;
        public Vector2 pos;
    }

    public Punto[] puntos = new Punto[0];

#if UNITY_EDITOR
    private bool abierto, verZonas;
    private Vector2 scroll;
    private readonly List<LineRenderer> lineas = new List<LineRenderer>();
    private static Material mat;

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.f8Key.wasPressedThisFrame) abierto = !abierto;
    }

    private void OnGUI()
    {
        if (!abierto)
        {
            GUI.Label(new Rect(Screen.width - 260, Screen.height - 44, 250, 20), "F8: depuración de la cueva (Editor)");
            return;
        }
        GUILayout.BeginArea(new Rect(10, 10, 260, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>CUEVA (Editor) — F8</b>");
        GUILayout.Label("Ir a:");
        foreach (Punto p in puntos)
            if (GUILayout.Button(p.nombre)) Ir(p.pos);
        foreach (Hoguera h in FindObjectsByType<Hoguera>(FindObjectsSortMode.None))
            if (GUILayout.Button("Hoguera: " + h.name)) Ir(h.PuntoReaparicion);

        GUILayout.Space(6);
        bool oscura = GUILayout.Toggle(OscuridadCueva.Permitida, "Oscuridad permitida");
        if (oscura != OscuridadCueva.Permitida) OscuridadCueva.Permitida = oscura;
        bool ver = GUILayout.Toggle(verZonas, "Ver peligros y zonas");
        if (ver != verZonas) { verZonas = ver; Dibujar(ver); }
        OscuridadCueva o = OscuridadCueva.Instancia;
        if (o != null)
        {
            GUILayout.Label($"Radio de luz: {o.radioJugador:0.0}");
            o.radioJugador = GUILayout.HorizontalSlider(o.radioJugador, 1.5f, 7f);
            GUILayout.Label($"Intensidad: {o.intensidad:0.00}");
            o.intensidad = GUILayout.HorizontalSlider(o.intensidad, 0f, 1f);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void Ir(Vector2 pos)
    {
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p == null) return;
        Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
        rb.position = pos;
        rb.linearVelocity = Vector2.zero;
        p.transform.position = pos;
    }

    private void Dibujar(bool on)
    {
        foreach (LineRenderer l in lineas) if (l != null) Destroy(l.gameObject);
        lineas.Clear();
        if (!on) return;
        foreach (DeadArea d in FindObjectsByType<DeadArea>(FindObjectsSortMode.None)) Caja(d.GetComponent<Collider2D>(), new Color(1f, 0.2f, 0.2f));
        foreach (SueloFalso s in FindObjectsByType<SueloFalso>(FindObjectsSortMode.None)) Caja(s.GetComponent<Collider2D>(), new Color(1f, 0.6f, 0.1f));
        foreach (ZonaOscura z in FindObjectsByType<ZonaOscura>(FindObjectsSortMode.None)) Caja(z.GetComponent<Collider2D>(), new Color(0.6f, 0.3f, 1f));
        foreach (DerrumbeAtajo d in FindObjectsByType<DerrumbeAtajo>(FindObjectsSortMode.None)) Caja(d.GetComponent<Collider2D>(), new Color(0.3f, 1f, 0.4f));
    }

    private void Caja(Collider2D c, Color color)
    {
        if (c == null) return;
        if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        LineRenderer l = new GameObject("GizmoCueva").AddComponent<LineRenderer>();
        l.useWorldSpace = true;
        l.sharedMaterial = mat;
        l.startColor = l.endColor = color;
        l.startWidth = l.endWidth = 0.06f;
        l.sortingLayerName = "VFX";
        l.sortingOrder = 60;
        l.loop = true;
        Bounds b = c.bounds;
        l.positionCount = 4;
        l.SetPositions(new[] { new Vector3(b.min.x, b.min.y), new Vector3(b.max.x, b.min.y), new Vector3(b.max.x, b.max.y), new Vector3(b.min.x, b.max.y) });
        lineas.Add(l);
    }
#endif
}
