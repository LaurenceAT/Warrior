using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Recursos de la Ronda 8 (se hacen antes de tocar las escenas):
//   - Tajos recoloreados: sagrado amarillo con blanco y sangrado rojo, sacados
//     de los del pack cambiando el tono (tenirlos encima los ensuciaba).
//   - Mapa de la hoja: donde esta la espada en cada fotograma del player.
//   - Sangre: las salpicaduras de Efecto_Sangre pasadas a blanco (para tenirlas
//     de cualquier color) y unas manchas para el suelo.
//   - Player: particulas del arma y los fotogramas de la muerte en el aire.
//   - Enemigos: quien sangra y de que color.
public static partial class Ronda8
{
    private const string Sangre = "Assets/SPRITES PARA NUEVOS NIVELES/EFECTOS DE ENTORNO/Efecto_Sangre/";
    private const string SangrePropia = "Assets/Sprites/Sangre/";
    private const string PrefabPlayer = "Assets/Prefabs/Player.prefab";
    private const string Derribado = "Assets/Sprites/PJ_Knight/2 - Otras versiones/Combate cuerpo a cuerpo/Derribado/";

    private static void Recursos()
    {
        TajosRecoloreados();
        MapaDeLaHoja();
        RecursosSangre();
        PlayerPrefab();
        SangreEnemigos();
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ Tajos

    private static void TajosRecoloreados()
    {
        foreach (string slash in new[] { "Slash 1", "Slash 2", "Slash 3" })
        {
            // Sagrado: del naranja al amarillo, con el centro casi blanco.
            Recolorear(ConfigurarRecursosRPG.CarpetaTajoOriginal(4, slash), Elemento.Sagrado, slash, 0.14f, 0.8f, 0.55f);
            // Sangrado: del carmesi a un rojo limpio.
            Recolorear(ConfigurarRecursosRPG.CarpetaTajoOriginal(2, slash), Elemento.Sangrado, slash, 0.0f, 1.1f, 0f);
        }
        AssetDatabase.Refresh();
        RecursosRPG r = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        r.tajosElemento = ConfigurarRecursosRPG.TajosPorElemento();
        EditorUtility.SetDirty(r);
        Debug.Log("[Ronda8] Tajos: " + string.Join(", ", Elementos.Todos.Select(e => e + "=" + ConfigurarRecursosRPG.CarpetaTajo(e, "Slash 1"))));
    }

    private static void Recolorear(string origen, Elemento e, string slash, float tono, float saturacion, float blanco)
    {
        if (!Directory.Exists(origen)) { Debug.LogWarning("[Ronda8] No existe " + origen); return; }
        string destino = ConfigurarRecursosRPG.TajosPropios + e + "/" + slash + "/Frames";
        Directory.CreateDirectory(destino);
        foreach (string f in Directory.GetFiles(origen, "*.png"))
        {
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(f));
            Color32[] px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                Color n = Color.HSVToRGB(tono, Mathf.Clamp01(s * saturacion), v);
                // Lo mas brillante tira a blanco (el filo del tajo).
                n = Color.Lerp(n, Color.white, blanco * v * v);
                px[i] = new Color32((byte)(n.r * 255), (byte)(n.g * 255), (byte)(n.b * 255), px[i].a);
            }
            t.SetPixels32(px);
            string nombre = Path.GetFileName(f).Replace("color2", "").Replace("color4", "");
            File.WriteAllBytes(Path.Combine(destino, nombre), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }
    }

    // ------------------------------------------------------------------ Mapa de la hoja

    // Tonos de la hoja (los del sombreador del brillo de la hoja).
    private static readonly Color[] TonosHoja =
    {
        new Color(0.761f, 0.784f, 0.941f), new Color(0.647f, 0.682f, 0.918f), new Color(0.522f, 0.565f, 0.624f),
    };

