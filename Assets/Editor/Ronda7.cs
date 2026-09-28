using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Septima ronda: fondo del nivel de nieve con profundidad (pixeles del tamano de
// los del personaje, tintes de distancia, dos neblinas que se mueven solas) y
// el ambiente (vineta, aurora, viento en la nieve, siluetas de primer plano).
// Warrior > Actualizar > Ronda 7 (o ActualizarProyecto.Ronda7 por linea de comandos).
public static class Ronda7
{
    private const string Pngs = "Assets/SPRITES PARA NUEVOS NIVELES/BACKGROUND/Fondo_CastilloHielo/pngs/";
    private const string Gandalf = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_BosqueGandalf/";

    [MenuItem("Warrior/Actualizar/Ronda 7")]
    public static void Todo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ConfigNivelEditor.RutaNieve);
        if (c == null) { Debug.LogError("[Ronda7] No esta Config Nivel Nieve"); return; }
        Fondo(c);

        Scene escena = EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaNieve);
        ParallaxCueva parallax = Object.FindFirstObjectByType<ParallaxCueva>();
        if (parallax != null) ConfigNivelEditor.ConstruirFondo(parallax, c);
        else Debug.LogWarning("[Ronda7] La escena no tiene fondo parallax");
        Ambiente(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda7] Listo.");
    }

    // De la capa mas lejana a la mas cercana. Lo lejano, mas claro y azulado
    // (algo transparente: se mezcla con el cielo) y lo cercano, mas oscuro.
    private static void Fondo(ConfigNivel c)
    {
        c.pixelesPorUnidadFondo = 28f;
        c.seguimientoVertical = 0.95f;
        c.capasFondo.Clear();
        Capa(c, "sky", "sky", 0.985f, new Color(1f, 1f, 1f, 1f), 0f, true, true);
        Capa(c, "4_BG_mts", "4_BG_mts", 0.95f, new Color(0.86f, 0.92f, 1f, 0.8f), 0f, false, true);
        Capa(c, "3_ice_castle", "3_ice_castle", 0.92f, new Color(0.9f, 0.95f, 1f, 0.92f), 0f, false, true);
        Capa(c, "Neblina lejana", "fog", 0.89f, new Color(1f, 1f, 1f, 0.45f), 0.12f, false, false);
        Capa(c, "2_foreground_mts", "2_foreground_mts", 0.84f, new Color(0.74f, 0.81f, 0.93f, 1f), 0f, false, true);
        Capa(c, "Neblina cercana", "fog", 0.8f, new Color(0.95f, 0.97f, 1f, 0.22f), -0.08f, false, false);
        Capa(c, "1_foreground_mts", "1_foreground_mts", 0.76f, new Color(0.58f, 0.66f, 0.82f, 1f), 0f, false, true);
        EditorUtility.SetDirty(c);
    }

    private static void Capa(ConfigNivel c, string nombre, string archivo, float seguimiento, Color color, float deriva, bool arriba, bool abajo)
    {
        Sprite s = AssetDatabase.LoadAllAssetsAtPath(Pngs + archivo + ".png").OfType<Sprite>().FirstOrDefault();
        if (s == null) { Debug.LogWarning("[Ronda7] Falta " + archivo); return; }
        c.capasFondo.Add(new ConfigNivel.CapaFondo
        {
            nombre = nombre, imagen = s, seguimiento = seguimiento, color = color, deriva = deriva,
            escala = 1f, rellenarArriba = arriba, rellenarAbajo = abajo,
        });
    }

    private static void Ambiente(Scene escena)
    {
        GameObject amb = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "AmbienteNieve");
        if (amb == null)
        {
            amb = new GameObject("AmbienteNieve");
            SceneManager.MoveGameObjectToScene(amb, escena);
        }
        if (amb.GetComponent<AmbienteNieve>() == null) amb.AddComponent<AmbienteNieve>();

        Transform pp = amb.transform.Find("PrimerPlano");
        if (pp == null)
        {
            pp = new GameObject("PrimerPlano").transform;
            pp.SetParent(amb.transform, false);
        }
        PrimerPlanoNieve primer = pp.GetComponent<PrimerPlanoNieve>();
        if (primer == null) primer = pp.gameObject.AddComponent<PrimerPlanoNieve>();

        // Pino nevado y hierba alta (los troncos sueltos parecian postes negros).
        Sprite[] pinos = AssetDatabase.LoadAllAssetsAtPath(Gandalf + "Pine Trees.png").OfType<Sprite>().ToArray();
        Sprite hierba = AssetDatabase.LoadAllAssetsAtPath(Gandalf + "Tall Grass.png").OfType<Sprite>().FirstOrDefault();
        primer.sprites = new[] { pinos.FirstOrDefault(s => s.name == "Pine Trees_5"), hierba }.Where(s => s != null).ToArray();
        primer.alturaMaxima = new Vector2(0.14f, 0.3f);
        if (primer.sprites.Length == 0) Debug.LogWarning("[Ronda7] No se encontraron sprites para el primer plano");
        EditorUtility.SetDirty(primer);
    }
}
