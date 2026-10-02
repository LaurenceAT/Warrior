using System.Collections;
using UnityEngine;

// Sello elemental: una barrera que solo se abre con la espada imbuida en un
// elemento concreto (sombra -> sagrado, hielo -> fuego...). Guarda un atajo, una
// sala secreta, un cofre de almas o una estatua.
//   - Abierto una vez, sigue abierto en la partida (bandera "sello_<escena>_<clave>").
//   - Sin la imbuicion correcta, al golpearlo o al acercarse reacciona un poco:
//     parpadea y suena (solo los de enseñanza dicen algo con texto).
//   - Con la imbuicion correcta, al acercarse brilla mas: "ahora si".
// Colores, sonidos y textos: Resources/PistasSellos.
public class SelloElemental : MonoBehaviour, IGolpeable
{
    public enum Estilo { Sombra, Hielo }

    public Elemento elemento = Elemento.Sagrado;
    public Estilo estilo = Estilo.Sombra;
    [Tooltip("Golpes con el elemento correcto para abrirlo.")]
    public int golpesNecesarios = 2;
    [Tooltip("Identificador para recordarlo abierto en la partida.")]
    public string clave = "sello";
    [Tooltip("Sello de enseñanza: al golpearlo con otro elemento dice con texto lo que necesita.")]
    public bool ensenanza;
    [Tooltip("Distancia a la que reacciona al acercarse el player.")]
    public float radioReaccion = 2.6f;
    public Collider2D solido;
    public SpriteRenderer[] visuales = new SpriteRenderer[0];
    [Tooltip("Simbolos (runas, grietas de color) que se apagan al abrirlo.")]
    public SimboloSello[] simbolos = new SimboloSello[0];

    public bool Abierto { get; private set; }
    private string Bandera => "sello_" + gameObject.scene.name + "_" + clave;

    private int golpes;
    private float siguienteTexto, siguienteReaccion, siguienteGolpe, parpadeo, t;
    private bool cercaAntes;
    private Vector3[] posiciones;
    private Color[] colores;
    private Transform player;

