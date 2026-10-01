using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ronda 18: entrada a las zonas de los jefes (muro de niebla).
//   Warrior > Zona de jefe > Generar (Ronda 18): crea el prefab "Zona de jefe" y
//   lo coloca en la entrada de las tres arenas (Nieve, Cueva y Bosque de la
//   Cazadora; los desafios usan esas mismas escenas). La niebla vieja se apaga
//   (queda desactivada en la escena, sin borrar). Se puede repetir: rehace la zona.
public static class Ronda18
{
    public const string RutaPrefab = "Assets/Prefabs/Zona de jefe.prefab";
    private const string Viento = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_Efectos/Sonidos_Magia/Wind Spell Pack/48000 kHz/ogg/";

    private struct Ajuste
    {
        public string escena;
        public Color niebla, denso, borde;
        public bool encuadrar;
        public float distancia; // < 0: se calcula con la camara de la arena
    }

    private static readonly Ajuste[] Arenas =
    {
        // Nieve: blanco azulado.
        new Ajuste { escena = "Assets/Scenes/Nivel Nieve.unity", encuadrar = true, distancia = -1f,
            niebla = new Color(0.7f, 0.79f, 0.92f), denso = new Color(0.85f, 0.9f, 1f), borde = new Color(0.93f, 0.98f, 1f) },
        // Cueva: verde grisaceo apagado.
        new Ajuste { escena = "Assets/Scenes/Nivel Cueva.unity", encuadrar = true, distancia = -1f,
            niebla = new Color(0.46f, 0.52f, 0.46f), denso = new Color(0.6f, 0.66f, 0.58f), borde = new Color(0.8f, 0.9f, 0.74f) },
        // Bosque de la Cazadora: verdoso oscuro. Su entrada ya mueve la camara hacia ella.
        new Ajuste { escena = "Assets/Scenes/Bosque Cazadora.unity", encuadrar = false, distancia = 6f,
            niebla = new Color(0.22f, 0.34f, 0.27f), denso = new Color(0.31f, 0.45f, 0.35f), borde = new Color(0.62f, 0.86f, 0.64f) },
    };

    [MenuItem("Warrior/Zona de jefe/Generar (Ronda 18)")]
    public static void Generar()
    {
        GameObject prefab = CrearPrefab();
        foreach (Ajuste a in Arenas) Colocar(a, prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda18] Zonas de jefe colocadas en " + Arenas.Length + " escenas.");
    }

