using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Paredes ilusorias, como las de Elden Ring. Va en un Tilemap aparte
// ("ParedesFalsas", sin colision): lo que se pinte en el se ve como la roca de
// al lado pero se atraviesa caminando. Solo lo delata un detalle sutil: un tono
// apenas distinto, una grieta fina en cada pared y algo de polvo que cae de vez
// en cuando.
// Con el player dentro de una pared, las paredes se vuelven medio transparentes
// para que no lo tapen. La primera vez que se cruza cada una suena un aviso.
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class ParedFalsa : MonoBehaviour
{
    [Tooltip("Tono de la pared (casi blanco: la diferencia con la roca debe ser minima).")]
    [SerializeField] private Color tinte = new Color(0.93f, 0.95f, 1f, 1f);
    [Tooltip("Transparencia de la pared con el player dentro.")]
    [Range(0f, 1f)] [SerializeField] private float alfaAtravesando = 0.35f;
    [Tooltip("Grieta que se dibuja en cada pared (vacio = sin grieta).")]
    [SerializeField] private Sprite grieta;
    [Tooltip("Segundos entre dos soplos de polvo de la grieta (0 = sin polvo).")]
    [SerializeField] private float cadaCuantoPolvo = 5f;
    [Tooltip("Clave del sonido al descubrirla (Resources/RecursosRPG).")]
    [SerializeField] private string sonidoDescubrir = "secreto_descubierto";

    private Tilemap tm;
    private bool pendiente = true;
    private readonly List<HashSet<Vector3Int>> paredes = new List<HashSet<Vector3Int>>();
    private readonly List<Vector2> grietas = new List<Vector2>();
    private readonly HashSet<int> descubiertas = new HashSet<int>();
    private GameObject contenedor;
    private float alfa = 1f;
    private float siguientePolvo;
    private Transform player;

    private void OnEnable()
    {
        tm = GetComponent<Tilemap>();
        Tilemap.tilemapTileChanged += AlCambiar;
        pendiente = true;
    }

    private void OnDisable()
    {
        Tilemap.tilemapTileChanged -= AlCambiar;
        if (contenedor != null) { if (Application.isPlaying) Destroy(contenedor); else DestroyImmediate(contenedor); }
    }

    private void OnValidate() => pendiente = true;

    private void AlCambiar(Tilemap t, Tilemap.SyncTile[] c)
    {
        if (t == tm) pendiente = true;
    }

    private void Update()
    {
        if (pendiente) { pendiente = false; Reconstruir(); }
        if (!Application.isPlaying) return;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        int dentro = player != null ? ParedEn(player.position + Vector3.up * 0.1f) : -1;
        if (dentro >= 0 && descubiertas.Add(dentro)) Sonido.Reproducir(sonidoDescubrir, 0.8f);

        float objetivo = dentro >= 0 ? alfaAtravesando : 1f;
        if (!Mathf.Approximately(alfa, objetivo))
        {
            alfa = Mathf.MoveTowards(alfa, objetivo, Time.deltaTime * 3f);
            Pintar(alfa);
        }

        if (cadaCuantoPolvo > 0f && grietas.Count > 0 && Time.time >= siguientePolvo)
        {
            siguientePolvo = Time.time + cadaCuantoPolvo * Random.Range(0.7f, 1.4f) / Mathf.Max(1, grietas.Count);
            Vector2 g = grietas[Random.Range(0, grietas.Count)] + Vector2.up * Random.Range(-0.8f, 0.8f);
            ParticulasFx.Rafaga(g, 2, new Color(0.85f, 0.88f, 0.95f, 0.8f), new Color(0.5f, 0.52f, 0.6f, 0.6f),
                                new Vector2(0.1f, 0.35f), 0.3f, new Vector2(0.03f, 0.05f), new Vector2(0.8f, 1.4f), 60f, -90f);
        }
    }

    // Indice de la pared que ocupa ese punto (o de al lado, a menos de un cuarto de casilla), o -1.
    private int ParedEn(Vector3 punto)
    {
        foreach (float dx in new[] { 0f, -0.25f, 0.25f })
        {
            Vector3Int c = tm.WorldToCell(punto + Vector3.right * dx);
            for (int i = 0; i < paredes.Count; i++) if (paredes[i].Contains(c)) return i;
        }
        return -1;
    }

    private void Pintar(float a)
    {
        tm.color = new Color(tinte.r, tinte.g, tinte.b, tinte.a * a);
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, a);
        }
    }

    // Separa las paredes (grupos de casillas pegadas) y pone una grieta en cada una.
    private void Reconstruir()
    {
        paredes.Clear();
        grietas.Clear();
        if (contenedor != null) { if (Application.isPlaying) Destroy(contenedor); else DestroyImmediate(contenedor); }
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name == "Grietas (automatico)")
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

        var vistas = new HashSet<Vector3Int>();
        foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
        {
            if (!tm.HasTile(c) || vistas.Contains(c)) continue;
            var grupo = new HashSet<Vector3Int>();
            var cola = new Queue<Vector3Int>();
            cola.Enqueue(c);
            vistas.Add(c);
            while (cola.Count > 0)
            {
                Vector3Int a = cola.Dequeue();
                grupo.Add(a);
                foreach (Vector3Int d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    if (tm.HasTile(a + d) && vistas.Add(a + d)) cola.Enqueue(a + d);
            }
            paredes.Add(grupo);
        }

        if (grieta == null || paredes.Count == 0) return;
        contenedor = new GameObject("Grietas (automatico)");
        contenedor.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        contenedor.transform.SetParent(transform, false);
        foreach (HashSet<Vector3Int> p in paredes)
        {
            // La grieta va en la columna central de la pared, a lo alto de ella.
            int xMin = int.MaxValue, xMax = int.MinValue;
            foreach (Vector3Int c in p) { xMin = Mathf.Min(xMin, c.x); xMax = Mathf.Max(xMax, c.x); }
            int xc = (xMin + xMax) / 2;
            int yMin = int.MaxValue, yMax = int.MinValue;
            foreach (Vector3Int c in p) if (c.x == xc) { yMin = Mathf.Min(yMin, c.y); yMax = Mathf.Max(yMax, c.y); }
            Vector3 abajo = tm.CellToWorld(new Vector3Int(xc, yMin, 0));
            float alto = (yMax - yMin + 1) * tm.cellSize.y;
            Vector2 centro = new Vector2(abajo.x + tm.cellSize.x * 0.6f, abajo.y + alto * 0.5f);
            grietas.Add(centro);

            GameObject g = new GameObject("Grieta");
            g.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            g.transform.SetParent(contenedor.transform, false);
            g.transform.position = centro;
            g.transform.localScale = new Vector3(1f, Mathf.Min(alto, 3f) / grieta.bounds.size.y, 1f);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = grieta;
            TilemapRenderer tr = GetComponent<TilemapRenderer>();
            sr.sortingLayerName = tr != null ? tr.sortingLayerName : "Ground";
            sr.sortingOrder = (tr != null ? tr.sortingOrder : 0) + 3;
        }
        Pintar(alfa);
    }
}
