using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 20: The Blind Huntress un 25 % mas grande.
//   Warrior > Cazadora > Tamano (Ronda 20): sus sprites (cuerpo y efectos) se
//   importan a menos pixeles por unidad, asi que se ven mas grandes; sus cajas de
//   dano (una por cuadro, sacadas del dibujo) y los cuerpos golpeables (el suyo y
//   el de la plantilla de ilusion) crecen lo mismo. Lo que va pegado a ella
//   (sombra, burbuja, textos) lo escala el codigo con SpritesCazadora.Escala.
// Se puede repetir: calcula lo que falta desde el tamano actual.
public static class Ronda20
{
    public const float Escala = 1.25f;
    private const float PPUBase = 28f;
    private const string RutaSprites = "Assets/Data/Jefes/Sprites Cazadora.asset";
    private const string RutaPrefab = "Assets/Prefabs/Enemies/Bosque/CazadoraCiega.prefab";
    private static readonly string[] Carpetas = { "Assets/Sprites/Cazadora/Cuerpo", "Assets/Sprites/Cazadora/Efecto" };

    [MenuItem("Warrior/Cazadora/Tamaño (Ronda 20)")]
    public static void Agrandar()
    {
        SpritesCazadora sc = AssetDatabase.LoadAssetAtPath<SpritesCazadora>(RutaSprites);
        if (sc == null) { Debug.LogError("[Ronda20] Falta " + RutaSprites); return; }
        float ppu = PPUBase / Escala;
        float factor = sc.pixelesPorUnidad / ppu;   // lo que falta por crecer
        if (Mathf.Abs(factor - 1f) < 0.001f) { Debug.Log("[Ronda20] Ya tiene el tamano pedido (x" + Escala + ")."); return; }

        // 1. Sprites: menos pixeles por unidad = mas grandes (mismos pivotes).
        int texturas = 0;
        foreach (string carpeta in Carpetas)
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti == null || Mathf.Abs(ti.spritePixelsPerUnit - ppu) < 0.001f) continue;
            ti.spritePixelsPerUnit = ppu;
            ti.SaveAndReimport();
            texturas++;
        }

        // 2. Cajas de dano de cada cuadro.
        int cajas = 0;
        foreach (SpritesCazadora.Clip c in sc.clips)
            for (int i = 0; i < c.cajas.Length; i++)
            {
                Rect r = c.cajas[i];
                c.cajas[i] = new Rect(r.x * factor, r.y * factor, r.width * factor, r.height * factor);
                if (r.width > 0f) cajas++;
            }
        sc.pixelesPorUnidad = ppu;
        EditorUtility.SetDirty(sc);

        // 3. Cuerpos golpeables (el suyo y el de la plantilla de ilusion).
        GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
        int colliders = 0;
        foreach (Collider2D col in raiz.GetComponentsInChildren<Collider2D>(true))
        {
            col.offset *= factor;
            if (col is CapsuleCollider2D cap) cap.size *= factor;
            else if (col is BoxCollider2D box) box.size *= factor;
            else if (col is CircleCollider2D cir) cir.radius *= factor;
            colliders++;
        }
        PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        PrefabUtility.UnloadPrefabContents(raiz);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Ronda20] Cazadora x{Escala}: {texturas} texturas a {ppu:0.##} px/u, {cajas} cajas y {colliders} cuerpos golpeables x{factor:0.###}");
    }
}
