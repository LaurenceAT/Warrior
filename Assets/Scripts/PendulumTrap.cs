using UnityEngine;

// Hace oscilar la cadena y la bola alrededor del punto de anclaje, dejando la base quieta.
//
// No rota este objeto entero: rota solo las piezas que le indiques, girándolas
// alrededor del pivote. Así la base puede seguir atornillada al techo mientras el
// resto se balancea.
//
// El movimiento es calculado, no simulado con físicas: la amplitud y el ritmo son
// siempre los mismos y el jugador puede aprenderse el patrón. Un péndulo con joints
// se amortigua, deriva y acaba siendo impredecible, que es lo peor para una trampa
// que hay que cronometrar.
[DisallowMultipleComponent]
public class PendulumTrap : MonoBehaviour
{
    [Header("Piezas que se balancean")]
    // La cadena y la bola. NO incluyas aquí la base: esa debe quedarse quieta.
    [SerializeField] private Transform[] swingingParts;
    // Punto alrededor del que giran, en coordenadas locales de este objeto.
    // Debe coincidir con el sitio del que cuelga la cadena.
    [SerializeField] private Vector2 pivotOffset;

    [Header("Oscilación")]
    // Ángulo máximo a cada lado, en grados. 90 sería completamente horizontal.
    [SerializeField] private float swingAngle = 60f;
    // Segundos que tarda en completar un vaivén entero, ida y vuelta.
    [SerializeField] private float period = 2.5f;
    // Desfase inicial de 0 a 1. Ponlo distinto en cada péndulo para que no vayan
    // todos sincronizados y el nivel no parezca un metrónomo.
    [Range(0f, 1f)]
    [SerializeField] private float phaseOffset;
    // Ángulo central del recorrido. 0 = la cadena cuelga recta hacia abajo.
    [SerializeField] private float restAngle;
    [SerializeField] private bool reverse;

    // Posición y giro de cada pieza al arrancar, para calcular desde ahí.
    private Vector3[] startPositions;
    private Quaternion[] startRotations;

    #region Ciclo de Unity

    private void Awake()
    {
        CachePartes();
    }

    private void Update()
    {
        if (period <= 0f || swingingParts == null) return;

        // El seno se mueve despacio en los extremos y rápido al pasar por el centro,
        // que es exactamente cómo se comporta un péndulo real.
        float t = (Time.time / period + phaseOffset) * Mathf.PI * 2f;
        float angulo = Mathf.Sin(t) * swingAngle;
        if (reverse) angulo = -angulo;

        AplicarAngulo(restAngle + angulo);
    }

    // Dibuja el pivote y el arco que recorre la pieza más alejada.
    private void OnDrawGizmosSelected()
    {
        Vector3 pivote = transform.TransformPoint(pivotOffset);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pivote, 0.12f);

        float largo = LargoDelBrazo();
        if (largo <= 0f) return;

        float desde = restAngle - swingAngle;
        float hasta = restAngle + swingAngle;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(pivote, ExtremoDelBrazo(pivote, desde, largo));
        Gizmos.DrawLine(pivote, ExtremoDelBrazo(pivote, hasta, largo));

        Gizmos.color = Color.yellow;
        Vector3 anterior = ExtremoDelBrazo(pivote, desde, largo);
        for (int i = 1; i <= 24; i++)
        {
            Vector3 actual = ExtremoDelBrazo(pivote, Mathf.Lerp(desde, hasta, i / 24f), largo);
            Gizmos.DrawLine(anterior, actual);
            anterior = actual;
        }
    }

    #endregion

    // Guarda la postura de reposo de cada pieza.
    private void CachePartes()
    {
        if (swingingParts == null) return;

        startPositions = new Vector3[swingingParts.Length];
        startRotations = new Quaternion[swingingParts.Length];

        for (int i = 0; i < swingingParts.Length; i++)
        {
            if (swingingParts[i] == null) continue;
            startPositions[i] = swingingParts[i].localPosition;
            startRotations[i] = swingingParts[i].localRotation;
        }
    }

    // Coloca cada pieza girada ese ángulo alrededor del pivote.
    private void AplicarAngulo(float anguloGrados)
    {
        if (startPositions == null || startPositions.Length != swingingParts.Length) CachePartes();

        Quaternion giro = Quaternion.Euler(0f, 0f, anguloGrados);
        Vector3 pivote = pivotOffset;

        for (int i = 0; i < swingingParts.Length; i++)
        {
            Transform pieza = swingingParts[i];
            if (pieza == null) continue;

            pieza.localPosition = pivote + giro * (startPositions[i] - pivote);
            pieza.localRotation = giro * startRotations[i];
        }
    }

    // Distancia del pivote a la pieza más lejana, que es la que marca el arco.
    private float LargoDelBrazo()
    {
        if (swingingParts == null) return 0f;

        Vector3 pivote = transform.TransformPoint(pivotOffset);
        float maximo = 0f;

        foreach (Transform pieza in swingingParts)
        {
            if (pieza == null) continue;
            maximo = Mathf.Max(maximo, Vector3.Distance(pivote, pieza.position));
        }

        return maximo;
    }

    private Vector3 ExtremoDelBrazo(Vector3 pivote, float anguloGrados, float largo)
    {
        Vector3 abajo = Quaternion.Euler(0f, 0f, anguloGrados) * Vector3.down;
        return pivote + abajo * largo;
    }

#if UNITY_EDITOR
    // Rellena la lista con todos los hijos. Después quita a mano la base.
    [ContextMenu("Rellenar con los hijos")]
    private void RellenarConHijos()
    {
        UnityEditor.Undo.RecordObject(this, "Rellenar piezas del péndulo");

        swingingParts = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++) swingingParts[i] = transform.GetChild(i);

        Debug.Log($"'{name}': {swingingParts.Length} piezas añadidas. Quita la base de la lista.", this);
    }
#endif
}
