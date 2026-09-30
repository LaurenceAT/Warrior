using UnityEngine;

// Brasas guia (mecanica propia de la cueva): en las zonas oscuras, unas chispas
// de ceniza encendida flotan despacio siguiendo el camino bueno, como si una
// corriente de aire las llevara hacia la salida. Solo con la oscuridad puesta.
public class GuiaBrasas : MonoBehaviour
{
    [Tooltip("Puntos del camino, en orden (en el mundo).")]
    public Vector2[] camino = new Vector2[0];
    [Tooltip("Segundos entre dos brasas.")]
    public float intervalo = 0.35f;
    [Tooltip("Rapidez con que avanzan por el camino.")]
    public float velocidad = 1.2f;
    [Tooltip("Solo si el player esta a menos de esto.")]
    public float alcance = 12f;
    public Color color = new Color(1f, 0.6f, 0.25f, 0.9f);

    private float siguiente;
    private float avance;
    private Transform player;

    private void Update()
    {
        if (camino == null || camino.Length < 2) return;
        OscuridadCueva o = OscuridadCueva.Instancia;
        if (o == null || !o.Oscuro) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            if (player == null) return;
        }
        if (Time.time < siguiente) return;
        siguiente = Time.time + intervalo;

        // Cada brasa nace en el tramo mas cercano al player y un poco por delante.
        int tramo = 0;
        float mejor = float.MaxValue;
        for (int i = 0; i < camino.Length - 1; i++)
        {
            float d = Vector2.Distance(player.position, (camino[i] + camino[i + 1]) * 0.5f);
            if (d < mejor) { mejor = d; tramo = i; }
        }
        if (mejor > alcance) return;
        avance = Mathf.Repeat(avance + 0.37f, 1f);
        Vector2 a = camino[tramo], b = camino[tramo + 1];
        Vector2 pos = Vector2.Lerp(a, b, avance) + Random.insideUnitCircle * 0.3f;
        Vector2 dir = (b - a).normalized;
        EmisorCueva.Emitir(EmisorCueva.Tipo.Brasa, pos, dir * velocidad + Vector2.up * 0.1f, color, Random.Range(0.035f, 0.055f), Random.Range(1.6f, 2.4f));
    }

    private void OnDrawGizmos()
    {
        if (camino == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
        for (int i = 0; i < camino.Length - 1; i++) Gizmos.DrawLine(camino[i], camino[i + 1]);
    }
}
