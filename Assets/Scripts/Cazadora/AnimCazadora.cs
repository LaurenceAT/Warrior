using UnityEngine;

// Muestra las animaciones de la Cazadora (SpritesCazadora) en dos capas: el
// cuerpo y el efecto blanco, que se tine del elemento. Sin Animator: la IA pide
// clips por nombre y mira en que cuadro va para sincronizar el golpe.
//
// Nunca ralentiza un tajo para "avisar": el aviso es una pose quieta (Pose) y el
// golpe se reproduce siempre a su ritmo. Asi el ataque se lee sin verse a
// camara lenta.
public class AnimCazadora : MonoBehaviour
{
    public SpritesCazadora hojas;
    public SpriteRenderer cuerpo;
    public SpriteRenderer efecto;

    // Ritmo externo (la escarcha del player la ralentiza; congelada = 0).
    [System.NonSerialized] public float multiplicador = 1f;

    private SpritesCazadora.Clip actual;
    private float t;
    private int fotograma = -1;
    private float velocidad = 1f;
    private bool pausado, alReves;
    private float temblor;
    private Color colorEfecto = Color.white;
    private float alfa = 1f;
    private MaterialPropertyBlock bloque;
    private float finDestello;
    private Color colorDestello = Color.white;
    private float fuerzaDestello;
    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    public string Actual => actual != null ? actual.nombre : null;
    public SpritesCazadora.Clip ClipActual => actual;
    public int Fotograma => fotograma;
    public bool Terminado => actual != null && !actual.bucle && !pausado &&
                             (alReves ? t * actual.fps >= actual.Cantidad : t * actual.fps >= actual.Cantidad);
    public Sprite SpriteCuerpo => cuerpo != null ? cuerpo.sprite : null;
    public bool Volteado => transform.lossyScale.x < 0f;
    // El cuadro actual es de golpe (efecto grande).
    public bool CuadroActivo => actual != null && actual.Activo(fotograma);

    private void Awake()
    {
        bloque = new MaterialPropertyBlock();
    }

    public void Reproducir(string nombre, float vel = 1f, int desde = 0)
    {
        SpritesCazadora.Clip c = hojas != null ? hojas.Buscar(nombre) : null;
        if (c == null || c.Cantidad == 0) { Debug.LogWarning("[Cazadora] Falta el clip " + nombre, this); return; }
        actual = c;
        velocidad = Mathf.Max(0.01f, vel);
        pausado = false;
        alReves = false;
        t = Mathf.Clamp(desde, 0, c.Cantidad - 1) / c.fps;
        fotograma = -1;
        Aplicar(Mathf.Clamp(desde, 0, c.Cantidad - 1));
    }

    // Del ultimo cuadro al primero (el polvo que se reconstruye).
    public void ReproducirAlReves(string nombre, float vel = 1f)
    {
        Reproducir(nombre, vel);
        if (actual == null) return;
        alReves = true;
        t = 0f;
        Aplicar(actual.Cantidad - 1);
    }

    // Un cuadro quieto (el aviso, la guardia, el aturdimiento...).
    public void Pose(string nombre, int cuadro)
    {
        SpritesCazadora.Clip c = hojas != null ? hojas.Buscar(nombre) : null;
        if (c == null || c.Cantidad == 0) return;
        actual = c;
        pausado = true;
        alReves = false;
        Aplicar(Mathf.Clamp(cuadro, 0, c.Cantidad - 1));
    }

    public void Velocidad(float vel) => velocidad = Mathf.Max(0.01f, vel);

    public void ColorEfecto(Color c)
    {
        colorEfecto = c;
        PintarEfecto();
    }

    public void Alfa(float a)
    {
        alfa = Mathf.Clamp01(a);
        if (cuerpo != null) { Color c = cuerpo.color; c.a = alfa; cuerpo.color = c; }
        PintarEfecto();
    }

    public float AlfaActual => alfa;

    public void Mostrar(bool v)
    {
        if (cuerpo != null) cuerpo.enabled = v;
        if (efecto != null) efecto.enabled = v;
    }

    // Temblor del sprite (rugido, aturdida, curandose). 0 = quieto.
    public void Temblar(float fuerza) => temblor = Mathf.Max(0f, fuerza);

    // Destello del cuerpo entero (blanco al recibir dano, rojo en el aviso de un
    // instakill...). Se apaga solo.
    public void Destello(Color c, float fuerza, float segundos)
    {
        colorDestello = c;
        fuerzaDestello = fuerza;
        finDestello = Time.time + segundos;
    }

    private void Update()
    {
        if (actual == null) return;
        if (!pausado)
        {
            t += Time.deltaTime * velocidad * multiplicador;
            int n = Mathf.FloorToInt(t * actual.fps);
            int i;
            if (actual.bucle) i = n % actual.Cantidad;
            else i = Mathf.Min(n, actual.Cantidad - 1);
            if (alReves) i = actual.Cantidad - 1 - i;
            if (i != fotograma) Aplicar(i);
        }
    }

    private void LateUpdate()
    {
        // Temblor: solo el dibujo, nunca la posicion real (ni la caja de dano).
        Vector3 d = temblor > 0f ? new Vector3(Random.Range(-temblor, temblor), Random.Range(-temblor, temblor) * 0.3f, 0f) : Vector3.zero;
        if (cuerpo != null) cuerpo.transform.localPosition = d;
        if (efecto != null) efecto.transform.localPosition = d;

        if (cuerpo == null) return;
        float f = Time.time < finDestello ? fuerzaDestello * Mathf.Clamp01((finDestello - Time.time) / 0.05f + 0.3f) : 0f;
        cuerpo.GetPropertyBlock(bloque);
        bloque.SetColor(IdFlashColor, colorDestello);
        bloque.SetFloat(IdFlashAmount, Mathf.Clamp01(f));
        cuerpo.SetPropertyBlock(bloque);
    }

    private void Aplicar(int i)
    {
        fotograma = i;
        if (cuerpo != null) cuerpo.sprite = i < actual.cuerpo.Length ? actual.cuerpo[i] : null;
        if (efecto != null) efecto.sprite = actual.efecto != null && i < actual.efecto.Length ? actual.efecto[i] : null;
    }

    private void PintarEfecto()
    {
        if (efecto == null) return;
        Color c = colorEfecto;
        c.a *= alfa;
        efecto.color = c;
    }
}
