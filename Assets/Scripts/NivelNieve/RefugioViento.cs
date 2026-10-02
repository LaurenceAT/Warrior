using System.Collections.Generic;
using UnityEngine;

// Refugio de la ventisca: tras una roca el viento no empuja. Se ve como un
// remanso (la nieve que vuela se aparta). La ZonaVentisca lo consulta.
[RequireComponent(typeof(Collider2D))]
public class RefugioViento : MonoBehaviour
{
    private static readonly List<RefugioViento> todos = new List<RefugioViento>();
    // Depuracion: false = los refugios no protegen.
    public static bool Activos = true;
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnEnable() => todos.Add(this);
    private void OnDisable() => todos.Remove(this);

    public static bool Protege(Vector2 punto)
    {
        if (!Activos) return false;
        foreach (RefugioViento r in todos)
            if (r != null && r.col != null && r.col.OverlapPoint(punto)) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(c.bounds.center + Vector3.up * (c.bounds.extents.y + 0.3f), "Refugio");
#endif
    }
}
