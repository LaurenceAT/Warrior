using System;
using UnityEngine;

// Estados que los enemigos le acumulan al player, como en Elden Ring:
//   - Sangrado: al llenarse, quita de golpe una parte de la vida maxima.
//   - Congelacion: al llenarse, quita algo de vida y, durante un rato, el player
//     recibe mas dano y recupera la estamina mas despacio.
//   - Quemadura: al llenarse, quema durante unos segundos.
//
// Cada golpe con ese estado llena su barra (se ve en el HUD, bajo la vida, el
// mana y la estamina, con su icono). Si se dejan de recibir golpes, la barra baja
// sola: despacio cuando esta casi llena y mas rapido a partir de la mitad. Al
// llegar a cero, la barra y su icono desaparecen. Descansar en la hoguera la vacia.
//
// Solo lo usan los ataques especiales de los jefes: sus golpes normales no
// acumulan nada. Se anade solo al player la primera vez (EstadosPlayer.Acumular).
public enum EstadoPlayer { Ninguno = -1, Sangrado = 0, Congelacion = 1, Quemadura = 2 }

public class EstadosPlayer : MonoBehaviour
{
    public const int Cantidad = 3;
    public static readonly EstadoPlayer[] Todos = { EstadoPlayer.Sangrado, EstadoPlayer.Congelacion, EstadoPlayer.Quemadura };

    // Lo pone el jefe de la cueva: el efecto del estallido de sangre.
    public static AnimadorHoja.Clip efectoSangrado;

    [Serializable]
    public class Ajuste
    {
        [Tooltip("Lo que hay que acumular para que salte.")]
        public float maximo = 100f;
        [Tooltip("Segundos sin recibir golpes antes de que la barra empiece a bajar.")]
        public float esperaAntesDeBajar = 1.5f;
        [Tooltip("Lo que baja por segundo cuando esta casi llena.")]
        public float bajadaLenta = 5f;
        [Tooltip("Lo que baja por segundo de la mitad para abajo.")]
        public float bajadaRapida = 16f;
        [Tooltip("Dano al saltar, en parte de la vida maxima (0.15 = 15 %).")]
        [Range(0f, 1f)] public float danoFraccion = 0.15f;
        [Tooltip("Dano fijo que se suma al saltar.")]
        public int danoFijo = 0;
        [Tooltip("Segundos que dura el efecto despues de saltar (la barra se vacia en ese tiempo).")]
        public float duracionEfecto = 1f;
    }

    [SerializeField] private Ajuste sangrado = new Ajuste { danoFraccion = 0.15f, danoFijo = 8, duracionEfecto = 1f };
    [SerializeField] private Ajuste congelacion = new Ajuste { danoFraccion = 0.1f, duracionEfecto = 10f };
    [SerializeField] private Ajuste quemadura = new Ajuste { danoFraccion = 0.2f, duracionEfecto = 3f };

    [Header("Congelacion (mientras dura)")]
    [Tooltip("Dano recibido extra (1.15 = +15 %).")]
    [SerializeField] private float congelacionDanoExtra = 1.15f;
    [Tooltip("Ritmo de recuperacion de la estamina (0.5 = la mitad).")]
    [Range(0f, 1f)] [SerializeField] private float congelacionEstamina = 0.5f;

    [Header("Quemadura")]
    [SerializeField] private float quemaduraIntervalo = 0.5f;

    private class Barra
    {
        public float carga;
        public float ultimaSubida = -99f;
        public float finEfecto = -99f;
        public float siguienteTick;
    }

    private readonly Barra[] barras = { new Barra(), new Barra(), new Barra() };
    private PlayerControler player;

    public static EstadosPlayer Instancia { get; private set; }

    // Suma carga del estado al player. Devuelve el componente (lo crea si falta).
    public static void Acumular(PlayerControler p, EstadoPlayer e, float cantidad)
    {
        if (p == null || e == EstadoPlayer.Ninguno || cantidad <= 0f) return;
        EstadosPlayer s = p.GetComponent<EstadosPlayer>();
        if (s == null) s = p.gameObject.AddComponent<EstadosPlayer>();
        s.Sumar(e, cantidad);
    }

    public static string ClaveIcono(EstadoPlayer e)
    {
        switch (e)
        {
            case EstadoPlayer.Sangrado: return "jugador_sangrado";
            case EstadoPlayer.Congelacion: return "jugador_congelacion";
            default: return "jugador_quemadura";
        }
    }

    public static string Nombre(EstadoPlayer e)
    {
        switch (e)
        {
            case EstadoPlayer.Sangrado: return "Sangrado";
            case EstadoPlayer.Congelacion: return "Congelación";
            default: return "Quemadura";
        }
    }

    public static Color ColorDe(EstadoPlayer e)
    {
        switch (e)
        {
            case EstadoPlayer.Sangrado: return new Color(0.78f, 0.06f, 0.1f, 1f);
            case EstadoPlayer.Congelacion: return new Color(0.45f, 0.82f, 1f, 1f);
            default: return new Color(1f, 0.5f, 0.12f, 1f);
        }
    }

    private Ajuste AjusteDe(EstadoPlayer e) => e == EstadoPlayer.Sangrado ? sangrado : e == EstadoPlayer.Congelacion ? congelacion : quemadura;

    // ------------------------------------------------------------------ Consultas (HUD y dano)

