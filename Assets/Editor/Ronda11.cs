using System.Linq;
using UnityEditor;
using UnityEngine;

// Undecima ronda: el nuevo parry de la Cazadora, el escudo que se rompe a golpes
// y los sonidos nuevos (Sonidos_Efectos/Sonidos_nuevos).
//   - Ajustes Cazadora: los tiempos del parry y los escudos de cada fase pasan a
//     los valores nuevos (lo demas no se toca).
//   - Sonidos Jefe Cazadora: los sustitutos se cambian por los sonidos nuevos.
// Warrior > Actualizar > Ronda 11 (o ActualizarProyecto.Ronda11).
public static class Ronda11
{
    private const string Nuevos = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_Efectos/Sonidos_nuevos/";

    [MenuItem("Warrior/Actualizar/Ronda 11 (Cazadora: parry, escudo y sonidos)")]
    public static void Todo()
    {
        AjustesCazadora a = AssetDatabase.LoadAssetAtPath<AjustesCazadora>("Assets/Data/Jefes/Ajustes Cazadora.asset");
        if (a == null) { Debug.LogError("[Ronda11] Falta Ajustes Cazadora (ejecuta antes la Ronda 10)."); return; }
        AjustesCazadora nuevo = ScriptableObject.CreateInstance<AjustesCazadora>();
        a.aturdimientoJugador = nuevo.aturdimientoJugador;
        a.esperaEstocada = nuevo.esperaEstocada;
        a.escudos = nuevo.escudos;
        a.sonidoEscritura = Clip("sonidos de escritura");
        a.volumenEscritura = 0.35f;
        Object.DestroyImmediate(nuevo);
        EditorUtility.SetDirty(a);

        SonidosAcciones s = AssetDatabase.LoadAssetAtPath<SonidosAcciones>("Assets/Data/Sonidos/Sonidos Jefe Cazadora.asset");
        if (s != null)
        {
            Poner(s, "latido", "Latido", 0.8f, Clip("latido"));
            Poner(s, "escudo_golpe", "Golpe contra el escudo", 0.6f, Clip("golpes a cristal"));
            Poner(s, "escudo_golpe_oscuro", "Golpe con oscuridad contra el escudo (el cristal, mas grave)", 0.75f, Clip("golpes a cristal"));
            Poner(s, "escudo_roto", "Escudo roto", 0.9f, Clip("cristal roto"));
            // La katana se suma a los tajos que ya habia (suena uno al azar).
            SonidosAcciones.Accion tajo = s.acciones.FirstOrDefault(x => x.clave == "tajo");
            AudioClip katana = Clip("sonido_katana");
            if (tajo != null && katana != null && !tajo.clips.Contains(katana)) tajo.clips = tajo.clips.Concat(new[] { katana }).ToArray();
            // El rugido sigue siendo un sustituto (no hay rugido en la libreria): mas bajo.
            SonidosAcciones.Accion rugido = s.acciones.FirstOrDefault(x => x.clave == "rugido");
            if (rugido != null) { rugido.volumen = 0.6f; rugido.accion = "Rugido (SUSTITUTO: no hay rugido en la libreria)"; }
            EditorUtility.SetDirty(s);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda11] Listo.");
    }

    private static void Poner(SonidosAcciones s, string clave, string que, float volumen, AudioClip clip)
    {
        if (clip == null) return;
        SonidosAcciones.Accion a = s.acciones.FirstOrDefault(x => x.clave == clave);
        if (a == null) { a = new SonidosAcciones.Accion { clave = clave }; s.acciones.Add(a); }
        a.accion = que;
        a.clips = new[] { clip };
        a.volumen = volumen;
        a.variacionTono = 0.05f;
    }

    private static AudioClip Clip(string nombre)
    {
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(Nuevos + nombre + ".mp3");
        if (c == null) Debug.LogWarning("[Ronda11] No encuentro " + nombre);
        return c;
    }
}
