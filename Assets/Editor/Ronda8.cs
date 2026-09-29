using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Octava ronda:
//   - Interfaz: fuera el boton de pausa de la pantalla; las pistas sueltas pasan
//     a estatuas que se leen con la F.
// Warrior > Actualizar > Ronda 8 (o ActualizarProyecto.Ronda8 por linea de comandos).
public static partial class Ronda8
{
    private const string EscenaNieve = "Assets/Scenes/Nivel Nieve.unity";
    private const string EscenaCueva = "Assets/Scenes/Nivel Cueva.unity";
    private const string Gandalf = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_BosqueGandalf/";
    private const string Cementerio = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_Cementerio/free.png";

    [MenuItem("Warrior/Actualizar/Ronda 8")]
    public static void Todo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigurarRecursosRPG.PrepararPixel(Cementerio, 32f);
        Recursos();
        foreach (string ruta in new[] { EscenaNieve, EscenaCueva })
        {
            Scene escena = EditorSceneManager.OpenScene(ruta);
            bool nieve = ruta == EscenaNieve;
            QuitarBotonPausa(escena);
            Estatuas(escena, nieve);
            Decoracion(escena, nieve);
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda8] Listo.");
    }

    // La decoracion va en Ronda8Decoracion.cs.
    static partial void Decoracion(Scene escena, bool nieve);

    // ------------------------------------------------------------------ Pausa

    // La pausa sigue con la tecla Esc; el boton de la esquina sobra.
    private static void QuitarBotonPausa(Scene escena)
    {
        foreach (GameObject r in escena.GetRootGameObjects())
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BtnPause").ToList())
            {
                Debug.Log("[Ronda8] Quitado " + Ruta(t));
                Object.DestroyImmediate(t.gameObject);
            }
    }

    // ------------------------------------------------------------------ Estatuas

    // Titulo de cada pista segun como empieza su texto (el resto: "Inscripcion").
    private static readonly (string inicio, string titulo)[] Titulos =
    {
        ("El hielo de la ladera", "La ladera helada"),
        ("Junto a la hoguera", "La espada clavada"),
        ("Más allá, la ventisca", "La ventisca"),
        ("Un guerrero congelado", "El guerrero congelado"),
        ("Una sombra viva", "La grieta sombría"),
        ("El lago humea", "El lago helado"),
        ("Un muro de hielo", "El muro de hielo"),
        ("Arañazos profundos", "Arañazos en la roca"),
    };

    // Cada pista suelta (PistaNarrativa) se sustituye por una estatua en el suelo,
    // en el mismo sitio, con su mismo texto. Si ya habia una estatua de adorno
    // cerca, se usa esa.
    private static void Estatuas(Scene escena, bool nieve)
    {
        Physics2D.SyncTransforms();
        var pistas = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PistaNarrativa>(true)).ToList();
        if (pistas.Count == 0) { Debug.Log("[Ronda8] " + escena.name + ": sin pistas sueltas"); return; }

        Transform padre = Raiz(escena, "Estatuas");
        Sprite sprite = nieve ? Sprites(Gandalf + "Angel Statue.png").OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault()
                              : Sprites(Cementerio).FirstOrDefault(s => s.name == "free_20");
        Color tinte = nieve ? new Color(0.75f, 0.82f, 0.95f) : new Color(0.78f, 0.7f, 0.72f);
        Color brillo = nieve ? new Color(0.55f, 0.88f, 1f) : new Color(1f, 0.55f, 0.35f);
        float alto = nieve ? 2.6f : 2.3f;

        var adornos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                            .Where(sr => sr.name == "Estatua" && sr.GetComponent<EstatuaPista>() == null).ToList();
        int n = 0;
        foreach (PistaNarrativa p in pistas.OrderBy(p => p.transform.position.x))
        {
            string texto = new SerializedObject(p).FindProperty("texto").stringValue;
            Vector2 pos = p.transform.position;
            Collider2D col = p.GetComponent<Collider2D>();
            if (col != null) pos = new Vector2(col.bounds.center.x, col.bounds.min.y);

            SpriteRenderer sr = adornos.FirstOrDefault(a => Mathf.Abs(a.bounds.center.x - pos.x) < 8f);
            if (sr != null) adornos.Remove(sr);
            else if (sprite != null)
            {
                float suelo = Suelo(pos);
                sr = Apoyada(padre, "Estatua", sprite, pos.x, suelo, alto, "Middleground", 1, tinte);
            }
            if (sr == null) continue;

            n++;
            EstatuaPista e = sr.gameObject.AddComponent<EstatuaPista>();
            e.texto = texto;
            e.titulo = Titulos.FirstOrDefault(t => texto.StartsWith(t.inicio)).titulo ?? "Inscripción";
            e.id = "estatua_" + n;
            e.colorBrillo = brillo;
            Debug.Log($"[Ronda8] {escena.name}: estatua {e.id} \"{e.titulo}\" en {sr.bounds.center} (pista en {pos})");

            // Fuera la pista suelta: el objeto entero si solo era eso.
            GameObject go = p.gameObject;
            bool soloPista = go.GetComponents<Component>().All(c => c is Transform || c is PistaNarrativa || c is Collider2D) && go.transform.childCount == 0;
            if (soloPista) Object.DestroyImmediate(go);
            else
            {
                Object.DestroyImmediate(p);
                if (col != null) Object.DestroyImmediate(col);
            }
        }
    }

    // Altura del suelo debajo de un punto (lo primero solido hacia abajo).
    private static float Suelo(Vector2 desde)
    {
        // Sin contar el collider en el que empieza el rayo ni los triggers: el
        // primer suelo de verdad por debajo del pie de la pista.
        bool antes = Physics2D.queriesStartInColliders, antesT = Physics2D.queriesHitTriggers;
        Physics2D.queriesStartInColliders = false;
        Physics2D.queriesHitTriggers = false;
        int mascara = LayerMask.GetMask("Ground");
        RaycastHit2D h = Physics2D.Raycast(desde + Vector2.up * 0.5f, Vector2.down, 20f, mascara);
        Physics2D.queriesStartInColliders = antes;
        Physics2D.queriesHitTriggers = antesT;
        return h.collider != null ? h.point.y : desde.y;
    }

    // ------------------------------------------------------------------ Ayudas

    private static IEnumerable<Sprite> Sprites(string ruta) => AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>();

    private static Transform Raiz(Scene escena, string nombre)
    {
        GameObject g = escena.GetRootGameObjects().FirstOrDefault(r => r.name == nombre);
        if (g == null)
        {
            g = new GameObject(nombre);
            SceneManager.MoveGameObjectToScene(g, escena);
        }
        return g.transform;
    }

    // Sprite con la base apoyada en "suelo", de "alto" unidades.
    private static SpriteRenderer Apoyada(Transform padre, string nombre, Sprite s, float x, float suelo, float alto,
                                          string capa, int orden, Color color, bool voltear = false)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, true);
        sr.sprite = s;
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        sr.color = color;
        sr.flipX = voltear;
        float esc = alto / s.bounds.size.y;
        sr.transform.localScale = new Vector3(esc, esc, 1f);
        // Base del sprite (bounds.min) en el suelo, centrado en x.
        Vector2 c = s.bounds.center * esc, min = s.bounds.min * esc;
        sr.transform.position = new Vector3(x - c.x, suelo - min.y, 0f);
        return sr;
    }

    private static string Ruta(Transform t) => t.parent == null ? t.name : Ruta(t.parent) + "/" + t.name;
}
