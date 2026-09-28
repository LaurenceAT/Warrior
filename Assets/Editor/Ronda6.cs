using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Sexta ronda: sonidos suaves de los menus, sangrado en vez de acido, iconos de
// estados y objetos, cofres de mejora escondidos y recompensas de los jefes.
// Warrior > Actualizar > Ronda 6 (o ActualizarProyecto.Ronda6 por linea de comandos).
public static class Ronda6
{
    private const string CarpetaUI = "Assets/Audio/UI";
    private const string Cofres = "Assets/SPRITES PARA NUEVOS NIVELES/CHESTS/";

    [MenuItem("Warrior/Actualizar/Ronda 6")]
    public static void Todo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CrearSonidosMenu();
        RecursosRPG r = ConfigurarRecursosRPG.Configurar();
        Sustituir(r);
        CofresYRecompensas();
        AmbienteMenu();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda6] Listo.");
    }

    // ------------------------------------------------------------------ Sonidos del menu

    // Dos sonidos hechos por codigo, cortos y limpios:
    //   ui_mover: un "tic" muy suave y corto (pasar por una opcion).
    //   ui_confirmar: dos notas tipo campanilla, algo mas presentes (elegir).
    public static void CrearSonidosMenu()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
        if (!AssetDatabase.IsValidFolder(CarpetaUI)) AssetDatabase.CreateFolder("Assets/Audio", "UI");
        const int f = 44100;

        // Tic: 45 ms, de 1500 a 1100 Hz, se apaga enseguida.
        float[] tic = new float[Mathf.RoundToInt(f * 0.045f)];
        double fase = 0;
        for (int i = 0; i < tic.Length; i++)
        {
            float t = i / (float)f;
            float hz = Mathf.Lerp(1500f, 1100f, t / 0.045f);
            fase += 2 * Mathf.PI * hz / f;
            float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 90f);
            tic[i] = (float)(System.Math.Sin(fase) * 0.8 + System.Math.Sin(fase * 2) * 0.15) * env * 0.5f;
        }
        EscribirWav(CarpetaUI + "/ui_mover.wav", tic, f);

        // Confirmar: dos notas (La5 y Mi6) con armonicos de campana, 320 ms.
        float[] conf = new float[Mathf.RoundToInt(f * 0.32f)];
        for (int i = 0; i < conf.Length; i++)
        {
            float t = i / (float)f;
            float s = Nota(t, 880f, 0f) + Nota(t, 1318.5f, 0.07f) * 0.9f;
            conf[i] = s * 0.42f;
        }
        EscribirWav(CarpetaUI + "/ui_confirmar.wav", conf, f);
        AssetDatabase.Refresh();
    }

    private static float Nota(float t, float hz, float inicio)
    {
        float u = t - inicio;
        if (u < 0f) return 0f;
        float env = Mathf.Min(1f, u / 0.003f) * Mathf.Exp(-u * 13f);
        float w = 2f * Mathf.PI * hz * u;
        return (Mathf.Sin(w) + 0.35f * Mathf.Sin(w * 2.01f) + 0.12f * Mathf.Sin(w * 3.98f)) * env * 0.6f;
    }

    private static void EscribirWav(string ruta, float[] muestras, int frecuencia)
    {
        using (var fs = new FileStream(ruta, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int datos = muestras.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + datos);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(frecuencia);
            w.Write(frecuencia * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(datos);
            foreach (float m in muestras) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(m, -1f, 1f) * 32767f));
        }
    }

    // ------------------------------------------------------------------ Recursos

    // Claves que cambian de contenido en esta ronda (Configurar solo anade las que
    // faltan): el sonido de pasar por las opciones, el sangrado en lugar del acido
    // y los tajos de color.
    private static void Sustituir(RecursosRPG r)
    {
        const string CE = ConfigurarRecursosRPG.Pack + "SONIDOS/Sonidos_Efectos/Sonidos_CombateEspada/";
        const string Oscuro = ConfigurarRecursosRPG.Pack + "SONIDOS/Sonidos_Efectos/Sonidos_Magia/Dark Spell Pack/96000 kHz/ogg/";
        Poner(r, "menu_mover", 0.2f, CarpetaUI + "/ui_mover.wav");
        Poner(r, "menu_confirmar", 0.4f, CarpetaUI + "/ui_confirmar.wav");
        Poner(r, "menu_abrir", 0.35f, CarpetaUI + "/ui_confirmar.wav");
        Poner(r, "imbuir_4", 0.7f, Oscuro + "dark_spell_cast_smash_head_blood_02.ogg");
        Poner(r, "tajo_4", 0.4f, Oscuro + "dark_spell_cast_energy_wave_01.ogg", Oscuro + "dark_spell_cast_energy_wave_02.ogg");

        // Copia los gore del pack a Assets/Audio/Combate si aun no estan.
        foreach (string n in new[] { "01", "11", "17" })
        {
            string destino = "Assets/Audio/Combate/GOREStab_SwordStabGore_HoveAud_SwordCombat_" + n + ".wav";
            if (!File.Exists(destino)) AssetDatabase.CopyAsset(CE + "Main Sounds/Sword Stabs/w_Gore/" + Path.GetFileName(destino), destino);
        }
        Poner(r, "golpe_4", 0.55f, "Assets/Audio/Combate/GOREStab_SwordStabGore_HoveAud_SwordCombat_01.wav",
              "Assets/Audio/Combate/GOREStab_SwordStabGore_HoveAud_SwordCombat_17.wav");

        // Icono del sangrado (la gota) en lugar de la calavera del acido.
        string gota = ConfigurarRecursosRPG.Pack + "ICONOS/Iconos_RPGPack/15-status-effects/icon_05.png";
        Sprite s = AssetDatabase.LoadAllAssetsAtPath(gota).OfType<Sprite>().FirstOrDefault();
        RecursosRPG.EntradaIcono e = r.iconos.FirstOrDefault(i => i != null && i.clave == "elemento_4");
        if (e != null && s != null) e.sprite = s;
        r.iconos.RemoveAll(i => i != null && i.clave == "estado_corroido");

        // Tajos: el sangrado usa ahora el rojo.
        r.tajosElemento = new AnimadorHoja.Clip[0];
        EditorUtility.SetDirty(r);
        ConfigurarRecursosRPG.Configurar();
    }

    private static void Poner(RecursosRPG r, string clave, float volumen, params string[] rutas)
    {
        AudioClip[] clips = rutas.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c != null).ToArray();
        if (clips.Length == 0) { Debug.LogWarning("[Ronda6] Sin audio para " + clave); return; }
        RecursosRPG.GrupoSonido g = r.sonidos.FirstOrDefault(x => x != null && x.clave == clave);
        if (g == null) { g = new RecursosRPG.GrupoSonido { clave = clave }; r.sonidos.Add(g); }
        g.clips = clips;
        g.volumen = volumen;
        EditorUtility.SetDirty(r);
    }

    // ------------------------------------------------------------------ Cofres y jefes

    // Donde va cada cofre de mejora y lo que da el jefe de cada nivel.
    private static readonly (string escena, string hoja, (string clave, Equipo.Objeto objeto)[] cofres, Equipo.Objeto[] jefe)[] Niveles =
    {
        (ConfigNivelEditor.EscenaNieve, "Chests_Snow.png",
            new[] { ("mejora_1", Equipo.Objeto.LagrimaSagrada) },
            new[] { Equipo.Objeto.PiedraForja }),
        (ConfigNivelEditor.EscenaCueva, "Chests.png",
            new[] { ("mejora_1", Equipo.Objeto.PiedraForja) },
            new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaSagrada }),
    };

    public static void CofresYRecompensas()
    {
        foreach (var n in Niveles)
        {
            Scene e = EditorSceneManager.OpenScene(n.escena);
            GameObject nivel = e.GetRootGameObjects().FirstOrDefault(g => g.GetComponentInChildren<Grid>(true) != null) ?? e.GetRootGameObjects()[0];
            Grid grid = nivel.GetComponentInChildren<Grid>(true);
            (Sprite[] reposo, Sprite[] apertura) = FotogramasCofre(Cofres + n.hoja);

            // Los que ya se colocaron se dejan donde esten (se pueden mover a mano).
            var existentes = e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CofreMejora>(true)).ToList();
            List<Vector2> sitios = SitiosEscondidos(e, grid, n.cofres.Length);
            for (int i = 0; i < n.cofres.Length; i++)
            {
                string clave = n.cofres[i].clave;
                CofreMejora c = existentes.FirstOrDefault(x => new SerializedObject(x).FindProperty("clave").stringValue == clave);
                if (c == null)
                {
                    if (i >= sitios.Count) { Debug.LogWarning($"[Ronda6] {e.name}: no encuentro sitio escondido para {clave}"); continue; }
                    c = NuevoCofre(nivel.transform, sitios[i], clave);
                }
                SerializedObject so = new SerializedObject(c);
                so.FindProperty("clave").stringValue = clave;
                so.FindProperty("objeto").enumValueIndex = (int)n.cofres[i].objeto;
                PonerSprites(so.FindProperty("reposo"), reposo);
                PonerSprites(so.FindProperty("apertura"), apertura);
                so.ApplyModifiedPropertiesWithoutUndo();
                // Colocado por una version anterior junto al Mimic, pegado a el: se aparta.
                foreach (EnemigoMimic mm in e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemigoMimic>(true)))
                    if (Mathf.Abs(mm.transform.position.x - c.transform.position.x) < 3.5f)
                        c.transform.position = PieDe(mm.transform.position + new Vector3(4.5f, 0.5f, 0f));
                SpriteRenderer v = c.GetComponentInChildren<SpriteRenderer>();
                if (v != null && reposo.Length > 0) v.sprite = reposo[0];
                if (v != null && SortingLayer.GetLayerValueFromID(v.sortingLayerID) < SortingLayer.GetLayerValueFromName("Middleground"))
                {
                    v.sortingLayerName = "Middleground";
                    v.sortingOrder = 5;
                }
                Debug.Log($"[Ronda6] {e.name}: cofre {clave} ({n.cofres[i].objeto}) en {c.transform.position}");
            }

            foreach (ArenaJefe a in e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArenaJefe>(true)))
            {
                SerializedObject so = new SerializedObject(a);
                SerializedProperty p = so.FindProperty("recompensa");
                p.arraySize = n.jefe.Length;
                for (int i = 0; i < n.jefe.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)n.jefe[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Ronda6] {e.name}: el jefe da {string.Join(", ", n.jefe)}");
            }
            EditorSceneManager.MarkSceneDirty(e);
            EditorSceneManager.SaveScene(e);
        }
    }

    private static void PonerSprites(SerializedProperty p, Sprite[] s)
    {
        p.arraySize = s.Length;
        for (int i = 0; i < s.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = s[i];
    }

    // La hoja tiene 5 columnas x 8 filas de 48x32; cada tipo de cofre ocupa dos
    // filas (reposo y apertura). El rojo con dorado (filas 5 y 6 desde arriba) es
    // el de los objetos de mejora.
    private static (Sprite[], Sprite[]) FotogramasCofre(string ruta)
    {
        Sprite[] todos = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToArray();
        if (todos.Length == 0) { Debug.LogWarning("[Ronda6] No hay sprites en " + ruta); return (new Sprite[0], new Sprite[0]); }
        int alto = todos[0].texture.height;
        Sprite[] Fila(int fila) => Enumerable.Range(0, 5).Select(col =>
            todos.Where(s =>
                    Mathf.FloorToInt((s.rect.x + s.rect.width * 0.5f) / 48f) == col &&
                    Mathf.FloorToInt((alto - (s.rect.y + s.rect.height * 0.5f)) / 32f) == fila)
                 .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault())
            .Where(s => s != null).ToArray();
        Sprite[] reposo = Fila(4);
        Sprite[] apertura = Fila(5);
        // La apertura: cerrado, tapa arriba y abierto (el resto repite el abierto).
        if (apertura.Length > 3) apertura = apertura.Take(3).ToArray();
        return (reposo, apertura);
    }

    private static CofreMejora NuevoCofre(Transform padre, Vector2 pie, string clave)
    {
        GameObject go = new GameObject("CofreMejora_" + clave);
        go.transform.SetParent(padre, true);
        go.transform.position = pie;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 0.8f);
        col.offset = new Vector2(0f, 0.4f);

        GameObject v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        // Como el cofre de almas, y siempre por debajo de la sombra de las zonas
        // ocultas (desde fuera no se ve).
        CofreAlmas modelo = Object.FindFirstObjectByType<CofreAlmas>();
        SpriteRenderer ref0 = modelo != null ? modelo.GetComponentInChildren<SpriteRenderer>() : null;
        if (ref0 != null) { sr.sortingLayerID = ref0.sortingLayerID; sr.sortingOrder = ref0.sortingOrder; sr.sharedMaterial = ref0.sharedMaterial; }
        else { sr.sortingLayerName = "Middleground"; sr.sortingOrder = 5; }
        TilemapRenderer sombra = Object.FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "ZonasOcultas");
        if (sombra != null && (SortingLayer.GetLayerValueFromID(sr.sortingLayerID) > SortingLayer.GetLayerValueFromID(sombra.sortingLayerID)
            || (sr.sortingLayerID == sombra.sortingLayerID && sr.sortingOrder >= sombra.sortingOrder)))
        {
            sr.sortingLayerID = sombra.sortingLayerID;
            sr.sortingOrder = sombra.sortingOrder - 2;
        }
        // La hoja va a 100 px por unidad: x3 para que mida lo que un cofre (~0.9).
        v.transform.localScale = new Vector3(3f, 3f, 1f);
        v.transform.localPosition = new Vector3(0f, 0.33f, 0f);

        CofreMejora c = go.AddComponent<CofreMejora>();
        SerializedObject so = new SerializedObject(c);
        so.FindProperty("visual").objectReferenceValue = sr;
        so.FindProperty("clave").stringValue = clave;
        so.ApplyModifiedPropertiesWithoutUndo();
        return c;
    }

    // Sitios escondidos donde poner cofres: el suelo dentro de las zonas ocultas
    // (tilemap "ZonasOcultas"), una por zona, empezando por las mas grandes. Si no
    // hay bastantes, al lado del cofre de almas y del Mimic de la cueva secreta.
    private static List<Vector2> SitiosEscondidos(Scene e, Grid grid, int cuantos)
    {
        var sitios = new List<Vector2>();
        var ocupados = e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CofreAlmas>(true)).Select(c => (Vector2)c.transform.position).ToList();
        if (grid != null)
        {
            Tilemap zonas = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(t => t.name == "ZonasOcultas");
            Tilemap[] terreno = grid.GetComponentsInChildren<CapaTerreno>(true).Select(c => c.GetComponent<Tilemap>())
                                    .Where(t => t != null && t.name != "ParedesFalsas").ToArray();
            bool Solido(Vector3Int c) => terreno.Any(t => t.HasTile(c));
            if (zonas != null)
            {
                zonas.CompressBounds();
                var celdas = new HashSet<Vector3Int>();
                foreach (Vector3Int c in zonas.cellBounds.allPositionsWithin) if (zonas.HasTile(c)) celdas.Add(c);
                foreach (List<Vector3Int> region in Regiones(celdas).OrderByDescending(r => r.Count))
                {
                    Vector2 centro = new Vector2((float)region.Average(c => c.x), (float)region.Average(c => c.y));
                    var suelos = region.Where(c => !Solido(c) && !Solido(c + Vector3Int.up) && Solido(c + Vector3Int.down))
                                       .Select(c => (Vector2)zonas.CellToWorld(c) + new Vector2(zonas.cellSize.x * 0.5f, 0f))
                                       .Where(p => ocupados.All(o => Vector2.Distance(o, p) > 1.8f))
                                       .OrderBy(p => Vector2.Distance(p, (Vector2)zonas.CellToWorld(Vector3Int.RoundToInt(centro))))
                                       .ToList();
                    if (suelos.Count == 0) continue;
                    sitios.Add(suelos[0]);
                    ocupados.Add(suelos[0]);
                    if (sitios.Count >= cuantos) return sitios;
                }
            }
        }
        // Respaldo: junto a lo que ya esta escondido.
        foreach (CofreAlmas c in e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CofreAlmas>(true)))
            if (sitios.Count < cuantos) sitios.Add((Vector2)c.transform.position + new Vector2(-1.6f, 0f));
        foreach (EnemigoMimic m in e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemigoMimic>(true)))
            if (sitios.Count < cuantos) sitios.Add(PieDe(m.transform.position + new Vector3(4.5f, 0.5f, 0f)));
        return sitios;
    }

    private static Vector2 PieDe(Vector2 p)
    {
        RaycastHit2D h = Physics2D.Raycast(p, Vector2.down, 10f, LayerMask.GetMask("Ground"));
        return h ? h.point : p;
    }

    private static IEnumerable<List<Vector3Int>> Regiones(HashSet<Vector3Int> celdas)
    {
        var vistas = new HashSet<Vector3Int>();
        foreach (Vector3Int c in celdas)
        {
            if (!vistas.Add(c)) continue;
            var region = new List<Vector3Int>();
            var cola = new Queue<Vector3Int>();
            cola.Enqueue(c);
            while (cola.Count > 0)
            {
                Vector3Int a = cola.Dequeue();
                region.Add(a);
                foreach (Vector3Int d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    if (celdas.Contains(a + d) && vistas.Add(a + d)) cola.Enqueue(a + d);
            }
            yield return region;
        }
    }

    // ------------------------------------------------------------------ Menu principal

    // El ambiente del menu: el fuego que se anadio en "Sonido menu".
    public static void AmbienteMenu()
    {
        AudioClip fuego = AssetDatabase.LoadAssetAtPath<AudioClip>(ConfigurarRecursosRPG.Pack + "SONIDOS/Sonido menu/fire.mp3");
        if (fuego == null) { Debug.LogWarning("[Ronda6] No encuentro Sonido menu/fire.mp3"); return; }
        Scene e = EditorSceneManager.OpenScene(CrearMenuPrincipal.Escena);
        foreach (MenuPrincipal m in e.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MenuPrincipal>(true)))
        {
            SerializedObject so = new SerializedObject(m);
            so.FindProperty("ambiente").objectReferenceValue = fuego;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(e);
        EditorSceneManager.SaveScene(e);
    }
}
