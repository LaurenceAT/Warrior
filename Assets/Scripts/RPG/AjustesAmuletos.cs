using UnityEngine;

// Numeros de lo que hace cada amuleto (Resources/AjustesAmuletos). Todo se edita
// en el Inspector. Los porcentajes van como multiplicador (1,2 = +20 %; 0,85 = -15 %).
// Lo usa EfectosAmuletos. Los numeros del titulo son su orden en la coleccion.
public class AjustesAmuletos : ScriptableObject
{
    [Header("Ataque")]
    [Tooltip("11 Craneo de bestia: el ultimo golpe del combo.")]
    public float ultimoGolpeCombo = 1.2f;
    [Tooltip("6 Mano del osario: ataques en el aire y estocada en picado.")]
    public float aereoYPicado = 1.25f;
    [Tooltip("9 Colmillo curvo: el golpe justo despues de un parry.")]
    public float trasParry = 1.3f;
    [Tooltip("20 Corona rota: con la vida al maximo.")]
    public float vidaLlena = 1.15f;
    [Tooltip("24 Garra de sombra: con poca vida (por debajo de 'umbralVidaBaja').")]
    public float vidaBaja = 1.25f;
    [Range(0f, 1f)] public float umbralVidaBaja = 0.25f;
    [Tooltip("21 Rama de hueso: durante un rato tras esquivar.")]
    public float trasEsquiva = 1.2f;
    public float segundosTrasEsquiva = 0.6f;
    [Tooltip("40 Colmillo colgante: con la espada sin imbuir.")]
    public float sinImbuir = 1.12f;
    [Tooltip("37 Bota del caminante: la onda de la estocada al tocar el suelo (parte del dano del picado y ancho).")]
    [Range(0f, 2f)] public float ondaPicado = 0.5f;
    public Vector2 cajaOndaPicado = new Vector2(6f, 1.2f);

    [Header("Imbuciones (con desventaja)")]
    [Tooltip("1 Anillo de la brasa: dano con fuego imbuido y quemadura mas fuerte.")]
    public float brasaDano = 1.12f;
    public float brasaQuemadura = 1.15f;
    [Tooltip("1 Desventaja: dano magico recibido mientras el arma arde.")]
    public float brasaDanoMagicoRecibido = 1.1f;
    [Tooltip("25 Cristal de escarcha: congela antes (acumulacion).")]
    public float escarchaCongelar = 1.18f;
    [Tooltip("25 Desventaja: estamina extra por golpe con escarcha imbuida.")]
    public float escarchaEstaminaGolpe = 8f;
    [Tooltip("18 Lazo nocturno: vida drenada con oscuridad.")]
    public float lazoDrenaje = 1.25f;
    [Tooltip("18 Desventaja: lo que cura el frasco de sangre.")]
    public float lazoFrasco = 0.85f;
    [Tooltip("22 Garra amarilla: probabilidad de aturdir con sagrado.")]
    public float garraSagrado = 1.25f;
    [Tooltip("22 Desventaja: segundos que tarda de mas en volver la estamina tras aturdir.")]
    public float garraEsperaEstamina = 1f;
    [Tooltip("23 Rosario de dientes: sangrado en el enemigo y coste en vida de imbuirlo.")]
    public float rosarioSangrado = 1.18f;
    public float rosarioCosteVida = 0.75f;
    [Tooltip("23 Desventaja: el sangrado que te pones tu mismo.")]
    public float rosarioSangradoPropio = 1.25f;
    [Tooltip("17 Dedo anillado: duracion de la imbuicion.")]
    public float dedoDuracion = 1.5f;
    [Tooltip("14 Seso marchito: coste en mana de imbuir y (desventaja) su duracion.")]
    public float sesoCosteMana = 0.5f;
    public float sesoDuracion = 0.7f;

    [Header("Defensa")]
    [Tooltip("3 Craneo del guardian: dano fisico recibido.")]
    public float guardianFisico = 0.85f;
    [Tooltip("19 Anillo de amatista hueca: dano magico recibido.")]
    public float amatistaMagico = 0.85f;
    [Tooltip("29 Vela del requiem: lo que se te llenan los estados (sangrado, congelacion, quemadura).")]
    public float velaEstados = 0.6f;
    [Tooltip("35 Yelmo abollado: los golpes de menos de esta parte de tu vida maxima no te empujan.")]
    [Range(0f, 1f)] public float yelmoGolpeLeve = 0.12f;
    [Tooltip("31 Muneco de trapo: parte del dano recibido que vuelve al atacante.")]
    [Range(0f, 1f)] public float munecoEspinas = 0.2f;
    [Tooltip("30 Pluma de fenix: invulnerabilidad de la esquiva.")]
    public float plumaEsquiva = 1.3f;
    [Tooltip("28 Calavera votiva: un golpe mortal te deja con 1 de vida (una vez por descanso).")]
    public bool calaveraActiva = true;

    [Header("Parry y guardia")]
    [Tooltip("13 Ojos del vigia: ventana de parry.")]
    public float vigiaVentanaParry = 1.4f;
    [Tooltip("15 Brazo seco: estamina al bloquear.")]
    public float brazoBloqueo = 0.7f;
    [Tooltip("38 Totem de ciervo: mana por cada parry justo.")]
    public float ciervoManaParry = 10f;

    [Header("Estamina y movimiento")]
    [Tooltip("39 Raiz retorcida: recuperacion de la estamina.")]
    public float raizRecuperacion = 1.25f;
    [Tooltip("7 Costillar hueco: estamina maxima.")]
    public float costillarEstamina = 1.2f;
    [Tooltip("16 Pie del peregrino: coste de correr y velocidad al correr.")]
    public float pieCosteCorrer = 0.7f;
    public float pieVelocidad = 1.1f;
    [Tooltip("36 Guante de cuero: estamina de correr por la pared y de agarrarse a cornisas.")]
    public float guanteParedCornisa = 0.6f;

    [Header("Vida, mana y frascos")]
    [Tooltip("12 Corazon que late: vida maxima.")]
    public float corazonVida = 1.15f;
    [Tooltip("8 Muelas del gloton: lo que cura el frasco de sangre.")]
    public float muelasFrasco = 1.25f;
    [Tooltip("33 Cuenco del festin: lo que devuelve el frasco de mana.")]
    public float cuencoFrascoMana = 1.3f;
    [Tooltip("5 Hueso nudoso: cargas de mas del frasco de sangre.")]
    public int huesoCargas = 1;
    [Tooltip("2 Anillo de la marea: mana que vuelve solo, por segundo.")]
    public float mareaManaSegundo = 1f;
    [Tooltip("26 Gema de sangre: vida por golpe que das, y cada cuanto como mucho (segundos).")]
    public int gemaCura = 2;
    public float gemaEspera = 1f;

    [Header("Otros")]
    [Tooltip("34 Llave del osario: precios del totem.")]
    public float llavePrecios = 0.9f;
    [Tooltip("4 Mandibula del mudo: lo lejos que te ven los enemigos y te oye la Cazadora.")]
    public float mandibulaRuido = 0.7f;

    private static AjustesAmuletos instancia;

    public static AjustesAmuletos Get()
    {
        if (instancia == null)
        {
            instancia = Resources.Load<AjustesAmuletos>("AjustesAmuletos");
            if (instancia == null) instancia = CreateInstance<AjustesAmuletos>();
        }
        return instancia;
    }
}
