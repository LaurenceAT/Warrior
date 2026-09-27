using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Inspector del archivo de configuracion de nivel (ConfigNivel), con el boton
// que vuelca el terreno y el fondo en la escena. Tambien crea los archivos de
// la cueva y la nieve a partir de lo que hay ahora en sus escenas
// (Warrior > Crear configuraciones de nivel).
[CustomEditor(typeof(ConfigNivel))]
public class ConfigNivelEditor : Editor
{
    public const string Carpeta = "Assets/Data/Niveles";
    public const string RutaNieve = Carpeta + "/Config Nivel Nieve.asset";
    public const string RutaCueva = Carpeta + "/Config Nivel Cueva.asset";
    public const string EscenaNieve = "Assets/Scenes/Nivel Nieve.unity";
    public const string EscenaCueva = "Assets/Scenes/Nivel Cueva.unity";

    public override void OnInspectorGUI()
    {
        ConfigNivel c = (ConfigNivel)target;
        string escena = EscenaDe(c);

        EditorGUILayout.HelpBox(
            "Musica, ambiente, sonidos y frases: se usan al darle a Play, sin hacer nada mas.\n" +
            "Terreno y fondo: estan dibujados en la escena. Despues de cambiarlos, pulsa el boton de abajo " +
            "(con la escena del nivel abierta) y guarda la escena (Ctrl+S).", MessageType.Info);

        bool abierta = escena != null && SceneManager.GetActiveScene().path == escena;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = abierta || escena == null;
            if (GUILayout.Button("Aplicar terreno y fondo a la escena", GUILayout.Height(30)))
            {
                Aplicar(c, SceneManager.GetActiveScene());
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            GUI.enabled = true;
            if (escena != null && !abierta && GUILayout.Button("Abrir la escena del nivel", GUILayout.Height(30), GUILayout.Width(180)))
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(escena);
        }
        EditorGUILayout.Space();

        serializedObject.Update();
        SerializedProperty p = serializedObject.GetIterator();
        p.NextVisible(true);
        while (p.NextVisible(false))
        {
            if (p.name == "m_Script") continue;
            // Solo se ensena la parte del terreno que usa este nivel.
            if (c.tipoTerreno == ConfigNivel.TipoTerreno.Tiles && (p.name == "losas" || p.name == "pilares" || p.name == "relleno" || p.name == "colorPiezas")) continue;
            if (c.tipoTerreno == ConfigNivel.TipoTerreno.Piezas && p.name == "relleno" && Buscar<BordesCueva>(SceneManager.GetActiveScene()) != null) continue;
            if (c.tipoTerreno == ConfigNivel.TipoTerreno.Piezas && p.name == "conjuntosTiles") continue;
            EditorGUILayout.PropertyField(p, true);
        }
        serializedObject.ApplyModifiedProperties();
    }

    private static string EscenaDe(ConfigNivel c)
    {
        string ruta = AssetDatabase.GetAssetPath(c);
        return ruta == RutaNieve ? EscenaNieve : ruta == RutaCueva ? EscenaCueva : null;
    }

    // ------------------------------------------------------------------ Aplicar

    public static void Aplicar(ConfigNivel c, Scene escena)
    {
        if (c.tipoTerreno == ConfigNivel.TipoTerreno.Tiles) AplicarTiles(c, escena);
        else AplicarPiezas(c, escena);
        ParallaxCueva fondo = Buscar<ParallaxCueva>(escena);
        if (fondo != null && c.capasFondo.Count > 0) ConstruirFondo(fondo, c);
        AssetDatabase.SaveAssets();
        Debug.Log("[Config] Aplicado " + c.name + " a " + escena.name);
    }

    private static void AplicarTiles(ConfigNivel c, Scene escena)
    {
        foreach (ConfigNivel.ConjuntoTiles conj in c.conjuntosTiles)
            for (int i = 0; i < conj.tiles.Length && i < conj.sprites.Length; i++)
            {
                Tile t = conj.tiles[i];
                if (t == null) continue;
                if (conj.sprites[i] != null) t.sprite = conj.sprites[i];
                t.color = conj.color;
                EditorUtility.SetDirty(t);
            }
        foreach (GameObject r in escena.GetRootGameObjects())
            foreach (Tilemap tm in r.GetComponentsInChildren<Tilemap>(true)) tm.RefreshAllTiles();
    }

