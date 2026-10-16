using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Parallax mas notorio en la Nieve (tarea de clase). Saca capturas del fondo
// antes del cambio, aplica los valores nuevos (en Config Nivel Nieve y en la
// escena) y vuelve a sacar las capturas. Las imagenes van a "Informe Parallax/imagenes".
// Batch: -executeMethod ParallaxNotorio.Todo -quit
public static class ParallaxNotorio
{
    // De la capa mas lejana a la mas cercana (mismo orden que Config Nivel Nieve).
    private static readonly (string nombre, float seguimiento)[] Nuevos =
    {
        ("sky", 0.97f),
        ("4_BG_mts", 0.88f),
        ("3_ice_castle", 0.8f),
        ("Neblina lejana", 0.72f),
        ("2_foreground_mts", 0.62f),
        ("Neblina cercana", 0.55f),
        ("1_foreground_mts", 0.45f),
    };
    private const float Intensidad = 1.15f;
    private const float ProfundidadVertical = 0.35f;
    private const float VelocidadPrimerPlano = 1.7f;

    private const float TamanoCamara = 4.7f;
    private static readonly float[] Viejos = { 0.985f, 0.95f, 0.92f, 0.89f, 0.84f, 0.8f, 0.76f };
    private const int Ancho = 1280, Alto = 720;

    [MenuItem("Warrior/Parallax/Capturas, aplicar y capturas")]
    public static void Todo()
    {
        Capturar("antes");
        Aplicar();
        Capturar("despues");
        Debug.Log("[Parallax] Listo");
    }

    // Repite las capturas sin tocar nada (las de antes con los valores viejos en memoria).
    public static void SoloCapturas()
    {
        Capturar("antes", true);
        Capturar("despues");
    }

    [MenuItem("Warrior/Parallax/Aplicar valores nuevos")]
    public static void Aplicar()
    {
        var c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ConfigNivelEditor.RutaNieve);
        var escena = EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaNieve, OpenSceneMode.Single);
        var p = Object.FindFirstObjectByType<ParallaxCueva>();
        if (c == null || p == null || c.capasFondo.Count != Nuevos.Length || p.capas.Length != Nuevos.Length)
        {
            Debug.LogError("[Parallax] No coinciden las capas; no se cambia nada.");
            return;
        }
        for (int i = 0; i < Nuevos.Length; i++)
        {
            if (c.capasFondo[i].nombre != Nuevos[i].nombre || p.capas[i].raiz.name != "Capa_" + Nuevos[i].nombre)
            {
                Debug.LogError("[Parallax] Capa inesperada: " + c.capasFondo[i].nombre);
                return;
            }
            c.capasFondo[i].seguimiento = Nuevos[i].seguimiento;
            p.capas[i].seguimiento = Nuevos[i].seguimiento;
        }
        c.intensidadParallax = p.intensidad = Intensidad;
        c.profundidadVertical = p.profundidadVertical = ProfundidadVertical;
        EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(p);

        var pp = Object.FindFirstObjectByType<PrimerPlanoNieve>();
        if (pp != null) { pp.velocidadParallax = VelocidadPrimerPlano; EditorUtility.SetDirty(pp); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Parallax] Valores nuevos aplicados a la Nieve");
    }

    // Dos parejas de capturas: camara movida en horizontal y en vertical. El
    // suelo se mueve entero; las capas del fondo, segun su profundidad.
    public static void Capturar(string etiqueta, bool valoresViejos = false)
    {
        EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaNieve, OpenSceneMode.Single);
        var p = Object.FindFirstObjectByType<ParallaxCueva>();
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 inicio = (player != null ? player.transform.position : Vector3.zero) + new Vector3(4f, 2.2f, 0f);
        if (valoresViejos)
        {
            for (int i = 0; i < p.capas.Length && i < Viejos.Length; i++) p.capas[i].seguimiento = Viejos[i];
            p.intensidad = 1f;
            p.profundidadVertical = 0f;
        }
        p.PrepararRellenos();

        string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "Informe Parallax", "imagenes");
        Directory.CreateDirectory(carpeta);
        var vistas = new (string n, Vector3 pos)[]
        {
            ("h1", inicio),
            ("h2", inicio + new Vector3(10f, 0f, 0f)),
            ("v1", inicio + new Vector3(10f, 0f, 0f)),
            ("v2", inicio + new Vector3(10f, 6f, 0f)),
        };

        GameObject go = new GameObject("CamCaptura");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = TamanoCamara;
        cam.aspect = (float)Ancho / Alto;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
        foreach (var v in vistas)
        {
            p.FijarInicio(inicio);
            p.Colocar(v.pos, 0f);
            go.transform.position = new Vector3(v.pos.x, v.pos.y, -20f);
            RenderTexture rt = new RenderTexture(Ancho, Alto, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, etiqueta + "_" + v.n + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
        }
        Object.DestroyImmediate(go);
        Debug.Log("[Parallax] Capturas " + etiqueta + " en " + carpeta);
    }
}
