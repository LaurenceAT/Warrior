using System.Collections;
using UnityEngine;

// La arena de la Cazadora (el bosque sin hojas). Al entrar el player:
//   1. La camara se acerca despacio a ella, con bandas de cine. Ella habla hacia
//      el vacio (nunca te mira). Solo la primera vez: el dialogo completo, con
//      tu respuesta. Al reintentar, la pelea empieza enseguida.
//   2. Al empezar de verdad el combate (tras el dialogo y la eleccion) el muro de
//      niebla de su ZonaJefe se forma detras: no se sale hasta vencer.
//   3. Musica por fase, con fundido cruzado en cada muerte falsa, y bajada
//      (ducking) en el aviso de cada instakill y en El Silencio.
//   4. El ambiente cambia con las fases (niebla, cielo, luz, oscuridad).
// Si el player muere, todo vuelve a como estaba antes de entrar.
[RequireComponent(typeof(Collider2D))]
public class ArenaCazadora : MonoBehaviour, IArenaJefe
{
    private enum Estado { Esperando, Entrada, Combate, Vencido }

    [Header("Jefa")]
    [SerializeField] private JefeCazadora prefabJefe;
    [SerializeField] private AjustesCazadora ajustes;
    [SerializeField] private string nombre = "The Blind Huntress";
    [SerializeField] private string titulo = "La Cazadora Ciega";
    [Tooltip("Limites de la arena (para ella y sus ataques).")]
    [SerializeField] private Rect zona = new Rect(0f, 0f, 40f, 14f);
    [SerializeField] private float suelo;
    [Tooltip("Donde esta ella al entrar.")]
    [SerializeField] private float xAparicion;
    [Tooltip("Frases de la pantalla de muerte (vacio = las del nivel).")]
    [SerializeField] private string[] burlas =
    {
        "Te oí respirar. Siempre se oye.",
        "Los árboles ya tienen un hueso más.",
        "Corre más despacio la próxima vez. Así suena menos.",
    };

    [Header("Bloqueo de la arena")]
    [SerializeField] private Collider2D muro;
    [SerializeField] private SpriteRenderer[] visualMuro = new SpriteRenderer[0];
    [SerializeField] private Color muroAbierto = new Color(0.55f, 0.7f, 0.6f, 0.2f);
    [SerializeField] private Color muroCerrado = new Color(0.6f, 0.78f, 0.66f, 0.75f);

    [Header("Escena")]
    [SerializeField] private AmbienteBosque ambiente;
    [SerializeField] private CamaraCazadora camara;
    [Tooltip("Tamano de la camara al acercarse a ella en la entrada.")]
    [SerializeField] private float zoomEntrada = 4.8f;
    [SerializeField] private string bannerVictoria = "ENEMIGO CAÍDO";

    private Estado estado;
    private JefeCazadora jefe;
    private BarraJefe barra;
    private OscuridadCazadora oscuridad;
    private AudioSource[] musica = new AudioSource[2];
    private float[] mezcla = new float[2];
    private float[] mezclaObjetivo = new float[2];
    private float[] volumenFase = new float[2];
    private Vector2[] bucle = new Vector2[2];
    private int fuente;
    private float musicaMult = 1f, musicaMultObjetivo = 1f, rapidezMezcla = 1f;
    private AudioSource ambienteSrc, vientoSrc;
    private float vientoObjetivo;
    private int intentos;
    private bool enSilencio;
    private float siguienteLatido;
    private Elemento resistenciaMostrada = Elemento.Ninguno;
    // Barra en la que esta (0, 1 o 2) y si ya dijo la frase de "casi vencida".
    private int barraActual;
    private bool dichoCasiVencida;

    public static ArenaCazadora Actual { get; private set; }
    public JefeCazadora Jefe => jefe;
    public OscuridadCazadora Oscuridad => oscuridad;

    private void Awake()
    {
        Actual = this;
        GetComponent<Collider2D>().isTrigger = true;
        for (int i = 0; i < 2; i++) musica[i] = Fuente(true);
        ambienteSrc = Fuente(true);
        vientoSrc = Fuente(true);
        if (ajustes != null && ajustes.ambiente != null) { ambienteSrc.clip = ajustes.ambiente; ambienteSrc.Play(); }
        if (ajustes != null && ajustes.viento != null) { vientoSrc.clip = ajustes.viento; vientoSrc.Play(); }
        oscuridad = new GameObject("Oscuridad").AddComponent<OscuridadCazadora>();
        if (ajustes != null) { oscuridad.radio = ajustes.radioLuz; oscuridad.opacidad = ajustes.opacidadOscuridad; }
        if (camara == null) camara = gameObject.AddComponent<CamaraCazadora>();
        camara.Configurar(ajustes);
        UICazadora.Crear();
        if (ambiente != null && ajustes != null) ambiente.Reiniciar(ajustes.FaseN(0));
        PonerMuro(false);
#if UNITY_EDITOR
        gameObject.AddComponent<DepuracionCazadora>();
#endif
    }