    // Cueva: cambia la imagen de cada losa, pilar y relleno, conservando su
    // sitio y su tamano.
    private static void AplicarPiezas(ConfigNivel c, Scene escena)
    {
        // Cueva pintada con tiles: los bordes los pone BordesCueva; se rehacen.
        BordesCueva bc = Buscar<BordesCueva>(escena);
        if (bc != null)
        {
            bc.config = c;
            bc.Reconstruir();
            EditorUtility.SetDirty(bc);
            return;
        }
        foreach (GameObject r in escena.GetRootGameObjects())
            foreach (SpriteRenderer sr in r.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Sprite[] lista = sr.name == "Losa" ? c.losas : sr.name == "Pilar" ? c.pilares
                               : sr.name == "Relleno" && c.relleno != null ? new[] { c.relleno } : null;
                if (lista == null || lista.Length == 0 || sr.sprite == null) continue;
                Vector3 centro = sr.bounds.center, tamano = sr.bounds.size;
                int k = Mathf.Abs(Mathf.RoundToInt(centro.x * 7.3f + centro.y * 3.1f)) % lista.Length;
                Sprite s = lista[k] != null ? lista[k] : lista.FirstOrDefault(x => x != null);
                if (s == null) continue;
                Undo.RecordObject(sr, "Aplicar config");
                Undo.RecordObject(sr.transform, "Aplicar config");
                sr.sprite = s;
                sr.color = sr.name == "Relleno" ? sr.color : c.colorPiezas;
                Vector2 escala = new Vector2(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y);
                sr.transform.localScale = new Vector3(escala.x, escala.y, 1f);
                Vector3 desvio = new Vector3(s.bounds.center.x * escala.x * (sr.flipX ? -1f : 1f), s.bounds.center.y * escala.y * (sr.flipY ? -1f : 1f), 0f);
                sr.transform.position = new Vector3(centro.x - desvio.x, centro.y - desvio.y, sr.transform.position.z);
            }
    }

    // Rehace las capas del parallax (tres copias en fila por capa).
    public static void ConstruirFondo(ParallaxCueva parallax, ConfigNivel c)
    {
        for (int i = parallax.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parallax.transform.GetChild(i).gameObject);
        var capas = new List<ParallaxCueva.Capa>();
        for (int i = 0; i < c.capasFondo.Count; i++)
        {
            ConfigNivel.CapaFondo cf = c.capasFondo[i];
            if (cf.imagen == null) continue;
            Transform raiz = new GameObject("Capa_" + (string.IsNullOrEmpty(cf.nombre) ? cf.imagen.name : cf.nombre)).transform;
            Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Fondo");
            raiz.SetParent(parallax.transform, false);
            float escala = c.altoFondo / cf.imagen.bounds.size.y;
            float ancho = cf.imagen.bounds.size.x * escala;
            for (int k = -1; k <= 1; k++)
            {
                SpriteRenderer sr = new GameObject("Copia").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(raiz, false);
                sr.transform.localPosition = new Vector3(k * ancho, cf.desplazamientoY, 0f);
                sr.transform.localScale = new Vector3(escala, escala, 1f);
                sr.sprite = cf.imagen;
                sr.color = cf.color;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = i;
            }
            capas.Add(new ParallaxCueva.Capa { raiz = raiz, ancho = ancho, seguimiento = cf.seguimiento });
        }
        Undo.RecordObject(parallax, "Fondo");
        parallax.capas = capas.ToArray();
        parallax.seguimientoVertical = c.seguimientoVertical;
        EditorUtility.SetDirty(parallax);
    }

    // ------------------------------------------------------------------ Crear desde la escena

