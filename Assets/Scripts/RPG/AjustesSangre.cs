using UnityEngine;

// Numeros y dibujos de la sangre (Resources/AjustesSangre). Se editan en el
// Inspector; los cambios se notan al darle a Play. Ver SangreFx.
[CreateAssetMenu(menuName = "Warrior/Ajustes de sangre", fileName = "AjustesSangre")]
public class AjustesSangre : ScriptableObject
{
    [Header("Dibujos (los pone la Ronda 8)")]
    [Tooltip("Animaciones de salpicadura (en blanco: se tinen del color de cada uno).")]
    public AnimadorHoja.Clip[] salpicaduras;
    [Tooltip("Manchas del suelo (en blanco).")]
    public Sprite[] manchas;

    [Header("Tu muerte")]
    public bool sangreMuerte = true;
    public Color colorPlayer = new Color(0.62f, 0.02f, 0.05f, 0.95f);
    [Tooltip("Manchas que deja cada muerte.")]
    public int manchasMuertePlayer = 3;
    [Tooltip("Limite de manchas por nivel: al pasarlo, las mas antiguas se desvanecen.")]
    public int maximoManchasPorNivel = 45;
    public float tamanoMuertePlayer = 1.1f;

    [Header("Golpes que recibes (solo gotas, sin mancha)")]
    public bool gotasPlayer = true;
    [Tooltip("Gotas de un golpe normal (los fuertes, mas).")]
    public int gotasPlayerCantidad = 7;
    [Tooltip("Multiplica la cantidad y la fuerza de las gotas.")]
    [Range(0f, 2f)] public float gotasPlayerIntensidad = 1f;

    [Header("Enemigos (si tienen sangre: EnemyHealth > Sangre)")]
    public bool sangreEnemigos = true;
    [Tooltip("Tamano de la salpicadura de cada golpe.")]
    public float tamanoGolpe = 0.55f;
    public int gotasGolpe = 4;
    public float tamanoMuerteEnemigo = 0.95f;
    [Tooltip("Manchas (como mucho) que deja al morir.")]
    public int manchasMuerteEnemigo = 2;
    [Tooltip("Segundos que duran sus manchas antes de desvanecerse.")]
    public float segundosManchaEnemigo = 10f;

    [Header("Manchas")]
    [Tooltip("Tamano de las manchas del suelo.")]
    public float tamanoMancha = 1f;

    private static AjustesSangre instancia;

    public static AjustesSangre Get()
    {
        if (instancia == null)
        {
            instancia = Resources.Load<AjustesSangre>("AjustesSangre");
            if (instancia == null) instancia = CreateInstance<AjustesSangre>();
        }
        return instancia;
    }
}
