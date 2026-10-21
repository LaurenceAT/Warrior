using System.Runtime.InteropServices;
using UnityEngine;

// Version web: evita que Esc saque de pantalla completa y pide almacenamiento
// permanente para lo guardado (ver Plugins/WebGL/WarriorWeb.jslib).
public static class WebTeclado
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WarriorBloquearEscape();
    [DllImport("__Internal")] private static extern void WarriorPedirPersistente();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WarriorBloquearEscape();
        // Que el navegador no borre lo guardado cuando le falte espacio.
        WarriorPedirPersistente();
#endif
    }
}
