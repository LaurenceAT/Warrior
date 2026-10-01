using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Volumenes guardados (sirven para todas las partidas):
//   - General: todo el sonido (AudioListener).
//   - Musica: la musica de los niveles y de los jefes.
//   - Efectos: golpes, pasos, hechizos, ambiente...
public static class ControlVolumen
{
    public static event System.Action AlCambiar;

    public static float General
    {
        get => PlayerPrefs.GetFloat(RegistroGuardado.Clave("volGeneral"), 1f);
        set { PlayerPrefs.SetFloat(RegistroGuardado.Clave("volGeneral"), value); AudioListener.volume = value; AlCambiar?.Invoke(); }
    }

    public static float Musica
    {
        get => PlayerPrefs.GetFloat(RegistroGuardado.Clave("volMusica"), 1f);
        set { PlayerPrefs.SetFloat(RegistroGuardado.Clave("volMusica"), value); AlCambiar?.Invoke(); }
    }

    public static float Efectos
    {
        get => PlayerPrefs.GetFloat(RegistroGuardado.Clave("volEfectos"), 1f);
        set { PlayerPrefs.SetFloat(RegistroGuardado.Clave("volEfectos"), value); AlCambiar?.Invoke(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Aplicar()
    {
        AudioListener.volume = General;
    }
}

// Brillo de la pantalla (0.5 = mas oscuro, 1 = normal, 1.5 = mas claro). Se
// aplica con la exposicion del postprocesado de URP en cada escena.
public static class ControlBrillo
{
    private static Volume volumen;
    private static ColorAdjustments ajuste;

    public static float Brillo
    {
        get => PlayerPrefs.GetFloat(RegistroGuardado.Clave("brillo"), 1f);
        set { PlayerPrefs.SetFloat(RegistroGuardado.Clave("brillo"), Mathf.Clamp(value, 0.5f, 1.5f)); Aplicar(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded += (e, m) => Aplicar();
        Aplicar();
    }

    public static void Aplicar()
    {
        if (volumen == null)
        {
            GameObject go = new GameObject("Brillo");
            Object.DontDestroyOnLoad(go);
            volumen = go.AddComponent<Volume>();
            volumen.isGlobal = true;
            volumen.priority = 100f;
            VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            ajuste = perfil.Add<ColorAdjustments>(true);
            volumen.sharedProfile = perfil;
        }
        // Exposicion en pasos de luz: el doble de brillo es +1.
        ajuste.postExposure.overrideState = true;
        ajuste.postExposure.value = Mathf.Log(Mathf.Clamp(Brillo, 0.5f, 1.5f), 2f);
        // La camara tiene que tener el postprocesado encendido para que se note.
        foreach (Camera c in Camera.allCameras)
        {
            UniversalAdditionalCameraData d = c.GetUniversalAdditionalCameraData();
            if (d != null) d.renderPostProcessing = true;
        }
    }
}
