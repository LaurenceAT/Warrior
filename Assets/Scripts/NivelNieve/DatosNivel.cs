using System.Collections.Generic;
using UnityEngine;

// Datos propios de un nivel: el sonido de ambiente, la musica de combate y las
// frases de la pantalla de muerte.
//
// El audio va por estados y se decide en cada momento (no por eventos sueltos,
// que tras morir y reintentar dejaban el ambiente sonando encima del jefe):
//   - Explorando: solo el ambiente de la nevada.
//   - Combate (un enemigo comun te ha visto y esta cerca): el ambiente baja casi
//     del todo y entra la musica de combate. Al acabar vuelve el ambiente.
//   - Jefe (hay un combate de jefe en curso): ni ambiente ni musica de combate;
//     solo suena la musica del jefe (la pone su arena).
public class DatosNivel : MonoBehaviour
{
    private enum Estado { Explorando, Combate, Jefe }

    [SerializeField] private string[] frasesMuerte;
    // Musica de combate contra enemigos comunes.
    [SerializeField] private AudioClip musica;
    [Range(0f, 1f)] [SerializeField] private float volumenMusica = 0.35f;
    [SerializeField] private AudioClip ambiente;
    [Range(0f, 1f)] [SerializeField] private float volumenAmbiente = 0.4f;
    // Distancia a la que un enemigo en alerta cuenta como combate.
    [SerializeField] private float distanciaCombate = 12f;
    // Tras el ultimo enemigo en combate, cuanto se espera para volver al ambiente.
    [SerializeField] private float esperaFinCombate = 3f;

    private AudioSource fuenteMusica, fuenteAmbiente;
    private Estado estado = Estado.Explorando;
    private float ultimoCombate = -99f;
    private float siguienteBusqueda;
    private Transform player;
    private readonly List<EnemigoBase> enemigos = new List<EnemigoBase>();
    private float siguienteLista;

    // El ambiente tambien lo mira la ventisca (su viento baja con el jefe).
    public static float FactorAmbiente { get; private set; } = 1f;

    private void Awake()
    {
        PantallaMuerte.FrasesNivel = frasesMuerte;
    }

    private void Start()
    {
        fuenteMusica = Fuente(musica, 0f);
        fuenteAmbiente = Fuente(ambiente, 0f);
    }

    private AudioSource Fuente(AudioClip clip, float vol)
    {
        if (clip == null) return null;
        AudioSource a = gameObject.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = true;
        a.volume = vol;
        a.Play();
        return a;
    }

    private void OnDisable()
    {
        if (PantallaMuerte.FrasesNivel == frasesMuerte) PantallaMuerte.FrasesNivel = null;
        FactorAmbiente = 1f;
    }

    private void Update()
    {
        estado = DecidirEstado();

        float musicaObjetivo = estado == Estado.Combate ? volumenMusica * ControlVolumen.Musica : 0f;
        float ambienteObjetivo = estado == Estado.Explorando ? volumenAmbiente : estado == Estado.Combate ? volumenAmbiente * 0.12f : 0f;
        FactorAmbiente = estado == Estado.Explorando ? 1f : estado == Estado.Combate ? 0.25f : 0f;

        // Entrar en combate o en el jefe es rapido; volver a la calma, lento.
        float dt = Time.unscaledDeltaTime;
        if (fuenteMusica != null)
            fuenteMusica.volume = Mathf.MoveTowards(fuenteMusica.volume, musicaObjetivo, dt * (musicaObjetivo > fuenteMusica.volume ? 0.6f : estado == Estado.Jefe ? 1.5f : 0.2f));
        if (fuenteAmbiente != null)
            fuenteAmbiente.volume = Mathf.MoveTowards(fuenteAmbiente.volume, ambienteObjetivo, dt * (ambienteObjetivo > fuenteAmbiente.volume ? 0.15f : 0.8f));
    }

    private Estado DecidirEstado()
    {
        if (ArenaJefe.EnCombate) return Estado.Jefe;

        if (Time.unscaledTime >= siguienteBusqueda)
        {
            siguienteBusqueda = Time.unscaledTime + 0.25f;
            if (HayEnemigoEnCombate()) ultimoCombate = Time.unscaledTime;
        }
        return Time.unscaledTime - ultimoCombate < esperaFinCombate ? Estado.Combate : Estado.Explorando;
    }

    private bool HayEnemigoEnCombate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return false;
            player = p.transform;
        }
        // La lista de enemigos se refresca de vez en cuando (reaparecen al descansar).
        if (Time.unscaledTime >= siguienteLista)
        {
            siguienteLista = Time.unscaledTime + 1f;
            enemigos.Clear();
            enemigos.AddRange(FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None));
        }
        foreach (EnemigoBase e in enemigos)
        {
            if (e == null || e is JefeBase || !e.EnCombate) continue;
            if (Vector2.Distance(e.transform.position, player.position) <= distanciaCombate) return true;
        }
        return false;
    }
}
