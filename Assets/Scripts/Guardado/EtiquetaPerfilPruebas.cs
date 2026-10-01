#if UNITY_EDITOR
using UnityEngine;

// Etiqueta en la ventana Game cuando "Simular jugador nuevo" esta activado
// (Herramientas > Progreso del juego). Solo existe en el Editor.
public class EtiquetaPerfilPruebas : MonoBehaviour
{
    private GUIStyle estilo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (!RegistroGuardado.PerfilPruebas || FindFirstObjectByType<EtiquetaPerfilPruebas>() != null) return;
        GameObject go = new GameObject("EtiquetaPerfilPruebas");
        DontDestroyOnLoad(go);
        go.AddComponent<EtiquetaPerfilPruebas>();
    }

    private void OnGUI()
    {
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            estilo.normal.textColor = new Color(1f, 0.85f, 0.4f);
        }
        GUI.depth = -1000;
        GUI.Box(new Rect(Screen.width - 230, 8, 222, 24), "PERFIL DE PRUEBAS (jugador nuevo)", estilo);
    }
}
#endif