    private static GameObject CrearPrefab()
    {
        GameObject raiz = new GameObject("ZonaJefe");
        raiz.layer = LayerMask.NameToLayer("Items");
        raiz.AddComponent<BoxCollider2D>().isTrigger = true;
        raiz.AddComponent<ZonaJefe>();
        GameObject m = new GameObject("MuroNiebla");
        m.layer = LayerMask.NameToLayer("Ground");
        m.transform.SetParent(raiz.transform, false);
        m.AddComponent<BoxCollider2D>();
        MuroNiebla muro = m.AddComponent<MuroNiebla>();
        muro.sonidoFormar = AssetDatabase.LoadAssetAtPath<AudioClip>(Viento + "wind_spell_cast_air_rise_loud_impact_01.ogg");
        muro.sonidoContinuo = AssetDatabase.LoadAssetAtPath<AudioClip>(Viento + "wind_spell_cast_air_long_loop_01.ogg");
        if (muro.sonidoFormar == null || muro.sonidoContinuo == null) Debug.LogWarning("[Ronda18] No se encontraron los sonidos de viento");
        GameObject p = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);
        return p;
    }

    private static void Colocar(Ajuste a, GameObject prefab)
    {
        Scene escena = EditorSceneManager.OpenScene(a.escena, OpenSceneMode.Single);
        MonoBehaviour arena = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c is IArenaJefe);
        if (arena == null) { Debug.LogWarning("[Ronda18] sin arena en " + a.escena); return; }
        SerializedObject so = new SerializedObject(arena);
        bool cazadora = arena is ArenaCazadora;
        Rect zona = so.FindProperty("zona").rectValue;
        float suelo = so.FindProperty("suelo").floatValue;
        SerializedProperty pMuro = so.FindProperty(cazadora ? "muro" : "muroNiebla");
        SerializedProperty pVisual = so.FindProperty(cazadora ? "visualMuro" : "visualNiebla");
        Collider2D viejo = pMuro.objectReferenceValue as Collider2D;
        Transform padre = arena.transform.parent;

        // La zona de antes (si se repite) se rehace.
        float xMuro = viejo != null ? viejo.transform.position.x : zona.xMin;
        foreach (ZonaJefe z in Object.FindObjectsByType<ZonaJefe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (viejo == null) xMuro = z.transform.position.x;
            Object.DestroyImmediate(z.gameObject);
        }
        Vector2 jefe = ((IArenaJefe)arena).PuntoJefe;
        if (cazadora) jefe = new Vector2(so.FindProperty("xAparicion").floatValue, suelo);

        // Donde ya se ve al jefe: su sitio entra en la camara de la arena.
        float distancia = a.distancia;
        if (distancia < 0f)
        {
            float tam = 5.4f;
            foreach (ZonaCamara zc in Object.FindObjectsByType<ZonaCamara>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Collider2D c = zc.GetComponent<Collider2D>();
                if (c != null && c.bounds.Contains(new Vector3(zona.center.x, zona.center.y, c.bounds.center.z))) tam = zc.Tamano;
            }
            float medioAncho = tam * 16f / 9f;
            distancia = Mathf.Max(4f, jefe.x - medioAncho - 0.5f - xMuro);
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        go.name = "ZonaJefe";
        go.transform.SetParent(padre, true);
        go.transform.position = new Vector3(xMuro, suelo, 0f);
        ZonaJefe zj = go.GetComponent<ZonaJefe>();
        SerializedObject sz = new SerializedObject(zj);
        sz.FindProperty("arena").objectReferenceValue = arena;
        sz.FindProperty("ladoArena").intValue = 1;
        sz.FindProperty("distanciaActivador").floatValue = distancia;
        sz.FindProperty("largoActivador").floatValue = Mathf.Max(4f, zona.xMax - 0.5f - (xMuro + distancia));
        sz.FindProperty("altoActivador").floatValue = Mathf.Max(6f, zona.yMax - suelo);
        sz.FindProperty("encuadrarAlEmpezar").boolValue = a.encuadrar;
        sz.ApplyModifiedPropertiesWithoutUndo();
        // Recoloca el collider del activador con los valores nuevos.
        BoxCollider2D act = go.GetComponent<BoxCollider2D>();
        act.size = new Vector2(zj.largoActivador, zj.altoActivador);
        act.offset = new Vector2(zj.distanciaActivador + zj.largoActivador * 0.5f, zj.altoActivador * 0.5f - 0.5f);

        MuroNiebla muro = go.GetComponentInChildren<MuroNiebla>(true);
        SerializedObject sm = new SerializedObject(muro);
        float alto = zona.yMax - suelo + 3f;
        sm.FindProperty("alto").floatValue = alto;
        sm.FindProperty("colorNiebla").colorValue = a.niebla;
        sm.FindProperty("colorDenso").colorValue = a.denso;
        sm.FindProperty("colorBorde").colorValue = a.borde;
        sm.ApplyModifiedPropertiesWithoutUndo();
        BoxCollider2D bm = muro.GetComponent<BoxCollider2D>();
        bm.size = new Vector2(muro.anchoBloqueo, alto);
        bm.offset = new Vector2(0f, alto * 0.5f);

        // La niebla de antes: fuera de la arena y apagada (no se borra).
        if (viejo != null)
        {
            viejo.gameObject.SetActive(false);
            if (!viejo.name.Contains("antigua")) viejo.name += " (antigua, sin uso)";
        }
        pMuro.objectReferenceValue = null;
        pVisual.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log($"[Ronda18] {escena.name}: muro en x={xMuro:0.0}, suelo {suelo}, alto {alto:0.0}; activador desde x={xMuro + distancia:0.0} " +
                  $"(jefe en x={jefe.x:0.0}), largo {zj.largoActivador:0.0}; encuadre {a.encuadrar}");
    }
    private static readonly string[] Escenas =
    {
        "Assets/Scenes/Nivel Nieve.unity",
        "Assets/Scenes/Nivel Cueva.unity",
        "Assets/Scenes/Bosque Cazadora.unity",
    };

    // Solo lee: como estan las arenas en cada escena.
    public static void Diagnostico()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string ruta in Escenas)
        {
            EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            sb.AppendLine("=== " + ruta);
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string tipo = mb.GetType().Name;
                if (tipo != "ArenaJefe" && tipo != "ArenaCazadora" && tipo != "Hoguera" && tipo != "PortalNivel" && tipo != "ModoDesafio"
                    && !tipo.Contains("Camara") && !tipo.Contains("Confin")) continue;
                sb.AppendLine($"{tipo} '{Ruta(mb.transform)}' pos={mb.transform.position} activo={mb.gameObject.activeInHierarchy}");
                foreach (Collider2D c in mb.GetComponents<Collider2D>())
                    sb.AppendLine($"   collider {c.GetType().Name} trigger={c.isTrigger} bounds={c.bounds.min}..{c.bounds.max}");
                SerializedObject so = new SerializedObject(mb);
                SerializedProperty p = so.GetIterator();
                p.NextVisible(true);
                while (p.NextVisible(false))
                {
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null)
                    {
                        Object o = p.objectReferenceValue;
                        string extra = "";
                        if (o is Component comp)
                        {
                            extra = " en " + Ruta(comp.transform) + " pos=" + comp.transform.position;
                            if (o is Collider2D cc) extra += $" bounds={cc.bounds.min}..{cc.bounds.max} trigger={cc.isTrigger}";
                        }
                        sb.AppendLine($"   {p.propertyPath} = {o.name} ({o.GetType().Name}){extra}");
                    }
                    else if (p.isArray && p.propertyType == SerializedPropertyType.Generic && p.arraySize > 0 && p.arraySize < 20)
                    {
                        for (int i = 0; i < p.arraySize; i++)
                        {
                            SerializedProperty e = p.GetArrayElementAtIndex(i);
                            if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue is Component ec)
                            {
                                string s = "";
                                if (ec is SpriteRenderer sr) s = $" sprite={(sr.sprite ? sr.sprite.name : "-")} bounds={sr.bounds.min}..{sr.bounds.max} capa={sr.sortingLayerName}/{sr.sortingOrder}";
                                sb.AppendLine($"   {p.propertyPath}[{i}] = {Ruta(ec.transform)}{s}");
                            }
                        }
                    }
                    else if (p.propertyType == SerializedPropertyType.Rect) sb.AppendLine($"   {p.propertyPath} = {p.rectValue}");
                    else if (p.propertyType == SerializedPropertyType.Float && (p.name == "suelo" || p.name == "xAparicion")) sb.AppendLine($"   {p.propertyPath} = {p.floatValue}");
                }
            }
            // Todo lo que se llame niebla / muro / fog.
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name.ToLower();
                if (!(n.Contains("niebla") || n.Contains("muro") || n.Contains("fog") || n.Contains("bruma") || n.Contains("salida") || n.Contains("bloqueo"))) continue;
                string s = "";
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null) s += $" sprite={(sr.sprite ? sr.sprite.name : "-")} color={sr.color} bounds={sr.bounds.min}..{sr.bounds.max} capa={sr.sortingLayerName}/{sr.sortingOrder} activo={t.gameObject.activeInHierarchy}";
                foreach (Collider2D c in t.GetComponents<Collider2D>()) s += $" col={c.GetType().Name} trig={c.isTrigger} en={c.enabled} b={c.bounds.min}..{c.bounds.max}";
                foreach (Component c in t.GetComponents<Component>()) if (!(c is Transform) && !(c is SpriteRenderer) && !(c is Collider2D)) s += " +" + c.GetType().Name;
                sb.AppendLine($"  [{Ruta(t)}] pos={t.position} esc={t.lossyScale}{s}");
            }
            PlayerControler pc = Object.FindFirstObjectByType<PlayerControler>(FindObjectsInactive.Include);
            if (pc != null) sb.AppendLine("  Player inicio " + pc.transform.position);
        }
        File.WriteAllText("Logs/Ronda18_Diagnostico.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }

    private static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;
}
