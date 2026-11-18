using System.Collections;
using UnityEngine;

// El Black Knight (6): DIEZMO DE SANGRE (desde la fase 2).
//
// Se teletransporta al otro extremo, la camara se abre y se le ve canalizando
// algo (aura roja que crece). Entonces cae del cielo sobre ti: si te pilla,
// quedas agarrado y aturdido, sin poder moverte, y te roba un frasco de sangre
// para curarse lo mismo que ese frasco te curaria a ti (animacion arriba, como
// la del sello). Si no te queda ninguno, te revienta de un golpe y te manda por
// los aires. No se para ni se bloquea: hay que no estar debajo.
public partial class JefeBlackKnight
{
    private bool enDiezmo;
    private float finEnfriamientoDiezmo;

    public bool EnDiezmo => enDiezmo;
    public int FrascosRobados { get; private set; }
    public int DiezmosSinFrasco { get; private set; }
    public int DiezmosFallados { get; private set; }

    private IEnumerator Diezmo()
    {
        finEnfriamientoDiezmo = Time.time + EnfriamientoConPresion(ajustes.enfriamientoDiezmo);
        enDiezmo = true;
        camaraAmplia = true;
        CambiarEstado(EstadoEnemigo.Anticipacion);

        // 1. Se va lejos de un teletransporte (no corriendo: no da tiempo a seguirle).
        invulnerable = true;
        yield return Teleportar(DestinoAlejarse());

        // 2. Canaliza: la pose del buff con el aura roja creciendo.
        anim.Reproducir(ClipBuff, true);
        Sonar("jefe_revivir", 0.8f, 0.7f);
        float canalizar = ajustes.canalizarDiezmo / Mathf.Max(0.5f, Desafio.MultVelocidad);
        for (float t = 0f; t < canalizar; t += Time.deltaTime)
        {
            float k = t / canalizar;
            ritmo.Destello(ajustes.colorDiezmo, 0.2f + 0.6f * k * (0.5f + 0.5f * Mathf.Sin(t * (8f + 20f * k))));
            if (anim.Terminado) anim.Reproducir(ClipBuff, true);
            Parar();
            MirarYa();
            if (t > canalizar * 0.5f && Random.value < 0.25f)
                ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 1f, 3, ajustes.colorDiezmo, Color.white,
                                    new Vector2(1f, 3f), -0.6f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f));
            yield return null;
        }
        ritmo.Destello(ajustes.colorDiezmo, 0f);

        // 3. Desaparece y cae desde el cielo sobre donde estas (la sombra avisa).
        yield return FundirCaballero(0.08f, false);
        BuscarPlayer();
        float xCaida = LimitarX(PosPlayer.x);
        float ySuelo = PeligrosJefe.SueloBajo(new Vector2(xCaida, PosPlayer.y + 1f), suelo);
        MarcaSuelo marca = MarcaSuelo.Poner(MarcaSuelo.Forma.Aro, new Vector2(xCaida, ySuelo),
                                            new Vector2(ajustes.cajaDiezmo.x * Esc, 0.5f), ajustes.colorDiezmo, ajustes.caidaDiezmo + 0.2f);
        if (marca != null) marca.Solida();
        volando = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        Colocar(new Vector2(xCaida, ySuelo + ajustes.alturaDiezmo));
        yield return FundirCaballero(0.05f, true);
        anim.Reproducir(ClipAire, true);
        float caida = ajustes.caidaDiezmo / Mathf.Max(0.5f, Desafio.MultVelocidad);
        float y0 = transform.position.y;
        for (float t = 0f; t < caida; t += Time.deltaTime)
        {
            float u = (t / caida) * (t / caida);
            Colocar(new Vector2(xCaida, Mathf.Lerp(y0, ySuelo, u)));
            yield return null;
        }
        Colocar(new Vector2(xCaida, ySuelo));
        if (marca != null) marca.Quitar();
        anim.Reproducir(ClipAterrizar, true);
        Sonar("jefe_caida");
        CamaraDinamica.Sacudir(1.1f);
        ScreenFlash.Destello(new Color(ajustes.colorDiezmo.r, ajustes.colorDiezmo.g, ajustes.colorDiezmo.b, 0.22f), 0.25f);
        ParticulasFx.Rafaga(new Vector2(xCaida, ySuelo + 0.2f), 20, ajustes.colorDiezmo, Color.white, new Vector2(3f, 7f), 0.6f,
                            new Vector2(0.06f, 0.12f), new Vector2(0.2f, 0.5f));

        // 4. Quien este debajo, cae agarrado.
        PlayerControler p = GolpeCazadora.PlayerEn(new Vector2(xCaida, ySuelo + ajustes.cajaDiezmo.y * Esc * 0.5f), ajustes.cajaDiezmo * Esc);
        bool atrapado = p != null && p.IntentarAgarre();
        yield return new WaitForFixedUpdate();
        TerminarVuelo();
        AtravesarPlayer(false);
        invulnerable = false;
        MirarYa();

        if (!atrapado)
        {
            DiezmosFallados++;
            TextoFlotante.Mostrar("¡Esquivado!", (Vector2)transform.position + Vector2.up * 2.4f, new Color(0.8f, 1f, 0.8f), 1f);
            while (!anim.Terminado) yield return null;
            enDiezmo = false;
            camaraAmplia = false;
            yield return Aturdirse(ajustes.aturdidoCieloFallado * 0.7f, "diezmo");
            yield break;
        }

        // En la escena del robo manda el primer plano, no la camara abierta.
        camaraAmplia = false;
        yield return RobarFrasco(p);
        enDiezmo = false;
        camaraAmplia = false;
        yield return Quieto(0.6f / Vel);
    }

    // Te tiene agarrado: o te quita el frasco, o te manda por los aires.
    private IEnumerator RobarFrasco(PlayerControler p)
    {
        Vector2 sitio = (Vector2)transform.position + new Vector2(mirada * 0.9f, 0.6f);
        p.MoverAgarrado(sitio);
        anim.Reproducir(ClipArte, true, 0.8f);
        ReservaPociones reserva = ReservaPociones.Get();
        bool hay = reserva != null && reserva.Cargas > 0;

        if (!hay)
        {
            // Sin nada que robar: te revienta y te manda lejos, volando en curva.
            TextoFlotante.Mostrar(ajustes.textoDiezmoVacio, sitio + Vector2.up * 1.2f, new Color(0.8f, 0.8f, 0.8f), 1f);
            Sonar("jefe_tajo_fuerte", 1f, 0.8f);
            yield return EsperarCielo(0.25f);
            DiezmosSinFrasco++;
            p.CorteAgarre(Mathf.RoundToInt(ajustes.danoDiezmoSinFrasco * Desafio.MultDano));
            if (p != null && p.Agarrado)
            {
                p.SoltarAgarre(false);
                p.Derribar(new Vector2(mirada * ajustes.impulsoDiezmo.x, ajustes.impulsoDiezmo.y), true);
            }
            CamaraDinamica.Sacudir(0.9f);
            ScreenFlash.Destello(new Color(1f, 0.6f, 0.6f, 0.3f), 0.25f);
            while (!anim.Terminado) yield return null;
            yield break;
        }

        // Te lo quita, pero despacio y en primer plano: una escena corta.
        // Tu te quedas encogido del golpe, sin control, mientras el bebe y su
        // barra sube a la vista; al final te aparta de un golpe que no hace dano.
        CamaraDinamica.Acercar(ajustes.camaraDiezmo, ajustes.escenaDiezmo + 1.2f);
        Vector2 medio = Vector2.Lerp(transform.position, p.transform.position, 0.5f) + Vector2.up * 1.1f;
        CamaraDinamica.Encuadrar(medio, 0.85f, ajustes.escenaDiezmo + 1.2f, 4f);
        p.CorteAgarre(Mathf.RoundToInt(ajustes.danoDiezmo * Desafio.MultDano));

        // 1. Te arranca el frasco del cinturon: el frasco vuela hacia su mano.
        yield return EsperarCielo(0.35f);
        reserva.Gastar(ReservaPociones.Tipo.Vida);
        FrascosRobados++;
        Sonar("jefe_parry", 0.6f, 1.5f);
        FrascoRobadoBK frasco = FrascoRobadoBK.Crear(ReservaPociones.Frasco(), p.transform.position + Vector3.up * 0.8f, ajustes.colorDiezmo);
        Vector2 mano = (Vector2)transform.position + new Vector2(mirada * 0.45f, 1.25f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (frasco != null) frasco.Volar(mano, t / 0.3f);
            p.MoverAgarrado(sitio);
            yield return null;
        }

        // 2. Bebe: la barra del jefe sube poco a poco, delante de tus narices.
        int cura = Mathf.Max(1, Mathf.RoundToInt(p.VidaMaxima * Equipo.CuraFrasco * ajustes.multCuraDiezmo));
        IndicadorRoboBK.Mostrar(ReservaPociones.Frasco(), ajustes.textoDiezmo, ajustes.colorDiezmo, cura);
        anim.Reproducir(ClipBuff, true, 0.7f);
        float escena = ajustes.escenaDiezmo / Mathf.Max(0.5f, Desafio.MultVelocidad);
        int dado = 0;
        for (float t = 0f; t < escena; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / escena);
            int toca = Mathf.RoundToInt(cura * k) - dado;
            if (toca > 0) { salud.Curar(toca); dado += toca; }
            if (frasco != null) frasco.Beber(mano, k);
            ritmo.Destello(ajustes.colorDiezmo, 0.25f + 0.25f * Mathf.Sin(t * 9f));
            if (anim.Terminado) anim.Reproducir(ClipBuff, true, 0.7f);
            if (Random.value < 0.3f)
                ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 1.1f, 2, ajustes.colorDiezmo, Color.white,
                                    new Vector2(1f, 2.5f), -0.7f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f));
            p.MoverAgarrado(sitio);
            yield return null;
        }
        if (dado < cura) salud.Curar(cura - dado);
        ritmo.Destello(ajustes.colorDiezmo, 0f);
        if (frasco != null) frasco.Tirar(mirada);
        Sonar("jefe_revivir", 0.9f, 1.1f);
        TextoFlotante.Mostrar("+" + cura, (Vector2)transform.position + Vector2.up * 2.2f, ajustes.colorDiezmo, 1f);

        // 3. Y te aparta de un golpe: vuelas lejos, pero este no hace dano.
        yield return EsperarCielo(0.25f);
        anim.Reproducir(ClipContra, true);
        while (anim.Fotograma < 2 && !anim.Terminado) yield return null;
        Sonar("jefe_tajo_fuerte", 1f, 0.9f);
        CamaraDinamica.Sacudir(0.9f);
        ScreenFlash.Destello(new Color(1f, 0.8f, 0.8f, 0.2f), 0.2f);
        if (p != null && p.Agarrado)
        {
            p.SoltarAgarre(false);
            p.Derribar(new Vector2(mirada * ajustes.impulsoDiezmo.x, ajustes.impulsoDiezmo.y), true);
        }
        while (!anim.Terminado) yield return null;
    }
}
