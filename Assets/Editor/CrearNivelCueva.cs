using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Genera la escena "Nivel Cueva" desde el menu Warrior > Crear nivel Cueva.
//
// Parte de una copia del Nivel 1 (para heredar GameManager, camara, HUD, player,
// pausa...), quita lo que es propio de ese nivel y monta la cueva:
//   - La roca es una lista de rectangulos (Roca) con su BoxCollider2D en la capa
//     Ground. Por dentro va rellena de negro y, en los lados que dan a espacio
//     abierto, se decora con las piezas pintadas del pack de cueva: losas arriba
//     y abajo, pilares en los lados.
//   - Plataformas, hogueras, trampas, fondo parallax y marcadores para las
//     siguientes partes (enemigos, jefe, arena).
//
// Se puede volver a ejecutar: regenera la escena entera (avisa antes).
public static class CrearNivelCueva
{
    private const string Plantilla = "Assets/Scenes/Nivel 1.unity";
    private const string Destino = "Assets/Scenes/Nivel Cueva.unity";
    private const string Pack = "Assets/SPRITES PARA NUEVOS NIVELES/";
    private const string Cueva = Pack + "MAPAS/Mapa_CuevaVegetacion/Assets 1024 Cave/";
    private const string Vegetacion = Pack + "MAPAS/Mapa_CuevaVegetacion/Vegetation/";
    private const string Trampas = Pack + "TRAMPAS/Trampas_TrampasYArmas/";
    private const string Fondo = Pack + "BACKGROUND/Fondo_Cueva/";
    private const string Torre = Pack + "HOGUERA O CHECKPOINT/Checkpoint_TorreLunaSangre/RedMoonTower_free_idle_animation..png";

    // Lo que es del Nivel 1 y aqui sobra (por nombre, o nombre seguido de " (n)").
    private static readonly string[] Quitar =
        { "Grid", "Diamonds", "Enemies", "Saw_Idle", "Sierras_Moviles", "DeadZones", "Checkpoint", "Heart" };

    // Limites del mapa: fuera de aqui todo cuenta como roca.
    private static readonly Rect Mapa = Rect.MinMaxRect(-8f, -8f, 182f, 42f);

    // ------------------------------------------------------------------ Trazado
    // Roca maciza: x, y (borde de abajo), ancho, alto. Una unidad = una casilla.
    //
    //  A  pasillo de entrada (suelo 0, techo 7) con foso de pinchos en x 18-21
    //  B  pozo vertical x 40-52, de 0 a 27, con plataformas y paredes para saltar
    //  C  pasillo alto x 52-112 (suelo 20, techo 27), foso de pinchos en x 60-63
    //  S  cueva secreta x 72-112, y 8-14: se abre a la caida D por la izquierda
    //  D  caida x 112-122, de 27 a 4
    //  E  antesala x 112-140 (suelo 4, techo 12), hoguera 2 y la puerta del jefe
    //  F  arena x 140-176 (suelo 4, techo 22)
    private static readonly Rect[] Roca =
    {
        new Rect(-8f, -8f, 4f, 50f),     // pared izquierda
        new Rect(-4f, -8f, 22f, 8f),     // A: suelo hasta el foso
        new Rect(18f, -8f, 3f, 6f),      // A: fondo del foso
        new Rect(21f, -8f, 31f, 8f),     // A: suelo tras el foso y base del pozo
        new Rect(-4f, 7f, 44f, 35f),     // A: techo y roca de encima
        new Rect(52f, -8f, 8f, 28f),     // C: suelo junto a la hoguera 1
        new Rect(60f, -8f, 3f, 26f),     // C: fondo del foso
        new Rect(63f, -8f, 9f, 28f),     // C: suelo tras el foso
        new Rect(72f, -8f, 40f, 16f),    // S: suelo de la cueva secreta
        new Rect(72f, 14f, 40f, 6f),     // S: techo / C: suelo
        new Rect(40f, 27f, 82f, 15f),    // C: techo
        new Rect(112f, -8f, 64f, 12f),   // E y F: suelo
        new Rect(122f, 12f, 18f, 30f),   // E: techo
        new Rect(140f, 22f, 36f, 20f),   // F: techo de la arena
        new Rect(176f, -8f, 6f, 50f),    // pared derecha

        // Pozo B: sin plataformas flotantes. Se sube por roca natural: dos columnas
        // que salen del suelo y repisas pegadas a las paredes, en zigzag (2.5 de
        // alto y 3-4 de hueco: a salto doble, o rebotando en las paredes).
        new Rect(41f, 0f, 3f, 2.5f),      // columna baja junto a la entrada
        new Rect(47f, 0f, 5f, 5f),        // columna alta pegada a la pared derecha
        new Rect(40f, 6.8f, 4f, 0.7f),    // repisas: izquierda 7.5
        new Rect(48f, 9.3f, 4f, 0.7f),    // derecha 10
        new Rect(40f, 11.8f, 4f, 0.7f),   // izquierda 12.5
        new Rect(48f, 14.3f, 4f, 0.7f),   // derecha 15
        new Rect(40f, 16.8f, 4f, 0.7f),   // izquierda 17.5
        new Rect(48f, 19.3f, 4f, 0.7f),   // derecha 20: sale al pasillo alto
    };

    // Plataformas sueltas (x, y, ancho, alto).
    private static readonly Rect[] Plataformas = { };

    private static readonly Vector2 Salida = new Vector2(2f, 0f);
    private static readonly Vector2[] Hogueras = { new Vector2(55f, 20f), new Vector2(127f, 4f) };
    // Estalactitas: x y el techo del que cuelgan.
    private static readonly Vector2[] Estalactitas =
    {
        new Vector2(9f, 7f), new Vector2(25f, 7f), new Vector2(32f, 7f),
        new Vector2(67f, 27f), new Vector2(80f, 27f), new Vector2(98f, 27f), new Vector2(106f, 27f),
        new Vector2(84f, 14f), new Vector2(100f, 14f),
        new Vector2(131f, 12f),
    };
    // Respiraderos de fuego: x y el suelo, con su desfase.
    private static readonly Vector3[] Fuegos =
    {
        new Vector3(36f, 0f, 0f), new Vector3(68f, 20f, 0.5f), new Vector3(88f, 20f, 1.2f), new Vector3(104f, 20f, 0f),
    };
    // Fosos de pinchos: x, ancho, y del fondo.
    private static readonly Vector3[] Pinchos = { new Vector3(18f, 3f, -2f), new Vector3(60f, 3f, 18f) };

    // Enemigos: tipo y posicion (a ras de suelo; los voladores, en el aire).
    private static readonly (string tipo, Vector2 pos)[] Enemigos =
    {
        ("Slime", new Vector2(27f, 0f)),         // A: tras el foso
        ("Mago", new Vector2(33f, 0f)),          // A: dispara mientras peleas con el slime
        ("Volador", new Vector2(46f, 21f)),      // B: acosa en lo alto del pozo
        ("Slime", new Vector2(80f, 20f)),        // C
        ("Volador", new Vector2(84f, 24.5f)),    // C: junto a las estalactitas
        ("Mago", new Vector2(97f, 20f)),         // C: al fondo del pasillo
        ("Mimic", new Vector2(75f, 8f)),         // S: el cofre de la cueva secreta
        ("Volador", new Vector2(117f, 14f)),     // D: en la caida
        ("Slime", new Vector2(133f, 4f)),        // E: antes de la puerta del jefe
    };

