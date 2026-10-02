using System.Collections;
using UnityEngine;

// Rata de escarcha: pequena, rapida y en grupo. Golpea y se retira.
//   - Mordisco: pegado al player, un aviso muy corto y una dentellada.
//   - Salto: a media distancia se agacha (aviso) y salta encima del player.
// En manada: la que te descubre chilla y avisa a las demas.
// Tras atacar se aparta un poco (golpea y huye), asi que conviene esperarla con
// un parry o castigarla al aterrizar. Debil al fuego; resiste la escarcha.
public class EnemigoRata : EnemigoBase
{
    [SerializeField] private float velocidadPatrulla = 1.4f;
    [SerializeField] private float velocidadCarrera = 5f;
    [Tooltip("Peso del mordisco (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoMordisco = 1f;
    [SerializeField] private float pesoSalto = 1.33f;
    [Tooltip("Altura del salto, en unidades (el player mide 1).")]
    [SerializeField] private float alturaSalto = 1.1f;
    [SerializeField] private float rangoMordisco = 1.1f;
    [SerializeField] private Vector2 rangoSalto = new Vector2(2.6f, 4.6f);
    [SerializeField] private Vector2 enfriamiento = new Vector2(0.8f, 1.5f);
    [Tooltip("Al descubrir al player avisa a las ratas a esta distancia.")]
    [SerializeField] private float radioManada = 7f;

    [Header("Tacticas")]
    [Tooltip("Si otro enemigo ya te ataca de frente, salta por encima de ti para atacarte por la espalda.")]
    [SerializeField] private bool flanquear;
    [Tooltip("Con poca vida huye, avisa a otros y vuelve con ellos (una vez).")]
    [SerializeField] private bool retirarse;
    [Range(0.1f, 0.8f)] [SerializeField] private float vidaRetirada = 0.35f;
    [Tooltip("Reacciona a la imbuicion del player: con escarcha (la resiste) se crece; con fuego (su debilidad) se vuelve cauta y guarda distancia.")]
    [SerializeField] private bool reaccionImbuicion = true;

    private float listoPara, siguienteFlanqueo;
    private bool retiradaHecha;
    private Elemento imbuicionVista = Elemento.Ninguno;

    // Lo que hace ahora por la imbuicion del player (para la depuracion y las pruebas).
    public string Actitud { get; private set; } = "normal";
    public bool Flanquea => flanquear;
    public bool SeRetira => retirarse;
    public int Flanqueos { get; private set; }
    public bool Retirada => retiradaHecha;

    private Elemento ImbuicionPlayer()
    {
        if (player == null) return Elemento.Ninguno;
        ArmaImbuida a = player.GetComponent<ArmaImbuida>();
        return a != null ? a.Activo : Elemento.Ninguno;
    }

    // Escarcha: se crece (ataca antes y no se retira). Fuego: cauta.
    private void MirarImbuicion()
    {
        if (!reaccionImbuicion) return;
        Elemento e = ImbuicionPlayer();
        if (e == imbuicionVista) return;
        imbuicionVista = e;
        string nueva = e == Elemento.Hielo ? "crecida" : e == Elemento.Fuego ? "cauta" : "normal";
        if (nueva == Actitud) return;
        Actitud = nueva;
        if (nueva == "crecida") { Sonido.Reproducir("rata_chillido", 0.5f, 0.85f); Deformar(1.15f, 0.9f); }
        else if (nueva == "cauta") { Deformar(0.9f, 0.85f); listoPara = Mathf.Max(listoPara, Time.time + 0.6f); }
    }

    // La imbuicion se mira siempre que este en alerta (tambien a mitad de un ataque).
    protected override void Update()
    {
        base.Update();
        if (Alerta && !muerto) MirarImbuicion();
    }

    private float Enfriamiento => Random.Range(enfriamiento.x, enfriamiento.y) * (Actitud == "crecida" ? 0.5f : Actitud == "cauta" ? 1.5f : 1f);

    // Otro enemigo esta entre esta rata y el player, pegado a el.
    private bool OtroDeFrente()
    {
        foreach (EnemigoBase o in Activos)
        {
            if (o == this || o == null || !o.Alerta) continue;
            float dxo = player.position.x - o.transform.position.x;
            if (Mathf.Sign(dxo) == Mathf.Sign(DxPlayer) && Mathf.Abs(dxo) < Mathf.Abs(DxPlayer) && Mathf.Abs(dxo) < 2.5f) return true;
        }
        return false;
    }

    protected override IEnumerator Cerebro()
    {
        float giro = Random.Range(1.5f, 3f);
        while (true)
        {
            Vector2 ojos = Alto(0.3f);
            if (!VerPlayer(ojos))
            {
                giro -= Time.deltaTime;
                if (giro <= 0f || HayParedDelante(0.5f, 0.2f) || !HaySueloDelante(0.4f) || (LejosDeZona && mirada != HaciaZona)) { Mirar(-mirada); giro = Random.Range(1.5f, 3f); }
                anim.Reproducir("andar", false, 0.6f);
                Andar(velocidadPatrulla);
                yield return null;
                continue;
            }

            MirarAlPlayer();
            MirarImbuicion();
            float dx = Mathf.Abs(DxPlayer);
            float dy = player.position.y - transform.position.y;
            bool listo = Time.time >= listoPara && Mathf.Abs(dy) < 1.5f;

            // Con poca vida: huye, avisa y vuelve con otros.
            if (retirarse && !retiradaHecha && salud.CurrentHealth > 0 && salud.CurrentHealth <= salud.MaxHealth * vidaRetirada)
            { yield return Huir(); continue; }
            // Flanqueo: si otro ya le ataca de frente, salta por encima y ataca por detras.
            if (flanquear && listo && PuedeSaltar && Time.time >= siguienteFlanqueo && dx < 4.5f && dx > 1f && OtroDeFrente())
            { yield return Flanquear(); continue; }
            // Cauta (fuego): guarda distancia hasta que puede atacar.
            if (Actitud == "cauta" && !listo && dx < 2.2f && HaySueloHacia(-mirada, 0.45f))
            {
                anim.Reproducir("andar", false, 1.2f);
                AndarDir(-mirada, velocidadCarrera * 0.6f);
                yield return null;
                continue;
            }

            if (listo && dx <= rangoMordisco) { yield return Mordisco(); continue; }
            if (listo && PuedeSaltar && dx >= rangoSalto.x && dx <= rangoSalto.y && Random.value < Time.deltaTime * 1.8f) { yield return Salto(); continue; }

            if (dx > rangoMordisco * 0.8f && HaySueloDelante(0.45f) && !HayParedDelante(0.5f, 0.2f))
            {
                anim.Reproducir("andar", false, 1.5f);
                Andar(velocidadCarrera);
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(20f);
            }
            yield return null;
        }
    }

    private IEnumerator Mordisco()
    {
        FrenarAtaque();
        // Se encoge (fotogramas 0-6, el aviso) y muerde al estirarse (7-10).
        anim.Reproducir("morder", true, Ritmo);
        LanzarAviso(7f / 22f, 0.5f);
        yield return HastaFotograma(7);
        Sonido.Reproducir("rata_mordisco", 0.7f);
        rb.linearVelocity = new Vector2(mirada * 3.5f, rb.linearVelocity.y);
        yield return VentanaGolpe(7, 10, new Vector2(0.55f, 0.3f), new Vector2(0.9f, 0.6f), Dano(pesoMordisco));
        yield return Retirarse();
    }

    private IEnumerator Salto()
    {
        FrenarAtaque();
        anim.Reproducir("quieto", true);
        LanzarAviso(0.45f, 0.6f);
        // Se agacha (aviso) y salta en arco hasta donde estaba el player.
        yield return Agacharse(0.45f / Mathf.Max(0.3f, Ritmo));

        Sonido.Reproducir("rata_chillido", 0.6f);
        anim.Reproducir("morder", true);
        bool pego = false;
        yield return Saltar(DxPlayer, alturaSalto, () =>
        {
            if (!pego && Golpear(new Vector2(0.3f, 0.3f), new Vector2(1f, 0.8f), Dano(pesoSalto)).HasValue) pego = true;
        }, false);
        // Al aterrizar se queda un instante expuesta.
        anim.Reproducir("quieto");
        yield return EsperarRitmo(0.35f);
        yield return Retirarse();
    }

    // Salta por encima del player para caer a su espalda y morder.
    private IEnumerator Flanquear()
    {
        siguienteFlanqueo = Time.time + 4f;
        Flanqueos++;
        FrenarAtaque();
        anim.Reproducir("quieto", true);
        LanzarAviso(0.35f, 0.5f);
        yield return Agacharse(0.3f);
        Sonido.Reproducir("rata_chillido", 0.5f, 1.3f);
        float destino = DxPlayer + Mathf.Sign(DxPlayer) * 1.6f;
        yield return Saltar(destino, 1.9f, null, false);
        MirarAlPlayer();
        MirarYa();
        listoPara = Time.time;
        if (player != null && Mathf.Abs(DxPlayer) <= rangoMordisco * 1.3f) yield return Mordisco();
    }

    // Huye de espaldas, avisa a los cercanos y vuelve (una vez por vida).
    private IEnumerator Huir()
    {
        retiradaHecha = true;
        EstadoIA = "Retirada";
        Sonido.Reproducir("rata_chillido", 0.6f, 1.4f);
        Mirar(DxPlayer > 0f ? -1 : 1);
        MirarYa();
        anim.Reproducir("andar", false, 1.6f);
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            if (!HaySueloDelante(0.45f) || HayParedDelante(0.5f, 0.2f)) break;
            Andar(velocidadCarrera * 1.1f);
            yield return null;
        }
        Frenar(30f);
        // Avisa a otros (con tope) y vuelve con ellos.
        int avisados = 0;
        foreach (EnemigoBase o in Activos)
            if (o != this && o != null && !o.Alerta && avisados < 2 && Vector2.Distance(o.transform.position, transform.position) < 9f)
            { o.Alertar(Random.Range(0.1f, 0.3f)); avisados++; }
        anim.Reproducir("quieto");
        yield return EsperarRitmo(0.6f);
        listoPara = Time.time + 0.3f;
        EstadoIA = "Persecucion";
    }

    private IEnumerator Retirarse()
    {
        listoPara = Time.time + Enfriamiento;
        // Crecida (escarcha): no se aparta tras atacar.
        if (Actitud == "crecida") yield break;
        Mirar(-mirada);
        anim.Reproducir("andar", false, 1.4f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            if (!HaySueloDelante(0.45f) || HayParedDelante(0.5f, 0.2f)) break;
            Andar(velocidadCarrera * 0.7f);
            yield return null;
        }
        Frenar(30f);
        MirarYa();
    }

    // En manada: al descubrir al player chilla y avisa a las ratas cercanas,
    // que reaccionan un poco despues cada una.
    protected override void AlDescubrir()
    {
        Sonido.Reproducir("rata_chillido", 0.45f, Random.Range(1.05f, 1.25f));
        foreach (EnemigoBase o in Activos)
            if (o != this && o is EnemigoRata && Vector2.Distance(o.transform.position, transform.position) < radioManada)
                o.Alertar(Random.Range(0.1f, 0.35f));
    }
}
