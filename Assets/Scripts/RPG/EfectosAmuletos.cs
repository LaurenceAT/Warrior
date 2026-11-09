using UnityEngine;

// Lo que hacen los amuletos equipados (numeros en AjustesAmuletos). Cada sistema
// le pregunta aqui su multiplicador; sin amuletos puestos todo vale 1.
// Sin efecto todavia (exploracion): 10 Craneo de cuervo, 27 Collar de luto y
// 32 Ojos gemelos.
public static class EfectosAmuletos
{
    // Ids de la ficha (Resources/Amuletos), por su numero en la coleccion.
    public const string Brasa = "amuleto_00", Marea = "amuleto_01", Guardian = "amuleto_u01", Mandibula = "amuleto_u03",
        Hueso = "amuleto_u04", ManoOsario = "amuleto_u05", Costillar = "amuleto_u07", Muelas = "amuleto_u08",
        ColmilloCurvo = "amuleto_u09", Bestia = "amuleto_u11", Corazon = "amuleto_u12", Vigia = "amuleto_u13",
        Seso = "amuleto_u14", BrazoSeco = "amuleto_u15", Pie = "amuleto_u16", Dedo = "amuleto_u17", Lazo = "amuleto_u18",
        Amatista = "amuleto_u19", Corona = "amuleto_u20", Rama = "amuleto_u23", GarraAmarilla = "amuleto_u24",
        Rosario = "amuleto_u26", GarraSombra = "amuleto_u27", Escarcha = "amuleto_u28", Gema = "amuleto_u29",
        Calavera = "amuleto_u33", Vela = "amuleto_u34", Pluma = "amuleto_u37", Muneco = "amuleto_u38",
        Cuenco = "amuleto_u40", Llave = "amuleto_u41", Yelmo = "amuleto_u44", Guante = "amuleto_u45", Bota = "amuleto_u46",
        Ciervo = "amuleto_u48", Raiz = "amuleto_u49", ColmilloColgante = "amuleto_u50";

    private static AjustesAmuletos A => AjustesAmuletos.Get();

    public static bool Lleva(string id) => Amuletos.HuecoDe(id) >= 0;
    private static float Si(string id, float valor) => Lleva(id) ? valor : 1f;

    // ------------------------------------------------------------------ Ataque

    // Dano de un golpe de la espada (o de la estocada) segun lo que llevas.
    public static float MultDano(bool ultimoCombo, bool aereoOPicado, bool trasParry, Elemento imbuido, float fraccionVida, float desdeEsquiva)
    {
        float m = 1f;
        if (ultimoCombo) m *= Si(Bestia, A.ultimoGolpeCombo);
        if (aereoOPicado) m *= Si(ManoOsario, A.aereoYPicado);
        if (trasParry) m *= Si(ColmilloCurvo, A.trasParry);
        if (fraccionVida >= 0.999f) m *= Si(Corona, A.vidaLlena);
        if (fraccionVida < A.umbralVidaBaja) m *= Si(GarraSombra, A.vidaBaja);
        if (desdeEsquiva <= A.segundosTrasEsquiva) m *= Si(Rama, A.trasEsquiva);
        if (imbuido == Elemento.Ninguno) m *= Si(ColmilloColgante, A.sinImbuir);
        if (imbuido == Elemento.Fuego) m *= Si(Brasa, A.brasaDano);
        return m;
    }

    // Estamina extra de un golpe (desventaja del Cristal de escarcha).
    public static float CosteExtraGolpe(Elemento imbuido) => imbuido == Elemento.Hielo && Lleva(Escarcha) ? A.escarchaEstaminaGolpe : 0f;

    public static bool OndaPicado => Lleva(Bota);

    // ------------------------------------------------------------------ Imbuciones y estados del enemigo

    public static float MultQuemadura => Si(Brasa, A.brasaQuemadura);
    public static float MultCongelar => Si(Escarcha, A.escarchaCongelar);
    public static float MultDrenaje => Si(Lazo, A.lazoDrenaje);
    public static float MultSagrado => Si(GarraAmarilla, A.garraSagrado);
    public static float MultSangradoEnemigo => Si(Rosario, A.rosarioSangrado);
    public static float MultCosteVidaSangrado => Si(Rosario, A.rosarioCosteVida);
    public static float MultSangradoPropio => Si(Rosario, A.rosarioSangradoPropio);
    public static float MultDuracionImbuir => Si(Dedo, A.dedoDuracion) * Si(Seso, A.sesoDuracion);
    public static float MultCosteImbuir => Si(Seso, A.sesoCosteMana);

