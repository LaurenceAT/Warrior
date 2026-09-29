using UnityEngine;

// Estatua con una pista. Al acercarse sale "Presiona F para leer"; con la F se
// abre el cuadro de texto (CuadroPista). Mientras no se haya leido, brilla un
// poco: un contorno de un pixel que late despacio y alguna chispa suelta.
// Lo leido se recuerda (en la partida guardada) y aparece en el libro de pistas
// del menu de pausa.
public class EstatuaPista : MonoBehaviour, IInteractuable
{
    [Tooltip("Nombre que sale arriba del cuadro y en el libro de pistas.")]
    public string titulo = "Inscripción";
    [TextArea(3, 8)] public string texto;
    [Tooltip("Identificador unico en el nivel (no cambiarlo si ya hay partidas: se usa para saber si esta leida).")]
    public string id;

    [Header("Lectura")]
    [Tooltip("Letras por segundo de la maquina de escribir.")]
    public float velocidadEscritura = 38f;
    public float radio = 1.4f;
    [Tooltip("Altura del aviso \"Presiona F\" sobre el pie de la estatua.")]
    public float alturaAviso = 3.2f;

    [Header("Brillo mientras no se ha leido")]
    public bool brillo = true;
    public Color colorBrillo = new Color(0.55f, 0.85f, 1f, 1f);
    [Range(0f, 1f)] public float intensidadBrillo = 0.45f;
    [Tooltip("Chispas por segundo (0 = ninguna).")]
    public float chispas = 0.8f;

    private SpriteRenderer sr, contorno;
    private Material materialContorno;
    private float siguienteChispa;

    public Vector2 PuntoInteraccion => sr != null ? new Vector2(sr.bounds.center.x, sr.bounds.min.y + 0.5f) : (Vector2)transform.position;
    public float RadioInteraccion => radio;
    public bool PuedeInteractuar => !string.IsNullOrEmpty(texto) && !CuadroPista.Abierto;
    public string TextoInteraccion => "Presiona F para leer";
    public bool Leida => Partida.PistaLeidaYa(Escena, Id);

    private string Escena => gameObject.scene.name;
    private string Id => string.IsNullOrEmpty(id) ? $"{Mathf.RoundToInt(transform.position.x)}_{Mathf.RoundToInt(transform.position.y)}" : id;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        Interacciones.Registrar(this);
    }

    private void OnDisable()
    {
        Interacciones.Quitar(this);
    }

    private void Start()
    {
        // El aviso va sobre el pie de la estatua, no sobre su centro.
        // (Crear lo pone en coordenadas locales: se quita la escala de la estatua.)
        float alto = sr != null ? sr.bounds.min.y - transform.position.y : 0f;
        AvisoInteraccion.Crear(transform, this, (alto + alturaAviso) / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y)));
    }

    public void Interactuar(PlayerControler p)
    {
        Partida.LeerPista(Escena, Id, titulo, texto);
        CuadroPista.Mostrar(titulo, texto, velocidadEscritura, p);
    }

    private void LateUpdate()
    {
        bool mostrar = brillo && sr != null && !Leida;
        if (!mostrar)
        {
            if (contorno != null) contorno.enabled = false;
            return;
        }
        if (contorno == null && !CrearContorno()) return;

        contorno.enabled = sr.enabled;
        contorno.sprite = sr.sprite;
        contorno.flipX = sr.flipX;
        contorno.sortingLayerID = sr.sortingLayerID;
        contorno.sortingOrder = sr.sortingOrder + 1;
        // Late muy despacio: se ve, pero no llama la atencion en combate.
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f);
        materialContorno.SetColor("_AuraColor", colorBrillo);
        materialContorno.SetFloat("_Amount", intensidadBrillo * (0.45f + 0.55f * pulso));

        if (chispas > 0f && Time.time >= siguienteChispa)
        {
            siguienteChispa = Time.time + Random.Range(0.6f, 1.4f) / chispas;
            Bounds b = sr.bounds;
            Vector2 p = new Vector2(Random.Range(b.min.x + b.size.x * 0.2f, b.max.x - b.size.x * 0.2f), Random.Range(b.min.y + b.size.y * 0.3f, b.max.y));
            ParticulasFx.Rafaga(p, 1, colorBrillo, Color.white, new Vector2(0.1f, 0.3f), -0.05f,
                                new Vector2(0.04f, 0.06f), new Vector2(0.6f, 1.1f), 40f, 90f);
        }
    }

    // Copia del sprite con el sombreador del aura: un contorno de un pixel.
    private bool CrearContorno()
    {
        Shader s = RecursosRPG.Get().shaderAura;
        if (s == null) s = Shader.Find("Sprites/Aura");
        if (s == null) return false;
        materialContorno = new Material(s);
        materialContorno.SetFloat("_Width", 1f);
        materialContorno.SetFloat("_Inner", 0.06f);
        GameObject go = new GameObject("BrilloPista");
        go.transform.SetParent(sr.transform, false);
        contorno = go.AddComponent<SpriteRenderer>();
        contorno.sharedMaterial = materialContorno;
        return true;
    }

    private void OnDestroy()
    {
        if (materialContorno != null) Destroy(materialContorno);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(PuntoInteraccion, radio);
    }
}
