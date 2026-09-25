using UnityEngine;
using UnityEngine.UI;

// Barra de vida pequena sobre la cabeza de un enemigo, en espacio de mundo, al estilo
// de Dark Souls: oculta hasta el primer golpe, luego visible, y se desvanece si pasa
// un rato sin recibir dano. Con estela de dano, como la del jugador.
//
// La crea EnemyHealth y solo escucha lo que este le dice: no lleva logica de vida.
// No es hija del enemigo a proposito: el enemigo se voltea invirtiendo su escala y
// el destello de golpe la estira, y la barra se daria la vuelta o temblaria con el.
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Color backColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);
    [SerializeField] private Color fillColor = new Color(0.72f, 0.1f, 0.08f, 1f);
    [SerializeField] private Color trailColor = new Color(0.95f, 0.86f, 0.74f, 0.9f);
    // Alto de la barra en unidades del mundo (dos pixeles del juego, mas o menos).
    [SerializeField] private float height = 0.07f;
    [SerializeField] private float trailDelay = 0.4f;
    [SerializeField] private float trailSharpness = 4f;
    // Segundos sin recibir dano antes de desvanecerse.
    [SerializeField] private float fadeDelay = 4f;
    [SerializeField] private float fadeSpeed = 2.5f;

    // Cuantos pixeles de canvas hay por unidad del mundo. Solo sirve para no trabajar
    // con tamanos diminutos dentro del canvas.
    private const float PixelesPorUnidad = 100f;

    private Transform objetivo;
    private Vector2 offset;
    private CanvasGroup grupo;
    private RectTransform relleno;
    private RectTransform estela;

    private float vida = 1f;
    private float vidaEstela = 1f;
    private float esperaEstela;
    private float sinDano;
    private bool mostrada;
    private bool muriendo;

    public static EnemyHealthBar Crear(Transform enemigo, Vector2 offset, float ancho)
    {
        GameObject go = new GameObject($"BarraVida ({enemigo.name})");
        EnemyHealthBar barra = go.AddComponent<EnemyHealthBar>();
        barra.objetivo = enemigo;
        barra.offset = offset;
        barra.Construir(ancho);
        barra.Seguir();
        return barra;
    }

    public void Mostrar(int actual, int maximo)
    {
        float nueva = maximo > 0 ? Mathf.Clamp01((float)actual / maximo) : 0f;

        // La estela se queda donde estaba (mas arriba) y espera antes de bajar.
        vidaEstela = Mathf.Max(vidaEstela, vida);
        vida = nueva;
        esperaEstela = trailDelay;

        mostrada = true;
        sinDano = 0f;
        grupo.alpha = 1f;
        Dibujar();
    }

    // Al morir: se va apagando rapido en vez de desaparecer de golpe.
    public void Ocultar()
    {
        muriendo = true;
        sinDano = fadeDelay;
    }

    private void LateUpdate()
    {
        if (objetivo == null) { Destroy(gameObject); return; }

        Seguir();
        float dt = Time.deltaTime;

        // Estela: espera y luego baja rapido al principio y frena al final.
        if (vidaEstela > vida)
        {
            if (esperaEstela > 0f) esperaEstela -= dt;
            else
            {
                float paso = (vidaEstela - vida) * (1f - Mathf.Exp(-trailSharpness * dt));
                vidaEstela = Mathf.Max(vida, vidaEstela - Mathf.Max(paso, 0.03f * dt));
            }
        }

        // Visible mientras recibe dano o la estela aun baja; luego se desvanece.
        if (mostrada)
        {
            sinDano += dt;
            bool visible = !muriendo && (sinDano < fadeDelay || vidaEstela > vida);
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, visible ? 1f : 0f, (muriendo ? fadeSpeed * 2f : fadeSpeed) * dt);
        }

        Dibujar();
    }

    private void Seguir()
    {
        transform.position = (Vector2)objetivo.position + offset;
    }

    private void Dibujar()
    {
        Anclar(relleno, vida);
        Anclar(estela, vidaEstela);
    }

    private void Construir(float ancho)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Por encima de todo lo del escenario, incluido el polvo.
        int capa = SortingLayer.NameToID("VFX");
        if (SortingLayer.IsValid(capa)) canvas.sortingLayerID = capa;
        canvas.sortingOrder = 20;

        RectTransform raiz = (RectTransform)transform;
        raiz.sizeDelta = new Vector2(ancho, height) * PixelesPorUnidad;
        transform.localScale = Vector3.one / PixelesPorUnidad;

        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        RectTransform fondo = Rect("Fondo", raiz, backColor);
        Estirar(fondo);
        estela = Rect("Estela", fondo, trailColor);
        relleno = Rect("Relleno", fondo, fillColor);
        Dibujar();
    }

    private static RectTransform Rect(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // Relleno sin Image Filled (que no funciona sin sprite): se estira el ancho.
    private static void Anclar(RectTransform r, float fraccion)
    {
        if (r == null) return;
        r.anchorMin = Vector2.zero;
        r.anchorMax = new Vector2(Mathf.Clamp01(fraccion), 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
