using System;
using System.Collections.Generic;
using UnityEngine;

// Sonidos, iconos y tajos de color que usan los sistemas nuevos (almas, mana,
// imbuir, hoguera, estados...). Vive en Resources, asi cualquier nivel lo tiene
// sin montar nada en la escena. Lo rellena el generador del nivel nevado
// (Warrior > Crear nivel Nieve, o Warrior > Configurar recursos RPG).
[CreateAssetMenu(menuName = "Warrior/Recursos RPG")]
public class RecursosRPG : ScriptableObject
{
    [Serializable]
    public class GrupoSonido
    {
        public string clave;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volumen = 0.7f;
        // Variacion de tono al azar (0.05 = +-5 %), para que no suene repetido.
        public float variacionTono = 0.06f;
    }

    [Serializable]
    public class EntradaIcono
    {
        public string clave;
        public Sprite sprite;
    }

    public List<GrupoSonido> sonidos = new List<GrupoSonido>();
    public List<EntradaIcono> iconos = new List<EntradaIcono>();
    // Tajos de color: 3 por elemento (horizontal, curvo, ascendente), en el orden
    // de Elemento (Fuego, Hielo, Oscuro, Sagrado, Acido).
    public AnimadorHoja.Clip[] tajosElemento = new AnimadorHoja.Clip[0];
    // Chispa de impacto elemental (se tine con el color del elemento).
    public AnimadorHoja.Clip impactoElemental;
    public AnimadorHoja.Clip fxCongelado;
    // Shader del aura del player imbuido (se referencia aqui para que entre en la build).
    public Shader shaderAura;

    private static RecursosRPG instancia;
    private Dictionary<string, GrupoSonido> porClave;
    private Dictionary<string, Sprite> iconoPorClave;

    public static RecursosRPG Get()
    {
        if (instancia == null)
        {
            instancia = Resources.Load<RecursosRPG>("RecursosRPG");
            if (instancia == null) instancia = CreateInstance<RecursosRPG>();
        }
        return instancia;
    }

    public GrupoSonido Sonido(string clave)
    {
        if (porClave == null)
        {
            porClave = new Dictionary<string, GrupoSonido>();
            foreach (GrupoSonido g in sonidos) if (g != null && !string.IsNullOrEmpty(g.clave)) porClave[g.clave] = g;
        }
        porClave.TryGetValue(clave, out GrupoSonido r);
        return r;
    }

    public Sprite Icono(string clave)
    {
        if (iconoPorClave == null)
        {
            iconoPorClave = new Dictionary<string, Sprite>();
            foreach (EntradaIcono i in iconos) if (i != null && !string.IsNullOrEmpty(i.clave)) iconoPorClave[i.clave] = i.sprite;
        }
        iconoPorClave.TryGetValue(clave, out Sprite s);
        return s;
    }

    public AnimadorHoja.Clip Tajo(Elemento e, int tipo)
    {
        int i = (int)e * 3 + Mathf.Clamp(tipo, 0, 2);
        if (e == Elemento.Ninguno || tajosElemento == null || i < 0 || i >= tajosElemento.Length) return null;
        return tajosElemento[i];
    }

    private void OnValidate()
    {
        porClave = null;
        iconoPorClave = null;
    }
}

// Reproduce los sonidos de RecursosRPG por su clave, con un pequeno grupo de
// fuentes reutilizables. El volumen general ya lo aplica el AudioListener.
public static class Sonido
{
    private static readonly List<AudioSource> fuentes = new List<AudioSource>();
    private static Transform raiz;
    // Evita que un mismo sonido se apile muchas veces en el mismo instante (un
    // tajo que toca a cinco enemigos a la vez).
    private static readonly Dictionary<string, float> ultimo = new Dictionary<string, float>();

    public static void Reproducir(string clave, float volumen = 1f, float tono = 1f) => ReproducirYDevolver(clave, volumen, tono);

