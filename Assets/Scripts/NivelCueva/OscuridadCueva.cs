using System.Collections.Generic;
using UnityEngine;

// Oscuridad de la cueva nueva. Se oscurece al entrar en una ZonaOscura (con un
// fundido) y vuelve al salir. Dentro solo se ve lo que alumbran las FuenteLuz:
// un circulo alrededor del player, las antorchas encendidas y las hogueras.
// El player, los enemigos y los efectos van por encima: siempre se leen.
// Los peligros (grietas, suelos falsos) llevan su propio brillo tenue.
public class OscuridadCueva : MonoBehaviour
{
    public static OscuridadCueva Instancia { get; private set; }

    [Header("Ajustes (editables jugando)")]
    [Tooltip("Radio de la luz alrededor del player (unidades).")]
    public float radioJugador = 3.2f;
    [Tooltip("Segundos del fundido al entrar o salir de una zona oscura.")]
    public float transicion = 0.75f;
    [Tooltip("Lo negra que es la oscuridad (0 = nada, 1 = negro total).")]
    [Range(0f, 1f)] public float intensidad = 0.9f;
    [Tooltip("Suavidad del borde de cada luz.")]
    [Range(0.05f, 1f)] public float suavidad = 0.45f;
    public Color color = new Color(0.01f, 0.01f, 0.03f, 1f);
    [Tooltip("Capa y orden de dibujo: por encima del escenario, por debajo de los personajes.")]
    public string capa = "Items";
    public int orden = 900;
    [Tooltip("Shader de la oscuridad (lo pone el generador; asi entra en la build).")]
    public Shader shader;

    // Desde la depuracion: false apaga la oscuridad aunque se este en una zona.
    public static bool Permitida = true;

    private readonly List<ZonaOscura> dentro = new List<ZonaOscura>();
    private MeshRenderer quad;
    private Material material;
    private float actual;
    private Transform player;
    private readonly Vector4[] luces = new Vector4[8];
    private static readonly int IdLuces = Shader.PropertyToID("_Luces");
    private static readonly int IdCantidad = Shader.PropertyToID("_Cantidad");
    private static readonly int IdOscuridad = Shader.PropertyToID("_Oscuridad");
    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdBorde = Shader.PropertyToID("_Borde");

    public float Nivel => actual;
    public bool Oscuro => actual > 0.02f;

    private void Awake()
    {
        Instancia = this;
        Shader s = shader != null ? shader : Shader.Find("Warrior/OscuridadCueva");
        material = new Material(s);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Velo";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        quad = go.GetComponent<MeshRenderer>();
        quad.sharedMaterial = material;
        quad.sortingLayerName = capa;
        quad.sortingOrder = orden;
        quad.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public void Entrar(ZonaOscura z) { if (!dentro.Contains(z)) dentro.Add(z); }
    public void Salir(ZonaOscura z) => dentro.Remove(z);
    public bool EnZonaOscura => dentro.Count > 0;

    private void LateUpdate()
    {
        dentro.RemoveAll(z => z == null || !z.isActiveAndEnabled);
        float objetivo = Permitida && dentro.Count > 0 ? 1f : 0f;
        actual = Mathf.MoveTowards(actual, objetivo, Time.deltaTime / Mathf.Max(0.05f, transicion));
        quad.enabled = actual > 0.001f;
        if (!quad.enabled) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        // El quad cubre la vista entera (con margen).
        float alto = cam.orthographicSize * 2f + 2f;
        float ancho = alto * cam.aspect + 2f;
        Vector3 c = cam.transform.position;
        quad.transform.position = new Vector3(c.x, c.y, 0f);
        quad.transform.localScale = new Vector3(ancho, alto, 1f);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Las 8 luces mas cercanas a la camara (el player siempre la primera).
        int n = 0;
        if (player != null) luces[n++] = new Vector4(player.position.x, player.position.y + 0.3f, radioJugador, 1f);
        FuenteLuz.Cercanas(c, luces, ref n);
        material.SetVectorArray(IdLuces, luces);
        material.SetFloat(IdCantidad, n);
        material.SetFloat(IdOscuridad, intensidad * actual);
        material.SetColor(IdColor, color);
        material.SetFloat(IdBorde, suavidad);
    }
}