    // Posiciones para las siguientes partes.
    private static readonly (string nombre, Vector2 pos)[] Marcadores =
    {
        ("Recompensa_CuevaSecreta", new Vector2(74.5f, 8f)),
        ("PistaNarrativa", new Vector2(134f, 4f)),
        ("PuertaJefe", new Vector2(140f, 4f)),
        ("CentroArena", new Vector2(158f, 4f)),
        ("CaidaJefe", new Vector2(160f, 21f)),
    };

    // ------------------------------------------------------------------ Menu

    // Retirado del menu: las escenas ya se editan a mano (y se pintan con la Tile
    // Palette). Regenerar el nivel borraria esos cambios. Se conserva el codigo.
    public static void Crear()
    {
        if (File.Exists(Destino) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Nivel Cueva",
                "La escena ya existe. Regenerarla borra los cambios que le hayas hecho a mano.",
                "Regenerar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        PrepararImportacion();
        // Antes de montar la escena: guardar el prefab del Player recarga sus
        // instancias, y se perdia la posicion de salida del player.
        EfectosDelPlayer();

        if (File.Exists(Destino)) AssetDatabase.DeleteAsset(Destino);
        if (!AssetDatabase.CopyAsset(Plantilla, Destino))
        {
            Debug.LogError("[Cueva] No se pudo copiar " + Plantilla);
            return;
        }

        Scene escena = EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);
        LimpiarPlantilla(escena);

        System.Random azar = new System.Random(1234);
        Transform nivel = new GameObject("Nivel").transform;

        ConstruirRoca(nivel, azar);
        ConstruirPlataformas(nivel);
        ConstruirFondo(nivel);
        ConstruirHogueras(nivel);
        ConstruirTrampas(nivel);
        ConstruirMarcadores(nivel);
        ConstruirEnemigos(nivel);
        ConstruirZonasCamara(nivel);
        new GameObject("ReinicioEnemigos", typeof(ReinicioEnemigos)).transform.SetParent(nivel);
        ColocarPlayerYCamara(escena);
        ConstruirArena(nivel, escena);
        IluminarTodasLasCapas(escena);
        ConfigurarPortales.Instalar(escena);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        ConfigNivelEditor.Asegurar(Destino, ConfigNivelEditor.RutaCueva, ConfigNivel.TipoTerreno.Piezas);
        AnadirABuild();

        Debug.Log("[Cueva] Nivel generado en " + Destino);
    }

    // Vista de todo el mapa en un PNG (junto al proyecto, "NivelCueva_mapa.png").
    // Sirve para revisar el trazado sin darle al Play.
    [MenuItem("Warrior/Captura del nivel Cueva")]
    public static void Captura()
    {
        CapturarZona(Rect.MinMaxRect(-9f, -9f, 183f, 43f), 4800, "NivelCueva_mapa.png");
    }

    // Tres vistas de cerca, con el tamano de la camara del juego aproximado.
    public static void CapturasDetalle()
    {
        CapturarZona(Rect.MinMaxRect(-4f, -3f, 26f, 9f), 2400, "NivelCueva_inicio.png");
        CapturarZona(Rect.MinMaxRect(36f, -1f, 72f, 29f), 2000, "NivelCueva_pozo.png");
        CapturarZona(Rect.MinMaxRect(110f, 1f, 178f, 24f), 3000, "NivelCueva_arena.png");
    }

    private static void CapturarZona(Rect vista, int ancho, string archivo)
    {
        if (SceneManager.GetActiveScene().path != Destino) EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);

        // El fondo parallax sigue a la camara: se pone centrado en la vista.
        foreach (ParallaxCueva p in Object.FindObjectsByType<ParallaxCueva>(FindObjectsSortMode.None))
            foreach (ParallaxCueva.Capa c in p.capas)
                if (c.raiz != null) c.raiz.position = new Vector3(vista.center.x, vista.center.y, 0f);

        int alto = Mathf.RoundToInt(ancho * vista.height / vista.width);

        Camera cam = new GameObject("CamaraCaptura").AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = vista.height * 0.5f;
        cam.aspect = vista.width / vista.height;
        cam.transform.position = new Vector3(vista.center.x, vista.center.y, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

        // El fondo parallax sigue a la camara del juego: para la captura se deja donde esta.
        RenderTexture rt = new RenderTexture(ancho, alto, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        string ruta = Path.Combine(Directory.GetCurrentDirectory(), archivo);
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(rt);
        Debug.Log("[Cueva] Captura guardada en " + ruta);
    }

    // Para ejecutar desde la linea de comandos: genera y captura.
    public static void CrearYCapturar()
    {
        Crear();
        Captura();
        CapturasDetalle();
    }

    // ------------------------------------------------------------------ Plantilla

    private static void LimpiarPlantilla(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            bool sobra = Quitar.Any(n => raiz.name == n || raiz.name.StartsWith(n + " "));
            if (sobra) Object.DestroyImmediate(raiz);
        }
    }

    private static void ColocarPlayerYCamara(Scene escena)
    {
        Vector3 salida = new Vector3(Salida.x, Salida.y + 0.7f, 0f);

        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            switch (raiz.name)
            {
                case "Player":
                case "RespawnPoint":
                case "Main Camera":
                    raiz.transform.position = new Vector3(salida.x, salida.y, raiz.transform.position.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(raiz.transform);
                    break;

                case "EntranceDoor":
                    raiz.transform.position = new Vector3(Salida.x - 1f, 0f, 0f);
                    ApoyarEnSuelo(raiz, Salida.y);
                    break;

                case "ExitDoor":
                    // La salida del nivel queda al fondo de la arena, apagada hasta
                    // que caiga el jefe (eso llega con la parte del jefe).
                    raiz.transform.position = new Vector3(172f, 4f, 0f);
                    ApoyarEnSuelo(raiz, 4f);
                    raiz.SetActive(false);
                    break;

                case "CinemachineCamera":
                    if (raiz.GetComponent<CamaraDinamica>() == null) raiz.AddComponent<CamaraDinamica>();
                    break;

                case "CameraLimit":
                    raiz.transform.position = Vector3.zero;
                    raiz.transform.localScale = Vector3.one;
                    PolygonCollider2D poly = raiz.GetComponent<PolygonCollider2D>();
                    if (poly != null)
                    {
                        poly.offset = Vector2.zero;
                        poly.pathCount = 1;
                        poly.SetPath(0, new[]
                        {
                            new Vector2(-4f, -1.5f), new Vector2(176f, -1.5f),
                            new Vector2(176f, 27.5f), new Vector2(-4f, 27.5f),
                        });
                    }
                    break;
            }
        }
    }

