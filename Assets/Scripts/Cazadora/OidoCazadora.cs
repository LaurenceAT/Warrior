using UnityEngine;

// El oido de la Cazadora (es ciega). Escucha lo que hace el player y recuerda
// DONDE sono el ultimo ruido: de lejos, sus ataques van ahi, no a donde estas.
//   - Ruidos: atacar, rodar, aterrizar, saltar, correr, beber un frasco y
//     cambiar de imbuicion. Cada uno con su volumen (AjustesCazadora).
//   - Un ruido se oye si esta a menos de volumen x alcance.
//   - De cerca (rangoCercano) siente tus pasos: sabe donde estas siempre.
// Cuando te oye, un anillo pequeno aparece donde sono el ruido: es la pista de
// que la mecanica existe.
public class OidoCazadora : MonoBehaviour
{
    public enum Tipo { Atacar, Rodar, Aterrizar, Saltar, Correr, Frasco, Imbuir }

    // Se lanza con cada ruido oido (El Silencio lo escucha).
    public event System.Action<Tipo, Vector2> AlOir;

    private AjustesCazadora aj;
    private PlayerControler player;
    private ArmaImbuida arma;
    private bool atacaba, esquivaba, enSuelo = true;
    private float siguientePaso, caidaMax;

    public Vector2 UltimaPosicion { get; private set; }
    public float UltimoRuido { get; private set; } = -99f;
    public bool Sordo { get; set; }
    // El Silencio: oye cualquier ruido de la arena, este donde este.
    public bool Atento { get; set; }

    public void Configurar(AjustesCazadora ajustes) => aj = ajustes;

    private void OnEnable()
    {
        ReservaPociones.AlBeber += Bebio;
    }

    private void OnDisable()
    {
        ReservaPociones.AlBeber -= Bebio;
        if (arma != null) arma.AlCambiar -= Imbuyo;
    }

    private PlayerControler Player
    {
        get
        {
            if (player != null) return player;
            player = FindFirstObjectByType<PlayerControler>();
            if (player != null)
            {
                if (arma != null) arma.AlCambiar -= Imbuyo;
                arma = player.GetComponent<ArmaImbuida>();
                if (arma != null) arma.AlCambiar += Imbuyo;
                UltimaPosicion = player.transform.position;
                enSuelo = player.EnSuelo;
            }
            return player;
        }
    }

    // A donde apuntar: de cerca, a ti; de lejos, al ultimo ruido.
    public Vector2 Objetivo(Vector2 desde)
    {
        PlayerControler p = Player;
        if (p == null) return UltimaPosicion;
        Vector2 real = p.transform.position;
        if (Vector2.Distance(desde, real) <= aj.rangoCercano) { UltimaPosicion = real; UltimoRuido = Time.time; }
        return UltimaPosicion;
    }

    // Te tiene "localizado": te oyo hace poco o estas cerca.
    public bool Localizado(Vector2 desde)
    {
        PlayerControler p = Player;
        if (p == null) return false;
        return Vector2.Distance(desde, p.transform.position) <= aj.rangoCercano || Time.time - UltimoRuido < aj.olvido;
    }

    private void Update()
    {
        PlayerControler p = Player;
        if (p == null || aj == null || p.VidaActual <= 0) return;

        bool ataca = p.Atacando;
        if (ataca && !atacaba) Oir(Tipo.Atacar, aj.ruidoAtacar);
        atacaba = ataca;

        bool esquiva = p.Esquivando;
        if (esquiva && !esquivaba) Oir(Tipo.Rodar, aj.ruidoRodar);
        esquivaba = esquiva;

        bool suelo = p.EnSuelo;
        if (!suelo) caidaMax = Mathf.Min(caidaMax, p.Velocidad.y);
        if (suelo && !enSuelo && caidaMax < -4f) Oir(Tipo.Aterrizar, aj.ruidoAterrizar);
        if (!suelo && enSuelo && p.Velocidad.y > 1f) Oir(Tipo.Saltar, aj.ruidoSaltar);
        if (suelo) caidaMax = 0f;
        enSuelo = suelo;

        if (suelo && p.Corriendo && Mathf.Abs(p.Velocidad.x) > 1f && Time.time >= siguientePaso)
        {
            siguientePaso = Time.time + 0.35f;
            Oir(Tipo.Correr, aj.ruidoCorrer);
        }
    }

    private void Bebio() => Oir(Tipo.Frasco, aj != null ? aj.ruidoFrasco : 1f);
    private void Imbuyo() => Oir(Tipo.Imbuir, aj != null ? aj.ruidoImbuir : 1f);

    private void Oir(Tipo tipo, float volumen)
    {
        PlayerControler p = Player;
        if (p == null || aj == null || Sordo) return;
        Vector2 donde = p.transform.position;
        if (!Atento && Vector2.Distance(transform.position, donde) > volumen * aj.alcanceOido) return;
        UltimaPosicion = donde;
        UltimoRuido = Time.time;
        if (aj.anilloRuido) AnilloRuido.Mostrar(donde + Vector2.down * 0.45f, volumen);
        AlOir?.Invoke(tipo, donde);
    }
}