    // Garra amarilla: tras aturdir con sagrado, la estamina tarda mas en volver.
    public static void AlAturdirConSagrado()
    {
        if (!Lleva(GarraAmarilla)) return;
        PlayerStamina s = Object.FindFirstObjectByType<PlayerStamina>();
        if (s != null) s.Retrasar(A.garraEsperaEstamina);
    }

    // ------------------------------------------------------------------ Defensa

    public static float MultDanoRecibido(bool magico, Elemento imbuido)
    {
        float m = magico ? Si(Amatista, A.amatistaMagico) : Si(Guardian, A.guardianFisico);
        if (magico && imbuido == Elemento.Fuego) m *= Si(Brasa, A.brasaDanoMagicoRecibido);
        return m;
    }

    public static float MultEstadosRecibidos => Si(Vela, A.velaEstados);
    public static bool SinEmpujon(int dano, int vidaMax) => Lleva(Yelmo) && dano < vidaMax * A.yelmoGolpeLeve;
    public static float Espinas => Lleva(Muneco) ? A.munecoEspinas : 0f;
    public static float MultEsquiva => Si(Pluma, A.plumaEsquiva);

    // Calavera votiva: una vez por descanso (o reaparicion) evita un golpe mortal.
    private static bool calaveraGastada;
    public static bool CalaveraGastada => calaveraGastada;
    public static bool SalvarGolpeMortal()
    {
        if (!Lleva(Calavera) || !A.calaveraActiva || calaveraGastada) return false;
        calaveraGastada = true;
        return true;
    }
    public static void RecargarCalavera() => calaveraGastada = false;

    // Descansar en la hoguera o reaparecer recargan la Calavera votiva.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        Hoguera.AlDescansar -= RecargarCalavera;
        Hoguera.AlDescansar += RecargarCalavera;
        GameManager.AlReaparecerPlayer -= RecargarCalavera;
        GameManager.AlReaparecerPlayer += RecargarCalavera;
    }

    // ------------------------------------------------------------------ Parry y guardia

    public static float MultVentanaParry => Si(Vigia, A.vigiaVentanaParry);
    public static float MultCosteBloqueo => Si(BrazoSeco, A.brazoBloqueo);
    public static float ManaPorParry => Lleva(Ciervo) ? A.ciervoManaParry : 0f;

    // ------------------------------------------------------------------ Estamina y movimiento

    public static float MultRegenEstamina => Si(Raiz, A.raizRecuperacion);
    public static float MultEstaminaMax => Si(Costillar, A.costillarEstamina);
    public static float MultCosteCorrer => Si(Pie, A.pieCosteCorrer);
    public static float MultVelocidadCorrer => Si(Pie, A.pieVelocidad);
    public static float MultCosteParedCornisa => Si(Guante, A.guanteParedCornisa);

    // ------------------------------------------------------------------ Vida, mana y frascos

    public static float MultVidaMax => Si(Corazon, A.corazonVida);
    public static float MultFrascoSangre => Si(Muelas, A.muelasFrasco) * Si(Lazo, A.lazoFrasco);
    public static float MultFrascoMana => Si(Cuenco, A.cuencoFrascoMana);
    public static int CargasSangreExtra => Lleva(Hueso) ? Mathf.Max(0, A.huesoCargas) : 0;
    public static float ManaPorSegundo => Lleva(Marea) ? A.mareaManaSegundo : 0f;

    // Gema de sangre: vida al golpear, como mucho una vez cada "gemaEspera".
    private static float ultimaGema = -99f;
    public static int CuraAlGolpear()
    {
        if (!Lleva(Gema) || Time.time - ultimaGema < A.gemaEspera) return 0;
        ultimaGema = Time.time;
        return Mathf.Max(0, A.gemaCura);
    }

    // ------------------------------------------------------------------ Otros

    public static float MultPrecios => Si(Llave, A.llavePrecios);
    public static float MultRuido => Si(Mandibula, A.mandibulaRuido);
}
