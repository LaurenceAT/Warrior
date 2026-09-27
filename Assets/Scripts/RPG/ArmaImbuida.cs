using UnityEngine;

// La espada imbuida con un elemento (rueda de la E). Guarda cual esta puesto y
// cuanto le queda, y pinta el aura del color del elemento alrededor del player
// mientras dure. El gesto de imbuir (quieto un momento, sin poder atacar) lo
// hace el PlayerControler; aqui solo se aplica al terminar.
//
// Limites para que no se cambie sin pensar en mitad de una pelea:
//   - Cuesta mana (costeMana).
//   - El gesto tarda un poco y se interrumpe si te golpean (se pierde el mana).
//   - Tras imbuir hay que esperar "recarga" para volver a cambiar.
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
    private float siguienteChispa;

    public event System.Action AlCambiar;

    public Elemento Activo => activo;
    public float CosteMana => costeMana;
    public float TiempoGesto => tiempoGesto;
    public float Restante => activo == Elemento.Ninguno ? 0f : Mathf.Max(0f, fin - Time.time);
    public float FraccionRestante => duracion > 0f ? Restante / duracion : 0f;
    public float RecargaRestante => Mathf.Max(0f, siguienteCambio - Time.time);

    private void Awake()
    {
        origen = GetComponent<SpriteRenderer>();
    }

    // Motivo por el que no se puede imbuir ahora (null si se puede).
    public string Impedimento(PlayerMana mana)
    {
        if (RecargaRestante > 0f) return "Espera " + Mathf.CeilToInt(RecargaRestante) + " s";
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
        materialAura.SetColor("_AuraColor", c);
        materialAura.SetFloat("_Amount", cantidad);

        // Alguna chispa del elemento subiendo.
        if (Time.time >= siguienteChispa)
        {
            siguienteChispa = Time.time + Random.Range(0.15f, 0.3f);
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.4f, 0.3f));
            float grav = activo == Elemento.Acido ? 0.3f : activo == Elemento.Hielo ? 0.1f : -0.4f;
            ParticulasFx.Rafaga(p, 1, c, Color.Lerp(c, Color.white, 0.5f), new Vector2(0.3f, 0.8f), grav,
                                new Vector2(0.04f, 0.07f), new Vector2(0.4f, 0.8f), 50f, 90f);
        }
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
