using System.Collections;
using UnityEngine;

// Camara del combate con la Cazadora, encima de la CamaraDinamica de siempre:
//   - Encuadre que te mantiene a ti y a ella en pantalla y se abre si os
//     alejais (o cuando ella hace un dash largo).
//   - Temblores con tope, congelados breves al conectar golpes pesados, camara
//     lenta, acercamientos y la subida al caer ilusiones del cielo.
// Nunca te pierde de vista: el encuadre siempre parte de ti. Los valores estan en
// AjustesCazadora (seccion Camara). El zoom apaga el Pixel Perfect solo mientras
// dura (lo hace la CamaraDinamica): sin borrosidad al volver.
public class CamaraCazadora : MonoBehaviour
{
    private static CamaraCazadora inst;

    [Tooltip("Tamano normal de la camara en la arena.")]
    public float tamanoBase = 6.4f;

    private AjustesCazadora aj;
    private Transform jefe, player;
    private bool siguiendo;
    private float finAbrir, subida, finSubida;
    private Coroutine congelado;

    private void Awake() => inst = this;

    private void OnDestroy()
    {
        if (inst == this) inst = null;
        if (congelado != null && Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = 1f;
    }

    public void Configurar(AjustesCazadora ajustes) => aj = ajustes;

    public void Seguir(Transform elJefe, bool activo)
    {
        jefe = elJefe;
        siguiendo = activo;
    }

    // ------------------------------------------------------------------ API

    public static void Sacudir(float fuerza)
    {
        float tope = inst != null && inst.aj != null ? inst.aj.temblorMaximo : 1.5f;
        CamaraDinamica.Sacudir(Mathf.Min(fuerza, tope));
    }

    // Congela el juego un instante (tiempo real). "escala": lo lento que va.
    public static void Congelar(float segundos, float escala = 0.02f)
    {
        if (inst == null || segundos <= 0f || Time.timeScale < 0.05f) return;
        if (inst.congelado != null) inst.StopCoroutine(inst.congelado);
        inst.congelado = inst.StartCoroutine(inst.RutinaCongelar(segundos, escala));
    }

    public static void Lenta(float escala, float segundos) => CamaraDinamica.CamaraLenta(escala, segundos);

    public static void Acercar(float tamano, float segundos) => CamaraDinamica.Acercar(tamano, segundos);

    // Abre el plano hasta "tamano" un rato (el escudo: se ve todo a la vez).
    public static void Ampliar(float tamano, float segundos) => CamaraDinamica.Ampliar(tamano, segundos);

    // Abre el plano al maximo un rato (dash largo, cruces).
    public static void Abrir(float segundos)
    {
        if (inst != null) inst.finAbrir = Mathf.Max(inst.finAbrir, Time.time + segundos);
    }

    // La camara sube un poco (ilusiones cayendo del cielo).
    public static void Subir(float cuanto, float segundos)
    {
        if (inst == null) return;
        inst.subida = cuanto;
        inst.finSubida = Time.time + segundos;
    }

    private IEnumerator RutinaCongelar(float segundos, float escala)
    {
        float previa = Time.timeScale;
        Time.timeScale = Mathf.Min(previa, escala);
        yield return new WaitForSecondsRealtime(segundos);
        bool pausado = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (!pausado && Time.timeScale > 0f) Time.timeScale = previa >= 0.99f ? 1f : previa;
        congelado = null;
    }

    // ------------------------------------------------------------------ Encuadre

    private void LateUpdate()
    {
        if (!siguiendo || aj == null || jefe == null) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        Vector2 pj = player.position, bj = jefe.position;
        float d = Mathf.Abs(bj.x - pj.x);
        float u = Mathf.Clamp01((d - aj.distanciaApertura * 0.5f) / Mathf.Max(0.1f, aj.distanciaApertura));
        float tam = Mathf.Lerp(tamanoBase, aj.aperturaMaxima, u);
        if (Time.time < finAbrir) tam = aj.aperturaMaxima;
        if (tam > tamanoBase + 0.05f) CamaraDinamica.Ampliar(tam, 0.2f);

        float arriba = Time.time < finSubida ? subida : 0f;
        float peso = Mathf.Clamp01(aj.pesoEncuadre);
        Vector2 punto = new Vector2(bj.x, pj.y + (peso > 0.01f ? arriba / peso : 0f));
        CamaraDinamica.Encuadrar(punto, peso, 0.2f, aj.apartamientoMaximo);
    }
}
