using UnityEditor;
using UnityEngine;

// Inspector de la ZonaJefe (solo Editor): estado del activador y del muro, y
// botones de depuracion para cerrar y abrir el muro jugando. En la escena se ven
// el activador (amarillo), el muro y donde aparece el jefe (rojo).
[CustomEditor(typeof(ZonaJefe))]
public class ZonaJefeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ZonaJefe z = (ZonaJefe)target;
        EditorGUILayout.Space();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("El activador (amarillo) empieza la pelea; el muro de niebla (hijo MuroNiebla) se ajusta en su propio componente: tamaño, color, velocidad, sonidos...\nJugando aparecen aquí los botones de depuración.", MessageType.Info);
            if (z.Muro != null || z.GetComponentInChildren<MuroNiebla>(true) != null)
                if (GUILayout.Button("Seleccionar el muro de niebla")) Selection.activeObject = z.GetComponentInChildren<MuroNiebla>(true);
            return;
        }
        EditorGUILayout.LabelField("Estado", $"activador {(z.Armado ? "armado" : "usado")} · muro {(z.Muro != null ? z.Muro.Estado.ToString() : "-")} · disparos {z.Disparos}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Cerrar muro")) z.ForzarCierre();
            if (GUILayout.Button("Abrir muro")) z.ForzarApertura();
        }
        Repaint();
    }

    [MenuItem("Warrior/Zona de jefe/Cerrar el muro (jugando)")]
    private static void CerrarTodas()
    {
        foreach (ZonaJefe z in Object.FindObjectsByType<ZonaJefe>(FindObjectsSortMode.None)) z.ForzarCierre();
    }

    [MenuItem("Warrior/Zona de jefe/Abrir el muro (jugando)")]
    private static void AbrirTodas()
    {
        foreach (ZonaJefe z in Object.FindObjectsByType<ZonaJefe>(FindObjectsSortMode.None)) z.ForzarApertura();
    }

    [MenuItem("Warrior/Zona de jefe/Cerrar el muro (jugando)", true)]
    [MenuItem("Warrior/Zona de jefe/Abrir el muro (jugando)", true)]
    private static bool Jugando() => Application.isPlaying;

    [MenuItem("Warrior/Zona de jefe/Seleccionar la zona de la escena")]
    private static void Seleccionar()
    {
        ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
        if (z != null) { Selection.activeObject = z; SceneView.lastActiveSceneView?.FrameSelected(); }
        else Debug.Log("[ZonaJefe] Esta escena no tiene zona de jefe.");
    }
}
