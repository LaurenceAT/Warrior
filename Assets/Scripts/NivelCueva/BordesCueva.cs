using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Bordes de la cueva, automaticos: la roca se pinta con la Tile Palette (tiles
// negros) y este componente coloca las losas del pack en los suelos y techos y
// los pilares en las paredes, en cada lado de roca que da a espacio abierto.
// Se rehace solo al pintar o borrar (tambien en el editor), asi que basta con
// pintar la forma de la cueva.
//
// Las piezas no se guardan en la escena (se crean al abrirla). Las que van en el
// borde de una pared falsa cuelgan de la pared falsa, para desvanecerse con ella.
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class BordesCueva : MonoBehaviour
{
    [Tooltip("Archivo de configuracion del nivel: de ahi salen las losas, los pilares y su color.")]
    public ConfigNivel config;
    [Tooltip("Grosor de las losas y los pilares.")]
    public float grosor = 0.6f;
    [Tooltip("Cambialo para otra combinacion de piezas.")]
    public int semilla = 7;

    private Tilemap tm;
    private bool pendiente = true;
    private readonly List<GameObject> contenedores = new List<GameObject>();

    private void OnEnable()
    {
        tm = GetComponent<Tilemap>();
        Tilemap.tilemapTileChanged += AlCambiar;
        pendiente = true;
    }

    private void OnDisable()
    {
        Tilemap.tilemapTileChanged -= AlCambiar;
        Limpiar();
    }

    private void OnValidate() => pendiente = true;

    private void AlCambiar(Tilemap t, Tilemap.SyncTile[] c)
    {
        if (tm != null && t.layoutGrid == tm.layoutGrid) pendiente = true;
    }

    private void Update()
    {
        if (!pendiente) return;
        pendiente = false;
        Reconstruir();
    }

    public void Reconstruir()
    {
        Limpiar();
        if (tm == null) tm = GetComponent<Tilemap>();
        if (config == null || ((config.losas == null || config.losas.Length == 0) && (config.pilares == null || config.pilares.Length == 0))) return;

        // Roca de verdad (este Tilemap) y paredes falsas (los de ParedFalsa del Grid).
        var falsas = new List<Tilemap>();
        foreach (ParedFalsa pf in tm.layoutGrid.GetComponentsInChildren<ParedFalsa>())
        {
            Tilemap f = pf.GetComponent<Tilemap>();
            if (f != null) falsas.Add(f);
        }

        Transform real = Contenedor(transform);
        var porFalsa = new Dictionary<Tilemap, Transform>();

        bool Roca(Vector3Int c)
        {
            if (tm.HasTile(c)) return true;
            foreach (Tilemap f in falsas) if (f.HasTile(c)) return true;
            return false;
        }
        Tilemap FalsaEn(Vector3Int c)
        {
            if (tm.HasTile(c)) return null;
            foreach (Tilemap f in falsas) if (f.HasTile(c)) return f;
            return null;
        }
        Transform Padre(Tilemap f)
        {
            if (f == null) return real;
            if (!porFalsa.TryGetValue(f, out Transform p)) porFalsa[f] = p = Contenedor(f.transform);
            return p;
        }

        BoundsInt b = tm.cellBounds;
        foreach (Tilemap f in falsas)
        {
            BoundsInt fb = f.cellBounds;
            if (fb.size.x == 0) continue;
            b.xMin = Mathf.Min(b.xMin, fb.xMin); b.yMin = Mathf.Min(b.yMin, fb.yMin);
            b.xMax = Mathf.Max(b.xMax, fb.xMax); b.yMax = Mathf.Max(b.yMax, fb.yMax);
        }

        // Suelos (arriba) y techos (abajo): tramos seguidos de la misma capa.
        for (int y = b.yMin; y < b.yMax; y++)
            for (int lado = 1; lado >= -1; lado -= 2)
            {
                int x = b.xMin;
                while (x < b.xMax)
                {
                    Vector3Int c = new Vector3Int(x, y, 0);
                    if (!Roca(c) || Roca(c + new Vector3Int(0, lado, 0))) { x++; continue; }
                    Tilemap f = FalsaEn(c);
                    int x0 = x;
                    while (x < b.xMax)
                    {
                        Vector3Int d = new Vector3Int(x, y, 0);
                        if (!Roca(d) || Roca(d + new Vector3Int(0, lado, 0)) || FalsaEn(d) != f) break;
                        x++;
                    }
                    Vector3 w0 = tm.CellToWorld(new Vector3Int(x0, y, 0)), w1 = tm.CellToWorld(new Vector3Int(x, y, 0));
                    float yBorde = lado > 0 ? w0.y + tm.cellSize.y : w0.y;
                    Losas(Padre(f), w0.x, w1.x, yBorde, lado < 0, x0 * 31 + y * 17);
                }
            }

        // Paredes (izquierda y derecha).
        for (int x = b.xMin; x < b.xMax; x++)
            for (int lado = 1; lado >= -1; lado -= 2)
            {
                int y = b.yMin;
                while (y < b.yMax)
                {
                    Vector3Int c = new Vector3Int(x, y, 0);
                    if (!Roca(c) || Roca(c + new Vector3Int(lado, 0, 0))) { y++; continue; }
                    Tilemap f = FalsaEn(c);
                    int y0 = y;
                    while (y < b.yMax)
                    {
                        Vector3Int d = new Vector3Int(x, y, 0);
                        if (!Roca(d) || Roca(d + new Vector3Int(lado, 0, 0)) || FalsaEn(d) != f) break;
                        y++;
                    }
                    Vector3 w0 = tm.CellToWorld(new Vector3Int(x, y0, 0)), w1 = tm.CellToWorld(new Vector3Int(x, y, 0));
                    float xBorde = lado > 0 ? w0.x + tm.cellSize.x : w0.x;
                    Pilares(Padre(f), w0.y, w1.y, xBorde, lado > 0, x * 13 + y0 * 29);
                }
            }
    }

    // Fila de losas a lo largo de un borde horizontal. Asoman un poco por encima
    // de la roca para que los pies no se hundan en la pintura.
    private void Losas(Transform padre, float x0, float x1, float y, bool techo, int s)
    {
        Sprite[] losas = config.losas;
        if (losas == null || losas.Length == 0) return;
        System.Random azar = new System.Random(semilla * 1000 + s);
        float x = x0 - 0.1f, fin = x1 + 0.1f;
        while (x < fin - 0.05f)
        {
            Sprite sp = losas[azar.Next(losas.Length)];
            if (sp == null) break;
            float ancho = Mathf.Min(grosor * sp.rect.width / sp.rect.height, fin - x);
            float cy = techo ? y - 0.08f + grosor * 0.5f : y + 0.08f - grosor * 0.5f;
            Pieza(padre, "Losa", sp, new Vector2(x + ancho * 0.5f, cy), new Vector2(ancho, grosor), 2, techo, false);
            x += ancho * 0.92f;
        }
    }

    private void Pilares(Transform padre, float y0, float y1, float x, bool derecha, int s)
    {
        Sprite[] pilares = config.pilares;
        if (pilares == null || pilares.Length == 0) return;
        System.Random azar = new System.Random(semilla * 1000 + s);
        float y = y0 - 0.1f, fin = y1 + 0.1f;
        while (y < fin - 0.05f)
        {
            Sprite sp = pilares[azar.Next(pilares.Length)];
            if (sp == null) break;
            float alto = Mathf.Min(grosor * sp.rect.height / sp.rect.width, fin - y);
            float cx = derecha ? x + 0.08f - grosor * 0.5f : x - 0.08f + grosor * 0.5f;
            Pieza(padre, "Pilar", sp, new Vector2(cx, y + alto * 0.5f), new Vector2(grosor, alto), 1, false, !derecha);
            y += alto * 0.92f;
        }
    }

    private void Pieza(Transform padre, string nombre, Sprite s, Vector2 centro, Vector2 tamano, int orden, bool voltearY, bool voltearX)
    {
        GameObject go = new GameObject(nombre);
        go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        go.transform.SetParent(padre, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.flipY = voltearY;
        sr.flipX = voltearX;
        sr.color = config.colorPiezas;
        sr.sortingLayerName = "Ground";
        sr.sortingOrder = orden;
        Vector2 escala = new Vector2(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y);
        go.transform.localScale = new Vector3(escala.x, escala.y, 1f);
        Vector2 desvio = new Vector2(s.bounds.center.x * escala.x, s.bounds.center.y * escala.y * (voltearY ? -1f : 1f));
        go.transform.position = new Vector3(centro.x - desvio.x, centro.y - desvio.y, 0f);
    }

    private Transform Contenedor(Transform padre)
    {
        GameObject go = new GameObject("Bordes (automatico)");
        go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        go.transform.SetParent(padre, false);
        contenedores.Add(go);
        return go.transform;
    }

    private void Limpiar()
    {
        foreach (GameObject g in contenedores)
            if (g != null)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }
        contenedores.Clear();
        // Restos de una recarga de scripts (la lista se pierde, los objetos no).
        foreach (Transform t in new[] { transform }) Restos(t);
        foreach (ParedFalsa pf in GetComponentInParent<Grid>() != null ? GetComponentInParent<Grid>().GetComponentsInChildren<ParedFalsa>() : new ParedFalsa[0]) Restos(pf.transform);
    }

    private static void Restos(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform c = t.GetChild(i);
            if (c.name != "Bordes (automatico)") continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }
}
