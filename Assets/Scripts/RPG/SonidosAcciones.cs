using System;
using System.Collections.Generic;
using UnityEngine;

// Los sonidos de un personaje, accion por accion, para cambiarlos en el Inspector
// sin tocar codigo: que clips suenan en cada accion y a que volumen. Hay uno
// para el player (Assets/Data/Sonidos/Sonidos Player) y uno por jefe.
//
// Cada accion lleva su "clave" (el nombre con el que la pide el juego): no hay
// que cambiarla. Si una accion se deja sin clips, suena la de la biblioteca
// general (Resources/RecursosRPG).
[CreateAssetMenu(menuName = "Warrior/Sonidos de un personaje", fileName = "Sonidos")]
public class SonidosAcciones : ScriptableObject
{
    [Serializable]
    public class Accion
    {
        [Tooltip("Que es (solo para reconocerla).")]
        public string accion;
        [Tooltip("Nombre con el que la pide el juego. No cambiarlo.")]
        public string clave;
        [Tooltip("Uno o varios sonidos: si hay varios, se elige uno al azar cada vez.")]
        public AudioClip[] clips;
        [Range(0f, 1.5f)] public float volumen = 0.7f;
        [Tooltip("Variacion de tono al azar (0.05 = +-5 %), para que no suene siempre igual.")]
        [Range(0f, 0.3f)] public float variacionTono = 0.06f;
    }

    [Tooltip("Sube o baja todos los sonidos de este personaje a la vez.")]
    [Range(0f, 2f)] public float volumenGeneral = 1f;
    public List<Accion> acciones = new List<Accion>();

    private Dictionary<string, RecursosRPG.GrupoSonido> cache;

    // El grupo listo para sonar (con el volumen general aplicado), o null si esa
    // accion no esta o no tiene clips.
    public RecursosRPG.GrupoSonido Buscar(string clave)
    {
        if (cache == null)
        {
            cache = new Dictionary<string, RecursosRPG.GrupoSonido>();
            foreach (Accion a in acciones)
                if (a != null && !string.IsNullOrEmpty(a.clave) && a.clips != null && a.clips.Length > 0)
                    cache[a.clave] = new RecursosRPG.GrupoSonido
                    {
                        clave = a.clave, clips = a.clips, volumen = a.volumen * volumenGeneral, variacionTono = a.variacionTono,
                    };
        }
        cache.TryGetValue(clave, out RecursosRPG.GrupoSonido g);
        return g;
    }

    private void OnValidate() => cache = null;
}
