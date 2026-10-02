using UnityEngine;

// Datos de los sellos elementales (Resources/PistasSellos): para cada elemento,
// su color, la runa que se graba cerca de cada sello, el sonido al reaccionar
// sin la imbuicion correcta y los textos. Editable en el Inspector.
[CreateAssetMenu(menuName = "Warrior/Pistas de los sellos", fileName = "PistasSellos")]
public class PistasSellos : ScriptableObject
{
    [System.Serializable]
    public class Entrada
    {
        public Elemento elemento;
        [Tooltip("Color del sello, de su runa, del brillo del suelo y de las grietas cercanas.")]
        public Color color = Color.white;
        [Tooltip("Runa grabada cerca del sello (segunda señal).")]
        public Sprite runa;
        [Tooltip("Sonido al golpearlo o acercarse sin la imbuicion correcta (clave de RecursosRPG).")]
        public string sonidoReaccion = "sombra_rebote";
        [Tooltip("Sonido al abrirse.")]
        public string sonidoAbrir = "sello_roto";
        [Tooltip("Texto al golpearlo con otro elemento (solo en los sellos de enseñanza).")]
        public string textoPista = "";
        [Tooltip("Texto al abrirse.")]
        public string textoAbrir = "";
    }

    public Entrada[] entradas = new Entrada[0];

    private static PistasSellos instancia;

    public static PistasSellos Get()
    {
        if (instancia == null) instancia = Resources.Load<PistasSellos>("PistasSellos");
        if (instancia == null) instancia = CreateInstance<PistasSellos>();
        return instancia;
    }

    public Entrada De(Elemento e)
    {
        foreach (Entrada x in entradas) if (x != null && x.elemento == e) return x;
        return new Entrada { elemento = e, color = Color.white };
    }
}
