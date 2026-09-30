using UnityEngine;

// Ficha de un enemigo normal (Assets/Data/Enemigos). Se disena por golpes: la
// vida y el dano reales los calcula AjustesEnemigos con el poder esperado del
// player en su zona. Los valores "manual" mandan sobre el calculo (casos
// especiales). La lleva EnemyHealth y la aplica al despertar.
[CreateAssetMenu(menuName = "Warrior/Enemigo", fileName = "Enemigo")]
public class DefinicionEnemigo : ScriptableObject
{
    public string nombre = "Enemigo";
    [Range(1, 3)] public int zona = 1;
    public RolEnemigo rol = RolEnemigo.Comun;

    [Header("Diseno por golpes (0 = el de la tabla de su zona y rol)")]
    [Tooltip("Golpes de espada (media del combo) que aguanta.")]
    public float golpesParaMatar;
    [Tooltip("Parte de tu vida maxima esperada que quita su golpe principal, en %.")]
    public float porcentajeDano;

    [Header("Elementos")]
    public Afinidad fuego;
    public Afinidad hielo;
    public Afinidad oscuro;
    public Afinidad sagrado;
    public Afinidad sangrado;

    [Header("Almas y tamano")]
    [Tooltip("-1 = las de la tabla de su zona y rol.")]
    public int almas = -1;
    [Tooltip("Escala del enemigo (1 = como esta dibujado). Escala todo: dibujo, cuerpo, golpes y barra.")]
    public float escala = 1f;

    [Header("Valores manuales (0 = calculado)")]
    public int vidaManual;
    public int danoManual;

    [Header("Elite")]
    [Tooltip("Nombre propio que sale en su barra y en el titulo al verlo (vacio = uno de la lista de AjustesEnemigos).")]
    public string nombrePropio;

    [Header("Variante de color")]
    [Tooltip("Giro del tono en grados (0 = colores originales).")]
    [Range(-180f, 180f)] public float tono;
    [Tooltip("Saturacion (1 = igual).")]
    [Range(0f, 2f)] public float saturacion = 1f;
    [Tooltip("Brillo (1 = igual).")]
    [Range(0.5f, 1.5f)] public float brillo = 1f;

    private static AjustesEnemigos A => AjustesEnemigos.Get();
    public AjustesEnemigos.Zona Zona => A.ZonaN(zona);
    public AjustesEnemigos.Rol Rol => A.RolDe(rol);
    public bool EsElite => rol == RolEnemigo.Elite;
    public bool EsVariante => Mathf.Abs(tono) > 0.5f || Mathf.Abs(saturacion - 1f) > 0.01f || Mathf.Abs(brillo - 1f) > 0.01f;

    // Golpes que debe aguantar: el suyo o el punto medio del rango de la tabla,
    // redondeado hacia arriba (5-6 -> 6).
    public float Golpes => golpesParaMatar > 0f ? golpesParaMatar : Mathf.Ceil((Zona.Golpes(rol).x + Zona.Golpes(rol).y) * 0.5f - 0.01f);

    public float Porcentaje => porcentajeDano > 0f ? porcentajeDano : Zona.Dano(rol);

    // Vida para morir justo al golpe N con el dano medio esperado: N - 0.3 golpes
    // (asi un golpe algo mas flojo no obliga a dar uno de mas).
    public int Vida
    {
        get
        {
            float v = vidaManual > 0 ? vidaManual : (Golpes - 0.3f) * Zona.DanoJugador;
            return Mathf.Max(1, Mathf.RoundToInt(v * Mathf.Max(0.05f, A.multiplicadorVida)));
        }
    }

    // Dano del golpe principal; los demas ataques lo multiplican por su peso.
    public int DanoBase
    {
        get
        {
            float d = danoManual > 0 ? danoManual : Porcentaje * 0.01f * Zona.VidaJugador;
            return Mathf.Max(1, Mathf.RoundToInt(d * Mathf.Max(0.05f, A.multiplicadorDano)));
        }
    }

    public int Almas => Mathf.Max(0, Mathf.RoundToInt((almas >= 0 ? almas : Zona.Almas(rol)) * Mathf.Max(0f, A.multiplicadorAlmas)));

    public Afinidad AfinidadDe(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return fuego;
            case Elemento.Hielo: return hielo;
            case Elemento.Oscuro: return oscuro;
            case Elemento.Sagrado: return sagrado;
            case Elemento.Sangrado: return sangrado;
            default: return Afinidad.Normal;
        }
    }

    public float Multiplicador(Elemento e)
    {
        switch (AfinidadDe(e))
        {
            case Afinidad.Debil: return Zona.debilidad;
            case Afinidad.Resiste: return Zona.resistencia;
            case Afinidad.Inmune: return 0f;
            default: return 1f;
        }
    }

    // Su nombre propio, o uno fijo de la lista (siempre el mismo para esta ficha).
    public string NombreElite
    {
        get
        {
            if (!string.IsNullOrEmpty(nombrePropio)) return nombrePropio;
            string[] l = A.nombresElite;
            if (l == null || l.Length == 0) return nombre;
            int h = 0;
            foreach (char c in name) h = h * 31 + c;
            return l[Mathf.Abs(h) % l.Length];
        }
    }

    // Golpes del player para matarlo y golpes suyos (principal) para matar al
    // player, con el poder esperado de su zona. Lo usan el informe y la depuracion.
    public int GolpesParaMatarlo => Mathf.CeilToInt(Vida / Mathf.Max(0.01f, Zona.DanoJugador) - 0.001f);
    public int GolpesParaMorir => Mathf.CeilToInt(Zona.VidaJugador / Mathf.Max(1f, DanoBase) - 0.001f);
}