    [MenuItem("Warrior/Crear configuraciones de nivel")]
    public static void CrearTodas()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Asegurar(EscenaCueva, RutaCueva, ConfigNivel.TipoTerreno.Piezas);
        Asegurar(EscenaNieve, RutaNieve, ConfigNivel.TipoTerreno.Tiles);
    }

    // Abre la escena, crea su archivo (si no existe) con lo que hay en ella y lo
    // enlaza a sus DatosNivel y ArenaJefe. Si ya existia, lo aplica a la escena.
    public static ConfigNivel Asegurar(string rutaEscena, string rutaConfig, ConfigNivel.TipoTerreno tipo)
    {
        Scene escena = SceneManager.GetActiveScene().path == rutaEscena ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(rutaEscena);
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Data", "Niveles");

        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(rutaConfig);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<ConfigNivel>();
            c.tipoTerreno = tipo;
            Capturar(c, escena);
            AssetDatabase.CreateAsset(c, rutaConfig);
        }
        else Aplicar(c, escena);

        Enlazar(c, escena);
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        return c;
    }

    // Pone el archivo en los DatosNivel y la ArenaJefe de la escena (crea
    // DatosNivel si no hay).
    public static void Enlazar(ConfigNivel c, Scene escena)
    {
        DatosNivel d = Buscar<DatosNivel>(escena);
        if (d == null)
        {
            GameObject go = new GameObject("DatosNivel");
            SceneManager.MoveGameObjectToScene(go, escena);
            d = go.AddComponent<DatosNivel>();
        }
        Poner(d, "config", c);
        ArenaJefe a = Buscar<ArenaJefe>(escena);
        if (a != null) Poner(a, "config", c);
    }

    private static void Capturar(ConfigNivel c, Scene escena)
    {
        // Terreno.
        if (c.tipoTerreno == ConfigNivel.TipoTerreno.Tiles)
        {
            string[] piezas = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };
            foreach (string conj in new[] { "nieve", "hielo" })
            {
                var cj = new ConfigNivel.ConjuntoTiles { nombre = conj == "nieve" ? "Suelo nevado" : "Hielo resbaladizo" };
                for (int i = 0; i < 9; i++)
                {
                    Tile t = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Nieve/" + conj + "_" + piezas[i] + ".asset");
                    cj.tiles[i] = t;
                    cj.sprites[i] = t != null ? t.sprite : null;
                    if (t != null) cj.color = t.color;
                }
                c.conjuntosTiles.Add(cj);
            }
        }
        else
        {
            var losas = new List<Sprite>();
            var pilares = new List<Sprite>();
            foreach (GameObject r in escena.GetRootGameObjects())
                foreach (SpriteRenderer sr in r.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sprite == null) continue;
                    if (sr.name == "Losa" && !losas.Contains(sr.sprite)) losas.Add(sr.sprite);
                    else if (sr.name == "Pilar" && !pilares.Contains(sr.sprite)) pilares.Add(sr.sprite);
                    else if (sr.name == "Relleno" && c.relleno == null && sr.transform.parent != null && sr.transform.parent.name.StartsWith("Roca_")) c.relleno = sr.sprite;
                }
            c.losas = losas.OrderBy(s => s.name).ToArray();
            c.pilares = pilares.OrderBy(s => s.name).ToArray();
        }

        // Fondo.
        ParallaxCueva p = Buscar<ParallaxCueva>(escena);
        if (p != null)
        {
            c.seguimientoVertical = p.seguimientoVertical;
            foreach (ParallaxCueva.Capa capa in p.capas)
            {
                if (capa.raiz == null || capa.raiz.childCount == 0) continue;
                SpriteRenderer sr = capa.raiz.GetChild(0).GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;
                c.altoFondo = sr.bounds.size.y;
                c.capasFondo.Add(new ConfigNivel.CapaFondo
                {
                    nombre = capa.raiz.name.Replace("Capa_", ""), imagen = sr.sprite, seguimiento = capa.seguimiento, color = sr.color,
                    desplazamientoY = sr.transform.localPosition.y,
                });
            }
        }

        // Musica y frases.
        DatosNivel d = Buscar<DatosNivel>(escena);
        if (d != null)
        {
            SerializedObject so = new SerializedObject(d);
            c.musicaCombate = so.FindProperty("musica").objectReferenceValue as AudioClip;
            c.volumenCombate = so.FindProperty("volumenMusica").floatValue;
            c.ambiente = so.FindProperty("ambiente").objectReferenceValue as AudioClip;
            c.volumenAmbiente = so.FindProperty("volumenAmbiente").floatValue;
            SerializedProperty f = so.FindProperty("frasesMuerte");
            c.frasesMuerte = new string[f.arraySize];
            for (int i = 0; i < f.arraySize; i++) c.frasesMuerte[i] = f.GetArrayElementAtIndex(i).stringValue;
        }
        ArenaJefe a = Buscar<ArenaJefe>(escena);
        if (a != null)
        {
            SerializedObject so = new SerializedObject(a);
            c.jefeFase1 = new ConfigNivel.MusicaFase
            {
                pista = so.FindProperty("musica").objectReferenceValue as AudioClip, volumen = so.FindProperty("volumen").floatValue,
                inicio = so.FindProperty("inicioFase1").floatValue, bucle = so.FindProperty("bucleFase1").vector2Value,
            };
            c.jefeFase2 = new ConfigNivel.MusicaFase
            {
                pista = so.FindProperty("musicaFase2").objectReferenceValue as AudioClip, volumen = so.FindProperty("volumenFase2").floatValue,
                inicio = so.FindProperty("inicioFase2").floatValue, bucle = so.FindProperty("bucleFase2").vector2Value,
            };
        }
    }

    private static T Buscar<T>(Scene escena) where T : Component
    {
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            T t = r.GetComponentInChildren<T>(true);
            if (t != null) return t;
        }
        return null;
    }

    private static void Poner(Object objetivo, string campo, Object valor)
    {
        SerializedObject so = new SerializedObject(objetivo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) return;
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
