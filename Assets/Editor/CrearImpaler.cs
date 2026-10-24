using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// El Impaler (jefe nuevo, SOLO en la sala de pruebas). Menu Warrior > Impaler.
//
// Sus sprites son copias a 1x de los originales (Assets/SPRITES PARA NUEVOS
// NIVELES/BOSSES/Boss_Impaler, que no se tocan) en Assets/Sprites/Impaler: un
// PNG por cuadro distinto, recortados con la misma caja dentro de cada
// animacion. Sus anclajes (los pies) y cuanto se desplaza el dibujo en cada
// cuadro estan en Assets/Sprites/Impaler/cuadros.json.
//
// "Crear o actualizar" hace:
//   - la ficha Assets/Data/Jefes/Ajustes Impaler (solo si no existe: lo editado no se pisa),
//   - el prefab Assets/Prefabs/Enemies/Nuevos/JefeImpaler (si existe, solo rehace sus clips),
//   - su entrada en el catalogo de la sala (solo si no esta).
// No lo conecta con niveles, Desafios, Bitacora, Bestiario, logros ni Resources.
public static class CrearImpaler
{
    public const string CarpetaSprites = "Assets/Sprites/Impaler";
    public const string RutaFicha = "Assets/Data/Jefes/Ajustes Impaler.asset";
    public const string RutaPrefab = "Assets/Prefabs/Enemies/Nuevos/JefeImpaler.prefab";
    public const string NombreCatalogo = "Impaler";
    private const string MaterialFlash = "Assets/Materiales/Enemy_Flash.mat";
    private const string ShaderBrillo = "Assets/Shaders/BladeGlow.shader";
    // Los clips de los hechizos salen del Wraith (se copian; el Wraith no se toca).
    private const string PrefabWraith = "Assets/Prefabs/Enemies/Cueva/CrimsonWraith.prefab";
    private static readonly string[] ClipsHechizos =
    {
        "fxOnda", "fxPilar", "fxGlifo", "fxOrbe", "fxSangre", "fxMeteoro", "fxExplosion", "fxMedialuna",
        "fxVorticeInicio", "fxVorticeBucle", "fxVorticeFin",
    };

