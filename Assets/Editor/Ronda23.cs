using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 23: aligerar el juego para itch.io (web). Solo cambia ajustes de
// importacion (los .meta): no toca ningun sonido ni imagen original.
//   - Audio: lo que usa el juego (escenas, Resources, Data y Prefabs) se
//     importa a 44,1 kHz y con menos calidad: musica larga al 60 %, ambientes y
//     efectos al 70 %, y los efectos cortos en mono (el juego es 2D).
//     Sin "Streaming": en la web no existe y Unity lo trata como comprimido.
//   - death.png de la Sombra (8151 de ancho): se comprueba que su limite sea 8192
//     (ya lo era: nunca estuvo reducido, no cambia nada).
//   - Las imagenes NO se comprimen: son pixel art y se estropearian.
// Para deshacerlo: git checkout de los .meta cambiados (o Reimport con los
// valores de antes). Menu Warrior > Web > Aligerar audio (Ronda 23).
public static class Ronda23
{
    // Segundos: por encima de "musica" es una pista larga; por debajo de "corto", un efecto breve.
    private const float Musica = 25f, Ambiente = 6f, Corto = 2.5f;
    private const float CalidadMusica = 0.6f, CalidadAmbiente = 0.7f, CalidadEfecto = 0.7f;

    [MenuItem("Warrior/Web/Aligerar audio (Ronda 23)")]
    public static void Aplicar()
    {
        var raices = new List<string>();
        raices.AddRange(EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path));
        foreach (string carpeta in new[] { "Assets/Resources", "Assets/Data", "Assets/Prefabs" })
            if (Directory.Exists(carpeta)) raices.AddRange(AssetDatabase.FindAssets("", new[] { carpeta }).Select(AssetDatabase.GUIDToAssetPath));
        string[] usados = AssetDatabase.GetDependencies(raices.Distinct().ToArray(), true)
            .Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AudioClip)).Distinct().ToArray();

        long antes = 0, despues = 0;
        int musica = 0, ambiente = 0, efectos = 0, cambiados = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string ruta in usados)
            {
                AudioImporter imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
                if (imp == null || clip == null) continue;
                antes += new FileInfo(ruta).Length;

                float largo = clip.length;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                float calidad;
                if (largo >= Musica) { musica++; calidad = CalidadMusica; }
                else if (largo >= Ambiente) { ambiente++; calidad = CalidadAmbiente; }
                else { efectos++; calidad = CalidadEfecto; }

                s.quality = calidad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                // Sin Streaming (en la web no existe). Los efectos muy cortos se descomprimen al
                // cargar (sin retraso al sonar); lo demas queda comprimido en memoria.
                s.loadType = largo < 0.6f ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 44100;

                bool mono = largo < Corto && clip.channels > 1;
                bool cambia = imp.forceToMono != (mono || imp.forceToMono)
                              || !Mathf.Approximately(imp.defaultSampleSettings.quality, s.quality)
                              || imp.defaultSampleSettings.loadType != s.loadType
                              || imp.defaultSampleSettings.sampleRateSetting != s.sampleRateSetting
                              || imp.defaultSampleSettings.sampleRateOverride != s.sampleRateOverride;
                if (mono) imp.forceToMono = true;
                imp.defaultSampleSettings = s;
                // Un override de WebGL (si lo hubiera) no debe pisar estos valores.
                imp.ClearSampleSettingOverride("WebGL");
                if (cambia) { cambiados++; imp.SaveAndReimport(); }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        Debug.Log($"[Ronda23] Audio usado por el juego: {usados.Length} sonidos ({musica} pistas largas, {ambiente} ambientes, {efectos} efectos). Reimportados: {cambiados}. Peso de los originales: {antes / 1048576f:0.0} MB (no cambian).");
        DeathSombra();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda23] Listo.");
    }

    // death.png de la Sombra: 8151 x 87. Comprueba que se importe a 8192 (sin reducir).
    private static void DeathSombra()
    {
        const string ruta = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Boss_ShadowedWetlands/Sprite Sheet/new/death.png";
        TextureImporter t = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (t == null) { Debug.LogWarning("[Ronda23] No se encuentra " + ruta); return; }
        bool cambia = t.maxTextureSize != 8192;
        t.maxTextureSize = 8192;
        foreach (string plataforma in new[] { "Standalone", "WebGL" })
        {
            TextureImporterPlatformSettings p = t.GetPlatformTextureSettings(plataforma);
            if (p.overridden && p.maxTextureSize < 8192) { p.maxTextureSize = 8192; t.SetPlatformTextureSettings(p); cambia = true; }
        }
        if (cambia) t.SaveAndReimport();
        Debug.Log("[Ronda23] death.png de la Sombra: limite 8192 " + (cambia ? "(cambiado)" : "(ya estaba asi; sin cambios)"));
    }
}
