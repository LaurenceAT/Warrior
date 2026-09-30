using System.Collections.Generic;
using UnityEngine;

// Algo que alumbra dentro de la oscuridad de la cueva (antorcha encendida,
// hoguera, brillo de un peligro). Solo cuenta mientras esta activa y encendida.
public class FuenteLuz : MonoBehaviour
{
    [Tooltip("Radio de la luz (unidades).")]
    public float radio = 4f;
    [Tooltip("Fuerza de la luz (1 = se ve del todo).")]
    [Range(0f, 1f)] public float intensidad = 1f;
    [Tooltip("Parpadeo suave (0 = fija). Las antorchas tiemblan un poco.")]
    public float parpadeo = 0.06f;
    public Vector2 desplazamiento;
    public bool encendida = true;

    private static readonly List<FuenteLuz> todas = new List<FuenteLuz>();
    private float fase;

    private void OnEnable() { todas.Add(this); fase = Random.value * 10f; }
    private void OnDisable() => todas.Remove(this);

    public float RadioActual => radio * (1f + parpadeo * Mathf.Sin((Time.time + fase) * 9f) * Mathf.Sin((Time.time + fase) * 3.3f));

    // Rellena "luces" desde la posicion n con las mas cercanas a "desde".
    public static void Cercanas(Vector3 desde, Vector4[] luces, ref int n)
    {
        int libres = luces.Length - n;
        if (libres <= 0) return;
        todas.Sort((a, b) => ((Vector2)(a.transform.position - desde)).sqrMagnitude.CompareTo(((Vector2)(b.transform.position - desde)).sqrMagnitude));
        foreach (FuenteLuz f in todas)
        {
            if (n >= luces.Length) break;
            if (f == null || !f.encendida || f.intensidad <= 0f) continue;
            Vector2 p = (Vector2)f.transform.position + f.desplazamiento;
            if (((Vector2)desde - p).sqrMagnitude > 900f) continue;
            luces[n++] = new Vector4(p.x, p.y, f.RadioActual, f.intensidad);
        }
    }
}
