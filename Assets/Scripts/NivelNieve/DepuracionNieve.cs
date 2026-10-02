using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Depuracion de la nieve (solo Editor, F6): ir a cada zona y hoguera, ver los
// peligros, sellos, refugios y emboscadas, y encender o apagar cada mecanica.
public class DepuracionNieve : MonoBehaviour
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
        if (k != null && k.f6Key.wasPressedThisFrame) abierto = !abierto;
    }

    private void OnGUI()
    {
        if (!abierto)
        {
            GUI.Label(new Rect(Screen.width - 260, Screen.height - 44, 250, 20), "F6: depuración de la nieve (Editor)");
            return;
        }
        GUILayout.BeginArea(new Rect(10, 10, 270, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>NIEVE (Editor) — F6</b>");
        GUILayout.Label("Ir a:");
        foreach (Punto p in puntos)
            if (GUILayout.Button(p.nombre)) Ir(p.pos);
        foreach (Hoguera h in FindObjectsByType<Hoguera>(FindObjectsSortMode.None))
            if (GUILayout.Button("Hoguera: " + h.NombreLugar)) Ir(h.PuntoReaparicion);

        GUILayout.Space(6);
        GUILayout.Label("Mecánicas:");
        ZonaVentisca.Activa = GUILayout.Toggle(ZonaVentisca.Activa, "Ventisca empuja");
        RefugioViento.Activos = GUILayout.Toggle(RefugioViento.Activos, "Refugios protegen");
        bool q = GUILayout.Toggle(Quebradizos(), "Hielo quebradizo cede");
        if (q != Quebradizos()) foreach (SueloFalso s in FindObjectsByType<SueloFalso>(FindObjectsSortMode.None)) s.enabled = q;
        MonticuloEmboscada.Activas = GUILayout.Toggle(MonticuloEmboscada.Activas, "Emboscadas (al cargar)");
        if (GUILayout.Button("Sacar todas las emboscadas"))
            foreach (MonticuloEmboscada m in FindObjectsByType<MonticuloEmboscada>(FindObjectsSortMode.None)) m.ForzarSalida();
        if (GUILayout.Button("Abrir todos los sellos"))
            foreach (SelloElemental s in FindObjectsByType<SelloElemental>(FindObjectsSortMode.None)) s.ForzarAbrir();

        GUILayout.Space(6);
        bool ver = GUILayout.Toggle(verZonas, "Ver peligros, sellos y emboscadas");
        if (ver != verZonas) { verZonas = ver; Dibujar(ver); }
        if (verZonas && GUILayout.Button("Actualizar dibujo")) Dibujar(true);
        GUILayout.Label("<color=#ff5555>rojo</color>: muerte  <color=#ffaa33>naranja</color>: hielo quebradizo\n" +
                        "<color=#55ff77>verde</color>: refugio/atajo  <color=#ffdd55>amarillo</color>: sello\n<color=#99ccff>azul</color>: emboscada");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static bool Quebradizos()
    {
        SueloFalso s = FindFirstObjectByType<SueloFalso>();
        return s == null || s.enabled;
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
        foreach (RefugioViento r in FindObjectsByType<RefugioViento>(FindObjectsSortMode.None)) Caja(r.GetComponent<Collider2D>(), new Color(0.3f, 1f, 0.45f));
        foreach (DerrumbeAtajo d in FindObjectsByType<DerrumbeAtajo>(FindObjectsSortMode.None)) Caja(d.GetComponent<Collider2D>(), new Color(0.3f, 1f, 0.45f));
        foreach (SelloElemental s in FindObjectsByType<SelloElemental>(FindObjectsSortMode.None))
            Caja(new Bounds(s.transform.position, new Vector3(1.2f, 1.2f, 0f)), new Color(1f, 0.85f, 0.3f));
        foreach (MonticuloEmboscada m in FindObjectsByType<MonticuloEmboscada>(FindObjectsSortMode.None))
            Caja(new Bounds(m.transform.position + Vector3.up * 0.5f, new Vector3(m.radio * 2f, m.alturaMaxima * 2f, 0f)), new Color(0.6f, 0.8f, 1f));
    }

    private void Caja(Collider2D c, Color color) { if (c != null) Caja(c.bounds, color); }

    private void Caja(Bounds b, Color color)
    {
        if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        LineRenderer l = new GameObject("GizmoNieve").AddComponent<LineRenderer>();
        l.useWorldSpace = true;
        l.sharedMaterial = mat;
        l.startColor = l.endColor = color;
        l.startWidth = l.endWidth = 0.06f;
        l.sortingLayerName = "VFX";
        l.sortingOrder = 60;
        l.loop = true;
        l.positionCount = 4;
        l.SetPositions(new[] { new Vector3(b.min.x, b.min.y), new Vector3(b.max.x, b.min.y), new Vector3(b.max.x, b.max.y), new Vector3(b.min.x, b.max.y) });
        lineas.Add(l);
    }
#endif
}
