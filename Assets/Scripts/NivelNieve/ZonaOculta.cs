using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Interiores secretos. Va en un Tilemap aparte ("ZonasOcultas"): lo que se pinte
// en el con el tile "Sombra" queda a oscuras desde fuera y no deja ver lo que hay
// detras. Cada zona (grupo de casillas de sombra pegadas) se aclara al meterse
// el player en ella y se vuelve a oscurecer al salir; las demas siguen a oscuras.
// En el editor la sombra se ve a medias, para poder trabajar debajo.
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class ZonaOculta : MonoBehaviour
{
    [Tooltip("Transparencia de la sombra mientras editas (no afecta al juego).")]
    [Range(0f, 1f)] [SerializeField] private float alfaEnEditor = 0.45f;
    [SerializeField] private float velocidadRevelar = 2.5f;
    [SerializeField] private float velocidadOcultar = 1.2f;

    private Tilemap tm;
    private bool pendiente = true;
    private readonly List<List<Vector3Int>> zonas = new List<List<Vector3Int>>();
    private readonly Dictionary<Vector3Int, int> zonaDe = new Dictionary<Vector3Int, int>();
    private readonly List<float> alfas = new List<float>();
    private Transform player;
    // Cambiar el color de una casilla tambien cuenta como "cambio en el Tilemap":
    // mientras lo hace este script, no hay que volver a separar las zonas.
    private bool pintando;

    private void OnEnable()
    {
        tm = GetComponent<Tilemap>();
        Tilemap.tilemapTileChanged += AlCambiar;
        pendiente = true;
    }

    private void OnDisable() => Tilemap.tilemapTileChanged -= AlCambiar;

    private void AlCambiar(Tilemap t, Tilemap.SyncTile[] c)
    {
        if (t != tm || pintando) return;
        // Solo si se ha pintado o borrado alguna casilla (no si solo cambio su color).
        foreach (Tilemap.SyncTile s in c)
            if ((s.tile != null) != zonaDe.ContainsKey(s.position)) { pendiente = true; return; }
    }

    private void Update()
    {
        if (pendiente) { pendiente = false; Separar(); }
        if (!Application.isPlaying)
        {
            tm.color = new Color(1f, 1f, 1f, alfaEnEditor);
            return;
        }
        tm.color = Color.white;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        int dentro = -1;
        if (player != null && zonaDe.TryGetValue(tm.WorldToCell(player.position + Vector3.up * 0.1f), out int z)) dentro = z;

        for (int i = 0; i < zonas.Count; i++)
        {
            float objetivo = i == dentro ? 0f : 1f;
            if (Mathf.Approximately(alfas[i], objetivo)) continue;
            alfas[i] = Mathf.MoveTowards(alfas[i], objetivo, Time.deltaTime * (objetivo < alfas[i] ? velocidadRevelar : velocidadOcultar));
            pintando = true;
            foreach (Vector3Int c in zonas[i])
            {
                Color col = tm.GetColor(c);
                tm.SetColor(c, new Color(col.r, col.g, col.b, alfas[i]));
            }
            pintando = false;
        }
    }

    private void Separar()
    {
        pintando = true;
        zonas.Clear();
        zonaDe.Clear();
        alfas.Clear();
        foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
        {
            if (!tm.HasTile(c) || zonaDe.ContainsKey(c)) continue;
            var grupo = new List<Vector3Int>();
            var cola = new Queue<Vector3Int>();
            cola.Enqueue(c);
            zonaDe[c] = zonas.Count;
            while (cola.Count > 0)
            {
                Vector3Int a = cola.Dequeue();
                grupo.Add(a);
                foreach (Vector3Int d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    if (tm.HasTile(a + d) && !zonaDe.ContainsKey(a + d)) { zonaDe[a + d] = zonas.Count; cola.Enqueue(a + d); }
            }
            zonas.Add(grupo);
            alfas.Add(1f);
            foreach (Vector3Int g in grupo)
            {
                Color col = tm.GetColor(g);
                tm.SetColor(g, new Color(col.r, col.g, col.b, 1f));
            }
        }
        pintando = false;
    }
}
