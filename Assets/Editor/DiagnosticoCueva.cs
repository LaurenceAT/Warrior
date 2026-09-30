using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Solo lectura: inventario de una escena de nivel (por defecto la cueva) para
// el diagnostico. Escribe un informe de texto y no guarda la escena.
public static class DiagnosticoCueva
{
    public static void Inventario() => Inventario(System.Environment.GetEnvironmentVariable("ESCENA_DIAG") ?? "Assets/Scenes/Nivel Cueva.unity");

    public static void Inventario(string ruta)
    {
        EditorSceneManager.OpenScene(ruta);
        var sb = new StringBuilder();
        sb.AppendLine("== RAICES");
        foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            sb.AppendLine($"  {r.name} (activo={r.activeSelf}) hijos={r.transform.childCount} pos={r.transform.position}");

        sb.AppendLine("== JERARQUIA (Nivel y DecoracionExtra, 2 niveles)");
        foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name != "Nivel" && r.name != "DecoracionExtra" && r.name != "Estatuas") continue;
            foreach (Transform h in r.transform)
            {
                sb.AppendLine($"  {r.name}/{h.name} hijos={h.childCount} comps=[{string.Join(",", h.GetComponents<Component>().Select(c => c.GetType().Name))}]");
                if (h.childCount <= 12)
                    foreach (Transform n in h) sb.AppendLine($"     {n.name} pos={n.position} comps=[{string.Join(",", n.GetComponents<Component>().Select(c => c.GetType().Name))}]");
            }
        }

        sb.AppendLine("== TILEMAPS");
        foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            TilemapRenderer tr = tm.GetComponent<TilemapRenderer>();
            var sprites = new HashSet<Sprite>();
            foreach (Vector3Int c in tm.cellBounds.allPositionsWithin) { Sprite s = tm.GetSprite(c); if (s != null) sprites.Add(s); }
            var texturas = sprites.GroupBy(s => s.texture).Select(g => $"{g.Key.name}(ppu {g.First().pixelsPerUnit}, filtro {g.Key.filterMode}, {g.Count()} sprites)");
            sb.AppendLine($"  {Ruta(tm.transform)} capa={tm.gameObject.layer} orden={(tr != null ? tr.sortingLayerName + "/" + tr.sortingOrder : "-")} celda={tm.layoutGrid.cellSize} bounds={tm.cellBounds} tiles={tm.GetUsedTilesCount()} " +
                          $"colision={(tm.GetComponent<TilemapCollider2D>() != null)} composite={(tm.GetComponent<CompositeCollider2D>() != null)}");
            sb.AppendLine("     texturas: " + string.Join(", ", texturas));
        }

        sb.AppendLine("== SPRITES por textura (sin tilemaps)");
        var srs = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(s => s.sprite != null).ToList();
        foreach (var g in srs.GroupBy(s => s.sprite.texture).OrderByDescending(g => g.Count()))
        {
            Sprite s0 = g.First().sprite;
            var escalas = g.Select(s => s.transform.lossyScale).Select(e => $"{Mathf.Abs(e.x):0.##}").Distinct().Take(6);
            var capas = g.Select(s => s.sortingLayerName).Distinct();
            var raices = g.Select(s => s.transform.root.name).Distinct().Take(4);
            sb.AppendLine($"  {g.Key.name} x{g.Count()} ppu={s0.pixelsPerUnit} filtro={g.Key.filterMode} escalas=[{string.Join(",", escalas)}] capas=[{string.Join(",", capas)}] en=[{string.Join(",", raices)}]");
        }

        sb.AppendLine("== COLISIONES Ground");
        int ground = LayerMask.NameToLayer("Ground");
        var cols = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.gameObject.layer == ground).ToList();
        foreach (var g in cols.GroupBy(c => c.GetType().Name)) sb.AppendLine($"  {g.Key}: {g.Count()}");

        sb.AppendLine("== OBJETOS DE JUEGO");
        void Lista<T>(string titulo, System.Func<T, string> texto) where T : Component
        {
            var l = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(c => c.transform.position.x).ToList();
            sb.AppendLine($"  -- {titulo}: {l.Count}");
            foreach (T c in l) sb.AppendLine($"     {c.transform.position.x,7:0.0} {c.transform.position.y,6:0.0}  {c.name}  {texto(c)}");
        }
        Lista<Hoguera>("Hogueras", h => "");
        Lista<CofreMejora>("Cofres de mejora", c => Campos(c, "objeto", "cantidad", "extras", "clave"));
        Lista<CofreAlmas>("Cofres de almas", c => Campos(c, "almas", "darFrasco", "clave"));
        Lista<EstatuaPista>("Estatuas", e => $"id={e.id} \"{e.titulo}\": {e.texto?.Replace("\n", " ")}");
        Lista<EnemyHealth>("Enemigos", e => e.Definicion != null ? e.Definicion.name : "(sin ficha)");
        Lista<Estalactita>("Estalactitas (trampa)", e => "");
        Lista<TrampaFuego>("Fuegos (trampa)", e => "");
        Lista<ZonaDano>("ZonaDano", e => "");
        Lista<DeadArea>("DeadArea", e => Bounds(e));
        Lista<PortalNivel>("PortalNivel", e => Campos(e, "escenaDestino"));
        Lista<PortalEntrada>("PortalEntrada", e => "");
        Lista<ZonaCamara>("ZonaCamara", e => $"tamano={e.Tamano} dy={e.DesplazamientoY} {Bounds(e)}");
        Lista<ArenaJefe>("ArenaJefe", e => Campos(e, "zona", "suelo", "nombre") + " " + Bounds(e));
        Lista<ParallaxCueva>("ParallaxCueva", e => $"capas={e.capas?.Length}");
        Lista<Checkpoint>("Checkpoint", e => "");
        foreach (string n in new[] { "CameraLimit", "RespawnPoint", "Player", "EntranceDoor", "ExitDoor", "CinemachineCamera" })
        {
            GameObject go = GameObject.Find(n);
            if (go == null) continue;
            sb.AppendLine($"  {n}: pos={go.transform.position} activo={go.activeSelf}");
            PolygonCollider2D poly = go.GetComponent<PolygonCollider2D>();
            if (poly != null) sb.AppendLine("     limite: " + string.Join(" ", poly.GetPath(0).Select(p => (Vector2)go.transform.TransformPoint(p))));
        }
        sb.AppendLine("  Luces2D: " + Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        sb.AppendLine("  Particulas: " + Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        sb.AppendLine("  AudioSources: " + string.Join(", ", Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(a => a.name + ":" + (a.clip != null ? a.clip.name : "-"))));

        string salida = Path.Combine(Path.GetTempPath(), "diagnostico_cueva.txt");
        File.WriteAllText(salida, sb.ToString());
        Debug.Log("[DiagCueva] " + salida);
    }

    private static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;

    private static string Bounds(Component c)
    {
        Collider2D col = c.GetComponent<Collider2D>();
        return col != null ? $"caja=({col.bounds.min.x:0.#},{col.bounds.min.y:0.#})-({col.bounds.max.x:0.#},{col.bounds.max.y:0.#})" : "";
    }

    private static string Campos(Object o, params string[] nombres)
    {
        var so = new SerializedObject(o);
        var partes = new List<string>();
        foreach (string n in nombres)
        {
            SerializedProperty p = so.FindProperty(n);
            if (p == null) continue;
            string v;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: v = p.intValue.ToString(); break;
                case SerializedPropertyType.Boolean: v = p.boolValue.ToString(); break;
                case SerializedPropertyType.Float: v = p.floatValue.ToString("0.##"); break;
                case SerializedPropertyType.String: v = p.stringValue; break;
                case SerializedPropertyType.Enum: v = p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString(); break;
                case SerializedPropertyType.Rect: v = p.rectValue.ToString(); break;
                default:
                    if (p.isArray) { var a = new List<string>(); for (int i = 0; i < p.arraySize; i++) { var e = p.GetArrayElementAtIndex(i); a.Add(e.propertyType == SerializedPropertyType.Enum ? e.enumDisplayNames[Mathf.Max(0, e.enumValueIndex)] : e.intValue.ToString()); } v = "[" + string.Join(",", a) + "]"; }
                    else v = p.propertyType.ToString();
                    break;
            }
            partes.Add($"{n}={v}");
        }
        return string.Join(" ", partes);
    }
}