    // Copia al Impaler los clips de efectos del Wraith que le falten.
    private static int CopiarClipsHechizos(JefeImpaler jefe)
    {
        GameObject pw = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabWraith);
        JefeWraith w = pw != null ? pw.GetComponent<JefeWraith>() : null;
        if (w == null) { Debug.LogWarning("[Impaler] No se encuentra el Wraith para copiar los clips de los hechizos."); return 0; }
        int n = 0;
        foreach (string campo in ClipsHechizos)
        {
            var fw = typeof(JefeWraith).GetField(campo);
            var fi = typeof(JefeImpaler).GetField(campo);
            if (fw == null || fi == null) continue;
            AnimadorHoja.Clip actual = fi.GetValue(jefe) as AnimadorHoja.Clip;
            if (actual != null && actual.fotogramas != null && actual.fotogramas.Length > 0) continue;
            AnimadorHoja.Clip origen = fw.GetValue(w) as AnimadorHoja.Clip;
            if (origen == null) continue;
            var copia = new AnimadorHoja.Clip();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(origen), copia);
            fi.SetValue(jefe, copia);
            n++;
        }
        return n;
    }

    [MenuItem("Warrior/Impaler/Crear o actualizar", false, 41)]
    public static void Crear()
    {
        AjustarSprites();
        AjustesImpaler aj = Ficha();
        GameObject prefab = Prefab(aj);
        Catalogo(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Impaler] Listo: {RutaFicha}, {RutaPrefab} y su entrada en la sala de pruebas.");
    }

    // ------------------------------------------------------------------ Sprites

    // Pixel art: filtro Point, sin compresion ni mipmaps y a su tamano real.
    [MenuItem("Warrior/Impaler/Ajustar sprites", false, 40)]
    public static void AjustarSprites()
    {
        int cambiados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaSprites }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti == null || Listo(ti)) continue;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.isReadable = false;
            ti.SaveAndReimport();
            cambiados++;
        }
        Debug.Log($"[Impaler] Sprites ajustados: {cambiados} cambiados en {CarpetaSprites}.");
    }

    private static bool Listo(TextureImporter ti) =>
        ti.textureType == TextureImporterType.Default && ti.npotScale == TextureImporterNPOTScale.None && !ti.mipmapEnabled &&
        ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.maxTextureSize == 2048;

    // Comprobacion: todas las copias con los ajustes de arriba y a 1x.
    public static bool Comprobar(out string informe)
    {
        int total = 0, mal = 0;
        long bytes = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaSprites }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            total++;
            if (ti == null || t == null || !Listo(ti) || t.width > 640 || t.height > 265) { mal++; Debug.LogWarning("[Impaler] Mal importado: " + ruta); continue; }
            bytes += (long)t.width * t.height * 4;
        }
        informe = $"{total} texturas, {mal} mal importadas, {bytes / (1024f * 1024f):0.0} MB en memoria";
        return total > 0 && mal == 0;
    }

    // ------------------------------------------------------------------ Cuadros (cuadros.json)

    [System.Serializable]
    public class Animacion
    {
        public string nombre;
        public int[] recorte;
        public int[] tamano;
        public string[] cuadros;
        public float[] pivotesX;
        public float[] pivotesY;
        public int[] dx;
    }

    [System.Serializable]
    public class Cuadros
    {
        public int msPorCuadro;
        public float pixelesPorUnidad;
        public List<Animacion> animaciones;
    }

    public static Cuadros LeerCuadros()
    {
        TextAsset t = AssetDatabase.LoadAssetAtPath<TextAsset>(CarpetaSprites + "/cuadros.json");
        return t != null ? JsonUtility.FromJson<Cuadros>(t.text) : null;
    }

    // Nombre del clip en el animador para cada animacion de los sprites.
    private static readonly Dictionary<string, string> NombreClip = new Dictionary<string, string>
    {
        { "idle", "quieto" }, { "walk", "andar" }, { "death", "muerte" },
    };

    public static List<AnimadorHoja.Clip> Clips(Cuadros c)
    {
        var lista = new List<AnimadorHoja.Clip>();
        foreach (Animacion a in c.animaciones)
        {
            string nombre = NombreClip.TryGetValue(a.nombre, out string n) ? n : a.nombre;
            lista.Add(Clip(c, a, nombre, false));
            // Renacer: la muerte al reves (la flor se cierra y vuelve el cuerpo).
            if (a.nombre == "death") lista.Add(Clip(c, a, "renacer", true));
        }
        return lista;
    }

    private static AnimadorHoja.Clip Clip(Cuadros c, Animacion a, string nombre, bool alReves)
    {
        var tex = new Texture2D[a.cuadros.Length];
        var piv = new Vector2[a.cuadros.Length];
        for (int i = 0; i < a.cuadros.Length; i++)
        {
            tex[i] = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaSprites}/{a.nombre}/{a.cuadros[i]}");
            if (tex[i] == null) Debug.LogError($"[Impaler] Falta {CarpetaSprites}/{a.nombre}/{a.cuadros[i]}");
            piv[i] = new Vector2(a.pivotesX[i], a.pivotesY[i]);
        }
        if (alReves) { System.Array.Reverse(tex); System.Array.Reverse(piv); }
        return new AnimadorHoja.Clip
        {
            nombre = nombre,
            fps = 1000f / Mathf.Max(1, c.msPorCuadro),
            bucle = a.nombre == "idle" || a.nombre == "walk",
            pixelesPorUnidad = c.pixelesPorUnidad,
            fotogramas = tex,
            pivotes = piv,
            pivote = piv.Length > 0 ? piv[0] : new Vector2(0.5f, 0f),
            cantidad = tex.Length,
        };
    }

    // ------------------------------------------------------------------ Ficha

    // Ataques de partida. A una ficha que ya existe solo se le anaden los que le
    // falten (por su id): lo ya editado no se toca.
    public static AjustesImpaler.Ataque[] AtaquesDePartida() => new[]
    {
        LanzazoLargo(), TajoConPausa(), ComboRelampago(), EmbestidaMediaLuna(), SaltoEstela(), CruceSangre(), Contraataque(),
    }.Select(ConFase2).ToArray();

    private static AjustesImpaler.Hechizo H(AjustesImpaler.TipoHechizo tipo, int golpe, AjustesImpaler.Momento momento, float retraso, bool soloUltimo,
                                            int dano, float distancia) => new AjustesImpaler.Hechizo
    {
        tipo = tipo, golpe = golpe, momento = momento, retraso = retraso, soloSiEsElUltimo = soloUltimo, dano = dano, distancia = distancia,
    };

    // Fase 2: Quemadura en los golpes pesados y los hechizos de cada ataque. Cada
    // hechizo sale en un hueco libre: nunca encima de otro golpe (0,55 s o mas) ni
    // sobre la ventana de castigo. Los que salen al final, solo si es lo ultimo que hace.
    private static AjustesImpaler.Ataque ConFase2(AjustesImpaler.Ataque a)
    {
        foreach (AjustesImpaler.Golpe g in a.golpes) if (g.peso == AjustesImpaler.Peso.Pesado) g.quemadura = 30f;
        // Adaptacion: los saltos no alargan su retraso (moveria el aterrizaje) ni el
        // contraataque (ya tiene su propio retraso tras tu golpe).
        a.admiteRetraso = !a.salto && !a.contraataque;
        var T = AjustesImpaler.TipoHechizo.Onda;
        switch (a.id)
        {
            case "lanzazo_largo":
            {
                // Vortice antes de la estocada larga: sin dano, solo te atrae hacia la estocada
                // (con dano 0, VorticeSangre no golpea; asi no cae encima de la estocada).
                AjustesImpaler.Hechizo v = H(AjustesImpaler.TipoHechizo.Vortice, 1, AjustesImpaler.Momento.AlAvisar, 0f, false, 0, 3.2f);
                v.duracion = 0.8f; v.radio = 5f; v.fuerza = 3f;
                AjustesImpaler.Hechizo o = H(T, 1, AjustesImpaler.Momento.TrasGolpe, 0.55f, true, 18, 1.2f);
                o.velocidad = 9f; o.sangrado = 20f;
                a.hechizosFase2 = new List<AjustesImpaler.Hechizo> { v, o };
                break;
            }
            case "tajo_pausa":
            {
                // La media luna sale con el tajo vertical: un solo golpe que llega mas lejos.
                AjustesImpaler.Hechizo m = H(AjustesImpaler.TipoHechizo.MediaLuna, 0, AjustesImpaler.Momento.AlGolpear, 0f, false, 18, 1f);
                m.velocidad = 10f; m.sangrado = 25f;
                a.hechizosFase2 = new List<AjustesImpaler.Hechizo> { m };
                break;
            }
            case "embestida_medialuna":
            {
                // Meteoros alrededor del aterrizaje: el glifo sale antes de aterrizar y caen 0,6 s despues.
                AjustesImpaler.Hechizo m = H(AjustesImpaler.TipoHechizo.Meteoros, 1, AjustesImpaler.Momento.AlAvisar, 0f, true, 20, 0f);
                m.aviso = 1.2f; m.cantidad = 2; m.separacion = 5f;
                a.hechizosFase2 = new List<AjustesImpaler.Hechizo> { m };
                break;
            }
            case "salto_estela":
            {
                // Pilares en la estela (detras de el): brotan despues de la ola, lejos de la ventana de castigo.
                AjustesImpaler.Hechizo p = H(AjustesImpaler.TipoHechizo.Pilares, 0, AjustesImpaler.Momento.AlGolpear, 0f, false, 20, -1.6f);
                p.aviso = 1.25f; p.cantidad = 3; p.separacion = 1.6f; p.sangrado = 30f;
                a.hechizosFase2 = new List<AjustesImpaler.Hechizo> { p };
                break;
            }
            case "cruce_sangre":
            {
                // El eco lanza una media luna hacia el jefe tras el ultimo tajo.
                AjustesImpaler.Hechizo m = H(AjustesImpaler.TipoHechizo.MediaLuna, 2, AjustesImpaler.Momento.TrasGolpe, 0.3f, true, 18, 0.5f);
                m.velocidad = 7f; m.sangrado = 25f; m.desdeElEco = true;
                a.hechizosFase2 = new List<AjustesImpaler.Hechizo> { m };
                break;
            }
        }
        return a;
    }

    // counter (cuadros sacados del .aseprite y el tajo blanco de counter.gif):
    // guardia con la lanza sobre la cabeza (0, sostenido mientras dura) y, si le
    // pegas, el tajo blanco hacia arriba (1-3) tras su aviso blanco.
    public static AjustesImpaler.Ataque Contraataque() => new AjustesImpaler.Ataque
    {
        id = "contraataque", nombre = "Contraataque blanco", clip = "counter", alcance = 2.5f, frecuencia = 0.5f, recuperacion = 0.4f,
        contraataque = true,
        golpes = new List<AjustesImpaler.Golpe>
        {
            G("Tajo blanco", AjustesImpaler.Peso.Contraataque, 0, 1, 3, 0, 0.45f, 34, new Vector2(1f, 1.7f), new Vector2(2.8f, 3.2f)),
        },
    };

    private static AjustesImpaler Ficha()
    {
        AjustesImpaler aj = AssetDatabase.LoadAssetAtPath<AjustesImpaler>(RutaFicha);
        bool nueva = aj == null;
        if (nueva) aj = ScriptableObject.CreateInstance<AjustesImpaler>();
        var anadidos = new List<string>();
        foreach (AjustesImpaler.Ataque a in AtaquesDePartida())
            if (aj.Buscar(a.id) == null) { aj.ataques.Add(a); anadidos.Add(a.id); }
        foreach (AjustesImpaler.Combo c in CombosDePartida())
        {
            AjustesImpaler.Combo ya = aj.BuscarCombo(c.id);
            if (ya == null) { aj.combos.Add(c); anadidos.Add(c.id); }
            else if ((ya.extraDificil == null || ya.extraDificil.Count == 0) && c.extraDificil.Count > 0)
            {
                ya.extraDificil = c.extraDificil;
                anadidos.Add(c.id + " (extra en Dificil)");
            }
        }
        // Fase 2 de los ataques que aun no la tienen (sin hechizos y sin Quemadura): se rellena; lo editado no se toca.
        foreach (AjustesImpaler.Ataque d in AtaquesDePartida())
        {
            AjustesImpaler.Ataque a = aj.Buscar(d.id);
            if (a == null || a == d) continue;
            if ((a.hechizosFase2 == null || a.hechizosFase2.Count == 0) && d.hechizosFase2.Count > 0)
            {
                a.hechizosFase2 = d.hechizosFase2;
                anadidos.Add(a.id + " (hechizos)");
            }
            if (a.golpes.All(g => g.quemadura <= 0f) && d.golpes.Any(g => g.quemadura > 0f))
            {
                for (int i = 0; i < a.golpes.Count && i < d.golpes.Count; i++) a.golpes[i].quemadura = d.golpes[i].quemadura;
                anadidos.Add(a.id + " (quemadura)");
            }
        }
        if (aj.shaderBrillo == null)
        {
            aj.shaderBrillo = AssetDatabase.LoadAssetAtPath<Shader>(ShaderBrillo);
            if (aj.shaderBrillo != null) anadidos.Add("shader de la lanza");
        }
        if (nueva)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaFicha));
            AssetDatabase.CreateAsset(aj, RutaFicha);
        }
        else if (anadidos.Count > 0) EditorUtility.SetDirty(aj);
        if (anadidos.Count > 0) Debug.Log("[Impaler] Ataques anadidos a la ficha: " + string.Join(", ", anadidos));
        return aj;
    }

    // Combos de partida: la Apertura empieza con el Lanzazo corto (solo la
    // primera estocada del Lanzazo largo). Pinza y Tormenta, solo en la fase 2.
    public static AjustesImpaler.Combo[] CombosDePartida() => new[]
    {
        Extra(Combo("apertura", "Apertura", false, 1f, E("lanzazo_largo", 0), E("tajo_pausa")), E("combo_relampago", 0)),
        Extra(Combo("cadena", "Cadena", false, 1f, E("tajo_pausa"), E("combo_relampago")), E("lanzazo_largo", 0)),
        Combo("persecucion", "Persecución", false, 0.8f, E("embestida_medialuna"), E("salto_estela")),
        Extra(Combo("pinza", "Pinza", true, 1f, E("cruce_sangre"), E("lanzazo_largo")), E("tajo_pausa", 0)),
        Combo("tormenta", "Tormenta", true, 0.8f, E("tajo_pausa"), E("combo_relampago"), E("lanzazo_largo")),
    };

    // Dificil: golpes extra al final del combo (en vez de solo ir mas rapido).
    private static AjustesImpaler.Combo Extra(AjustesImpaler.Combo c, params AjustesImpaler.Eslabon[] e)
    {
        c.extraDificil = new List<AjustesImpaler.Eslabon>(e);
        return c;
    }

    private static AjustesImpaler.Eslabon E(string ataque, int hasta = -1) => new AjustesImpaler.Eslabon { ataque = ataque, hastaGolpe = hasta };

    private static AjustesImpaler.Combo Combo(string id, string nombre, bool fase2, float frecuencia, params AjustesImpaler.Eslabon[] e) =>
        new AjustesImpaler.Combo { id = id, nombre = nombre, soloFase2 = fase2, frecuencia = frecuencia, eslabones = new List<AjustesImpaler.Eslabon>(e) };

    private static AjustesImpaler.Golpe G(string nombre, AjustesImpaler.Peso peso, int senal, int desde, int hasta, int pausa, float aviso,
                                          int dano, Vector2 centro, Vector2 tamano) => new AjustesImpaler.Golpe
    {
        nombre = nombre, peso = peso, senal = senal, activoDesde = desde, activoHasta = hasta, cuadroPausa = pausa,
        aviso = aviso, dano = dano, cajaCentro = centro, cajaTamano = tamano,
    };

    // attack3: tajo vertical (5-7), pausa con la lanza en alto sin fuego (cuadro
    // 11, de 0,2 a 0,5 s) y giro de 360 grados (12-17). En la fase 1, parar el
    // tajo corta el giro.
    public static AjustesImpaler.Ataque TajoConPausa()
    {
        AjustesImpaler.Golpe tajo = G("Tajo vertical", AjustesImpaler.Peso.Ligero, 0, 5, 7, 4, 0.5f, 22, new Vector2(1.6f, 2.2f), new Vector2(2.6f, 3.8f));
        tajo.parryCancelaFase1 = true;
        AjustesImpaler.Golpe giro = G("Giro", AjustesImpaler.Peso.Pesado, 8, 12, 17, 11, 0.6f, 32, new Vector2(0.6f, 1.4f), new Vector2(4.2f, 2.6f));
        giro.pausaVariable = new Vector2(0.2f, 0.5f);
        return new AjustesImpaler.Ataque
        {
            id = "tajo_pausa", nombre = "Tajo con pausa", clip = "attack3", alcance = 2.6f, frecuencia = 1f, recuperacion = 0.4f,
            golpes = new List<AjustesImpaler.Golpe> { tajo, giro },
        };
    }

    // attack2: version rapida del anterior (tajo 1-2, giro 4-6). Sus avisos no
    // bajan del minimo: se sostienen la pose de salida (0) y el cuadro repetido 3.
    public static AjustesImpaler.Ataque ComboRelampago() => new AjustesImpaler.Ataque
    {
        id = "combo_relampago", nombre = "Combo relámpago", clip = "attack2", alcance = 2.6f, frecuencia = 0.4f, recuperacion = 0.4f,
        golpes = new List<AjustesImpaler.Golpe>
        {
            G("Tajo rápido", AjustesImpaler.Peso.Ligero, 0, 1, 2, 0, 0.35f, 20, new Vector2(1.6f, 2.2f), new Vector2(2.6f, 3.8f)),
            G("Giro rápido", AjustesImpaler.Peso.Ligero, 3, 4, 6, 3, 0.6f, 24, new Vector2(0.6f, 1.4f), new Vector2(4.2f, 2.6f)),
        },
    };

    // attack1: salta con la media luna y la estocada de fuego en el aire (2-11)
    // mientras avanza hacia ti (4-17), y aterriza con un tajo diagonal (18-21).
    public static AjustesImpaler.Ataque EmbestidaMediaLuna() => new AjustesImpaler.Ataque
    {
        id = "embestida_medialuna", nombre = "Embestida y media luna", clip = "attack1", alcance = 7f, frecuencia = 0.8f, recuperacion = 0.4f,
        moverDesde = 4, moverHasta = 17, pararAntes = 1.2f, distanciaMax = 7f, salto = true,
        golpes = new List<AjustesImpaler.Golpe>
        {
            G("Media luna y estocada", AjustesImpaler.Peso.Ligero, 0, 2, 11, 0, 0.4f, 22, new Vector2(2.2f, 1.8f), new Vector2(3.6f, 2.8f)),
            G("Aterrizaje", AjustesImpaler.Peso.Pesado, 12, 18, 21, -1, 0.6f, 34, new Vector2(1.4f, 0.9f), new Vector2(3.4f, 1.8f)),
        },
    };

    // attack6: salta hacia ti (2-6) con el tajo desde arriba al caer (6), la ola
    // baja se enrosca detras de el (13) y se queda de rodillas con la lanza
    // clavada (14-19): la ventana de castigo mas larga.
    public static AjustesImpaler.Ataque SaltoEstela() => new AjustesImpaler.Ataque
    {
        id = "salto_estela", nombre = "Salto y estela", clip = "attack6", alcance = 8f, frecuencia = 0.7f, recuperacion = 0.2f,
        moverDesde = 2, moverHasta = 6, pararAntes = 0.8f, distanciaMax = 8f, salto = true,
        cuadroCastigo = 14, castigoMinimo = 1.4f,
        golpes = new List<AjustesImpaler.Golpe>
        {
            G("Tajo desde arriba", AjustesImpaler.Peso.Pesado, 0, 6, 6, 1, 0.6f, 34, new Vector2(0.8f, 1.2f), new Vector2(3f, 2.4f)),
            G("Ola baja", AjustesImpaler.Peso.Ligero, 7, 13, 13, -1, 0.6f, 24, new Vector2(-3.2f, 0.6f), new Vector2(5f, 1.2f)),
        },
    };

    // attack5: embestida (avanza en 4-11, golpea en 8-11) hasta quedarse delante de ti, aparece el eco rojo
    // al otro lado (16) y los dos tajan en diagonal: primero el eco (20, golpea
    // desde su lado) y, tras sostener el cuadro repetido 21, el Impaler (22-23).
    public static AjustesImpaler.Ataque CruceSangre()
    {
        AjustesImpaler.Golpe eco = G("Tajo del eco", AjustesImpaler.Peso.Ligero, 16, 20, 20, -1, 0.4f, 22, new Vector2(3.2f, 1.6f), new Vector2(4.4f, 3f));
        eco.origenX = 4.6f;
        return new AjustesImpaler.Ataque
        {
            id = "cruce_sangre", nombre = "Cruce de sangre", clip = "attack5", alcance = 8f, frecuencia = 0.8f, recuperacion = 0.3f,
            moverDesde = 4, moverHasta = 11, pararAntes = 2.6f, distanciaMax = 6f,
            golpes = new List<AjustesImpaler.Golpe>
            {
                G("Embestida", AjustesImpaler.Peso.Ligero, 0, 8, 11, 0, 0.8f, 22, new Vector2(1.4f, 1.1f), new Vector2(2.6f, 1.2f)),
                eco,
                G("Tajo del Impaler", AjustesImpaler.Peso.Pesado, 21, 22, 23, 21, 0.6f, 32, new Vector2(1.6f, 1.6f), new Vector2(2.6f, 3f)),
            },
        };
    }

    // attack4: estocada corta con fuego (cuadros 4-10, dos picotazos que tocan una
    // sola vez), pausa con el remolino (11-16) y estocada larga (17-21).
    public static AjustesImpaler.Ataque LanzazoLargo() => new AjustesImpaler.Ataque
    {
        id = "lanzazo_largo",
        nombre = "Lanzazo largo",
        clip = "attack4",
        alcance = 3.5f,
        frecuencia = 1f,
        recuperacion = 0.3f,
        golpes = new List<AjustesImpaler.Golpe>
        {
            new AjustesImpaler.Golpe
            {
                nombre = "Estocada corta", peso = AjustesImpaler.Peso.Ligero,
                senal = 0, activoDesde = 4, activoHasta = 10, cuadroPausa = 3, aviso = 0.4f, dano = 22,
                cajaCentro = new Vector2(1.9f, 1.1f), cajaTamano = new Vector2(3f, 0.9f),
            },
            new AjustesImpaler.Golpe
            {
                nombre = "Estocada larga", peso = AjustesImpaler.Peso.Pesado,
                senal = 11, activoDesde = 17, activoHasta = 21, cuadroPausa = -1, aviso = 0.6f, dano = 32,
                cajaCentro = new Vector2(2.6f, 1.1f), cajaTamano = new Vector2(4.4f, 1.1f),
            },
        },
    };

    // ------------------------------------------------------------------ Prefab

    private static GameObject Prefab(AjustesImpaler aj)
    {
        Cuadros c = LeerCuadros();
        if (c == null) { Debug.LogError("[Impaler] Falta " + CarpetaSprites + "/cuadros.json"); return null; }
        List<AnimadorHoja.Clip> clips = Clips(c);

        GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (existente != null)
        {
            // Ya existe: solo se rehacen sus clips (lo demas puede estar retocado).
            GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
            AnimadorHoja an = raiz.GetComponentInChildren<AnimadorHoja>();
            if (an != null) an.clips = clips;
            JefeImpaler je = raiz.GetComponent<JefeImpaler>();
            if (je != null) CopiarClipsHechizos(je);
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            PrefabUtility.UnloadPrefabContents(raiz);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        }

        // Como los jefes de siempre: capa Enemies, etiqueta Enemy, cuerpo con
        // gravedad y rotacion fija, la imagen en el hijo "Visual".
        GameObject go = new GameObject("JefeImpaler");
        go.layer = LayerMask.NameToLayer("Enemies");
        go.tag = "Enemy";
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.2f, 2.6f);
        col.offset = new Vector2(0f, 1.3f);
        EnemyHealth salud = go.AddComponent<EnemyHealth>();
        go.AddComponent<HitFlash>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        Material flash = AssetDatabase.LoadAssetAtPath<Material>(MaterialFlash);
        if (flash != null) sr.sharedMaterial = flash;
        AnimadorHoja anim = visual.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = clips;
        anim.clipInicial = "quieto";

        JefeImpaler jefe = go.AddComponent<JefeImpaler>();
        CopiarClipsHechizos(jefe);
        SerializedObject sj = new SerializedObject(jefe);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.FindProperty("visual").objectReferenceValue = visual.transform;
        sj.FindProperty("anim").objectReferenceValue = anim;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(salud);
        sh.FindProperty("showHealthBar").boolValue = false;
        sh.FindProperty("maxHealth").intValue = aj.vidaFase1;
        // Muerto, el cuerpo (la flor) se queda en la arena hasta que se limpie.
        sh.FindProperty("deathAnimDuration").floatValue = 3600f;
        sh.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(RutaPrefab));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, RutaPrefab);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ Sala de pruebas

    private static void Catalogo(GameObject prefab)
    {
        if (prefab == null) return;
        CrearSalaPruebas.Fichas();
        CatalogoPruebas cat = AssetDatabase.LoadAssetAtPath<CatalogoPruebas>(SalaPruebas.CarpetaDatos + "Catalogo Pruebas.asset");
        if (cat == null || cat.entradas.Any(e => e != null && e.nombre == NombreCatalogo)) return;
        cat.entradas.Add(new CatalogoPruebas.Entrada
        {
            nombre = NombreCatalogo,
            prefab = prefab,
            arena = CatalogoPruebas.TipoArena.Generica,
            titulo = "Jefe de prueba",
            marcaFase = 0f,
            barraPorFase = true,
            nombreFase2 = NombreCatalogo + " — Renacido",
            textoFase2 = "",
            vidaFase2 = 0f,
        });
        EditorUtility.SetDirty(cat);
    }
}
