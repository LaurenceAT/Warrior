using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Crea la escena del menu principal y deja el orden del juego:
//   0 Menu Principal -> 1 Nivel Nieve (primer nivel) -> 2 Nivel Cueva.
// Tambien pone nombre a las hogueras (sale en la lista de partidas guardadas).
// Warrior > Crear menu principal.
public static class CrearMenuPrincipal
{
    public const string Escena = "Assets/Scenes/Menu Principal.unity";

    private static readonly (string escena, float x, string nombre)[] Hogueras =
    {
        (ConfigNivelEditor.EscenaNieve, 10f, "Campamento del Paso"),
        (ConfigNivelEditor.EscenaNieve, 116f, "Ladera Helada"),
        (ConfigNivelEditor.EscenaNieve, 236f, "Orilla del Lago"),
        (ConfigNivelEditor.EscenaNieve, 350f, "Antesala de la Sombra"),
        (ConfigNivelEditor.EscenaCueva, 55f, "Pasillo Alto"),
        (ConfigNivelEditor.EscenaCueva, 127f, "Antesala Carmesí"),
    };

    [MenuItem("Warrior/Crear menu principal")]
    public static void Crear()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!System.IO.File.Exists(Escena))
        {
            Scene e = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            Camera c = cam.GetComponent<Camera>();
            c.orthographic = true;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            UniversalAdditionalCameraData d = c.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = true;

            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
            new GameObject("MenuPrincipal").AddComponent<MenuPrincipal>();
            EditorSceneManager.SaveScene(e, Escena);
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Escena, true),
            new EditorBuildSettingsScene(ConfigNivelEditor.EscenaNieve, true),
            new EditorBuildSettingsScene(ConfigNivelEditor.EscenaCueva, true),
        };

        foreach (string ruta in Hogueras.Select(h => h.escena).Distinct())
        {
            Scene e = EditorSceneManager.OpenScene(ruta);
            foreach (Hoguera h in e.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Hoguera>(true)))
            {
                var mas = Hogueras.Where(x => x.escena == ruta).OrderBy(x => Mathf.Abs(x.x - h.transform.position.x)).First();
                SerializedObject so = new SerializedObject(h);
                so.FindProperty("nombreLugar").stringValue = mas.nombre;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(e);
            EditorSceneManager.SaveScene(e);
        }
        Debug.Log("[Menu] Menu principal listo; orden: Menu, Nieve, Cueva.");
    }
}
