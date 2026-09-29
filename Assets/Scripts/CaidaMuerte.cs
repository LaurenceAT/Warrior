using UnityEngine;

// Muerte en el aire: en vez de la animacion de muerte (de rodillas, flotando),
// el cuerpo sale despedido con la animacion de derribo, cae con la gravedad y,
// al tocar el suelo, se queda tumbado (el ultimo fotograma del derribo). Solo
// entonces sale la sangre y la pantalla de muerte (GameManager espera a que
// acabe antes de hacer reaparecer al player).
// No se usa si no hay suelo debajo (caida al vacio) ni en el agarre del jefe.
public class CaidaMuerte : MonoBehaviour
{
    // Hay una caida en curso: la reaparicion espera.
    public static bool EnCurso { get; private set; }

    private SpriteRenderer sr;
    private Sprite[] fotogramas;
    private Vector2 velocidad;
    private float gravedad;
    private float pies;          // distancia del centro del objeto a los pies
    private bool contraJefe;
    private float t;
    private bool enSuelo;
    private float tiempoEnSuelo;

    // Segundos como mucho en el aire (red de seguridad).
    private const float MaximoEnAire = 4f;
    private const float Fps = 12f;

    public static void Crear(SpriteRenderer origen, Sprite[] fotogramas, Vector2 velocidad, float gravedad, float pies, bool contraJefe)
    {
        GameObject go = new GameObject("CaidaMuerte");
        go.transform.position = origen.transform.position;
        go.transform.localScale = origen.transform.lossyScale;
        CaidaMuerte c = go.AddComponent<CaidaMuerte>();
        c.sr = go.AddComponent<SpriteRenderer>();
        c.sr.sprite = fotogramas[0];
        c.sr.flipX = origen.flipX;
        c.sr.sortingLayerID = origen.sortingLayerID;
        c.sr.sortingOrder = origen.sortingOrder;
        c.sr.sharedMaterial = origen.sharedMaterial;
        c.fotogramas = fotogramas;
        // Sale algo despedido hacia atras y hacia arriba, sin pasarse.
        c.velocidad = new Vector2(Mathf.Clamp(velocidad.x, -3f, 3f), Mathf.Clamp(velocidad.y, -12f, 3f));
        c.gravedad = gravedad;
        c.pies = pies;
        c.contraJefe = contraJefe;
        EnCurso = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;

        if (!enSuelo)
        {
            // Primero el golpe (fotogramas 0 y 1), luego cayendo (2..penultimo).
            int penultimo = Mathf.Max(0, fotogramas.Length - 2);
            int f = (int)(t * Fps);
            if (f > penultimo) f = penultimo - 1 + (f - penultimo) % 2; // aleteo en el aire
            sr.sprite = fotogramas[Mathf.Clamp(f, 0, penultimo)];

            velocidad.x = Mathf.MoveTowards(velocidad.x, 0f, 2f * dt);
            velocidad.y -= gravedad * dt;
            Vector2 pos = transform.position;
            Vector2 siguiente = pos + velocidad * dt;

            // Suelo entre esta posicion y la siguiente (a la altura de los pies).
            if (velocidad.y <= 0f)
            {
                bool antes = Physics2D.queriesStartInColliders;
                Physics2D.queriesStartInColliders = false;
                RaycastHit2D h = Physics2D.Raycast(new Vector2(siguiente.x, pos.y - pies + 0.05f), Vector2.down,
                                                   Mathf.Abs(velocidad.y * dt) + 0.1f, LayerMask.GetMask("Ground"));
                Physics2D.queriesStartInColliders = antes;
                if (h.collider != null && !h.collider.isTrigger)
                {
                    siguiente.y = h.point.y + pies;
                    Aterrizar(new Vector2(siguiente.x, h.point.y));
                }
            }
            transform.position = siguiente;
            if (t > MaximoEnAire && !enSuelo) Aterrizar(transform.position);
            return;
        }

        tiempoEnSuelo += dt;
        // Un momento tumbado y entonces la pantalla de muerte.
        if (tiempoEnSuelo >= 0.45f && EnCurso)
        {
            EnCurso = false;
            PantallaMuerte.Mostrar(contraJefe);
        }
    }

    private void Aterrizar(Vector2 suelo)
    {
        enSuelo = true;
        sr.sprite = fotogramas[fotogramas.Length - 1];
        CamaraDinamica.Sacudir(0.25f);
        SangreFx.MuertePlayer(suelo);
    }

    // El cuerpo se va cuando reaparece el player (como el de la muerte normal).
    private void OnEnable() { GameManager.AlReaparecerPlayer += Quitar; }
    private void OnDisable() { GameManager.AlReaparecerPlayer -= Quitar; }
    private void Quitar() { if (this != null) Destroy(gameObject); }

    private void OnDestroy()
    {
        EnCurso = false;
    }
}