    private AudioSource Fuente(bool bucleAudio)
    {
        AudioSource a = gameObject.AddComponent<AudioSource>();
        a.loop = bucleAudio;
        a.playOnAwake = false;
        a.volume = 0f;
        return a;
    }

    private void OnEnable() => GameManager.AlReaparecerPlayer += Reiniciar;

    private void OnDisable()
    {
        GameManager.AlReaparecerPlayer -= Reiniciar;
        Cinematica.Activa = false;
        PlayerControler.TopeVentanaParry = -1f;
        if (estado == Estado.Combate || estado == Estado.Entrada) ArenaJefe.CombateExterno(false);
        PantallaMuerte.BurlasJefe = null;
        if (Actual == this) Actual = null;
    }

    // ------------------------------------------------------------------ Zona de entrada (ZonaJefe)

    // Con una ZonaJefe enlazada, la entrada la empieza su activador y el muro de
    // niebla se forma cuando empieza de verdad el combate (tras el dialogo).
    public ZonaJefe ZonaEntrada { get; set; }
    public bool PuedeEmpezar => estado == Estado.Esperando && prefabJefe != null;
    public Vector2 PuntoJefe => new Vector2(xAparicion, suelo + 1.5f);
    public void EmpezarDesdeZona(PlayerControler p) { if (PuedeEmpezar) StartCoroutine(Entrada(p)); }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (ZonaEntrada != null || estado != Estado.Esperando || !otro.CompareTag("Player") || prefabJefe == null) return;
        StartCoroutine(Entrada(otro.GetComponent<PlayerControler>()));
    }

    // ------------------------------------------------------------------ Entrada

    private IEnumerator Entrada(PlayerControler p)
    {
        estado = Estado.Entrada;
        intentos++;
        Cinematica.Activa = true;
        UICazadora.Bandas(true);
        ArenaJefe.CombateExterno(true);
        PantallaMuerte.BurlasJefe = burlas != null && burlas.Length > 0 ? burlas : null;
        if (ZonaEntrada == null) PonerMuro(true);
        if (ambiente != null) ambiente.Fase(ajustes.FaseN(0));

        jefe = Instantiate(prefabJefe, new Vector3(xAparicion, suelo, 0f), Quaternion.identity);
        jefe.Configurar(zona, suelo);
        if (Desafio.Activo && Desafio.Dificil) jefe.gameObject.AddComponent<BrilloDificil>();
        // Habla hacia el vacio: mira al lado contrario al player.
        if (p != null) jefe.Orientar(p.transform.position.x < xAparicion ? 1 : -1);
        EngancharEventos();

        barra = BarraJefe.Crear(nombre, 0f);
        EnemyHealth salud = jefe.GetComponent<EnemyHealth>();
        salud.AlCambiarVida += barra.Actualizar;
        barraActual = 0;
        dichoCasiVencida = false;
        salud.AlCambiarVida += CasiVencida;
        barra.PonerEstados(salud);
        UICazadora.MontarEnBarra(barra);
        UICazadora.FaseActual(0);

        FichaJefe ficha = FichaJefe.DeEscena(gameObject.scene.name);
        Bitacora.Intento(ficha);
        // Tras "Reiniciar desafio" cuenta como un reintento (salvo que la ficha pida el dialogo).
        bool reiniciado = Desafio.Activo && Desafio.Reiniciado && (ficha == null || !ficha.dialogoAlReiniciar);
        bool primeraVez = intentos == 1 && !reiniciado;
        bool completo = primeraVez && (!Globales.Marca("dialogo_cazadora") || (Desafio.Activo && Desafio.Reiniciado));
        float acercarse = primeraVez ? 2.2f : 0.6f;
        // Sin el dialogo completo (reintento): una frase suelta, segun como moriste.
        if (!completo) UICazadora.Subtitulo(FraseReintento(ficha), ajustes.segundosReintento);
        // La camara va despacio hacia ella (y vuelve a ti al acabar).
        if (GameManager.Instance != null) GameManager.Instance.SeguirConCamara(jefe.transform);
        CamaraDinamica.Acercar(zoomEntrada, completo ? 60f : acercarse + 0.8f);
        yield return Esperar(acercarse);

        int eleccion = -1;
        if (completo)
        {
            DialogoCazadora d = DialogoCazadora.Crear();
            d.sonidoLetras = ajustes.sonidoEscritura;
            d.volumenLetras = ajustes.volumenEscritura;
            d.Abrir();
            foreach (string l in ajustes.frasesEntrada) yield return d.Decir(ajustes.nombreDialogo, l, ajustes.letrasPorSegundo, ajustes.mantenerParaSaltar);
            if (!d.Saltado) yield return d.Elegir(new[] { ajustes.opcionRetirarse, ajustes.opcionDesafiar }, i => eleccion = i);
            string[] respuesta = eleccion == 0 ? ajustes.respuestaRetirarse : eleccion == 1 ? ajustes.respuestaDesafiar : new string[0];
            foreach (string l in respuesta) yield return d.Decir(ajustes.nombreDialogo, l, ajustes.letrasPorSegundo, ajustes.mantenerParaSaltar);
            d.Cerrar();
            Globales.PonerMarca("dialogo_cazadora", true);
        }

        // Fin de la entrada: la camara vuelve, sale el nombre y empieza la musica.
        if (GameManager.Instance != null && p != null) GameManager.Instance.SeguirConCamara(p.transform);
        CamaraDinamica.Acercar(camara.tamanoBase, 0.01f);
        UICazadora.Bandas(false);
        Cinematica.Activa = false;
        MensajePantalla.Titulo(nombre.ToUpper(), titulo);
        barra.NuevaFase(ajustes.FaseN(0).colorBarra, nombre);
        barra.Mostrar(true);
        EmpezarMusica(0, 2f);
        camara.Seguir(jefe.transform, true);
        estado = Estado.Combate;
        // Ahora si: la niebla se cierra detras (tambien si eligio "Retirarme").
        if (ZonaEntrada != null) ZonaEntrada.Cerrar();

        // "Voy a derrotarte": un tajo rapido. Si lo paras, empieza aturdida.
        if (eleccion == 1) yield return jefe.TajoDialogo();
        jefe.Activar();
        // Comenta lo que oye (frases sueltas, con tope y pausa entre ellas).
        jefe.gameObject.AddComponent<ReaccionesCazadora>().Configurar(ajustes);
    }

    // Los avisos de la jefa: fases, instakills, frases y victoria.
    private void EngancharEventos()
    {
        jefe.AlMuerteFalsa += MuerteFalsa;
        jefe.AlNuevaFase += NuevaFase;
        jefe.AlAvisoInstakill += AvisoInstakill;
        jefe.AlSilencioCaza += Silencio;
        jefe.AlFrase += t => UICazadora.Subtitulo(t);
        jefe.AlMuerteReal += MuerteReal;
        jefe.AlDerrotado += Victoria;
    }

    // ------------------------------------------------------------------ Frases sueltas

    // Al volver a entrar: segun de que moriste la ultima vez contra ella (en
    // memoria, CausaMuerte). Si no se sabe, una general; con racha larga, a veces
    // rompe la cuarta pared. Nunca la misma frase dos veces seguidas.
    private string FraseReintento(FichaJefe ficha)
    {
        CausaMuerte.Ultima u = CausaMuerte.TomarUltima();
        bool suya = u.valida && ficha != null && u.jefe == ficha.id;
        string f = null;
        if (suya && u.seguidas >= ajustes.rachaCuartaPared && Random.value < ajustes.probabilidadCuartaPared)
            f = AjustesCazadora.Una(ajustes.reintentoRacha);
        if (f == null && suya && ajustes.reintentoPorCausa != null)
        {
            bool estado = u.tipo == CausaMuerte.Tipo.Sangrado || u.tipo == CausaMuerte.Tipo.Frio || u.tipo == CausaMuerte.Tipo.Fuego;
            foreach (AjustesCazadora.ReintentoCausa c in ajustes.reintentoPorCausa)
            {
                if (c == null) continue;
                bool encaja = (c.estadoAlterado && estado) || (!estado && u.ataque != null && System.Array.IndexOf(c.ataques, u.ataque) >= 0);
                if (!encaja) continue;
                f = AjustesCazadora.Una(c.frases);
                if (f != null) break;
            }
        }
        return f ?? AjustesCazadora.Una(ajustes.reintentoGeneral);
    }

    // En la ultima barra, con poca vida: una vez por intento.
    private void CasiVencida(int vida, int maxima)
    {
        if (dichoCasiVencida || estado != Estado.Combate || barraActual < 2 || maxima <= 0 || vida <= 0) return;
        if (vida > maxima * ajustes.casiVencida) return;
        dichoCasiVencida = true;
        UICazadora.Subtitulo(AjustesCazadora.Una(ajustes.frasesCasiVencida));
    }

    // ------------------------------------------------------------------ Avisos de la jefa

    private void MuerteFalsa(int barraVaciada)
    {
        SeguirJefa(true);
        UICazadora.Bandas(true);
        CamaraCazadora.Lenta(ajustes.lentaTransicion, ajustes.lentaTransicionTiempo);
        CamaraCazadora.Acercar(ajustes.zoomTransicion, 2.6f);
        if (barra != null) barra.Mostrar(true);
        int nueva = Mathf.Min(2, barraVaciada + 1);
        EmpezarMusica(nueva, ajustes.crossfade);
        if (ambiente != null) ambiente.Fase(ajustes.FaseN(nueva));
        musicaMultObjetivo = 1f;
    }

    private void NuevaFase(int f)
    {
        barraActual = f;
        SeguirJefa(false);
        Bitacora.Fase(FichaJefe.DeEscena(gameObject.scene.name), f + 1);
        AjustesCazadora.Fase datos = ajustes.FaseN(f);
        if (barra != null) barra.NuevaFase(datos.colorBarra, nombre + "  ·  " + datos.nombre);
        UICazadora.FaseActual(f);
        UICazadora.Bandas(false);
        oscuridad.Activar(f >= 2);
        ArenaJefe.CombateExterno(true, f >= 1);
    }

    private void AvisoInstakill(bool empieza)
    {
        musicaMultObjetivo = empieza ? ajustes.musicaEnAviso : 1f;
    }

    private void Silencio(bool empieza)
    {
        enSilencio = empieza;
        musicaMultObjetivo = empieza ? ajustes.musicaEnSilencio : 1f;
        vientoObjetivo = empieza ? ajustes.volumenViento : 0f;
        UICazadora.BordesOscuros(empieza ? 0.85f : 0f);
        UICazadora.IconoOido(empieza);
        if (empieza) CamaraCazadora.Acercar(ajustes.zoomInstakill * 1.05f, ajustes.duracionSilencio + 0.6f);
        siguienteLatido = Time.time;
    }

    private void MuerteReal()
    {
        SeguirJefa(true);
        musicaMultObjetivo = 0f;
        rapidezMezcla = 0.5f;
        UICazadora.Bandas(true);
        UICazadora.Latido(false);
        CamaraCazadora.Lenta(0.3f, 1.8f);
        CamaraCazadora.Acercar(ajustes.zoomTransicion, 3f);
        oscuridad.Activar(false);
        vientoObjetivo = 0f;
    }

    private void Victoria()
    {
        estado = Estado.Vencido;
        if (barra != null) barra.Mostrar(false);
        camara.Seguir(null, false);
        StartCoroutine(TrasVictoria());
    }

    private IEnumerator TrasVictoria()
    {
        yield return Esperar(1.2f);
        SeguirJefa(false);
        MensajePantalla.Banner(bannerVictoria, new Color(0.8f, 0.95f, 0.8f), 4f);
        Sonido.Reproducir("victoria");
        PonerMuro(false);
        if (ZonaEntrada != null) ZonaEntrada.Abrir(true);
        if (ambiente != null) ambiente.Fase(ajustes.FaseN(0));
        UICazadora.Bandas(false);
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null) p.InvulnerableExterno = false;
        ArenaJefe.VictoriaExterna();
    }

    // El player murio: se deshace el intento.
    private void Reiniciar()
    {
        if (estado == Estado.Esperando || estado == Estado.Vencido) return;
        StopAllCoroutines();
        estado = Estado.Esperando;
        if (jefe != null) Destroy(jefe.gameObject);
        if (barra != null) Destroy(barra.gameObject);
        PoolCazadora.Limpiar();
        PeligrosJefe.LimpiarTodo();
        musicaMultObjetivo = 1f;
        mezclaObjetivo[0] = mezclaObjetivo[1] = 0f;
        rapidezMezcla = 1f;
        vientoObjetivo = 0f;
        enSilencio = false;
        if (ambiente != null) ambiente.Reiniciar(ajustes.FaseN(0));
        oscuridad.Activar(false);
        UICazadora.Bandas(false);
        UICazadora.Latido(false);
        UICazadora.BordesOscuros(0f);
        UICazadora.IconoOido(false);
        UICazadora.Escudo(-1f);
        UICazadora.Resistencia(Elemento.Ninguno);
        resistenciaMostrada = Elemento.Ninguno;
        PonerMuro(false);
        if (ZonaEntrada != null) ZonaEntrada.Reiniciar();
        Cinematica.Activa = false;
        PlayerControler.TopeVentanaParry = -1f;
        camara.Seguir(null, false);
        ArenaJefe.CombateExterno(false);
    }

    // ------------------------------------------------------------------ Cada fotograma

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ActualizarMusica(dt);
        if (ambienteSrc.clip != null)
        {
            float objetivo = (estado == Estado.Combate || estado == Estado.Entrada ? 0.5f : 1f) * ajustes.volumenAmbiente * ControlVolumen.Efectos;
            ambienteSrc.volume = Mathf.MoveTowards(ambienteSrc.volume, objetivo, dt * 0.5f);
        }
        if (vientoSrc.clip != null) vientoSrc.volume = Mathf.MoveTowards(vientoSrc.volume, vientoObjetivo * ControlVolumen.Efectos, dt * 0.7f);

        if (jefe == null || estado != Estado.Combate) return;
        UICazadora.Escudo(jefe.EscudoFraccion);
        Elemento r = jefe.Resistencia;
        if (r != resistenciaMostrada) { resistenciaMostrada = r; UICazadora.Resistencia(r); }
        oscuridad.Pulsar(jefe.Fase >= 2 && jefe.EscudoFraccion >= 0f);
        if (enSilencio && Time.time >= siguienteLatido)
        {
            siguienteLatido = Time.time + 0.9f;
            jefe.SonarPublico("latido", 0.8f);
        }
    }

    // ------------------------------------------------------------------ Musica

    private void EmpezarMusica(int fase, float fundido)
    {
        ConfigNivel.MusicaFase m = ajustes.FaseN(fase).musica;
        if (m == null || m.pista == null) return;
        int nueva = musica[fuente].isPlaying ? 1 - fuente : fuente;
        AudioSource a = musica[nueva];
        a.clip = m.pista;
        a.time = Mathf.Clamp(m.inicio, 0f, Mathf.Max(0f, m.pista.length - 1f));
        a.Play();
        volumenFase[nueva] = m.volumen;
        bucle[nueva] = m.bucle;
        mezcla[nueva] = 0f;
        mezclaObjetivo[nueva] = 1f;
        mezclaObjetivo[1 - nueva] = 0f;
        rapidezMezcla = 1f / Mathf.Max(0.05f, fundido);
        fuente = nueva;
    }

    private void ActualizarMusica(float dt)
    {
        musicaMult = Mathf.MoveTowards(musicaMult, musicaMultObjetivo, dt * (musicaMultObjetivo < musicaMult ? 2.5f : 0.8f));
        for (int i = 0; i < 2; i++)
        {
            AudioSource a = musica[i];
            mezcla[i] = Mathf.MoveTowards(mezcla[i], mezclaObjetivo[i], dt * rapidezMezcla);
            a.volume = volumenFase[i] * mezcla[i] * musicaMult * ControlVolumen.Musica;
            if (a.isPlaying && mezcla[i] <= 0f && mezclaObjetivo[i] <= 0f) a.Stop();
            if (a.isPlaying && bucle[i].y > 0f && a.time >= bucle[i].y) a.time = bucle[i].x;
        }
    }

    // ------------------------------------------------------------------ Ayudas

    private void PonerMuro(bool cerrado)
    {
        if (muro != null) muro.enabled = cerrado;
        foreach (SpriteRenderer s in visualMuro) if (s != null) s.color = cerrado ? muroCerrado : muroAbierto;
    }

    // En las muertes (falsas y la final) la camara la sigue a ella: es su momento.
    private void SeguirJefa(bool aElla)
    {
        if (GameManager.Instance == null) return;
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        Transform t = aElla && jefe != null ? jefe.transform : p != null ? p.transform : null;
        if (t != null) GameManager.Instance.SeguirConCamara(t);
    }

    // Tiempo real: la entrada no depende de la camara lenta.
    private static IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime) yield return null;
    }

    public void ForzarOscuridad(bool on) => oscuridad.Activar(on);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f);
        Gizmos.DrawWireCube(zona.center, zona.size);
        Gizmos.DrawLine(new Vector3(zona.xMin, suelo), new Vector3(zona.xMax, suelo));
        Gizmos.DrawWireSphere(new Vector3(xAparicion, suelo), 0.4f);
    }
}