    private static AudioSource ReproducirYDevolver(string clave, float volumen, float tono)
    {
        RecursosRPG.GrupoSonido g = RecursosRPG.Get().Sonido(clave);
        if (g == null || g.clips == null || g.clips.Length == 0) return null;
        if (ultimo.TryGetValue(clave, out float t) && Time.unscaledTime - t < 0.03f) return null;
        ultimo[clave] = Time.unscaledTime;

        AudioClip clip = g.clips[UnityEngine.Random.Range(0, g.clips.Length)];
        if (clip == null) return null;
        AudioSource f = Libre();
        f.pitch = tono * (1f + UnityEngine.Random.Range(-g.variacionTono, g.variacionTono));
        f.volume = g.volumen * volumen;
        f.clip = clip;
        f.Play();
        return f;
    }

    // Sonido en un canal: el anterior del mismo canal se apaga con un fundido
    // corto (no se corta en seco ni se amontona). Lo usan los espadazos.
    private static readonly Dictionary<string, AudioSource> canales = new Dictionary<string, AudioSource>();

    public static void ReproducirCanal(string canal, string clave, float volumen = 1f, float tono = 1f)
    {
        // El mismo sonido en el mismo instante (un tajo que toca a varios): uno solo.
        if (ultimo.TryGetValue(clave, out float tt) && Time.unscaledTime - tt < 0.03f) return;
        if (canales.TryGetValue(canal, out AudioSource previa) && previa != null && previa.isPlaying)
        {
            FundidoFuente f = previa.GetComponent<FundidoFuente>();
            if (f == null) f = previa.gameObject.AddComponent<FundidoFuente>();
            f.Empezar(0.07f);
        }
        canales[canal] = ReproducirYDevolver(clave, volumen, tono);
    }

    // Bucle con su propia fuente (viento, ambiente). Devuelve la fuente para
    // poder fundirla o pararla.
    public static AudioSource Bucle(string clave, float volumen = 1f)
    {
        RecursosRPG.GrupoSonido g = RecursosRPG.Get().Sonido(clave);
        if (g == null || g.clips == null || g.clips.Length == 0 || g.clips[0] == null) return null;
        AudioSource f = new GameObject("Bucle_" + clave).AddComponent<AudioSource>();
        f.clip = g.clips[0];
        f.loop = true;
        f.playOnAwake = false;
        f.spatialBlend = 0f;
        f.volume = g.volumen * volumen;
        f.Play();
        return f;
    }

    private static AudioSource Libre()
    {
        if (raiz == null)
        {
            raiz = new GameObject("Sonidos").transform;
            UnityEngine.Object.DontDestroyOnLoad(raiz.gameObject);
            fuentes.Clear();
        }
        foreach (AudioSource f in fuentes)
            if (f != null && !f.isPlaying && (f.GetComponent<FundidoFuente>() == null || !f.GetComponent<FundidoFuente>().enabled)) return f;

        if (fuentes.Count >= 24)
        {
            // Todas ocupadas: se reutiliza la que lleve mas tiempo sonando.
            AudioSource vieja = fuentes[0];
            fuentes.RemoveAt(0);
            fuentes.Add(vieja);
            return vieja;
        }
        AudioSource nueva = new GameObject("Fuente").AddComponent<AudioSource>();
        nueva.transform.SetParent(raiz, false);
        nueva.playOnAwake = false;
        nueva.spatialBlend = 0f;
        // El sonido sigue durante el hit stop y la pausa de la hoguera.
        nueva.ignoreListenerPause = true;
        fuentes.Add(nueva);
        return nueva;
    }
}

// Baja el volumen de una fuente hasta callarla (y la deja lista para reutilizarse).
public class FundidoFuente : MonoBehaviour
{
    private AudioSource fuente;
    private float duracion, t, inicio;

    public void Empezar(float segundos)
    {
        fuente = GetComponent<AudioSource>();
        duracion = Mathf.Max(0.01f, segundos);
        t = 0f;
        inicio = fuente.volume;
        enabled = true;
    }

    private void Update()
    {
        if (fuente == null) { enabled = false; return; }
        t += Time.unscaledDeltaTime;
        fuente.volume = Mathf.Lerp(inicio, 0f, t / duracion);
        if (t < duracion) return;
        fuente.Stop();
        enabled = false;
    }
}
