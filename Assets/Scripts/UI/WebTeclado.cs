using System.Runtime.InteropServices;
using UnityEngine;

// Version web: evita que Esc saque de pantalla completa (ver Plugins/WebGL/WarriorWeb.jslib).
public static class WebTeclado
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WarriorBloquearEscape();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WarriorBloquearEscape();
#endif
    }
}
