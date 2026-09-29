using UnityEngine;

// La espada imbuida con un elemento (rueda de la E). Guarda cual esta puesto y
// cuanto le queda, y pinta el aura del color del elemento alrededor del player
// mientras dure. El gesto de imbuir (quieto un momento, sin poder atacar) lo
// hace el PlayerControler; aqui solo se aplica al terminar.
//
// Limites para que no se cambie sin pensar en mitad de una pelea:
//   - Cuesta mana (costeMana). El sangrado cuesta vida en su lugar
//     (AjustesProgreso: sube en proporcion a la vida maxima) y no se puede
//     imbuir si con eso te quedarias sin vida.
//   - El gesto tarda un poco y se interrumpe si te golpean (se pierde lo pagado).
//   - Tras imbuir hay que esperar "recarga" para volver a cambiar.
// Las particulas del arma mientras dura van aparte (EfectoArma).
public class ArmaImbuida : MonoBehaviour
{
    [SerializeField] private float costeMana = 25f;
    [SerializeField] private float duracion = 75f;
    [SerializeField] private float recarga = 4f;
    [SerializeField] private float tiempoGesto = 0.8f;

    private Elemento activo = Elemento.Ninguno;
    private float fin;
    private float siguienteCambio;

    private SpriteRenderer origen;
    private SpriteRenderer aura;
    private Material materialAura;

    public event System.Action AlCambiar;

    public Elemento Activo => activo;
    public float CosteMana => costeMana;
    public float TiempoGesto => tiempoGesto;
    public float Restante => activo == Elemento.Ninguno ? 0f : Mathf.Max(0f, fin - Time.time);
    public float FraccionRestante => duracion > 0f ? Restante / duracion : 0f;
    public float RecargaRestante => Mathf.Max(0f, siguienteCambio - Time.time);

    // Vida que cuesta el sangrado ahora (con la vida inicial, la de Ajustes; con
    // el doble de vida maxima, el doble).
    public static int CosteVidaSangrado
    {
        get
        {
            AjustesProgreso a = AjustesProgreso.Get();
            return Mathf.Max(1, Mathf.RoundToInt(a.sangradoCosteVida * Progreso.VidaMax / Mathf.Max(1f, a.vidaBase)));
        }
    }

    public static bool CuestaVida(Elemento e) => e == Elemento.Sangrado;

    private void Awake()
    {
        origen = GetComponent<SpriteRenderer>();
        if (GetComponent<EfectoArma>() == null) gameObject.AddComponent<EfectoArma>();
    }

    // Motivo por el que no se puede imbuir ahora (null si se puede).
    public string Impedimento(PlayerMana mana) => Impedimento(mana, Elemento.Ninguno, 0);

    // Lo mismo para un elemento concreto: el sangrado mira la vida, no el mana.
    public string Impedimento(PlayerMana mana, Elemento e, int vidaActual)
    {
        if (RecargaRestante > 0f) return "Espera " + Mathf.CeilToInt(RecargaRestante) + " s";
        if (CuestaVida(e)) return vidaActual > CosteVidaSangrado ? null : "No tienes vida suficiente";
        if (mana == null || !mana.Tiene(costeMana)) return "Sin maná suficiente";
        return null;
    }

    public void Activar(Elemento e)
    {
        activo = e;
        fin = Time.time + duracion;
        siguienteCambio = Time.time + recarga;
        AlCambiar?.Invoke();
    }

    public void Apagar()
    {
        if (activo == Elemento.Ninguno) return;
        activo = Elemento.Ninguno;
        if (aura != null) aura.enabled = false;
        AlCambiar?.Invoke();
    }

    private void Update()
    {
        if (activo != Elemento.Ninguno && Time.time >= fin)
        {
            TextoFlotante.Mostrar("El arma se apaga", (Vector2)transform.position + Vector2.up * 1.2f, new Color(0.8f, 0.8f, 0.85f), 0.75f);
            Apagar();
        }
    }

    // El aura copia el sprite del player cada fotograma (despues del Animator).
    private void LateUpdate()
    {
        if (activo == Elemento.Ninguno || origen == null)
        {
            if (aura != null) aura.enabled = false;
            return;
        }
        if (aura == null && !CrearAura()) return;

        aura.enabled = origen.enabled;
        aura.sprite = origen.sprite;
        aura.flipX = origen.flipX;
        aura.sortingLayerID = origen.sortingLayerID;
        aura.sortingOrder = origen.sortingOrder - 1;

        Color c = Elementos.Color(activo);
        // Late despacio; en los ultimos 10 s parpadea para avisar de que se acaba.
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f);
        float cantidad = 0.45f + 0.3f * pulso;
        if (Restante < 10f) cantidad *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
        // El rojo del sangrado es muy intenso: con el aura normal el player se veia
        // todo rojo, como si le hubieran golpeado.
        if (activo == Elemento.Sangrado) cantidad *= 0.45f;
        materialAura.SetColor("_AuraColor", c);
        materialAura.SetFloat("_Amount", cantidad);
        // Las chispas que salian del cuerpo ahora salen del arma (EfectoArma).
    }

    private bool CrearAura()
    {
        Shader s = RecursosRPG.Get().shaderAura;
        if (s == null) s = Shader.Find("Sprites/Aura");
        if (s == null) return false;
        materialAura = new Material(s);
        materialAura.SetFloat("_Width", 1.2f);
        materialAura.SetFloat("_Inner", 0.12f);
        GameObject go = new GameObject("AuraElemento");
        go.transform.SetParent(transform, false);
        aura = go.AddComponent<SpriteRenderer>();
        aura.sharedMaterial = materialAura;
        return true;
    }
}