    private static void MapaDeLaHoja()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPlayer);
        Animator an = prefab != null ? prefab.GetComponent<Animator>() : null;
        if (an == null || an.runtimeAnimatorController == null) { Debug.LogWarning("[Ronda8] Sin Animator en el player"); return; }

        var sprites = new HashSet<Sprite>();
        foreach (AnimationClip c in an.runtimeAnimatorController.animationClips)
            foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(c))
                foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(c, b))
                    if (k.value is Sprite s) sprites.Add(s);

        const string ruta = "Assets/Resources/MapaHoja.asset";
        MapaHoja mapa = AssetDatabase.LoadAssetAtPath<MapaHoja>(ruta);
        if (mapa == null) { mapa = ScriptableObject.CreateInstance<MapaHoja>(); AssetDatabase.CreateAsset(mapa, ruta); }
        mapa.entradas.Clear();

        var texturas = new Dictionary<string, Texture2D>();
        int conHoja = 0;
        foreach (Sprite s in sprites)
        {
            string archivo = AssetDatabase.GetAssetPath(s.texture);
            if (!texturas.TryGetValue(archivo, out Texture2D t))
            {
                t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(archivo));
                texturas[archivo] = t;
            }
            Rect r = s.textureRect;
            var puntos = new List<Vector2>();
            for (int y = (int)r.y; y < (int)r.yMax; y++)
                for (int x = (int)r.x; x < (int)r.xMax; x++)
                {
                    Color c = t.GetPixel(x, y);
                    if (c.a < 0.5f) continue;
                    if (!TonosHoja.Any(k => Mathf.Abs(k.r - c.r) + Mathf.Abs(k.g - c.g) + Mathf.Abs(k.b - c.b) < 0.09f)) continue;
                    // Centro del pixel, en unidades respecto al pivote del sprite.
                    puntos.Add(new Vector2(x - r.x + 0.5f - s.pivot.x, y - r.y + 0.5f - s.pivot.y) / s.pixelsPerUnit);
                }
            if (puntos.Count == 0) continue;
            // Unos pocos repartidos por la hoja bastan.
            int paso = Mathf.Max(1, puntos.Count / 16);
            mapa.entradas.Add(new MapaHoja.Entrada { sprite = s, puntos = puntos.Where((p, i) => i % paso == 0).ToArray() });
            conHoja++;
        }
        foreach (Texture2D t in texturas.Values) Object.DestroyImmediate(t);
        EditorUtility.SetDirty(mapa);
        Debug.Log($"[Ronda8] Mapa de la hoja: {conHoja} de {sprites.Count} fotogramas con la hoja a la vista");
    }

    // ------------------------------------------------------------------ Sangre

    private static void RecursosSangre()
    {
        Directory.CreateDirectory(SangrePropia);
        var salpicaduras = new List<AnimadorHoja.Clip>();
        var manchas = new List<Sprite>();
        for (int pack = 1; pack <= 5; pack++)
        {
            string origen = Sangre + pack;
            if (!Directory.Exists(origen)) continue;
            string destino = SangrePropia + "Salpicadura" + pack;
            Directory.CreateDirectory(destino);
            // Los fotogramas se llaman 1_0 .. 1_29: se ordenan por el numero.
            var archivos = Directory.GetFiles(origen, "*.png")
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_').Last())).ToList();
            for (int i = 0; i < archivos.Count; i++)
            {
                string d = Path.Combine(destino, $"sangre_frame{i}.png").Replace('\\', '/');
                if (!File.Exists(d)) File.WriteAllBytes(d, EnBlanco(archivos[i]));
            }
        }
        AssetDatabase.Refresh();
        for (int pack = 1; pack <= 5; pack++)
        {
            string destino = SangrePropia + "Salpicadura" + pack;
            if (!Directory.Exists(destino)) continue;
            // Solo el principio de cada animacion: lo demas son gotas que se apagan.
            AnimadorHoja.Clip c = ConfigurarRecursosRPG.Clip("sangre_" + pack, destino, 30f, false, 45f, new Vector2(0.5f, 0.5f), 0, 17);
            if (c != null) salpicaduras.Add(c);
        }

        // Manchas: fotogramas de gotas ya desparramadas, recortados a su tamano.
        string carpetaManchas = SangrePropia + "Manchas";
        Directory.CreateDirectory(carpetaManchas);
        (int pack, int frame)[] elegidos = { (1, 9), (1, 12), (2, 10), (3, 9), (4, 7), (5, 9) };
        int n = 0;
        foreach ((int pack, int frame) in elegidos)
        {
            string f = SangrePropia + "Salpicadura" + pack + $"/sangre_frame{frame}.png";
            if (!File.Exists(f)) continue;
            string d = carpetaManchas + $"/mancha_{n++}.png";
            if (!File.Exists(d)) File.WriteAllBytes(d, Recortar(f));
        }
        AssetDatabase.Refresh();
        foreach (string f in Directory.GetFiles(carpetaManchas, "*.png").OrderBy(x => x))
        {
            string ruta = f.Replace('\\', '/');
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spritePixelsPerUnit != 28f))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePivot = new Vector2(0.5f, 0f);
                TextureImporterSettings st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                ti.SetTextureSettings(st);
                ti.spritePixelsPerUnit = 28f;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (s != null) manchas.Add(s);
        }

        const string rutaAj = "Assets/Resources/AjustesSangre.asset";
        AjustesSangre a = AssetDatabase.LoadAssetAtPath<AjustesSangre>(rutaAj);
        if (a == null) { a = ScriptableObject.CreateInstance<AjustesSangre>(); AssetDatabase.CreateAsset(a, rutaAj); }
        a.salpicaduras = salpicaduras.ToArray();
        a.manchas = manchas.ToArray();
        EditorUtility.SetDirty(a);
        Debug.Log($"[Ronda8] Sangre: {salpicaduras.Count} salpicaduras, {manchas.Count} manchas");
    }

    // El rojo del pack pasado a blanco conservando la luz y la sombra.
    private static byte[] EnBlanco(string archivo)
    {
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(archivo));
        Color32[] px = t.GetPixels32();
        byte max = 1;
        foreach (Color32 c in px) if (c.a > 0) max = (byte)Mathf.Max(max, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            byte v = (byte)Mathf.Clamp(Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) * 255 / max, 0, 255);
            px[i] = new Color32(v, v, v, px[i].a);
        }
        t.SetPixels32(px);
        byte[] png = t.EncodeToPNG();
        Object.DestroyImmediate(t);
        return png;
    }

    // Recorta un fotograma a lo que tiene pintado.
    private static byte[] Recortar(string archivo)
    {
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(archivo));
        int x0 = t.width, y0 = t.height, x1 = -1, y1 = -1;
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                if (t.GetPixel(x, y).a > 0.05f) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        if (x1 < 0) { byte[] vacio = t.EncodeToPNG(); Object.DestroyImmediate(t); return vacio; }
        Texture2D r = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
        r.SetPixels(t.GetPixels(x0, y0, r.width, r.height));
        r.Apply();
        byte[] png = r.EncodeToPNG();
        Object.DestroyImmediate(t);
        Object.DestroyImmediate(r);
        return png;
    }

    // ------------------------------------------------------------------ Player

    private static void PlayerPrefab()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(PrefabPlayer);
        try
        {
            if (raiz.GetComponent<EfectoArma>() == null) raiz.AddComponent<EfectoArma>();
            PlayerControler p = raiz.GetComponent<PlayerControler>();
            var fotos = Enumerable.Range(0, 7)
                .Select(i => AssetDatabase.LoadAllAssetsAtPath(Derribado + $"Derribado_0{i}.png").OfType<Sprite>().FirstOrDefault())
                .Where(s => s != null).ToArray();
            SerializedObject so = new SerializedObject(p);
            SerializedProperty prop = so.FindProperty("fotogramasCaidaMuerte");
            prop.arraySize = fotos.Length;
            for (int i = 0; i < fotos.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = fotos[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPlayer);
            Debug.Log($"[Ronda8] Player: EfectoArma y {fotos.Length} fotogramas de la muerte en el aire");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ------------------------------------------------------------------ Enemigos

    // Quien sangra: lo que esta vivo y es de carne. No: espectros ni sombras.
    private static readonly (string prefab, bool sangre, Color color)[] SangreDe =
    {
        ("Nieve/ArqueraArcana", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),     // humana
        ("Nieve/HechiceroSombrio", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),  // hechicero humano
        ("Nieve/MurcielagoCumbres", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)), // murcielago
        ("Nieve/OjoVigia", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),          // ojo volador, de carne
        ("Nieve/RataEscarcha", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),      // rata
        ("Nieve/SombraHumedales", false, Color.black),                           // sombra viva: no sangra
        ("Cueva/Cacodemonio", true, new Color(0.5f, 0.05f, 0.25f, 0.95f)),       // demonio: sangre oscura
        ("Cueva/Mago", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),              // humano
        ("Cueva/Mimic", true, new Color(0.7f, 0.03f, 0.06f, 0.95f)),             // la boca del cofre es de carne
        ("Cueva/Slime", true, new Color(0.35f, 0.85f, 0.25f, 0.9f)),             // baba verde
        ("Cueva/CrimsonWraith", false, Color.black),                             // espectro: no sangra
    };

    private static void SangreEnemigos()
    {
        foreach ((string nombre, bool sangre, Color color) in SangreDe)
        {
            string ruta = "Assets/Prefabs/Enemies/" + nombre + ".prefab";
            GameObject raiz = AssetDatabase.LoadAssetAtPath<GameObject>(ruta) != null ? PrefabUtility.LoadPrefabContents(ruta) : null;
            if (raiz == null) { Debug.LogWarning("[Ronda8] No esta " + ruta); continue; }
            try
            {
                foreach (EnemyHealth e in raiz.GetComponentsInChildren<EnemyHealth>(true))
                {
                    e.tieneSangre = sangre;
                    e.colorSangre = color;
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        Debug.Log("[Ronda8] Sangre de los enemigos configurada");
    }
}