    // La luz global heredada del Nivel 1 no incluye la capa "traps": las trampas
    // salian negras. Se aplica a todas las capas de dibujo.
    private static void IluminarTodasLasCapas(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (UnityEngine.Rendering.Universal.Light2D luz in raiz.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
        {
            if (luz.lightType != UnityEngine.Rendering.Universal.Light2D.LightType.Global) continue;
            SerializedObject so = new SerializedObject(luz);
            SerializedProperty capas = so.FindProperty("m_ApplyToSortingLayers");
            if (capas == null) continue;
            SortingLayer[] todas = SortingLayer.layers;
            capas.arraySize = todas.Length;
            for (int i = 0; i < todas.Length; i++) capas.GetArrayElementAtIndex(i).intValue = todas[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Baja o sube un objeto hasta que la parte de abajo de su sprite toque "suelo".
    private static void ApoyarEnSuelo(GameObject go, float suelo)
    {
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        go.transform.position += Vector3.up * (suelo - sr.bounds.min.y);
    }

    private static void AnadirABuild()
    {
        // Al regenerar, la escena cambia de identificador: se rehace su entrada.
        List<EditorBuildSettingsScene> escenas = EditorBuildSettings.scenes.ToList();
        int i = escenas.FindIndex(s => s.path == Destino);
        if (i >= 0) escenas[i] = new EditorBuildSettingsScene(Destino, true);
        else escenas.Add(new EditorBuildSettingsScene(Destino, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // Pixel art nitido: sin filtrado ni compresion en lo que es pixel art.
    private static void PrepararImportacion()
    {
        List<string> rutas = new List<string> { Torre, Trampas + "Fire.png", Trampas + "FireBox.png", Trampas + "Spike_B.png" };
        for (int i = 1; i <= 7; i++) rutas.Add(Fondo + i + ".png");
        // Las hojas de enemigos: algunas pasan de 2048 de ancho y Unity las encogeria.
        rutas.AddRange(Directory.GetFiles(Pack + "ENEMIGOS", "*.png", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')));
        rutas.AddRange(Directory.GetFiles(CarpetaFx, "*.png", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')));
        rutas.AddRange(Directory.GetFiles(CarpetaFxPlayer, "*.png", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')));

        // El jefe: dibujos grandes en lienzos de 1980x1080. Se comprimen (sin
        // comprimir, 29 lienzos ocupan cientos de MB) y se filtran suave, porque
        // en pantalla se ven algo reducidos.
        foreach (string f in Directory.GetFiles(CarpetaJefe, "*.png"))
        {
            TextureImporter tj = AssetImporter.GetAtPath(f.Replace('\\', '/')) as TextureImporter;
            if (tj == null) continue;
            if (tj.textureCompression == TextureImporterCompression.CompressedHQ && tj.filterMode == FilterMode.Bilinear && !tj.mipmapEnabled) continue;
            tj.textureCompression = TextureImporterCompression.CompressedHQ;
            tj.filterMode = FilterMode.Bilinear;
            tj.mipmapEnabled = false;
            tj.maxTextureSize = 2048;
            tj.SaveAndReimport();
        }

        foreach (string ruta in rutas)
        {
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[Cueva] No encuentro " + ruta); continue; }
            if (ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.maxTextureSize >= 4096) continue;
            ti.maxTextureSize = 4096;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ Roca

    private static void ConstruirRoca(Transform nivel, System.Random azar)
    {
        Transform padre = Hijo(nivel, "Roca");
        Sprite negro = PrimerSprite(Cueva + "Square - Black.jpg");
        Sprite[] suelo = Sprites(Cueva + "Cave - Floor.png");
        Sprite[] losas = suelo.Where(s => s.rect.width / s.rect.height >= 4f).ToArray();
        Sprite[] pilares = suelo.Where(s => s.rect.height / s.rect.width >= 2f).ToArray();
        Sprite[] rocas = Sprites(Cueva + "Cave - SmallRocks.png");
        Sprite[] estalagmitas = rocas.Where(s => s.rect.x > 1550 && s.rect.y < 1100 && s.rect.height > s.rect.width * 1.2f).ToArray();
        Sprite[] estalactitasDeco = rocas.Where(s => s.rect.x > 1550 && s.rect.y > 1350 && s.rect.width < 200).ToArray();
        Sprite[] plantas = new[] { "Comp 1", "Grass2", "Grass3", "Grass4", "Group Plant" }
            .Select(d => Directory.GetFiles(Vegetacion + d, "*.png").OrderBy(f => f).FirstOrDefault())
            .Where(f => f != null).Select(f => PrimerSprite(f.Replace('\\', '/'))).Where(s => s != null).ToArray();

        int capaSuelo = LayerMask.NameToLayer("Ground");

        for (int i = 0; i < Roca.Length; i++)
        {
            Rect r = Roca[i];
            GameObject bloque = new GameObject("Roca_" + i);
            bloque.layer = capaSuelo;
            bloque.transform.SetParent(padre);
            bloque.transform.position = r.center;
            BoxCollider2D box = bloque.AddComponent<BoxCollider2D>();
            box.size = r.size;

            if (negro != null) Pieza(bloque.transform, "Relleno", negro, r.center, r.size, "Ground", 0);

            // Arriba: losas, y encima plantas y alguna estalagmita de fondo.
            foreach (Vector2 t in TramosAbiertos(r, Vector2.up))
            {
                Losas(bloque.transform, losas, t.x, t.y, r.yMax, false, azar);
                Decorar(bloque.transform, plantas, estalagmitas, t.x, t.y, r.yMax, azar);
            }
            // Abajo (techos): losas del reves y estalactitas pequenas y oscuras,
            // para que no se confundan con las que caen.
            foreach (Vector2 t in TramosAbiertos(r, Vector2.down))
            {
                Losas(bloque.transform, losas, t.x, t.y, r.yMin, true, azar);
                DecorarTecho(bloque.transform, estalactitasDeco, t.x, t.y, r.yMin, azar);
            }
            foreach (Vector2 t in TramosAbiertos(r, Vector2.right)) Pilares(bloque.transform, pilares, t.x, t.y, r.xMax, true, azar);
            foreach (Vector2 t in TramosAbiertos(r, Vector2.left)) Pilares(bloque.transform, pilares, t.x, t.y, r.xMin, false, azar);
        }
    }

    // Tramos de un lado del bloque que dan a espacio abierto (ni otra roca, ni
    // fuera del mapa). Devuelve (desde, hasta) sobre el eje del lado.
    private static List<Vector2> TramosAbiertos(Rect r, Vector2 normal)
    {
        List<Vector2> tramos = new List<Vector2>();
        bool horizontal = normal.y != 0f;
        float desde = horizontal ? r.xMin : r.yMin;
        float hasta = horizontal ? r.xMax : r.yMax;
        const float paso = 0.25f;
        float inicio = float.NaN;

        for (float a = desde + paso * 0.5f; a < hasta; a += paso)
        {
            Vector2 p = horizontal
                ? new Vector2(a, normal.y > 0 ? r.yMax + 0.1f : r.yMin - 0.1f)
                : new Vector2(normal.x > 0 ? r.xMax + 0.1f : r.xMin - 0.1f, a);
            bool abierto = Mapa.Contains(p) && !Roca.Any(o => o.Contains(p));

            if (abierto && float.IsNaN(inicio)) inicio = a - paso * 0.5f;
            if (!abierto && !float.IsNaN(inicio)) { tramos.Add(new Vector2(inicio, a - paso * 0.5f)); inicio = float.NaN; }
        }
        if (!float.IsNaN(inicio)) tramos.Add(new Vector2(inicio, hasta));
        return tramos;
    }

    private const float Grosor = 0.6f;

    // Fila de losas a lo largo de un borde horizontal. Asoman un poco por encima
    // del collider para que los pies no se hundan en la pintura.
    private static void Losas(Transform padre, Sprite[] losas, float x0, float x1, float y, bool techo, System.Random azar)
    {
        if (losas.Length == 0) return;
        float x = x0 - 0.1f;
        float fin = x1 + 0.1f;
        while (x < fin - 0.05f)
        {
            Sprite s = losas[azar.Next(losas.Length)];
            float ancho = Mathf.Min(Grosor * s.rect.width / s.rect.height, fin - x);
            float cy = techo ? y - 0.08f + Grosor * 0.5f : y + 0.08f - Grosor * 0.5f;
            Pieza(padre, "Losa", s, new Vector2(x + ancho * 0.5f, cy), new Vector2(ancho, Grosor), "Ground", 2, techo);
            x += ancho * 0.92f;
        }
    }

    // Columna de pilares a lo largo de un borde vertical.
    private static void Pilares(Transform padre, Sprite[] pilares, float y0, float y1, float x, bool derecha, System.Random azar)
    {
        if (pilares.Length == 0) return;
        float y = y0 - 0.1f;
        float fin = y1 + 0.1f;
        while (y < fin - 0.05f)
        {
            Sprite s = pilares[azar.Next(pilares.Length)];
            float alto = Mathf.Min(Grosor * s.rect.height / s.rect.width, fin - y);
            float cx = derecha ? x + 0.08f - Grosor * 0.5f : x - 0.08f + Grosor * 0.5f;
            SpriteRenderer sr = Pieza(padre, "Pilar", s, new Vector2(cx, y + alto * 0.5f), new Vector2(Grosor, alto), "Ground", 1);
            sr.flipX = !derecha;
            y += alto * 0.92f;
        }
    }

    // Plantas y alguna estalagmita detras del player, sobre los suelos.
    private static void Decorar(Transform padre, Sprite[] plantas, Sprite[] estalagmitas, float x0, float x1, float y, System.Random azar)
    {
        for (float x = x0 + 0.8f; x < x1 - 0.8f; x += 1.6f + (float)azar.NextDouble() * 2f)
        {
            double tirada = azar.NextDouble();
            if (tirada < 0.45 && plantas.Length > 0)
            {
                Sprite s = plantas[azar.Next(plantas.Length)];
                float alto = 0.5f + (float)azar.NextDouble() * 0.4f;
                PiezaApoyada(padre, "Planta", s, x, y - 0.05f, alto, "Middleground", 1, new Color(0.75f, 0.8f, 0.75f));
            }
            else if (tirada < 0.58 && estalagmitas.Length > 0)
            {
                Sprite s = estalagmitas[azar.Next(estalagmitas.Length)];
                float alto = 0.8f + (float)azar.NextDouble() * 0.9f;
                PiezaApoyada(padre, "Estalagmita", s, x, y - 0.1f, alto, "Middleground", 0, new Color(0.55f, 0.5f, 0.52f));
            }
        }
    }

    private static void DecorarTecho(Transform padre, Sprite[] estalactitas, float x0, float x1, float y, System.Random azar)
    {
        if (estalactitas.Length == 0) return;
        for (float x = x0 + 0.6f; x < x1 - 0.6f; x += 1.2f + (float)azar.NextDouble() * 2.5f)
        {
            if (azar.NextDouble() > 0.45) continue;
            Sprite s = estalactitas[azar.Next(estalactitas.Length)];
            float alto = 0.5f + (float)azar.NextDouble() * 0.5f;
            float ancho = alto * s.rect.width / s.rect.height;
            Pieza(padre, "EstalactitaDeco", s, new Vector2(x, y + 0.1f - alto * 0.5f), new Vector2(ancho, alto),
                  "Middleground", 0).color = new Color(0.4f, 0.36f, 0.38f);
        }
    }

    private static void ConstruirPlataformas(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Plataformas");
        Sprite[] losas = Sprites(Cueva + "Cave - Floor.png").Where(s => s.rect.width / s.rect.height >= 4f).ToArray();
        int capaSuelo = LayerMask.NameToLayer("Ground");

        for (int i = 0; i < Plataformas.Length; i++)
        {
            Rect r = Plataformas[i];
            GameObject go = new GameObject("Plataforma_" + i);
            go.layer = capaSuelo;
            go.transform.SetParent(padre);
            go.transform.position = r.center;
            go.AddComponent<BoxCollider2D>().size = r.size;
            if (losas.Length == 0) continue;

            // Una losa que cubre la plataforma, un poco mas gruesa por abajo.
            Sprite s = losas[i % losas.Length];
            Pieza(go.transform, "Losa", s, new Vector2(r.center.x, r.yMax + 0.06f - 0.4f), new Vector2(r.width + 0.3f, 0.8f), "Ground", 2);
        }
    }

    // ------------------------------------------------------------------ Fondo

    private static void ConstruirFondo(Transform nivel)
    {
        GameObject go = new GameObject("Fondo");
        go.transform.SetParent(nivel);
        ParallaxCueva parallax = go.AddComponent<ParallaxCueva>();
        List<ParallaxCueva.Capa> capas = new List<ParallaxCueva.Capa>();

        // Alto de sobra para la vista mas abierta (la arena, tamano 5.3) y el poco
        // desplazamiento vertical del parallax.
        const float alto = 18f;

        // 0.png es la vista previa con todo junto: se usan de la 7 (la mas lejana,
        // un color liso) a la 1 (rocas del primer plano).
        for (int i = 7; i >= 1; i--)
        {
            Sprite s = PrimerSprite(Fondo + i + ".png");
            if (s == null) continue;

            Transform raiz = Hijo(go.transform, "Capa_" + i);
            float escala = alto / s.bounds.size.y;
            float ancho = s.bounds.size.x * escala;
            for (int k = -1; k <= 1; k++)
            {
                SpriteRenderer sr = new GameObject("Copia").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(raiz);
                sr.transform.localPosition = new Vector3(k * ancho, 0f, 0f);
                sr.transform.localScale = new Vector3(escala, escala, 1f);
                sr.sprite = s;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = 7 - i;
            }

            float t = (7 - i) / 6f;
            capas.Add(new ParallaxCueva.Capa { raiz = raiz, ancho = ancho, seguimiento = Mathf.Lerp(0.97f, 0.55f, t) });
        }

        parallax.capas = capas.ToArray();
    }

    // ------------------------------------------------------------------ Hogueras

    private static void ConstruirHogueras(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Hogueras");
        Sprite[] fotogramas = Sprites(Torre).OrderBy(s => -s.rect.y).ThenBy(s => s.rect.x).ToArray();

        for (int i = 0; i < Hogueras.Length; i++)
        {
            GameObject go = new GameObject("Hoguera_" + (i + 1));
            go.transform.SetParent(padre);
            go.transform.position = Hogueras[i];

            SpriteRenderer torre = new GameObject("Torre").AddComponent<SpriteRenderer>();
            torre.transform.SetParent(go.transform, false);
            torre.transform.localScale = new Vector3(2f, 2f, 1f);
            if (fotogramas.Length > 0) torre.sprite = fotogramas[0];
            torre.sortingLayerName = "Items";
            // Pie de la torre sobre el suelo.
            torre.transform.localPosition = new Vector3(0f, -torre.bounds.min.y + go.transform.position.y, 0f);

            AnimacionSprites anim = torre.gameObject.AddComponent<AnimacionSprites>();
            anim.fotogramas = fotogramas;
            anim.fps = 8f;

            Hoguera h = go.AddComponent<Hoguera>();
            Asignar(h, "torre", torre);
            AsignarFloat(h, "alturaAviso", torre.bounds.size.y + 0.35f);
        }
    }

    // ------------------------------------------------------------------ Trampas

    private static void ConstruirTrampas(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Trampas");
        Sprite[] rocas = Sprites(Cueva + "Cave - SmallRocks.png");
        Sprite[] estalactitas = rocas.Where(s => s.rect.x > 1550 && s.rect.y > 1350 && s.rect.width < 200 && s.rect.height > 150).ToArray();

        // Estalactitas que caen.
        for (int i = 0; i < Estalactitas.Length && estalactitas.Length > 0; i++)
        {
            Sprite s = estalactitas[i % estalactitas.Length];
            float alto = 1.2f + (i % 3) * 0.2f;
            float ancho = alto * s.rect.width / s.rect.height;

            // El sprite va en el propio objeto: la estalactita se mueve entera.
            GameObject go = new GameObject("Estalactita_" + i);
            go.layer = LayerMask.NameToLayer("Traps");
            go.transform.SetParent(padre);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingLayerName = "traps";
            go.transform.localScale = new Vector3(ancho / s.bounds.size.x, alto / s.bounds.size.y, 1f);
            go.transform.position = new Vector3(Estalactitas[i].x, Estalactitas[i].y + 0.1f - alto * 0.5f, 0f)
                                    - Vector3.Scale(s.bounds.center, go.transform.localScale);

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(box.size.x * 0.5f, box.size.y * 0.9f);
            go.AddComponent<Estalactita>();
        }

        // Respiraderos de fuego.
        Sprite caja = PrimerSprite(Trampas + "FireBox.png");
        Sprite[] fuego = Sprites(Trampas + "Fire.png").OrderBy(s => s.rect.x).ToArray();
        for (int i = 0; i < Fuegos.Length; i++)
        {
            GameObject go = new GameObject("Fuego_" + i);
            go.layer = LayerMask.NameToLayer("Traps");
            go.transform.SetParent(padre);
            go.transform.position = new Vector3(Fuegos[i].x, Fuegos[i].y, 0f);

            if (caja != null)
                Pieza(go.transform, "Base", caja, new Vector2(Fuegos[i].x, Fuegos[i].y + 0.11f), new Vector2(1.46f, 0.22f), "traps", 1);

            Transform llamas = Hijo(go.transform, "Llamas");
            llamas.localPosition = new Vector3(0f, 0.18f, 0f);
            for (int k = -1; k <= 1 && fuego.Length > 0; k++)
            {
                SpriteRenderer sr = new GameObject("Llama").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(llamas, false);
                sr.sprite = fuego[0];
                sr.sortingLayerName = "traps";
                sr.sortingOrder = 2;
                float escala = 0.9f / fuego[0].bounds.size.x;
                sr.transform.localScale = new Vector3(escala, escala, 1f);
                sr.transform.localPosition = new Vector3(k * 0.42f, 0.45f, 0f);
                AnimacionSprites a = sr.gameObject.AddComponent<AnimacionSprites>();
                a.fotogramas = fuego;
                a.fps = 12f;
                a.inicioAleatorio = true;
            }

            BoxCollider2D zona = go.AddComponent<BoxCollider2D>();
            zona.isTrigger = true;
            zona.size = new Vector2(1.2f, 1.9f);
            zona.offset = new Vector2(0f, 1.1f);
            go.AddComponent<ZonaDano>();

            TrampaFuego t = go.AddComponent<TrampaFuego>();
            Asignar(t, "llamas", llamas);
            Asignar(t, "zonaDano", zona);
            AsignarFloat(t, "escalaEncendida", 2.1f);
            AsignarFloat(t, "desfase", Fuegos[i].z);
        }

        // Fosos de pinchos.
        Sprite pincho = PrimerSprite(Trampas + "Spike_B.png");
        for (int i = 0; i < Pinchos.Length && pincho != null; i++)
        {
            Vector3 p = Pinchos[i];
            GameObject go = new GameObject("Pinchos_" + i);
            go.layer = LayerMask.NameToLayer("Traps");
            go.transform.SetParent(padre);
            go.transform.position = new Vector3(p.x + p.y * 0.5f, p.z + 0.25f, 0f);
            Pieza(go.transform, "Sprite", pincho, go.transform.position, new Vector2(p.y, 0.5f), "traps", 0);

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(p.y, 0.45f);
            go.AddComponent<ZonaDano>();
        }

        // Por si algo cae fuera del mapa.
        GameObject muerte = new GameObject("ZonaMuerte");
        muerte.layer = LayerMask.NameToLayer("Traps");
        muerte.transform.SetParent(padre);
        muerte.transform.position = new Vector3(87f, -14f, 0f);
        BoxCollider2D m = muerte.AddComponent<BoxCollider2D>();
        m.isTrigger = true;
        m.size = new Vector2(220f, 4f);
        muerte.AddComponent<DeadArea>();
    }

    private static void ConstruirMarcadores(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Marcadores");
        foreach (var (nombre, pos) in Marcadores)
        {
            Transform t = Hijo(padre, nombre);
            t.position = pos;
        }
    }

    // ------------------------------------------------------------------ Enemigos

    private const string CarpetaEnemigos = Pack + "ENEMIGOS/";
    private const string CarpetaPrefabs = "Assets/Prefabs/Enemies/Cueva";

    // Una animacion de una hoja, cortada por cuadricula.
    private static AnimadorHoja.Clip Clip(string nombre, string ruta, int celda, int cantidad, float fps, bool bucle,
                                          Vector2 pivote, int fila = 0)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (tex == null) Debug.LogWarning("[Cueva] No encuentro la hoja " + ruta);
        return new AnimadorHoja.Clip
        {
            nombre = nombre, hoja = tex, anchoCelda = celda, altoCelda = celda, fila = fila,
            primero = 0, cantidad = cantidad, fps = fps, bucle = bucle, pivote = pivote, pixelesPorUnidad = 28f,
        };
    }

    private static void ConstruirEnemigos(Transform nivel)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies")) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
        if (!AssetDatabase.IsValidFolder(CarpetaPrefabs)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies", "Cueva");

        Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>
        {
            { "Slime", PrefabSlime() },
            { "Mimic", PrefabMimic() },
            { "Mago", PrefabMago(PrefabProyectil()) },
            { "Volador", PrefabCacodemonio() },
        };

        Transform padre = Hijo(nivel, "Enemigos");
        foreach (var (tipo, pos) in Enemigos)
        {
            if (!prefabs.TryGetValue(tipo, out GameObject prefab) || prefab == null) continue;
            GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            e.transform.position = new Vector3(pos.x, pos.y + (tipo == "Volador" ? 0f : 0.02f), 0f);
            // El Mimic de la cueva secreta guarda la recompensa: un frasco extra.
            if (tipo == "Mimic") e.AddComponent<SoltarRecompensa>();
        }
    }

    // Esqueleto comun: raiz con fisica, vida y destello; hijo "Visual" con el
    // sprite y el animador.
    private static GameObject BaseEnemigo(string nombre, List<AnimadorHoja.Clip> clips, int vida, float barraAltura,
                                          float muerte, out AnimadorHoja anim)
    {
        GameObject go = new GameObject(nombre);
        go.tag = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemies");

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        GameObject v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -1;
        Material flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Enemy_Flash.mat");
        if (flash != null) sr.sharedMaterial = flash;
        // Vista previa en el editor: un fotograma cortado por Unity. En juego lo
        // sustituye el animador.
        Texture2D primera = clips[0].hoja;
        if (primera != null)
            sr.sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(primera)).OfType<Sprite>().FirstOrDefault();

        anim = v.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = clips;

        EnemyHealth salud = go.AddComponent<EnemyHealth>();
        SerializedObject so = new SerializedObject(salud);
        so.FindProperty("maxHealth").intValue = vida;
        so.FindProperty("deathAnimDuration").floatValue = muerte;
        so.FindProperty("healthBarOffset").vector2Value = new Vector2(0f, barraAltura);
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<HitFlash>();
        return go;
    }

    private static GameObject Guardar(GameObject go, string archivo)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CarpetaPrefabs + "/" + archivo + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void Enlazar(Component c, AnimadorHoja anim)
    {
        Asignar(c, "anim", anim);
        Asignar(c, "visual", anim.transform);
    }

    private static GameObject PrefabSlime()
    {
        string r = CarpetaEnemigos + "Enemigo_MonsterPack2/Slime/";
        Vector2 p = new Vector2(0.497f, 0.442f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Clip("quieto", r + "idle.png", 156, 14, 10f, true, p),
            Clip("andar", r + "walk.png", 156, 6, 10f, true, p),
            Clip("ataque", r + "attack.png", 156, 19, 14f, false, p),
            Clip("golpe", r + "hurt.png", 156, 3, 10f, false, p),
            Clip("muerte", r + "death.png", 156, 11, 12f, false, p),
        };
        GameObject go = BaseEnemigo("Slime", clips, 45, 0.95f, 1f, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.2f, 0.62f);
        box.offset = new Vector2(0f, 0.31f);
        Enlazar(go.AddComponent<EnemigoSlime>(), anim);
        return Guardar(go, "Slime");
    }

    private static GameObject PrefabMimic()
    {
        string r = CarpetaEnemigos + "Enemigo_MonsterPack2/Mimic/";
        Vector2 p = new Vector2(0.524f, 0.432f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Clip("cerrado", r + "Idle_closed.png", 146, 1, 1f, true, p),
            Clip("abrir", r + "opening.png", 146, 6, 10f, false, p),
            Clip("transformar", r + "transform.png", 146, 7, 10f, false, p),
            Clip("quieto", r + "idle_transformed.png", 146, 9, 10f, true, p),
            Clip("andar", r + "walk.png", 146, 6, 10f, true, p),
            Clip("mordisco", r + "attack_1.png", 146, 14, 16f, false, p),
            Clip("lengua", r + "attack_2.png", 146, 13, 16f, false, p),
            Clip("golpe", r + "hurt.png", 146, 3, 10f, false, p),
            Clip("muerte", r + "death.png", 146, 6, 8f, false, p),
        };
        GameObject go = BaseEnemigo("Mimic", clips, 110, 1.35f, 0.85f, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.0f, 1.0f);
        box.offset = new Vector2(0f, 0.5f);
        Enlazar(go.AddComponent<EnemigoMimic>(), anim);
        return Guardar(go, "Mimic");
    }

    private static GameObject PrefabProyectil()
    {
        string r = CarpetaEnemigos + "Enemigo_EvilWizard3/Sprites/Projectile/";
        Vector2 p = new Vector2(0.5f, 0.5f);
        GameObject go = new GameObject("ProyectilMago");
        go.layer = LayerMask.NameToLayer("Traps");
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        CircleCollider2D c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.22f;

        GameObject v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "VFX";
        AnimadorHoja anim = v.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = new List<AnimadorHoja.Clip>
        {
            Clip("vuelo", r + "Moving.png", 50, 4, 12f, true, p),
            Clip("explosion", r + "Explode.png", 50, 7, 16f, false, p),
        };

        ProyectilMagico pm = go.AddComponent<ProyectilMagico>();
        Asignar(pm, "anim", anim);
        return Guardar(go, "ProyectilMago");
    }

    private static GameObject PrefabMago(GameObject proyectil)
    {
        string r = CarpetaEnemigos + "Enemigo_EvilWizard3/Sprites/";
        Vector2 p = new Vector2(0.52f, 0.307f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Clip("quieto", r + "Idle.png", 140, 10, 10f, true, p),
            Clip("andar", r + "Walk.png", 140, 8, 10f, true, p),
            Clip("ataque", r + "Attack.png", 140, 13, 12f, false, p),
            Clip("golpe", r + "Get hit.png", 140, 3, 10f, false, p),
            Clip("muerte", r + "Death.png", 140, 18, 12f, false, p),
        };
        GameObject go = BaseEnemigo("Mago", clips, 55, 2.1f, 1.6f, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.9f, 1.75f);
        box.offset = new Vector2(0f, 0.88f);
        EnemigoMago mago = go.AddComponent<EnemigoMago>();
        Enlazar(mago, anim);
        if (proyectil != null) Asignar(mago, "proyectil", proyectil.GetComponent<ProyectilMagico>());
        return Guardar(go, "Mago");
    }

    private static GameObject PrefabCacodemonio()
    {
        string r = CarpetaEnemigos + "Enemigo_Cacodemonio/Cacodaemon Sprite Sheet.png";
        Vector2 p = new Vector2(0.49f, 0.45f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Clip("vuelo", r, 64, 6, 10f, true, p, 0),
            Clip("rugido", r, 64, 6, 12f, true, p, 1),
            Clip("golpe", r, 64, 4, 10f, false, p, 2),
            Clip("muerte", r, 64, 8, 10f, false, p, 3),
        };
        GameObject go = BaseEnemigo("Cacodemonio", clips, 70, 1.05f, 1.2f, out AnimadorHoja anim);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        CircleCollider2D c = go.AddComponent<CircleCollider2D>();
        c.radius = 0.6f;
        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("volador").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Enlazar(go.AddComponent<EnemigoCacodemonio>(), anim);
        return Guardar(go, "Cacodemonio");
    }

    // ------------------------------------------------------------------ Jefe

    private const string CarpetaJefe = Pack + "BOSSES/Boss_CrimsonWraith/";
    private const string CarpetaFx = Pack + "EFECTOS DE ATAQUE DE LOS ENEMIGOS/";
    private const string Musica = Pack + "SONIDOS/Sonidos_MusicaBosses/Sonidos_BossFightPack/01 - The final revalation - W-The Final Revalation.ogg";

    // Pie de cada pose del jefe (fila, desde arriba, en su lienzo de 1980x1080).
    // Las poses con circulo o anillo en el suelo se suben un poco: el anillo va
    // alrededor de los pies, no debajo.
    private static readonly Dictionary<int, int> PieJefe = new Dictionary<int, int>
    {
        {1, 767}, {2, 766}, {3, 766}, {4, 766}, {5, 766}, {6, 710}, {7, 710}, {8, 696}, {9, 696}, {10, 775},
        {11, 765}, {12, 706}, {13, 765}, {14, 706}, {15, 769}, {16, 767}, {17, 726}, {18, 697}, {19, 695},
        {20, 711}, {21, 692}, {22, 680}, {23, 696}, {24, 696}, {25, 720},
    };
    // Recorte comun alrededor del personaje (desde abajo a la izquierda) y el eje
    // del cuerpo: todas las poses comparten ese eje para no bailar.
    private static readonly RectInt RecorteJefe = new RectInt(480, 220, 1200, 860);
    private const float EjeJefe = 1100f;

    private static AnimadorHoja.Clip ClipJefe(string nombre, int[] poses, float fps, bool bucle)
    {
        var texturas = new Texture2D[poses.Length];
        var pivotes = new Vector2[poses.Length];
        for (int i = 0; i < poses.Length; i++)
        {
            texturas[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaJefe + poses[i] + ".png");
            float pieDesdeAbajo = 1080f - PieJefe[poses[i]];
            pivotes[i] = new Vector2((EjeJefe - RecorteJefe.x) / RecorteJefe.width,
                                     (pieDesdeAbajo - RecorteJefe.y) / RecorteJefe.height);
        }
        return new AnimadorHoja.Clip
        {
            nombre = nombre, fotogramas = texturas, pivotes = pivotes, recorte = RecorteJefe,
            cantidad = poses.Length, fps = fps, bucle = bucle, pixelesPorUnidad = 110f,
        };
    }

    // Efecto de fotogramas sueltos: todos los PNG "...frameN" de una carpeta, en orden.
    private static AnimadorHoja.Clip ClipFx(string nombre, string carpeta, float fps, bool bucle, float ppu, Vector2 pivote,
                                            int desde = 0, int hasta = -1)
    {
        return ClipDesde(nombre, CarpetaFx + carpeta, fps, bucle, ppu, pivote, desde, hasta);
    }

    private static AnimadorHoja.Clip ClipDesde(string nombre, string ruta, float fps, bool bucle, float ppu, Vector2 pivote,
                                               int desde = 0, int hasta = -1)
    {
        var archivos = Directory.GetFiles(ruta, "*.png")
            .Where(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains("frame"))
            .OrderBy(f => int.Parse(new string(Path.GetFileNameWithoutExtension(f).Reverse().TakeWhile(char.IsDigit).Reverse().ToArray())))
            .Select(f => AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/')))
            .Where(t => t != null).ToList();
        if (archivos.Count == 0) Debug.LogWarning("[Cueva] Sin fotogramas en " + ruta);
        if (hasta < 0 || hasta >= archivos.Count) hasta = archivos.Count - 1;
        var elegidos = archivos.Skip(desde).Take(Mathf.Max(0, hasta - desde + 1)).ToArray();
        return new AnimadorHoja.Clip
        {
            nombre = nombre, fotogramas = elegidos, cantidad = elegidos.Length, fps = fps, bucle = bucle,
            pivote = pivote, pixelesPorUnidad = ppu,
        };
    }

    private static GameObject PrefabJefe()
    {
        var clips = new List<AnimadorHoja.Clip>
        {
            ClipJefe("guardia", new[] { 6 }, 1f, true),
            ClipJefe("aturdido", new[] { 6 }, 1f, true),
            ClipJefe("preparar", new[] { 17 }, 1f, true),
            ClipJefe("tajo", new[] { 20, 21, 22 }, 9f, false),
            ClipJefe("frenar", new[] { 22 }, 1f, true),
            ClipJefe("embestida", new[] { 8, 23 }, 14f, true),
            ClipJefe("conjuro", new[] { 16, 1 }, 5f, true),
            ClipJefe("zarpazo", new[] { 2, 3 }, 6f, false),
            ClipJefe("flotar", new[] { 15, 25 }, 3f, true),
            ClipJefe("caida", new[] { 17 }, 1f, true),
            ClipJefe("impacto", new[] { 22 }, 1f, true),
            ClipJefe("grande_guardia", new[] { 12 }, 1f, true),
            ClipJefe("grande_zarpazo", new[] { 11 }, 1f, true),
            ClipJefe("grande_rugido", new[] { 10 }, 1f, true),
            ClipJefe("muerte", new[] { 25 }, 1f, true),
        };
        GameObject go = BaseEnemigo("CrimsonWraith", clips, 1600, 4.5f, 3.2f, out AnimadorHoja anim);
        anim.destino.sortingOrder = -2;
        // Vista previa en el editor: la pose de guardia.
        anim.destino.sprite = null;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.6f, 3.2f);
        box.offset = new Vector2(0f, 1.6f);

        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("showHealthBar").boolValue = false;
        so.FindProperty("inamovible").boolValue = true;
        so.FindProperty("bodyMass").floatValue = 500f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        go.AddComponent<NoReaparece>();

        JefeWraith j = go.AddComponent<JefeWraith>();
        Enlazar(j, anim);
        Vector2 centro = new Vector2(0.5f, 0.5f);
        j.fxOnda = ClipFx("onda", "Efecto_Warlock/VFX2/Frames", 14f, true, 64f, new Vector2(0.5f, 0.42f), 3, 10);
        j.fxPilar = ClipFx("pilar", "Efecto_Sacerdote/VFX 2/frames", 16f, false, 35.5f, new Vector2(0.5f, 0.04f));
        j.fxGlifo = ClipFx("glifo", "Efecto_MagoSangre/VFX1/part2(loop)/frames", 12f, true, 64f, centro);
        j.fxOrbe = ClipFx("orbe", "Efecto_MagoSangre/VFX3/frames", 12f, true, 64f, centro);
        j.fxSangre = ClipFx("sangre", "Efecto_MagoSangre/VFX2/frames", 18f, false, 64f, centro);
        j.fxMeteoro = ClipFx("meteoro", "Efecto_MagoFuego/VFX2/frames", 14f, true, 64f, centro);
        j.fxExplosion = ClipFx("explosion", "Efecto_ExplosionesFuego/VFX1/Frames", 16f, false, 64f, centro);
        j.fxAnillo = ClipFx("anillo", "Efecto_MagoFuego/VFX3/frames", 14f, false, 64f, centro);
        j.fxPortal = ClipFx("portal", "Efecto_Portal/Frames", 12f, false, 32f, centro);
        j.fxPolvo = ClipFx("polvo", "Efecto_HumoPolvo/VFX2/frames", 12f, false, 64f, new Vector2(0.5f, 0.3f));
        j.fxColumnaPolvo = ClipFx("columna", "Efecto_HumoPolvo/VFX3/frames", 14f, false, 64f, new Vector2(0.5f, 0.1f));
        j.fxMedialuna = ClipFx("medialuna", "Efecto_Halloween/VFX 1/Frames", 14f, true, 64f, centro);
        j.fxVorticeInicio = ClipFx("vortice_ini", "Efecto_MagoSangre/VFX1/part1(start)/frames", 12f, false, 64f, centro);
        j.fxVorticeBucle = ClipFx("vortice", "Efecto_MagoSangre/VFX1/part2(loop)/frames", 12f, true, 64f, centro);
        j.fxVorticeFin = ClipFx("vortice_fin", "Efecto_MagoSangre/VFX1/part3(end)/frames", 12f, false, 64f, centro);
        EditorUtility.SetDirty(j);
        return Guardar(go, "CrimsonWraith");
    }

    private static void ConstruirArena(Transform nivel, Scene escena)
    {
        Transform padre = Hijo(nivel, "ArenaJefe");
        int items = LayerMask.NameToLayer("Items");

        // Niebla de la puerta: muro solido (apagado hasta el combate) y humo rojo.
        GameObject niebla = new GameObject("NieblaJefe");
        niebla.layer = LayerMask.NameToLayer("Ground");
        niebla.transform.SetParent(padre);
        niebla.transform.position = new Vector3(140.6f, 8f, 0f);
        BoxCollider2D muro = niebla.AddComponent<BoxCollider2D>();
        muro.size = new Vector2(0.8f, 8f);
        AnimadorHoja.Clip humo = ClipFx("humo", "Efecto_HumoPolvo/VFX1/frames", 8f, true, 64f, new Vector2(0.5f, 0.5f));
        var humos = new List<SpriteRenderer>();
        for (int i = 0; i < 5; i++)
        {
            SpriteRenderer sr = new GameObject("Humo").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(niebla.transform, false);
            sr.transform.localPosition = new Vector3(i % 2 == 0 ? -0.15f : 0.15f, -3.2f + i * 1.6f, 0f);
            sr.transform.localScale = new Vector3(1.3f, 1.6f, 1f);
            sr.sortingLayerName = "VFX";
            sr.sortingOrder = 2;
            AnimadorHoja a = sr.gameObject.AddComponent<AnimadorHoja>();
            a.destino = sr;
            a.clips = new List<AnimadorHoja.Clip> { humo };
            a.clipInicial = "humo";
            humos.Add(sr);
        }

        // Pista narrativa junto a la puerta.
        GameObject pista = new GameObject("PistaNarrativa");
        pista.layer = items;
        pista.transform.SetParent(padre);
        pista.transform.position = new Vector3(134f, 6f, 0f);
        pista.AddComponent<BoxCollider2D>().size = new Vector2(1.5f, 4f);
        pista.AddComponent<PistaNarrativa>();

        // El gestor: su trigger es el interior de la arena (pasada la niebla).
        GameObject gestor = new GameObject("GestorArena");
        gestor.layer = items;
        gestor.transform.SetParent(padre);
        gestor.transform.position = new Vector3(159.5f, 13f, 0f);
        gestor.AddComponent<BoxCollider2D>().size = new Vector2(33f, 18f);
        ArenaJefe arena = gestor.AddComponent<ArenaJefe>();

        GameObject jefe = PrefabJefe();
        Asignar(arena, "prefabJefe", jefe != null ? jefe.GetComponent<JefeWraith>() : null);
        Asignar(arena, "muroNiebla", muro);
        Asignar(arena, "musica", AssetDatabase.LoadAssetAtPath<AudioClip>(Musica));
        GameObject salida = escena.GetRootGameObjects().FirstOrDefault(r => r.name == "ExitDoor");
        if (salida != null) Asignar(arena, "salida", salida);

        SerializedObject so = new SerializedObject(arena);
        SerializedProperty lista = so.FindProperty("visualNiebla");
        lista.arraySize = humos.Count;
        for (int i = 0; i < humos.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = humos[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Efectos del player

    private const string CarpetaFxPlayer = Pack + "EFECTOS DE ATAQUE DEL PLAYER/";

    // Tajos de espada (en azul cian, a juego con la hoja) e impactos, en el prefab
    // del Player: asi salen en todos los niveles.
    private static void EfectosDelPlayer()
    {
        const string ruta = "Assets/Prefabs/Player.prefab";
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        EfectosGolpePlayer ef = raiz.GetComponent<EfectosGolpePlayer>();
        if (ef == null) ef = raiz.AddComponent<EfectosGolpePlayer>();

        string tajos = CarpetaFxPlayer + "Efecto_Tajos/128x128/";
        Vector2 c = new Vector2(0.5f, 0.5f);
        AnimadorHoja.Clip horizontal = ClipDesde("tajo_h", tajos + "Slash 1/color5/Frames", 24f, false, 64f, c);
        AnimadorHoja.Clip curvo = ClipDesde("tajo_c", tajos + "Slash 3/color5/frames", 24f, false, 64f, c);
        AnimadorHoja.Clip ascendente = ClipDesde("tajo_a", tajos + "Slash 2/color5/Frames", 22f, false, 64f, c);

        // De momento sin tajos ni impactos normales (se veian demasiado): solo queda
        // el estallido del contraataque tras un parry. Para recuperarlos, por perfil
        // (0-2 combo, 3-4 aereos, 7 lanzador):
        //   ef.tajos = new[] { horizontal, curvo, horizontal, curvo, ascendente, null, null, ascendente };
        ef.tajos = new AnimadorHoja.Clip[0];
        ef.escalaTajo = new float[0];
        ef.impactoEspada = null;
        ef.impactoPunos = null;
        ef.impactoContra = ClipDesde("impacto_contra", CarpetaFxPlayer + "Efecto_Guerrero/VFX 5/Frames", 18f, false, 64f, c);

        PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        PrefabUtility.UnloadPrefabContents(raiz);
    }

    // ------------------------------------------------------------------ Camara

    // Zonas con zoom propio: el pozo y la caida algo mas abiertos (se ve a donde
    // se salta) y la arena bastante mas (se ve al jefe y sus ataques).
    private static readonly (string nombre, Rect zona, float tamano, float arriba)[] ZonasCamara =
    {
        ("Pozo", Rect.MinMaxRect(40f, 0f, 52f, 27f), 4.1f, 0.6f),
        ("Caida", Rect.MinMaxRect(112f, 4f, 122f, 27f), 4.1f, -0.8f),
        ("Arena", Rect.MinMaxRect(140.5f, 4f, 176f, 22f), 5.3f, 1.4f),
    };

    private static void ConstruirZonasCamara(Transform nivel)
    {
        Transform padre = Hijo(nivel, "ZonasCamara");
        int capa = LayerMask.NameToLayer("Items");
        foreach (var (nombre, zona, tamano, arriba) in ZonasCamara)
        {
            GameObject go = new GameObject("Zona_" + nombre);
            go.layer = capa;
            go.transform.SetParent(padre);
            go.transform.position = zona.center;
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = zona.size;
            ZonaCamara z = go.AddComponent<ZonaCamara>();
            SerializedObject so = new SerializedObject(z);
            so.FindProperty("tamano").floatValue = tamano;
            so.FindProperty("desplazamientoY").floatValue = arriba;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ------------------------------------------------------------------ Utilidades

    private static Transform Hijo(Transform padre, string nombre)
    {
        Transform t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        return t;
    }

    private static Sprite[] Sprites(string ruta)
    {
        Sprite[] s = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToArray();
        if (s.Length == 0) Debug.LogWarning("[Cueva] Sin sprites en " + ruta);
        return s;
    }

    private static Sprite PrimerSprite(string ruta)
    {
        return Sprites(ruta).OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
    }

    // Coloca un sprite ocupando exactamente "tamano" con su centro en "centro"
    // (en el mundo), sea cual sea su pivote.
    private static SpriteRenderer Pieza(Transform padre, string nombre, Sprite s, Vector2 centro, Vector2 tamano,
                                        string capa, int orden, bool voltearY = false)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, true);
        sr.sprite = s;
        sr.flipY = voltearY;
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;

        Vector2 escala = new Vector2(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y);
        sr.transform.localScale = new Vector3(escala.x, escala.y, 1f);
        Vector2 desvio = new Vector2(s.bounds.center.x * escala.x, s.bounds.center.y * escala.y * (voltearY ? -1f : 1f));
        sr.transform.position = new Vector3(centro.x - desvio.x, centro.y - desvio.y, 0f);
        return sr;
    }

    // Sprite de "alto" dado, con la base apoyada en (x, suelo).
    private static SpriteRenderer PiezaApoyada(Transform padre, string nombre, Sprite s, float x, float suelo, float alto,
                                               string capa, int orden, Color color)
    {
        float ancho = alto * s.rect.width / s.rect.height;
        SpriteRenderer sr = Pieza(padre, nombre, s, new Vector2(x, suelo + alto * 0.5f), new Vector2(ancho, alto), capa, orden);
        sr.color = color;
        return sr;
    }

    private static void Asignar(Object objetivo, string campo, Object valor)
    {
        SerializedObject so = new SerializedObject(objetivo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Cueva] {objetivo.GetType().Name} no tiene el campo {campo}"); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsignarFloat(Object objetivo, string campo, float valor)
    {
        SerializedObject so = new SerializedObject(objetivo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Cueva] {objetivo.GetType().Name} no tiene el campo {campo}"); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
