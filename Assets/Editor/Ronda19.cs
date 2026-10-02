using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Ronda 19: nieve renovada (diseno, secretos, mecanicas y enemigos).
public static class Ronda19
{
    private const string RutaNieve = "Assets/Scenes/Nivel Nieve.unity";

    // Solo lee: inventario completo del nivel de nieve actual.
    public static void Diagnostico()
    {
        EditorSceneManager.OpenScene(RutaNieve, OpenSceneMode.Single);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Raices");
        foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            sb.AppendLine($"{r.name} activo={r.activeSelf} hijos={r.transform.childCount}");
            foreach (Transform h in r.transform)
                sb.AppendLine($"   {h.name} activo={h.gameObject.activeSelf} hijos={h.childCount} pos={h.position} [{string.Join(",", h.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name))}]");
        }

        sb.AppendLine("=== Tilemaps");
        foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            var usos = new Dictionary<string, int>();
            foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
            {
                TileBase t = tm.GetTile(c);
                if (t == null) continue;
                usos.TryGetValue(t.name, out int n);
                usos[t.name] = n + 1;
            }
            Vector3 min = tm.CellToWorld(tm.cellBounds.min), max = tm.CellToWorld(tm.cellBounds.max);
            sb.AppendLine($"{Ruta(tm.transform)} celda={tm.layoutGrid.cellSize.x} mundo={min}..{max} col={(tm.GetComponent<TilemapCollider2D>() != null)} capa={LayerMask.LayerToName(tm.gameObject.layer)} tiles: {string.Join(", ", usos.OrderByDescending(u => u.Value).Take(12).Select(u => u.Key + "x" + u.Value))}");
        }

        string[] tipos =
        {
            "Hoguera", "CofreMejora", "CofreAlmas", "EstatuaPista", "PistaNarrativa", "SelloSombrio", "MuroHielo", "AguaCongelable",
            "SueloHielo", "ZonaVentisca", "ParedFalsa", "ZonaOculta", "DeadArea", "PortalNivel", "PortalEntrada", "ZonaCamara", "ArenaJefe",
            "ZonaJefe", "Damage", "TrapFx", "ArrowLauncher", "SawController", "PendulumTrap", "FallingPlatform", "Trampoline", "ZonaDano",
            "Checkpoint", "AgarreCornisa", "FrascoExtra", "HealthPickup", "Diamond", "DoorIn", "DoorsEvent", "AmbienteNieve", "DatosNivel",
            "TotemTienda", "Estalactita", "SueloFalso", "GrietaMortal",
        };
        sb.AppendLine("=== Objetos");
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(m => m.transform.position.x))
        {
            string tipo = mb.GetType().Name;
            if (!tipos.Contains(tipo)) continue;
            sb.Append($"{tipo} '{Ruta(mb.transform)}' pos=({mb.transform.position.x:0.0},{mb.transform.position.y:0.0}) activo={mb.gameObject.activeInHierarchy}");
            Collider2D col = mb.GetComponent<Collider2D>();
            if (col != null) sb.Append($" col={col.bounds.min.x:0.0},{col.bounds.min.y:0.0}..{col.bounds.max.x:0.0},{col.bounds.max.y:0.0} trig={col.isTrigger}");
            sb.AppendLine();
            Campos(sb, mb);
        }

        sb.AppendLine("=== Enemigos");
        foreach (EnemigoBase e in Object.FindObjectsByType<EnemigoBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.transform.position.x))
        {
            SerializedObject so = new SerializedObject(e);
            SerializedProperty def = so.FindProperty("definicion");
            string d = def != null && def.objectReferenceValue != null ? def.objectReferenceValue.name : "-";
            sb.AppendLine($"{e.GetType().Name} '{e.name}' pos=({e.transform.position.x:0.0},{e.transform.position.y:0.0}) def={d} activo={e.gameObject.activeInHierarchy}");
        }
        foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(h => h.GetComponent<EnemigoBase>() == null))
            sb.AppendLine($"(sin EnemigoBase) '{e.name}' pos=({e.transform.position.x:0.0},{e.transform.position.y:0.0}) [{string.Join(",", e.GetComponents<MonoBehaviour>().Select(c => c.GetType().Name))}]");

        sb.AppendLine("=== Nombres sospechosos (pinchos, portales, trampas)");
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(t => t.position.x))
        {
            string n = t.name.ToLower();
            if (!(n.Contains("pinch") || n.Contains("spike") || n.Contains("trap") || n.Contains("trampa") || n.Contains("portal") || n.Contains("sello") || n.Contains("oscur"))) continue;
            sb.AppendLine($"[{Ruta(t)}] pos=({t.position.x:0.0},{t.position.y:0.0}) activo={t.gameObject.activeInHierarchy} [{string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name))}]");
        }

        PlayerControler pc = Object.FindFirstObjectByType<PlayerControler>(FindObjectsInactive.Include);
        if (pc != null)
        {
            sb.AppendLine("=== Player " + pc.transform.position);
            Campos(sb, pc, true);
            Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null) sb.AppendLine($"   rb gravityScale={rb.gravityScale} gravedad={Physics2D.gravity}");
        }
        File.WriteAllText("Logs/Ronda19_Diagnostico.txt", sb.ToString());
    }

    // Capturas del nivel (modo edicion) por zonas: PruebaNieve/r19_vista_*.png.
    public static void Capturas()
    {
        EditorSceneManager.OpenScene(RutaNieve, OpenSceneMode.Single);
        string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
        Directory.CreateDirectory(carpeta);
        var vistas = new (string n, Rect r)[]
        {
            ("1_campamento", Rect.MinMaxRect(-6f, -7f, 40f, 16f)),
            ("2_bosque", Rect.MinMaxRect(38f, -7f, 84f, 16f)),
            ("2b_loma_claro", Rect.MinMaxRect(76f, -7f, 124f, 16f)),
            ("4_desfiladero_a", Rect.MinMaxRect(118f, -7f, 160f, 16f)),
            ("4_desfiladero_b", Rect.MinMaxRect(156f, -7f, 202f, 16f)),
            ("5_lago_tumulo", Rect.MinMaxRect(196f, -7f, 244f, 16f)),
            ("6_cuevas", Rect.MinMaxRect(236f, -7f, 286f, 16f)),
            ("7_ruinas", Rect.MinMaxRect(280f, -7f, 336f, 27f)),
            ("9_antesala", Rect.MinMaxRect(326f, -7f, 400f, 27f)),
        };
        GameObject go = new GameObject("CamCaptura");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
        foreach (var v in vistas)
        {
            int ancho = 1600, alto = Mathf.RoundToInt(ancho * v.r.height / v.r.width);
            cam.orthographicSize = v.r.height * 0.5f;
            cam.aspect = v.r.width / v.r.height;
            go.transform.position = new Vector3(v.r.center.x, v.r.center.y, -20f);
            RenderTexture rt = new RenderTexture(ancho, alto, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, "r19_vista_" + v.n + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
        }
        Object.DestroyImmediate(go);
    }

    // Perfil del terreno: para cada tramo de columnas con el mismo dibujo, los
    // tramos solidos (y desde..hasta). Tambien las zonas ocultas y paredes falsas.
    public static void Perfil()
    {
        EditorSceneManager.OpenScene(RutaNieve, OpenSceneMode.Single);
        StringBuilder sb = new StringBuilder();
        foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            if (tm.GetUsedTilesCount() == 0) continue;
            sb.AppendLine("=== " + tm.name);
            BoundsInt b = tm.cellBounds;
            string anterior = null;
            int desde = b.xMin;
            for (int x = b.xMin; x <= b.xMax; x++)
            {
                var tramos = new List<string>();
                int y0 = int.MinValue;
                for (int y = b.yMin; y <= b.yMax; y++)
                {
                    bool s = y < b.yMax && tm.HasTile(new Vector3Int(x, y, 0));
                    if (s && y0 == int.MinValue) y0 = y;
                    if (!s && y0 != int.MinValue) { tramos.Add(y0 + ".." + (y - 1)); y0 = int.MinValue; }
                }
                string fila = string.Join(" ", tramos);
                if (x == b.xMax || (anterior != null && fila != anterior))
                {
                    sb.AppendLine($"x {desde}..{x - 1}: {anterior}");
                    desde = x;
                }
                anterior = fila;
            }
        }
        foreach (EnemigoBase e in Object.FindObjectsByType<EnemigoBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).Take(1))
        {
            sb.AppendLine("=== Campos de " + e.GetType().Name);
            Campos(sb, e);
        }
        File.WriteAllText("Logs/Ronda19_Perfil.txt", sb.ToString());
    }

    private static void Campos(StringBuilder sb, Object o, bool soloNumeros = false)
    {
        SerializedObject so = new SerializedObject(o);
        SerializedProperty p = so.GetIterator();
        p.NextVisible(true);
        while (p.NextVisible(false))
        {
            if (p.name == "m_Script") continue;
            switch (p.propertyType)
            {
                case SerializedPropertyType.String:
                    if (!soloNumeros && !string.IsNullOrEmpty(p.stringValue)) sb.AppendLine($"   {p.name} = \"{p.stringValue.Replace("\n", " / ")}\"");
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                    string v = p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString("0.###") : p.propertyType == SerializedPropertyType.Boolean ? p.boolValue.ToString()
                             : p.propertyType == SerializedPropertyType.Enum ? (p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString()) : p.intValue.ToString();
                    sb.AppendLine($"   {p.name} = {v}");
                    break;
                case SerializedPropertyType.Vector2:
                    sb.AppendLine($"   {p.name} = {p.vector2Value}");
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (!soloNumeros && p.objectReferenceValue != null) sb.AppendLine($"   {p.name} -> {p.objectReferenceValue.name}");
                    break;
                case SerializedPropertyType.Generic:
                    if (!soloNumeros && p.isArray && p.arraySize > 0 && p.arraySize <= 12)
                    {
                        var vals = new List<string>();
                        for (int i = 0; i < p.arraySize; i++)
                        {
                            SerializedProperty e = p.GetArrayElementAtIndex(i);
                            vals.Add(e.propertyType == SerializedPropertyType.String ? "\"" + e.stringValue.Replace("\n", " / ") + "\"" :
                                     e.propertyType == SerializedPropertyType.Enum ? e.enumDisplayNames.ElementAtOrDefault(e.enumValueIndex) :
                                     e.propertyType == SerializedPropertyType.Integer ? e.intValue.ToString() :
                                     e.propertyType == SerializedPropertyType.ObjectReference ? (e.objectReferenceValue != null ? e.objectReferenceValue.name : "null") : e.type);
                        }
                        sb.AppendLine($"   {p.name}[{p.arraySize}] = {string.Join(" | ", vals)}");
                    }
                    break;
            }
        }
    }

    private static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;
}
