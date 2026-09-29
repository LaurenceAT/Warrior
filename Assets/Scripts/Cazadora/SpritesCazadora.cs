using System;
using System.Collections.Generic;
using UnityEngine;

// Las animaciones de la Cazadora ya cortadas (las prepara el editor, Ronda10).
// Cada cuadro viene en dos capas sacadas de la hoja original:
//   - cuerpo: ella (no se tine nunca).
//   - efecto: lo blanco (tajos, olas, estelas), que se tine del elemento.
// Tambien guarda, por cuadro, la caja que ocupa el efecto (en unidades, desde el
// pivote y mirando a la derecha): es la caja de dano de ese cuadro, asi que el
// golpe coincide exactamente con el tajo dibujado.
[CreateAssetMenu(menuName = "Warrior/Sprites de la Cazadora", fileName = "Sprites Cazadora")]
public class SpritesCazadora : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public string nombre;
        public Sprite[] cuerpo = new Sprite[0];
        public Sprite[] efecto = new Sprite[0];
        public float fps = 12f;
        public bool bucle;
        [Tooltip("Caja del efecto en cada cuadro (ancho 0 = sin efecto).")]
        public Rect[] cajas = new Rect[0];
        [Tooltip("Cuadros en los que el tajo hace dano (los de efecto grande).")]
        public bool[] activos = new bool[0];

        public int Cantidad => cuerpo != null ? cuerpo.Length : 0;
        public float Duracion => fps > 0f ? Cantidad / fps : 0f;
        public bool Activo(int f) => activos != null && f >= 0 && f < activos.Length && activos[f];
        public Rect Caja(int f) => cajas != null && f >= 0 && f < cajas.Length ? cajas[f] : default;

        // Primer y ultimo cuadro activo (el tajo), o -1.
        public int PrimerActivo { get { for (int i = 0; i < Cantidad; i++) if (Activo(i)) return i; return -1; } }
        public int UltimoActivo { get { for (int i = Cantidad - 1; i >= 0; i--) if (Activo(i)) return i; return -1; } }
    }

    public List<Clip> clips = new List<Clip>();
    [Tooltip("La silueta blanca (Hit): el destello al recibir dano cuando esta quieta.")]
    public Sprite golpe;
    [Tooltip("Pixeles por unidad (el mismo que el player: 28).")]
    public float pixelesPorUnidad = 28f;

    private Dictionary<string, Clip> porNombre;

    public Clip Buscar(string nombre)
    {
        if (porNombre == null || porNombre.Count != clips.Count)
        {
            porNombre = new Dictionary<string, Clip>();
            foreach (Clip c in clips) if (c != null && !string.IsNullOrEmpty(c.nombre)) porNombre[c.nombre] = c;
        }
        porNombre.TryGetValue(nombre, out Clip r);
        return r;
    }

    private void OnValidate() => porNombre = null;
}
