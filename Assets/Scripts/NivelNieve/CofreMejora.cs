using System.Collections;
using UnityEngine;

// Cofre especial, escondido: guarda un objeto de mejora (Piedra de forja o
// Lagrima sagrada). Se abre con la F; el player levanta el objeto y sale el
// cartel de "objeto obtenido". Solo una vez por partida guardada.
//
// Para distinguirlo de un cofre normal tiene una pista sutil: un halo dorado
// que respira despacio y alguna chispa de vez en cuando.
//
// Los fotogramas salen de la hoja de cofres (fila de reposo y fila de apertura).
[RequireComponent(typeof(Collider2D))]
public class CofreMejora : MonoBehaviour, IInteractuable
{
    [Header("Contenido")]
    [Tooltip("Nombre unico dentro del nivel (para recordar que ya se abrio).")]
    [SerializeField] private string clave = "mejora";
    [SerializeField] private Equipo.Objeto objeto = Equipo.Objeto.PiedraForja;
    [SerializeField] private int cantidad = 1;
    [Tooltip("Mas objetos (uno de cada), ademas del principal.")]
    [SerializeField] private Equipo.Objeto[] extra = new Equipo.Objeto[0];
    [Tooltip("Almas que suelta al abrirlo (0 = ninguna).")]
    [SerializeField] private int almas;

    [Header("Aspecto")]
    [SerializeField] private SpriteRenderer visual;
    [Tooltip("Fotogramas en reposo (cerrado, con un ligero vaiven).")]
    [SerializeField] private Sprite[] reposo = new Sprite[0];
    [Tooltip("Fotogramas al abrirse; el ultimo es el cofre abierto.")]
    [SerializeField] private Sprite[] apertura = new Sprite[0];
    [SerializeField] private float fps = 8f;
    [SerializeField] private Color colorBrillo = new Color(1f, 0.82f, 0.4f, 1f);
    [Tooltip("Intensidad del halo (sutil: 0.2-0.35).")]
    [Range(0f, 1f)] [SerializeField] private float intensidadBrillo = 0.28f;
    [SerializeField] private float radio = 1.4f;

    private bool abierto;
    private SpriteRenderer halo;
    private float siguienteChispa;
    private Coroutine anim;

    private string Clave => "cofre_" + gameObject.scene.name + "_" + clave;

    public Vector2 PuntoInteraccion => transform.position;
    public float RadioInteraccion => radio;
    public bool PuedeInteractuar => !abierto;
    public string TextoInteraccion => "Presiona F para abrir";
    public bool Abierto => abierto;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        abierto = Partida.Bandera(Clave);
        if (abierto) Poner(apertura, apertura.Length - 1);
        else
        {
            CrearHalo();
            anim = StartCoroutine(Reposo());
        }
        AvisoInteraccion.Crear(transform, this, 1.2f);
    }

    private void OnEnable() => Interacciones.Registrar(this);
    private void OnDisable() => Interacciones.Quitar(this);

    private void Update()
    {
        if (abierto || halo == null) return;
        // Respira despacio: no llama la atencion, pero se nota al mirarlo.
        float k = 0.6f + 0.4f * Mathf.Sin(Time.time * 1.6f);
        halo.color = new Color(colorBrillo.r, colorBrillo.g, colorBrillo.b, intensidadBrillo * k);
        if (Time.time >= siguienteChispa)
        {
            siguienteChispa = Time.time + Random.Range(0.9f, 1.8f);
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.1f, 0.5f));
            ParticulasFx.Rafaga(p, 1, colorBrillo, Color.white, new Vector2(0.15f, 0.4f), -0.15f,
                                new Vector2(0.03f, 0.05f), new Vector2(0.6f, 1f), 30f, 90f);
        }
    }

    public void Interactuar(PlayerControler p)
    {
        if (abierto) return;
        abierto = true;
        Partida.PonerBandera(Clave);
        if (anim != null) StopCoroutine(anim);
        StartCoroutine(Abrir(p));
    }

    private IEnumerator Abrir(PlayerControler p)
    {
        Sonido.Reproducir("cofre_abrir", 0.9f);
        for (int i = 0; i < apertura.Length; i++)
        {
            Poner(apertura, i);
            yield return new WaitForSeconds(1f / Mathf.Max(1f, fps));
        }
        if (halo != null) StartCoroutine(ApagarHalo());
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.3f, 20, colorBrillo, Color.white, new Vector2(1f, 3f), -0.2f,
                            new Vector2(0.04f, 0.09f), new Vector2(0.4f, 0.8f), 60f, 90f);
        if (almas > 0) OrbeAlma.Soltar((Vector2)transform.position + Vector2.up * 0.6f, almas);
        Equipo.Sumar(objeto, cantidad);
        foreach (Equipo.Objeto o in extra) Equipo.Sumar(o);
        string nombre = Equipo.Nombre(objeto) + (cantidad > 1 ? " x" + cantidad : "");
        Sprite icono = RecursosRPG.Get().Icono(Equipo.ClaveIcono(objeto));
        if (p != null) p.LevantarObjeto(icono, nombre, Equipo.Descripcion(objeto));
        else AvisoObjeto.Mostrar(icono, nombre, Equipo.Descripcion(objeto));
        // Los demas, uno detras de otro.
        foreach (Equipo.Objeto o in extra)
        {
            yield return new WaitForSecondsRealtime(2.2f);
            AvisoObjeto.Mostrar(RecursosRPG.Get().Icono(Equipo.ClaveIcono(o)), Equipo.Nombre(o), Equipo.Descripcion(o));
        }
    }

    // Lo usa el modo desafio para montar su cofre a partir de uno del nivel.
    public void Configurar(string nuevaClave, Equipo.Objeto principal, Equipo.Objeto[] otros, int nuevasAlmas)
    {
        clave = nuevaClave;
        objeto = principal;
        cantidad = 1;
        extra = otros ?? new Equipo.Objeto[0];
        almas = nuevasAlmas;
        abierto = Partida.Bandera(Clave);
    }

    private IEnumerator Reposo()
    {
        int i = 0;
        while (reposo.Length > 0)
        {
            Poner(reposo, i);
            i = (i + 1) % reposo.Length;
            yield return new WaitForSeconds(1f / Mathf.Max(1f, fps * 0.6f));
        }
    }

    private IEnumerator ApagarHalo()
    {
        Color c = halo.color;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            halo.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.8f));
            yield return null;
        }
        Destroy(halo.gameObject);
        halo = null;
    }

    private void Poner(Sprite[] fotogramas, int i)
    {
        if (visual == null || fotogramas == null || fotogramas.Length == 0) return;
        visual.sprite = fotogramas[Mathf.Clamp(i, 0, fotogramas.Length - 1)];
    }

    private void CrearHalo()
    {
        halo = new GameObject("Halo").AddComponent<SpriteRenderer>();
        halo.transform.SetParent(transform, false);
        halo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        Vector3 e = transform.lossyScale;
        halo.transform.localScale = new Vector3(1.6f / Mathf.Max(0.01f, Mathf.Abs(e.x)), 1.1f / Mathf.Max(0.01f, Mathf.Abs(e.y)), 1f);
        halo.sprite = EstiloMenu.Resplandor();
        halo.sharedMaterial = EfectoVisual.MaterialSinLuz();
        if (visual != null)
        {
            halo.sortingLayerID = visual.sortingLayerID;
            halo.sortingOrder = visual.sortingOrder - 1;
        }
        halo.color = new Color(colorBrillo.r, colorBrillo.g, colorBrillo.b, 0f);
    }
}
