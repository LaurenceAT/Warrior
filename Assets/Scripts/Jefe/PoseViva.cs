using UnityEngine;

// Da vida a un jefe hecho de poses sueltas (el Crimson Wraith tiene casi una
// imagen por accion): sin fotogramas intermedios, el cambio de pose se ve "a
// saltos". Esto anade, solo en el dibujo:
//   - Respiracion: se hincha y deshincha despacio.
//   - Balanceo leve (los tentaculos).
//   - Inclinacion hacia donde se mueve.
//   - Estirones: se encoge al anticipar un golpe y se estira al soltarlo, y
//     vuelve a su tamano con un muelle.
// No toca la posicion ni el volteo: el volteo (mirar a un lado) lo sigue
// poniendo el jefe con el signo de la escala.
public class PoseViva : MonoBehaviour
{
    [Tooltip("Cuanto se hincha al respirar (0.015 = 1.5 %).")]
    public float respiracion = 0.015f;
    public float velocidadRespiracion = 2.6f;
    [Tooltip("Balanceo en grados.")]
    public float balanceo = 1.2f;
    [Tooltip("Inclinacion maxima al moverse (grados).")]
    public float inclinacion = 3f;
    [Tooltip("Rapidez con la que vuelve a su forma tras un estiron.")]
    public float muelle = 14f;

    [System.NonSerialized] public Rigidbody2D cuerpo;

    private Vector3 escalaBase;
    private Vector2 estiron = Vector2.one, objetivo = Vector2.one;
    private float finEstiron;
    private float inclinacionActual;

    private void Awake()
    {
        escalaBase = new Vector3(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y), 1f);
        if (cuerpo == null) cuerpo = GetComponentInParent<Rigidbody2D>();
    }

    // Se deforma hasta (ancho, alto) durante "segundos" y luego vuelve.
    public void Estirar(float ancho, float alto, float segundos)
    {
        objetivo = new Vector2(ancho, alto);
        finEstiron = Time.time + segundos;
    }

    // Anticipacion (se encoge) y golpe (se estira), los dos casos de siempre.
    public void Anticipar(float fuerza = 1f, float segundos = 0.4f) => Estirar(1f + 0.08f * fuerza, 1f - 0.08f * fuerza, segundos);
    public void Soltar(float fuerza = 1f, float segundos = 0.12f) => Estirar(1f - 0.07f * fuerza, 1f + 0.1f * fuerza, segundos);

    private void LateUpdate()
    {
        if (Time.time >= finEstiron) objetivo = Vector2.one;
        float k = 1f - Mathf.Exp(-muelle * Time.deltaTime);
        estiron = Vector2.Lerp(estiron, objetivo, k);

        float t = Time.time;
        float resp = 1f + respiracion * Mathf.Sin(t * velocidadRespiracion);
        float signo = transform.localScale.x < 0f ? -1f : 1f;
        transform.localScale = new Vector3(signo * escalaBase.x * estiron.x, escalaBase.y * estiron.y * resp, 1f);

        float vx = cuerpo != null ? cuerpo.linearVelocity.x : 0f;
        float objetivoIncl = -Mathf.Clamp(vx / 12f, -1f, 1f) * inclinacion;
        inclinacionActual = Mathf.Lerp(inclinacionActual, objetivoIncl, 1f - Mathf.Exp(-8f * Time.deltaTime));
        float giro = inclinacionActual + balanceo * Mathf.Sin(t * 1.7f);
        transform.localRotation = Quaternion.Euler(0f, 0f, giro);
    }
}
