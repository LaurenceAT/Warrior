using UnityEngine;

// La Cazadora comenta lo que oye durante el combate: una frase corta como
// subtitulo, nunca varias seguidas. Solo texto: no cambia nada del combate.
//   - Bloquear muchas veces seguidas (sin atacar entre medias).
//   - Correr cerca de ella (su oido: OidoCazadora).
//   - Beber un frasco (su oido).
//   - Quedarse quieto y en silencio, lejos de ella.
//   - Golpearla varias veces seguidas con el mismo elemento.
// Tiempo minimo entre reacciones y tope por pelea en Ajustes Cazadora. No pisa
// otra frase suya en pantalla ni habla durante El Silencio. La pone ArenaCazadora
// al empezar el combate (en la jefa: se va con ella).
public class ReaccionesCazadora : MonoBehaviour
{
    private AjustesCazadora aj;
    private OidoCazadora oido;
    private EnemyHealth salud;
    private ArmaImbuida arma;
    private PlayerControler player;

    private float siguiente;
    private int dichas;
    private int bloqueos;
    private bool bloqueaba, atacaba, quietoDicho;
    private Elemento ultimoElemento = Elemento.Ninguno;
    private int golpesElemento;

    public void Configurar(AjustesCazadora ajustes)
    {
        aj = ajustes;
        siguiente = Time.time + (aj != null ? aj.reaccionIntervalo * 0.5f : 5f);
    }

    private void Start()
    {
        oido = GetComponent<OidoCazadora>();
        salud = GetComponent<EnemyHealth>();
        player = FindFirstObjectByType<PlayerControler>();
        if (player != null) arma = player.GetComponent<ArmaImbuida>();
        if (oido != null) oido.AlOir += Oyo;
        if (salud != null) salud.AlRecibirGolpe += Golpeada;
    }

    private void OnDestroy()
    {
        if (oido != null) oido.AlOir -= Oyo;
        if (salud != null) salud.AlRecibirGolpe -= Golpeada;
    }

    private void Update()
    {
        if (aj == null || oido == null || player == null || player.VidaActual <= 0) return;

        // Bloqueos seguidos: cuenta cada vez que levanta el escudo; atacar lo reinicia.
        bool bloquea = player.Bloqueando;
        if (bloquea && !bloqueaba) bloqueos++;
        bloqueaba = bloquea;
        bool ataca = player.Atacando;
        if (ataca && !atacaba) bloqueos = 0;
        atacaba = ataca;
        if (bloqueos >= Mathf.Max(1, aj.bloqueosSeguidos) && Decir(aj.reaccionBloqueo)) bloqueos = 0;

        // Quieto y en silencio, lejos de ella (de cerca te siente igual).
        bool lejos = Vector2.Distance(player.transform.position, transform.position) > aj.rangoCercano;
        bool quieto = Mathf.Abs(player.Velocidad.x) < 0.1f && player.EnSuelo && Time.time - oido.UltimoRuido >= aj.segundosQuieto;
        if (!quieto) quietoDicho = false;
        else if (lejos && !quietoDicho && Decir(aj.reaccionQuieto)) quietoDicho = true;
    }

    private void Oyo(OidoCazadora.Tipo tipo, Vector2 donde)
    {
        if (aj == null) return;
        if (tipo == OidoCazadora.Tipo.Frasco) Decir(aj.reaccionFrasco);
        else if (tipo == OidoCazadora.Tipo.Correr && Vector2.Distance(donde, transform.position) <= aj.distanciaCorrer) Decir(aj.reaccionCorrer);
    }

    // Solo los espadazos (no las quemaduras ni el parry, que tambien la "golpean").
    private void Golpeada()
    {
        if (aj == null || player == null || !player.Atacando) return;
        Elemento e = arma != null ? arma.Activo : Elemento.Ninguno;
        if (e == Elemento.Ninguno) { ultimoElemento = e; golpesElemento = 0; return; }
        golpesElemento = e == ultimoElemento ? golpesElemento + 1 : 1;
        ultimoElemento = e;
        if (golpesElemento >= Mathf.Max(1, aj.golpesMismoElemento) && Decir(aj.ReaccionElemento(e))) golpesElemento = 0;
    }

    // Dice una frase si toca (pausa minima, tope, nada mas en pantalla).
    private bool Decir(string[] lista)
    {
        if (Time.time < siguiente || dichas >= aj.reaccionTope || UICazadora.SubtituloActivo) return false;
        if (oido != null && oido.Atento) return false;   // El Silencio
        string f = AjustesCazadora.Una(lista);
        if (f == null) return false;
        UICazadora.Subtitulo(f, aj.segundosReaccion);
        dichas++;
        siguiente = Time.time + aj.reaccionIntervalo;
        return true;
    }
}
