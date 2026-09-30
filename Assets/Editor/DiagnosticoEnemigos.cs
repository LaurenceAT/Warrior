using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Solo lectura: mide a los enemigos normales (tamano visible del sprite en
// pixeles y unidades, colisionador, vida, dano, almas...) y al player, y lo
// escribe en un informe de texto. No cambia nada del proyecto.
public static class DiagnosticoEnemigos
{
    public static readonly string[] Prefabs =
    {
        "Assets/Prefabs/Enemies/Nieve/RataEscarcha.prefab",
        "Assets/Prefabs/Enemies/Nieve/MurcielagoCumbres.prefab",
        "Assets/Prefabs/Enemies/Nieve/OjoVigia.prefab",
        "Assets/Prefabs/Enemies/Nieve/ArqueraArcana.prefab",
        "Assets/Prefabs/Enemies/Nieve/HechiceroSombrio.prefab",
        "Assets/Prefabs/Enemies/Cueva/Slime.prefab",
        "Assets/Prefabs/Enemies/Cueva/Cacodemonio.prefab",
        "Assets/Prefabs/Enemies/Cueva/Mago.prefab",
        "Assets/Prefabs/Enemies/Cueva/Mimic.prefab",
    };

    public static void Medir()
    {
        var sb = new StringBuilder();
        GameObject jugador = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        SpriteRenderer psr = jugador.GetComponent<SpriteRenderer>() ?? jugador.GetComponentInChildren<SpriteRenderer>();
        if (psr != null && psr.sprite != null)
        {
            Sprite s = psr.sprite;
            Vector2Int caja = Opaco(s.texture, s.rect);
            sb.AppendLine($"PLAYER sprite={s.name} ppu={s.pixelsPerUnit} visible={caja.x}x{caja.y}px = {caja.y / s.pixelsPerUnit:0.00}u alto, escala={psr.transform.lossyScale}");
        }
        foreach (Collider2D c in jugador.GetComponents<Collider2D>()) sb.AppendLine($"  col {c.GetType().Name} {c.bounds.size}");
        PrefabUtility.UnloadPrefabContents(jugador);

        foreach (string ruta in Prefabs)
        {
            GameObject go = PrefabUtility.LoadPrefabContents(ruta);
            sb.AppendLine("== " + Path.GetFileNameWithoutExtension(ruta));
            AnimadorHoja a = go.GetComponentInChildren<AnimadorHoja>(true);
            if (a != null)
            {
                a.Preparar();
                var nombres = new StringBuilder();
                foreach (AnimadorHoja.Clip c in a.clips) if (c != null) nombres.Append(c.nombre + "(" + c.cantidad + ") ");
                sb.AppendLine("  clips: " + nombres);
                AnimadorHoja.Clip q = null;
                foreach (string n in new[] { "quieto", "vuelo", "andar" })
                    if (q == null) q = a.clips.Find(c => c != null && c.nombre == n && c.sprites != null && c.sprites.Length > 0);
                if (q != null)
                {
                    Sprite s = q.sprites[0];
                    Vector2Int caja = Opaco(s.texture, s.rect);
                    Vector3 esc = a.transform.lossyScale;
                    sb.AppendLine($"  clip={q.nombre} ppu={q.pixelesPorUnidad} celda={s.rect.width}x{s.rect.height} visible={caja.x}x{caja.y}px = {caja.x / q.pixelesPorUnidad * Mathf.Abs(esc.x):0.00}x{caja.y / q.pixelesPorUnidad * Mathf.Abs(esc.y):0.00}u escalaVisual={esc}");
                }
            }
            foreach (Collider2D c in go.GetComponents<Collider2D>())
                sb.AppendLine($"  col {c.GetType().Name} trig={c.isTrigger} {c.bounds.size} off={c.offset}");
            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) sb.AppendLine($"  rb grav={rb.gravityScale} mass={rb.mass}");
            foreach (MonoBehaviour m in go.GetComponents<MonoBehaviour>())
            {
                if (m == null) continue;
                var so = new SerializedObject(m);
                var linea = new StringBuilder("  " + m.GetType().Name + ": ");
                SerializedProperty p = so.GetIterator();
                bool entrar = true;
                while (p.NextVisible(entrar))
                {
                    entrar = false;
                    if (p.name == "m_Script") continue;
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.Integer: linea.Append($"{p.name}={p.intValue} "); break;
                        case SerializedPropertyType.Float: linea.Append($"{p.name}={p.floatValue:0.##} "); break;
                        case SerializedPropertyType.Boolean: linea.Append($"{p.name}={(p.boolValue ? 1 : 0)} "); break;
                        case SerializedPropertyType.Vector2: linea.Append($"{p.name}=({p.vector2Value.x:0.##},{p.vector2Value.y:0.##}) "); break;
                    }
                }
                sb.AppendLine(linea.ToString());
            }
            PrefabUtility.UnloadPrefabContents(go);
        }
        string salida = Path.Combine(Path.GetTempPath(), "diagnostico_enemigos.txt");
        File.WriteAllText(salida, sb.ToString());
        Debug.Log("[Diagnostico] " + salida);
    }

    // Alto y ancho (en pixeles) de lo que se ve dentro del rectangulo del sprite.
    public static Vector2Int Opaco(Texture2D tex, Rect r)
    {
        string ruta = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return Vector2Int.zero;
        var copia = new Texture2D(2, 2);
        copia.LoadImage(File.ReadAllBytes(ruta));
        float fx = (float)copia.width / tex.width, fy = (float)copia.height / tex.height;
        int x0 = Mathf.RoundToInt(r.x * fx), y0 = Mathf.RoundToInt(r.y * fy);
        int w = Mathf.RoundToInt(r.width * fx), h = Mathf.RoundToInt(r.height * fy);
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        Color32[] px = copia.GetPixels32();
        for (int y = y0; y < y0 + h && y < copia.height; y++)
        for (int x = x0; x < x0 + w && x < copia.width; x++)
        {
            if (px[y * copia.width + x].a < 30) continue;
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        Object.DestroyImmediate(copia);
        if (maxX < 0) return Vector2Int.zero;
        return new Vector2Int(Mathf.RoundToInt((maxX - minX + 1) / fx), Mathf.RoundToInt((maxY - minY + 1) / fy));
    }
}

public static class DiagnosticoColocacion
{
    // Lista los enemigos de cada nivel ordenados por X (sin guardar la escena).
    public static void Listar()
    {
        var sb = new StringBuilder();
        foreach (string escena in new[] { "Assets/Scenes/Nivel Nieve.unity", "Assets/Scenes/Nivel Cueva.unity" })
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(escena);
            sb.AppendLine("== " + escena);
            var lista = new System.Collections.Generic.List<EnemyHealth>(Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None));
            lista.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            foreach (EnemyHealth e in lista) sb.AppendLine($"  {e.transform.position.x,8:0.0} {e.transform.position.y,7:0.0}  {e.name}");
            foreach (Hoguera h in Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None)) sb.AppendLine($"  HOGUERA {h.transform.position.x:0.0}");
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "colocacion_enemigos.txt"), sb.ToString());
    }
}
