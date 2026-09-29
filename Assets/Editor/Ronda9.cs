using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Novena ronda: mejoras de frasco separadas, Desafios, totem-tienda y Logros.
//   - Iconos nuevos en RecursosRPG (lagrimas, frascos, estrella, marcas).
//   - Imagen de cada jefe para el menu (recortada de su sprite).
//   - Fichas de datos editables: jefes y tienda (Resources/Desafios) y logros
//     (Resources/Logros). Si ya existen, NO se pisan (se respetan tus cambios).
//   - Niveles: la Lagrima sagrada colocada pasa a Lagrima carmesi + celeste.
// Warrior > Actualizar > Ronda 9 (o ActualizarProyecto.Ronda9 por linea de comandos).
public static class Ronda9
{
    private const string Iconos = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/";
    private const string Bosses = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/";
    private const string Totem = "Assets/SPRITES PARA NUEVOS NIVELES/TOTEM/Totem_ObeliscoVolador/FlyingObelisk_no_lightnings_no_letter.png";
    private const string Propios = "Assets/Sprites/Desafios/";

    [MenuItem("Warrior/Actualizar/Ronda 9")]
    public static void Todo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RecursosRPG r = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        IconosNuevos(r);
        Directory.CreateDirectory("Assets/Resources/Desafios");
        Directory.CreateDirectory("Assets/Resources/Logros");
        Directory.CreateDirectory(Propios);
        AssetDatabase.Refresh();
        FichasJefes();
        Tienda();
        FichasLogros(r);
        MigrarNiveles();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda9] Listo.");
    }

    // ------------------------------------------------------------------ Iconos

    private static void IconosNuevos(RecursosRPG r)
    {
        (string clave, string ruta)[] nuevos =
        {
            ("objeto_lagrima_curacion", "16-runes-enchantments/icon_53.png"),
            ("objeto_lagrima_mana", "16-runes-enchantments/icon_40.png"),
            ("objeto_frasco_sangre", "03-potions-consumables/icon_08.png"),
            ("objeto_frasco_mana", "03-potions-consumables/icon_07.png"),
            ("estrella", "03-potions-consumables/icon_02.png"),
            ("marca_desafio", "08-loot-treasure/icon_24.png"),
            ("marca_dificil", "08-loot-treasure/icon_40.png"),
        };
        foreach ((string clave, string ruta) in nuevos) Icono(r, clave, Iconos + ruta);
        EditorUtility.SetDirty(r);
    }

    private static Sprite Icono(RecursosRPG r, string clave, string ruta)
    {
        ConfigurarRecursosRPG.PrepararPixel(ruta);
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.SaveAndReimport();
        }
        Sprite s = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
        if (s == null) { Debug.LogWarning("[Ronda9] Sin sprite en " + ruta); return null; }
        if (r == null) return s;
        RecursosRPG.EntradaIcono e = r.iconos.FirstOrDefault(i => i.clave == clave);
        if (e == null) r.iconos.Add(e = new RecursosRPG.EntradaIcono { clave = clave });
        e.sprite = s;
        return s;
    }

    // ------------------------------------------------------------------ Jefes

    private static void FichasJefes()
    {
        const string rw = "Assets/Resources/Desafios/Jefe_CrimsonWraith.asset";
        if (AssetDatabase.LoadAssetAtPath<FichaJefe>(rw) == null)
        {
            FichaJefe f = ScriptableObject.CreateInstance<FichaJefe>();
            f.id = "crimson_wraith";
            f.nombre = "Crimson Wraith";
            f.escena = "Nivel Cueva";
            f.orden = 1;
            f.imagen = Recorte(Bosses + "Boss_CrimsonWraith/1.png", "Jefe_CrimsonWraith.png", false);
            f.afinidades = new List<FichaJefe.Afinidad>
            {
                new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = 1.8f, cuando = "Fase 2" },
                new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.4f, cuando = "Fase 2" },
            };
            f.notaAfinidades = "En la fase 1 todo le hace el daño normal. En la fase 2 su coraza resiste la espada sin imbuir (x0.6): hay que imbuirla.";
            f.informacion =
                "Cae del techo al empezar: un parry justo al aterrizar lo deja aturdido.\n" +
                "<b>Fase 1</b>: alza la garra y encadena dos zarpazos; embiste cruzando la arena; lanza ondas de sangre por el suelo (sáltalas); " +
                "hace brotar pilares de sangre bajo tus pies y suelta orbes que te persiguen (un parry los devuelve). Pilares y orbes causan sangrado.\n" +
                "<b>Fase 2</b> (a mitad de vida): toma su forma grande y la arena se tiñe de rojo. Zarpazo gigante con ondas, lluvia de fuego, " +
                "teletransporte a tu espalda y pilares en cadena, con menos pausa entre ataques.";
            f.historia =
                "Nadie recuerda su rostro, porque no tiene uno que la mente acepte. Crimson Wraith fue algo más antiguo que la cueva, y la montaña " +
                "se cerró a su alrededor como una herida que cicatriza sobre una astilla. Se alimenta de la sangre que baja hasta él: la de los mineros, " +
                "la de los que vinieron a matarlo, la tuya. Sus garras arrancan lo que tocan. Sus tentáculos buscan en la oscuridad lo que las garras " +
                "no alcanzan. Y sus hechizos no piden permiso: la sangre simplemente obedece. Quienes lo vieron no saben describirlo. Solo dicen que sigue creciendo.";
            AssetDatabase.CreateAsset(f, rw);
        }

        const string rs = "Assets/Resources/Desafios/Jefe_ShadowedWetlands.asset";
        if (AssetDatabase.LoadAssetAtPath<FichaJefe>(rs) == null)
        {
            FichaJefe f = ScriptableObject.CreateInstance<FichaJefe>();
            f.id = "shadowed_wetlands";
            f.nombre = "Shadowed Wetlands";
            f.escena = "Nivel Nieve";
            f.orden = 0;
            f.imagen = Recorte(Bosses + "Boss_ShadowedWetlands/Sprite Sheet/idle.png", "Jefe_ShadowedWetlands.png", true);
            f.afinidades = new List<FichaJefe.Afinidad>
            {
                new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = 1.4f, cuando = "Fase 1" },
                new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.5f, cuando = "Fase 1" },
                new FichaJefe.Afinidad { elemento = Elemento.Fuego, multiplicador = 1.6f, cuando = "Fase 2" },
                new FichaJefe.Afinidad { elemento = Elemento.Sangrado, multiplicador = 1.1f, cuando = "Fase 2" },
                new FichaJefe.Afinidad { elemento = Elemento.Hielo, multiplicador = 0.2f, cuando = "Fase 2" },
                new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.8f, cuando = "Fase 2" },
            };
            f.notaAfinidades = "Con x0.5 o menos, ese elemento no le aplica su estado (ni quemadura, ni escarcha...).";
            f.informacion =
                "<b>Fase 1</b> (sombra): barrido a ras de suelo con mucho alcance; barrido seguido de un tajo alzado (dos ventanas de parry); " +
                "revés si estás a su espalda; paso sombrío (cruza al otro lado como una esfera y sale con un tajo circular); medialunas de sombra y caída desde lo alto.\n" +
                "<b>Agarre</b>: pose de carga muy visible. Si te atrapa, te lanza y te corta hasta matarte; al aparecer hay un instante a cámara lenta para esquivarlo.\n" +
                "<b>Fase 2</b>: al vaciar su vida revive imbuido en hielo, más rápido. Deja escarcha que te ralentiza y suma lanzas y estacas de hielo, " +
                "ventisca y medialunas heladas. Parar todos los golpes de un combo le rompe la postura.";
            f.historia =
                "Antes de la nevada eterna, estos humedales alimentaban a un pueblo entero. Luego llegó el frío, y con él la sombra: algo que no caza por hambre, " +
                "sino por costumbre. Los que cruzaron la ciénaga helada dicen que el silencio llega antes que el agarre. Quien lo ve venir aún puede huir. " +
                "Quien duda, no vuelve. Lo que queda es una presencia paciente, hecha de agua negra y hielo roto, que espera a quien todavía cree que el camino sigue.";
            AssetDatabase.CreateAsset(f, rs);
        }
    }

    // Recorta lo pintado de la imagen (del primer fotograma si es una hoja) y
    // lo guarda como sprite propio para el menu.
    private static Sprite Recorte(string origen, string nombre, bool pixel)
    {
        string destino = Propios + nombre;
        if (!File.Exists(destino))
        {
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(origen));
            int x0 = -1, x1 = -1, y0 = t.height, y1 = -1, vacias = 0;
            for (int x = 0; x < t.width; x++)
            {
                bool hay = false;
                for (int y = 0; y < t.height; y++) if (t.GetPixel(x, y).a > 0.1f) { hay = true; y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                if (hay) { if (x0 < 0) x0 = x; x1 = x; vacias = 0; }
                else if (x0 >= 0 && ++vacias >= 4) break; // fin del primer fotograma
            }
            if (x0 < 0) { Debug.LogWarning("[Ronda9] Imagen vacia: " + origen); return null; }
            Texture2D r = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
            r.SetPixels(t.GetPixels(x0, y0, r.width, r.height));
            r.Apply();
            File.WriteAllBytes(destino, r.EncodeToPNG());
            AssetDatabase.ImportAsset(destino);
        }
        TextureImporter ti = AssetImporter.GetAtPath(destino) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(destino);
    }

    // ------------------------------------------------------------------ Tienda

    private static void Tienda()
    {
        const string ruta = "Assets/Resources/Desafios/Tienda.asset";
        FichaTienda t = AssetDatabase.LoadAssetAtPath<FichaTienda>(ruta);
        bool nueva = t == null;
        if (nueva) t = ScriptableObject.CreateInstance<FichaTienda>();
        if (nueva)
        {
            FichaTienda.Articulo A(string id, string nombre, Equipo.Objeto o, int precio, string req = "") =>
                new FichaTienda.Articulo { id = id, nombre = nombre, objeto = o, precio = precio, requiere = req };
            t.articulos = new List<FichaTienda.Articulo>
            {
                A("arma", "Mejora de arma", Equipo.Objeto.PiedraForja, 1100),
                A("curacion", "Mejora de curación", Equipo.Objeto.LagrimaCarmesi, 650),
                A("mana", "Mejora de recuperación", Equipo.Objeto.LagrimaCeleste, 550),
                A("sangre_1", "Frasco de sangre I", Equipo.Objeto.FrascoSangre, 450),
                A("sangre_2", "Frasco de sangre II", Equipo.Objeto.FrascoSangre, 750, "sangre_1"),
                A("sangre_3", "Frasco de sangre III", Equipo.Objeto.FrascoSangre, 1100, "sangre_2"),
                A("mana_1", "Frasco de maná I", Equipo.Objeto.FrascoMana, 400),
                A("mana_2", "Frasco de maná II", Equipo.Objeto.FrascoMana, 650, "mana_1"),
            };
            t.frases = new List<string>
            {
                "Las almas no pesan. Lo que compras con ellas, sí.",
                "Todo tiene un precio. Hasta el descanso.",
                "Elige. Lo que dejes aquí, no volverá.",
                "Otros vinieron con las manos llenas. Aquí quedaron sus huesos.",
                "Gasta con cuidado, o no gastes.",
                "El tótem escucha. El tótem no perdona.",
                "No alcanza para todo. Nunca alcanza.",
                "Las almas que cargas no te pertenecen. Solo las guardas un rato.",
            };
        }
        // El aspecto del totem (siempre al dia con el sprite).
        ConfigurarRecursosRPG.PrepararPixel(Totem);
        Sprite[] partes = AssetDatabase.LoadAllAssetsAtPath(Totem).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        t.spriteTotem = partes.FirstOrDefault(s => s.rect.height > 150f);
        t.sombraTotem = partes.FirstOrDefault(s => s.rect.height < 40f);
        if (nueva) AssetDatabase.CreateAsset(t, ruta);
        EditorUtility.SetDirty(t);
    }

    // ------------------------------------------------------------------ Logros

    private static void FichasLogros(RecursosRPG r)
    {
        void L(string id, FichaLogro.Seccion s, int orden, string nombre, string desc, FichaLogro.Condicion c, string param, string icono)
        {
            string ruta = $"Assets/Resources/Logros/{s}_{orden}_{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<FichaLogro>(ruta) != null) return;
            FichaLogro l = ScriptableObject.CreateInstance<FichaLogro>();
            l.id = id; l.seccion = s; l.orden = orden; l.nombre = nombre; l.descripcion = desc;
            l.condicion = c; l.parametro = param;
            l.icono = Icono(null, null, Iconos + icono);
            AssetDatabase.CreateAsset(l, ruta);
        }
        L("descenso", FichaLogro.Seccion.Partida, 0, "Descenso", "Llega a la zona de cuevas.", FichaLogro.Condicion.LlegarAEscena, "Nivel Cueva", "16-runes-enchantments/icon_35.png");
        L("aliento_helado", FichaLogro.Seccion.Partida, 1, "Aliento helado", "Llega a la zona nevada.", FichaLogro.Condicion.LlegarAEscena, "Nivel Nieve", "16-runes-enchantments/icon_37.png");
        L("sangre_montana", FichaLogro.Seccion.Partida, 2, "Sangre bajo la montaña", "Derrota a Crimson Wraith.", FichaLogro.Condicion.DerrotarJefeEnPartida, "Nivel Cueva", "16-runes-enchantments/icon_45.png");
        L("humedales_callan", FichaLogro.Seccion.Partida, 3, "Los humedales callan", "Derrota a Shadowed Wetlands.", FichaLogro.Condicion.DerrotarJefeEnPartida, "Nivel Nieve", "16-runes-enchantments/icon_15.png");
        L("primera_ceniza", FichaLogro.Seccion.Partida, 4, "Primera ceniza", "Muere por primera vez.", FichaLogro.Condicion.PrimeraMuerte, "", "03-potions-consumables/icon_19.png");
        L("prueba_cueva", FichaLogro.Seccion.Desafios, 0, "Prueba de la cueva", "Completa el desafío de Crimson Wraith.", FichaLogro.Condicion.CompletarDesafio, "crimson_wraith", "08-loot-treasure/icon_25.png");
        L("prueba_humedales", FichaLogro.Seccion.Desafios, 1, "Prueba de los humedales", "Completa el desafío de Shadowed Wetlands.", FichaLogro.Condicion.CompletarDesafio, "shadowed_wetlands", "08-loot-treasure/icon_24.png");
        L("cicatriz_cueva", FichaLogro.Seccion.Dificil, 0, "Cicatriz de la cueva", "Derrota a Crimson Wraith en dificultad Difícil.", FichaLogro.Condicion.CompletarDesafioDificil, "crimson_wraith", "08-loot-treasure/icon_56.png");
        L("cicatriz_humedales", FichaLogro.Seccion.Dificil, 1, "Cicatriz de los humedales", "Derrota a Shadowed Wetlands en dificultad Difícil.", FichaLogro.Condicion.CompletarDesafioDificil, "shadowed_wetlands", "08-loot-treasure/icon_40.png");
    }

    // ------------------------------------------------------------------ Niveles

    // La Lagrima sagrada colocada en los niveles pasa a ser las dos nuevas (el
    // mismo poder total que antes).
    private static void MigrarNiveles()
    {
        foreach (string ruta in new[] { "Assets/Scenes/Nivel Nieve.unity", "Assets/Scenes/Nivel Cueva.unity" })
        {
            Scene escena = EditorSceneManager.OpenScene(ruta);
            bool cambio = false;
            foreach (CofreMejora c in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CofreMejora>(true)))
            {
                SerializedObject so = new SerializedObject(c);
                SerializedProperty o = so.FindProperty("objeto");
                if (o.intValue != (int)Equipo.Objeto.LagrimaSagrada) continue;
                o.intValue = (int)Equipo.Objeto.LagrimaCarmesi;
                SerializedProperty ex = so.FindProperty("extra");
                ex.arraySize = 1;
                ex.GetArrayElementAtIndex(0).intValue = (int)Equipo.Objeto.LagrimaCeleste;
                so.ApplyModifiedPropertiesWithoutUndo();
                cambio = true;
                Debug.Log($"[Ronda9] {escena.name}: cofre {c.name} -> Lágrima carmesí + Lágrima celeste");
            }
            foreach (ArenaJefe a in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArenaJefe>(true)))
            {
                SerializedObject so = new SerializedObject(a);
                SerializedProperty rec = so.FindProperty("recompensa");
                var lista = new List<int>();
                for (int i = 0; i < rec.arraySize; i++) lista.Add(rec.GetArrayElementAtIndex(i).intValue);
                if (!lista.Contains((int)Equipo.Objeto.LagrimaSagrada)) continue;
                lista = lista.SelectMany(v => v == (int)Equipo.Objeto.LagrimaSagrada
                    ? new[] { (int)Equipo.Objeto.LagrimaCarmesi, (int)Equipo.Objeto.LagrimaCeleste } : new[] { v }).ToList();
                rec.arraySize = lista.Count;
                for (int i = 0; i < lista.Count; i++) rec.GetArrayElementAtIndex(i).intValue = lista[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                cambio = true;
                Debug.Log($"[Ronda9] {escena.name}: recompensa del jefe -> {string.Join(", ", lista.Select(v => (Equipo.Objeto)v))}");
            }
            if (cambio) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        }
    }
}
