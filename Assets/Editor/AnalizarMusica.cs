using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Mide la intensidad de las pistas de musica de jefe a lo largo del tiempo (RMS
// por tramos de 2 s) y lo deja en "AnalisisMusica.txt", junto al proyecto. Sirve
// para elegir que pista (y que tramo) va en cada fase de un jefe: la fase 1 en
// un tramo contenido y la 2 en el climax.
public static class AnalizarMusica
{
    private const string Raiz = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_MusicaBosses";
    private const float Tramo = 2f;

    [MenuItem("Warrior/Analizar musica de jefes")]
    public static void Analizar()
    {
        string[] rutas = Directory.GetFiles(Raiz, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ogg") || f.EndsWith(".mp3"))
            .Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();

        StringBuilder sb = new StringBuilder();
        foreach (string ruta in rutas)
        {
            AudioImporter imp = (AudioImporter)AssetImporter.GetAtPath(ruta);
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            AudioClipLoadType previo = s.loadType;
            if (previo != AudioClipLoadType.DecompressOnLoad)
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null || !clip.LoadAudioData()) { sb.AppendLine("ERROR " + ruta); continue; }

            float[] datos = new float[clip.samples * clip.channels];
            clip.GetData(datos, 0);
            int porTramo = Mathf.RoundToInt(Tramo * clip.frequency) * clip.channels;
            var niveles = new System.Collections.Generic.List<float>();
            for (int i = 0; i + porTramo <= datos.Length; i += porTramo)
            {
                double suma = 0;
                for (int k = i; k < i + porTramo; k++) suma += datos[k] * datos[k];
                float rms = Mathf.Sqrt((float)(suma / porTramo));
                niveles.Add(20f * Mathf.Log10(Mathf.Max(rms, 1e-5f)));
            }

            float media = niveles.Count > 0 ? niveles.Average() : 0f;
            float max = niveles.Count > 0 ? niveles.Max() : 0f;
            sb.AppendLine($"## {Path.GetFileName(ruta)}  dur={clip.length:0}s  media={media:0.0}dB  max={max:0.0}dB");
            // Una fila por tramo: segundo inicial y barra de intensidad.
            for (int i = 0; i < niveles.Count; i++)
            {
                int barra = Mathf.Clamp(Mathf.RoundToInt((niveles[i] + 40f) * 1.2f), 0, 60);
                sb.AppendLine($"{i * Tramo,5:0} {niveles[i],6:0.0} {new string('#', barra)}");
            }
            sb.AppendLine();

            clip.UnloadAudioData();
            if (previo != AudioClipLoadType.DecompressOnLoad)
            {
                s.loadType = previo;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "AnalisisMusica.txt"), sb.ToString());
        Debug.Log("[Musica] Analisis guardado en AnalisisMusica.txt");
    }
}
