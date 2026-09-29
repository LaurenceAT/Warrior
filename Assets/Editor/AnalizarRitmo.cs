using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Mide, para cada pista de musica de jefe, lo "frenetica" que es: cuantos golpes
// (subidas bruscas de energia) tiene por segundo y lo fuerte que suena. Deja el
// resultado en "AnalisisRitmo.txt", junto al proyecto. Sirve para elegir la
// musica de cada fase de un jefe sin tener que escucharlas todas.
public static class AnalizarRitmo
{
    private const string Raiz = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_MusicaBosses";

    [MenuItem("Warrior/Analizar ritmo de la musica de jefes")]
    public static void Analizar()
    {
        string[] rutas = Directory.GetFiles(Raiz, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ogg") || f.EndsWith(".mp3") || (f.EndsWith(".wav") && !File.Exists(Path.ChangeExtension(f, ".ogg"))))
            .Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("pista | duracion | volumen medio (dB) | golpes por segundo | tramo mas intenso (s)");
        foreach (string ruta in rutas)
        {
            AudioImporter imp = (AudioImporter)AssetImporter.GetAtPath(ruta);
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            AudioClipLoadType previo = s.loadType;
            if (previo != AudioClipLoadType.DecompressOnLoad) { s.loadType = AudioClipLoadType.DecompressOnLoad; imp.defaultSampleSettings = s; imp.SaveAndReimport(); }
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null || !clip.LoadAudioData()) { sb.AppendLine("ERROR " + ruta); continue; }
            float[] d = new float[clip.samples * clip.channels];
            clip.GetData(d, 0);
            int ventana = Mathf.Max(1, Mathf.RoundToInt(0.023f * clip.frequency)) * clip.channels;
            int n = d.Length / ventana;
            float[] e = new float[n];
            for (int i = 0; i < n; i++)
            {
                double suma = 0;
                for (int k = i * ventana; k < (i + 1) * ventana; k++) suma += d[k] * d[k];
                e[i] = (float)(suma / ventana);
            }
            // Golpes: la energia salta por encima de 1.6 veces la media reciente.
            int golpes = 0;
            float media = e.Length > 0 ? e.Average() : 0f;
            for (int i = 20; i < n; i++)
            {
                float reciente = 0f;
                for (int k = i - 20; k < i; k++) reciente += e[k];
                reciente /= 20f;
                if (e[i] > reciente * 1.6f && e[i] > media * 0.3f && e[i] > e[i - 1]) { golpes++; i += 4; }
            }
            float gps = golpes / Mathf.Max(1f, clip.length);
            float db = 10f * Mathf.Log10(Mathf.Max(media, 1e-10f));
            // Tramo de 20 s mas intenso.
            int tramo = Mathf.RoundToInt(20f / 0.023f);
            float mejor = 0f; int donde = 0;
            for (int i = 0; i + tramo < n; i += tramo / 4)
            {
                float suma = 0f;
                for (int k = i; k < i + tramo; k++) suma += e[k];
                if (suma > mejor) { mejor = suma; donde = i; }
            }
            sb.AppendLine($"{Path.GetFileName(ruta)} | {clip.length:0}s | {db:0.0} | {gps:0.00} | {donde * 0.023f:0}");
            if (previo != AudioClipLoadType.DecompressOnLoad) { s.loadType = previo; imp.defaultSampleSettings = s; imp.SaveAndReimport(); }
        }
        File.WriteAllText("AnalisisRitmo.txt", sb.ToString());
        Debug.Log("[Ritmo] Escrito AnalisisRitmo.txt");
    }
}