    private void Awake()
    {
        if (visuales == null) visuales = new SpriteRenderer[0];
        posiciones = new Vector3[visuales.Length];
        colores = new Color[visuales.Length];
        for (int i = 0; i < visuales.Length; i++)
        {
            if (visuales[i] == null) continue;
            posiciones[i] = visuales[i].transform.localPosition;
            colores[i] = visuales[i].color;
        }
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(clave) && Partida.Bandera(Bandera)) AbrirYa();
    }

    private PistasSellos.Entrada Datos => PistasSellos.Get().De(elemento);

    private Elemento ImbuicionPlayer()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.transform : null;
        }
        ArmaImbuida a = player != null ? player.GetComponent<ArmaImbuida>() : null;
        return a != null ? a.Activo : Elemento.Ninguno;
    }

    private void Update()
    {
        if (Abierto) return;
        t += Time.deltaTime;
        parpadeo = Mathf.MoveTowards(parpadeo, 0f, Time.deltaTime * 3f);
        bool listo = false;

        // Al acercarse: reacciona si falta la imbuicion; brilla si ya la lleva.
        if (player != null || Time.frameCount % 15 == 0) ImbuicionPlayer();
        if (player != null)
        {
            bool cerca = Vector2.Distance(player.position, transform.position) < radioReaccion;
            listo = cerca && ImbuicionPlayer() == elemento;
            if (cerca && !cercaAntes && !listo && Time.time >= siguienteReaccion) Reaccionar(0.6f);
            cercaAntes = cerca;
        }

        Color tono = Datos.color;
        for (int i = 0; i < visuales.Length; i++)
        {
            SpriteRenderer s = visuales[i];
            if (s == null) continue;
            Color c = colores[i];
            if (estilo == Estilo.Sombra) c.a *= 0.75f + 0.25f * Mathf.Sin(t * 2.5f + i);
            float brillo = (listo ? 0.35f + 0.2f * Mathf.Sin(t * 6f) : 0f) + parpadeo;
            c = Color.Lerp(c, new Color(tono.r, tono.g, tono.b, c.a), Mathf.Clamp01(brillo));
            s.color = c;
        }
    }

    // Sin la imbuicion correcta: un parpadeo y un sonido (y texto si es de enseñanza).
    private void Reaccionar(float fuerza)
    {
        siguienteReaccion = Time.time + 2.5f;
        parpadeo = fuerza;
        Sonido.Reproducir(Datos.sonidoReaccion, 0.45f);
        StartCoroutine(Temblar(0.15f, 0.03f));
    }

    public void Golpear(Elemento e, Vector2 punto)
    {
        if (Abierto || Time.time < siguienteGolpe) return;
        siguienteGolpe = Time.time + 0.15f;
        PistasSellos.Entrada d = Datos;
        if (e != elemento)
        {
            parpadeo = 1f;
            Sonido.Reproducir(d.sonidoReaccion, 0.7f);
            ParticulasFx.Rafaga(punto, 6, d.color, Color.white, new Vector2(1f, 2.5f), 0.5f, new Vector2(0.04f, 0.08f), new Vector2(0.2f, 0.4f));
            StartCoroutine(Temblar(0.12f, 0.03f));
            if (ensenanza && !string.IsNullOrEmpty(d.textoPista) && Time.time >= siguienteTexto)
            {
                siguienteTexto = Time.time + 4f;
                TextoFlotante.Mostrar(d.textoPista, punto + Vector2.up * 1.3f, Color.Lerp(d.color, Color.white, 0.4f), 0.8f);
            }
            return;
        }
        golpes++;
        Sonido.Reproducir("golpe_" + (int)elemento);
        ParticulasFx.Rafaga(punto, 16, d.color, Color.white, new Vector2(1f, 4f), -0.2f, new Vector2(0.05f, 0.1f), new Vector2(0.4f, 0.8f));
        StartCoroutine(Temblar(0.2f, 0.06f));
        for (int i = 0; i < visuales.Length; i++)
            if (visuales[i] != null) { Color c = colores[i]; c.a *= 1f - 0.25f * golpes / Mathf.Max(1, golpesNecesarios); colores[i] = c; }
        if (golpes >= golpesNecesarios) StartCoroutine(Abrir(punto));
    }

    private IEnumerator Temblar(float dur, float fuerza)
    {
        for (float k = 0f; k < dur; k += Time.deltaTime)
        {
            for (int i = 0; i < visuales.Length; i++)
                if (visuales[i] != null) visuales[i].transform.localPosition = posiciones[i] + (Vector3)(Random.insideUnitCircle * fuerza);
            yield return null;
        }
        for (int i = 0; i < visuales.Length; i++) if (visuales[i] != null) visuales[i].transform.localPosition = posiciones[i];
    }

    private IEnumerator Abrir(Vector2 punto)
    {
        Abierto = true;
        if (!string.IsNullOrEmpty(clave)) Partida.PonerBandera(Bandera);
        PistasSellos.Entrada d = Datos;
        Sonido.Reproducir(d.sonidoAbrir);
        Sonido.Reproducir("secreto_descubierto", 0.45f);
        CamaraDinamica.Sacudir(0.35f);
        QuitarSolidos();
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (SpriteRenderer s in visuales) if (s != null) b.Encapsulate(s.bounds);
        for (int i = 0; i < 6; i++)
            ParticulasFx.Rafaga(new Vector2(b.center.x, Mathf.Lerp(b.min.y, b.max.y, i / 5f)), 16, d.color, Color.white,
                                new Vector2(1.5f, 5f), estilo == Estilo.Hielo ? 2f : -0.4f, new Vector2(0.05f, 0.14f), new Vector2(0.5f, 1.1f));
        if (!string.IsNullOrEmpty(d.textoAbrir))
            TextoFlotante.Mostrar(d.textoAbrir, (Vector2)b.center + Vector2.up * (b.extents.y + 0.3f), Color.Lerp(d.color, Color.white, 0.4f), 1f);
        foreach (SimboloSello s in simbolos) if (s != null) s.Apagar();
        for (float k = 0f; k < 0.7f; k += Time.deltaTime)
        {
            foreach (SpriteRenderer s in visuales)
                if (s != null) { Color c = s.color; c.a *= 1f - k / 0.7f; s.color = c; s.transform.localScale *= 1f + Time.deltaTime * 0.3f; }
            yield return null;
        }
        foreach (SpriteRenderer s in visuales) if (s != null) s.enabled = false;
    }

    // Ya estaba abierto en la partida: sin efectos.
    private void AbrirYa()
    {
        Abierto = true;
        QuitarSolidos();
        foreach (SpriteRenderer s in visuales) if (s != null) s.enabled = false;
        foreach (SimboloSello s in simbolos) if (s != null) s.Apagar(true);
    }

    private void QuitarSolidos()
    {
        if (solido != null) solido.enabled = false;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
    }

    // Depuracion: abrir o cerrar sin golpes.
    public void ForzarAbrir() { if (!Abierto) StartCoroutine(Abrir(transform.position)); }

    private void OnDrawGizmos()
    {
        Color c = PistasSellos.Get().De(elemento).color;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radioReaccion);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, "Sello: " + Elementos.Nombre(elemento));
#endif
    }
}
