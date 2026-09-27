using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cambia las puertas de entrada y salida de los niveles por los portales
// (Glitch Portals). Conserva el objeto (nombre, etiqueta, sitio y si esta activo),
// asi la arena del jefe sigue encendiendo la salida al vencer.
// Warrior > Instalar portales en los niveles.
public static class ConfigurarPortales
{
    private const string Portales = ConfigurarRecursosRPG.Pack + "ENTRADA Y SALIDA DE NIVEL/Glitch Portals/";
    private const string Destello = ConfigurarRecursosRPG.Pack + "EFECTOS DE ATAQUE DE LOS ENEMIGOS/Efecto_Sacerdote/VFX 2/frames";

    [MenuItem("Warrior/Instalar portales en los niveles")]
    public static void InstalarTodos()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string ruta in new[] { ConfigNivelEditor.EscenaCueva, ConfigNivelEditor.EscenaNieve })
        {
            Scene e = EditorSceneManager.OpenScene(ruta);
            Instalar(e);
            EditorSceneManager.MarkSceneDirty(e);
            EditorSceneManager.SaveScene(e);
        }
    }

    public static void Instalar(Scene escena)
    {
        AnimadorHoja.Clip portal = ClipPortal("V2 (64x64)");
        AnimadorHoja.Clip destello = ConfigurarRecursosRPG.Clip("destello_portal", Destello, 18f, false, 64f, new Vector2(0.5f, 0.03f));
        Color color = new Color(0.9f, 0.85f, 1f, 1f);

        GameObject[] raices = escena.GetRootGameObjects();
        GameObject entrada = raices.FirstOrDefault(r => r.name == "EntranceDoor");
        GameObject salida = raices.FirstOrDefault(r => r.name == "ExitDoor");
        GameObject inicio = raices.FirstOrDefault(r => r.name == "RespawnPoint") ?? raices.FirstOrDefault(r => r.name == "Player");

        if (entrada != null)
        {
            Vaciar(entrada);
            if (inicio != null) entrada.transform.position = new Vector3(inicio.transform.position.x, Suelo(inicio.transform.position), 0f);
            AnimadorHoja anim = Visual(entrada, portal);
            PortalEntrada pe = entrada.AddComponent<PortalEntrada>();
            SerializedObject so = new SerializedObject(pe);
            so.FindProperty("animacionPortal").objectReferenceValue = anim;
            Clip(so.FindProperty("efectoSalir"), destello);
            so.FindProperty("colorEfecto").colorValue = color;
            so.FindProperty("escalaEfecto").floatValue = 1.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (salida != null)
        {
            Vaciar(salida);
            salida.transform.position = new Vector3(salida.transform.position.x, Suelo(salida.transform.position + Vector3.up), 0f);
            AnimadorHoja anim = Visual(salida, portal);
            BoxCollider2D box = salida.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1f, 1.6f);
            box.offset = new Vector2(0f, 0.85f);
            PortalNivel pn = salida.AddComponent<PortalNivel>();
            SerializedObject so = new SerializedObject(pn);
            so.FindProperty("animacionPortal").objectReferenceValue = anim;
            Clip(so.FindProperty("efectoEntrar"), destello);
            so.FindProperty("colorEfecto").colorValue = color;
            so.FindProperty("escalaEfecto").floatValue = 1.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log($"[Portales] {escena.name}: entrada={(entrada != null)} salida={(salida != null)}");
    }

    // Quita todo lo de la puerta vieja (componentes e hijos) menos el Transform.
    private static void Vaciar(GameObject go)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(go), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        // Varias pasadas: algunos componentes dependen de otros (RequireComponent).
        for (int pasada = 0; pasada < 3; pasada++)
            foreach (Component c in go.GetComponents<Component>())
                if (!(c is Transform)) Object.DestroyImmediate(c);
        go.transform.localScale = Vector3.one;
    }

    private static AnimadorHoja Visual(GameObject padre, AnimadorHoja.Clip portal)
    {
        GameObject v = new GameObject("Portal");
        v.transform.SetParent(padre.transform, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Middleground";
        sr.sortingOrder = 5;
        AnimadorHoja a = v.AddComponent<AnimadorHoja>();
        a.destino = sr;
        a.clips = new System.Collections.Generic.List<AnimadorHoja.Clip> { portal };
        a.clipInicial = portal.nombre;
        if (portal.fotogramas.Length > 0)
            sr.sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(portal.fotogramas[0])).OfType<Sprite>().FirstOrDefault();
        return a;
    }

    private static AnimadorHoja.Clip ClipPortal(string version)
    {
        string carpeta = Portales + version;
        var rutas = Directory.GetFiles(carpeta, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
        foreach (string r in rutas) ConfigurarRecursosRPG.PrepararPixel(r, 30f);
        Texture2D[] tex = rutas.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t => t != null).ToArray();
        return new AnimadorHoja.Clip
        {
            nombre = "portal", fotogramas = tex, cantidad = tex.Length, fps = 20f, bucle = true,
            pivote = new Vector2(0.5f, 0.03f), pixelesPorUnidad = 30f,
        };
    }

    // Altura del suelo bajo un punto.
    private static float Suelo(Vector3 desde)
    {
        RaycastHit2D h = Physics2D.Raycast(desde + Vector3.up * 0.5f, Vector2.down, 30f, LayerMask.GetMask("Ground"));
        return h ? h.point.y : desde.y;
    }

    private static void Clip(SerializedProperty p, AnimadorHoja.Clip c)
    {
        if (c == null) return;
        p.FindPropertyRelative("nombre").stringValue = c.nombre;
        p.FindPropertyRelative("fps").floatValue = c.fps;
        p.FindPropertyRelative("bucle").boolValue = c.bucle;
        p.FindPropertyRelative("cantidad").intValue = c.cantidad;
        p.FindPropertyRelative("pixelesPorUnidad").floatValue = c.pixelesPorUnidad;
        p.FindPropertyRelative("pivote").vector2Value = c.pivote;
        SerializedProperty f = p.FindPropertyRelative("fotogramas");
        f.arraySize = c.fotogramas.Length;
        for (int i = 0; i < c.fotogramas.Length; i++) f.GetArrayElementAtIndex(i).objectReferenceValue = c.fotogramas[i];
    }
}
