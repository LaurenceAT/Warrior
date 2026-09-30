using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Duodecima ronda: el Crimson Wraith mas fluido y con poderes nuevos.
//   - Prefab: poses que no se usaban (embestida con desenfoque, preparacion y
//     reves de la forma grande, reves de la fase 1) y el orbe morado (26-29)
//     para las Semillas del vacio.
//   - Resistencias nuevas de la fase 2 (fuego x1.5, oscuridad x0.3).
//   - Ficha del desafio: debilidades, resistencias y ataques al dia.
// Warrior > Actualizar > Ronda 12 (o ActualizarProyecto.Ronda12).
public static class Ronda12
{
    private const string Carpeta = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Boss_CrimsonWraith/";
    private const string RutaPrefab = "Assets/Prefabs/Enemies/Cueva/CrimsonWraith.prefab";

    // Pie de cada pose (fila desde arriba en su lienzo de 1980x1080), como en CrearNivelCueva.
    private static readonly Dictionary<int, int> Pie = new Dictionary<int, int>
    {
        {1, 767}, {2, 766}, {3, 766}, {4, 766}, {5, 766}, {6, 710}, {7, 710}, {8, 696}, {9, 696}, {10, 775},
        {11, 765}, {12, 706}, {13, 765}, {14, 706}, {15, 769}, {16, 767}, {17, 726}, {18, 697}, {19, 695},
        {20, 711}, {21, 692}, {22, 680}, {23, 696}, {24, 696}, {25, 720},
    };
    private static readonly RectInt Recorte = new RectInt(480, 220, 1200, 860);
    private const float Eje = 1100f;

    [MenuItem("Warrior/Actualizar/Ronda 12 (Crimson Wraith)")]
    public static void Todo()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
        AnimadorHoja anim = raiz.GetComponentInChildren<AnimadorHoja>();
        JefeWraith jefe = raiz.GetComponent<JefeWraith>();
        if (anim == null || jefe == null) { Debug.LogError("[Ronda12] El prefab no tiene AnimadorHoja o JefeWraith"); PrefabUtility.UnloadPrefabContents(raiz); return; }

        Poner(anim, Clip("embestida", new[] { 8, 23, 24 }, 16f, true));
        Poner(anim, Clip("grande_preparar", new[] { 14 }, 1f, true));
        Poner(anim, Clip("grande_reves", new[] { 13 }, 1f, true));
        Poner(anim, Clip("zarpazo_reves", new[] { 4, 5 }, 6f, false));
        Poner(anim, Clip("reves", new[] { 18, 19 }, 9f, false));

        jefe.fxSemilla = Semilla();

        SerializedObject so = new SerializedObject(jefe);
        so.FindProperty("multFuego").floatValue = 1.5f;
        so.FindProperty("multOscuro").floatValue = 0.3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        PrefabUtility.UnloadPrefabContents(raiz);
        Ficha();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda12] Listo.");
    }

    private static void Poner(AnimadorHoja anim, AnimadorHoja.Clip c)
    {
        int i = anim.clips.FindIndex(x => x != null && x.nombre == c.nombre);
        if (i >= 0) anim.clips[i] = c; else anim.clips.Add(c);
        EditorUtility.SetDirty(anim);
    }

    private static AnimadorHoja.Clip Clip(string nombre, int[] poses, float fps, bool bucle)
    {
        var texturas = new Texture2D[poses.Length];
        var pivotes = new Vector2[poses.Length];
        for (int i = 0; i < poses.Length; i++)
        {
            texturas[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Carpeta + poses[i] + ".png");
            float pie = 1080f - Pie[poses[i]];
            pivotes[i] = new Vector2((Eje - Recorte.x) / Recorte.width, (pie - Recorte.y) / Recorte.height);
        }
        return new AnimadorHoja.Clip
        {
            nombre = nombre, fotogramas = texturas, pivotes = pivotes, recorte = Recorte,
            cantidad = poses.Length, fps = fps, bucle = bucle, pixelesPorUnidad = 110f,
        };
    }

    // El orbe morado (26-29): recorte comun alrededor de lo pintado en las cuatro.
    private static AnimadorHoja.Clip Semilla()
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        var texturas = new Texture2D[4];
        for (int k = 0; k < 4; k++)
        {
            string ruta = Carpeta + (26 + k) + ".png";
            texturas[k] = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(ruta));
            Color32[] px = t.GetPixels32();
            for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
            {
                if (px[y * t.width + x].a < 10) continue;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        }
        const int m = 4;
        RectInt r = new RectInt(x0 - m, y0 - m, x1 - x0 + 1 + m * 2, y1 - y0 + 1 + m * 2);
        return new AnimadorHoja.Clip
        {
            nombre = "semilla", fotogramas = texturas, recorte = r, cantidad = 4, fps = 10f, bucle = true,
            pivote = new Vector2(0.5f, 0.5f), pixelesPorUnidad = 110f,
        };
    }

    private static void Ficha()
    {
        FichaJefe f = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_CrimsonWraith.asset");
        if (f == null) return;
        f.afinidades = new List<FichaJefe.Afinidad>
        {
            new FichaJefe.Afinidad { elemento = Elemento.Fuego, multiplicador = 1.3f, cuando = "Fase 1" },
            new FichaJefe.Afinidad { elemento = Elemento.Hielo, multiplicador = 1.2f, cuando = "Fase 1" },
            new FichaJefe.Afinidad { elemento = Elemento.Sangrado, multiplicador = 0.6f, cuando = "Fase 1" },
            new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.7f, cuando = "Fase 1" },
            new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = 1.8f, cuando = "Fase 2" },
            new FichaJefe.Afinidad { elemento = Elemento.Fuego, multiplicador = 1.5f, cuando = "Fase 2" },
            new FichaJefe.Afinidad { elemento = Elemento.Hielo, multiplicador = 0.8f, cuando = "Fase 2" },
            new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.3f, cuando = "Fase 2" },
            new FichaJefe.Afinidad { elemento = Elemento.Sangrado, multiplicador = 0.2f, cuando = "Fase 2" },
        };
        f.notaAfinidades = "Fase 2: su coraza resiste la espada sin imbuir (x0.6) y el sangrado lo cura. " +
                           "Tras Nova, Lluvia o Frenesí su corazón brilla: recibe x1.5 de todo.";
        f.informacion =
            "Cae del techo al empezar: un parry justo al aterrizar lo deja aturdido.\n" +
            "<b>Fase 1</b>: zarpazos, embestida (y Zigzag de ida y vuelta), ondas de sangre, pilares, orbes, " +
            "raíces que brotan por donde pasas y Cosecha: si tu sangrado va por la mitad, lo hace estallar.\n" +
            "<b>Fase 2</b> (a mitad de vida): forma grande. Guadaña doble (adelante y atrás), lluvia de fuego, teletransporte, " +
            "pilares en cadena, Semillas del vacío, Transfusión (un hilo que lo cura: aléjate o para el pulso) y, con poca vida, Frenesí.";
        EditorUtility.SetDirty(f);
    }
}