    public bool EnEfecto(EstadoPlayer e) => e != EstadoPlayer.Ninguno && Time.time < barras[(int)e].finEfecto;

    // Lo que muestra la barra del HUD (0..1). Durante el efecto se vacia con el tiempo.
    public float Fraccion(EstadoPlayer e)
    {
        if (e == EstadoPlayer.Ninguno) return 0f;
        Barra b = barras[(int)e];
        Ajuste a = AjusteDe(e);
        if (EnEfecto(e)) return Mathf.Clamp01((b.finEfecto - Time.time) / Mathf.Max(0.01f, a.duracionEfecto));
        return Mathf.Clamp01(b.carga / Mathf.Max(1f, a.maximo));
    }

    public bool Visible(EstadoPlayer e) => Fraccion(e) > 0.001f;

    public float MultiplicadorDanoRecibido => EnEfecto(EstadoPlayer.Congelacion) ? congelacionDanoExtra : 1f;
    public float MultiplicadorRegenEstamina => EnEfecto(EstadoPlayer.Congelacion) ? congelacionEstamina : 1f;

    // ------------------------------------------------------------------ Ciclo

    private void Awake()
    {
        player = GetComponent<PlayerControler>();
        Instancia = this;
    }

    private void OnEnable() { Hoguera.AlDescansar += Limpiar; }
    private void OnDisable() { Hoguera.AlDescansar -= Limpiar; }
    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    public void Limpiar()
    {
        foreach (Barra b in barras) { b.carga = 0f; b.finEfecto = -99f; }
    }

    private void Sumar(EstadoPlayer e, float cantidad)
    {
        Barra b = barras[(int)e];
        // Mientras dura el efecto no se vuelve a llenar.
        if (EnEfecto(e)) return;
        b.carga += cantidad;
        b.ultimaSubida = Time.time;
        if (b.carga >= AjusteDe(e).maximo) Saltar(e);
    }

    private void Saltar(EstadoPlayer e)
    {
        Barra b = barras[(int)e];
        Ajuste a = AjusteDe(e);
        b.carga = 0f;
        b.finEfecto = Time.time + Mathf.Max(0.05f, a.duracionEfecto);
        b.siguienteTick = Time.time + quemaduraIntervalo;
        Color c = ColorDe(e);
        Vector2 cabeza = (Vector2)transform.position + Vector2.up * 1.3f;
        TextoFlotante.Mostrar("¡" + Nombre(e) + "!", cabeza, c, 1.1f);
        ScreenFlash.Destello(new Color(c.r, c.g, c.b, 0.35f), 0.25f);

        int max = player != null ? player.VidaMaxima : 100;
        switch (e)
        {
            case EstadoPlayer.Sangrado:
                if (efectoSangrado != null)
                    EfectoVisual.Crear(efectoSangrado, (Vector2)transform.position + Vector2.up * 0.2f, 1.1f, Color.white);
                Sonido.Reproducir("jugador_sangrado");
                Danar(Mathf.RoundToInt(max * a.danoFraccion) + a.danoFijo);
                break;
            case EstadoPlayer.Congelacion:
                Sonido.Reproducir("hielo_congelar");
                ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.4f, 22, new Color(0.85f, 0.95f, 1f), c,
                                    new Vector2(1f, 3f), 0.8f, new Vector2(0.05f, 0.11f), new Vector2(0.4f, 0.8f));
                Danar(Mathf.RoundToInt(max * a.danoFraccion) + a.danoFijo);
                break;
            case EstadoPlayer.Quemadura:
                // El dano se reparte en toques durante el efecto (Update).
                Sonido.Reproducir("golpe_0", 0.8f);
                break;
        }
    }

    private void Danar(int dano)
    {
        if (player != null) player.DanoEstado(Mathf.Max(1, dano));
    }

    private void Update()
    {
        float ahora = Time.time;
        for (int i = 0; i < barras.Length; i++)
        {
            EstadoPlayer e = (EstadoPlayer)i;
            Barra b = barras[i];
            Ajuste a = AjusteDe(e);

            if (EnEfecto(e))
            {
                if (e == EstadoPlayer.Quemadura && ahora >= b.siguienteTick)
                {
                    b.siguienteTick = ahora + quemaduraIntervalo;
                    int toques = Mathf.Max(1, Mathf.RoundToInt(a.duracionEfecto / Mathf.Max(0.05f, quemaduraIntervalo)));
                    int max = player != null ? player.VidaMaxima : 100;
                    Danar(Mathf.RoundToInt((max * a.danoFraccion + a.danoFijo) / toques));
                    ParticulasFx.Rafaga((Vector2)transform.position + new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), 0.2f), 4,
                                        new Color(1f, 0.6f, 0.15f), new Color(1f, 0.2f, 0.05f), new Vector2(0.6f, 1.4f), -0.6f,
                                        new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f), 60f, 90f);
                }
                continue;
            }

            if (b.carga <= 0f || ahora - b.ultimaSubida < a.esperaAntesDeBajar) continue;
            // Baja lento cuando esta casi llena y mas rapido de la mitad para abajo.
            float u = b.carga / Mathf.Max(1f, a.maximo);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, u));
            float velocidad = Mathf.Lerp(a.bajadaRapida, a.bajadaLenta, k);
            b.carga = Mathf.Max(0f, b.carga - velocidad * Time.deltaTime);
        }
    }
}
