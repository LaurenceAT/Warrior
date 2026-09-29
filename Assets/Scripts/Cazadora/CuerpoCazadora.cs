using UnityEngine;

// El "cuerpo" de la Cazadora: solo se mueve, no decide nada.
//   - Andar/Parar: la velocidad se acerca a la pedida con aceleracion y frenada
//     (nunca cambia de golpe).
//   - Trayecto: dashes, saltos y caidas guiados por una curva, calculados en el
//     paso de fisica (con interpolacion: sin tirones en pantalla).
//   - Nunca sale de los limites de la arena (ni se mete en una pared): todo se
//     recorta a ellos, y un dash se detiene en el borde.
// El cuerpo es cinematico y el player lo atraviesa: no hay forma de quedarse
// atascados el uno contra el otro.
[RequireComponent(typeof(Rigidbody2D))]
public class CuerpoCazadora : MonoBehaviour
{
    public enum Curva { Lineal, Dash, Suave, Caida }

    [Tooltip("Distancia minima a las paredes de la arena.")]
    public float margen = 0.9f;

    private Rigidbody2D rb;
    private float xMin = -100f, xMax = 100f, suelo;
    private Vector2 pos;
    private float vx, objetivoVx, acel = 30f, fren = 40f;

    private bool guiado;
    private Vector2 desde, hasta;
    private float duracion, tt, altura;
    private Curva curva;
    private Vector2 anterior;

    public bool EnTrayecto => guiado;
    public Vector2 Posicion => pos;
    public float Suelo => suelo;
    public float XMin => xMin + margen;
    public float XMax => xMax - margen;
    // Velocidad real (tambien durante un trayecto): para el polvo y las estelas.
    public Vector2 Velocidad { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.useFullKinematicContacts = false;
        pos = rb.position;
    }

    public void Configurar(float izquierda, float derecha, float alturaSuelo)
    {
        xMin = izquierda;
        xMax = derecha;
        suelo = alturaSuelo;
        pos = new Vector2(Limitar(rb.position.x), Mathf.Max(suelo, rb.position.y));
        rb.position = pos;
        transform.position = pos;
    }

    public float Limitar(float x) => Mathf.Clamp(x, XMin, XMax);

    // Corre hacia la velocidad pedida (con signo), acelerando o frenando.
    public void Andar(float velocidad, float aceleracion, float frenada)
    {
        if (guiado) return;
        objetivoVx = velocidad;
        acel = Mathf.Max(0.1f, aceleracion);
        fren = Mathf.Max(0.1f, frenada);
    }

    public void Parar(float frenada)
    {
        objetivoVx = 0f;
        fren = Mathf.Max(0.1f, frenada);
    }

    // En seco (aturdida, parry). Se usa poco: casi todo frena con curva.
    public void Detener()
    {
        objetivoVx = 0f;
        vx = 0f;
    }

    // Se mueve de donde esta a "destino" en "segundos", con la curva pedida y,
    // si altura > 0, en arco (salto). El destino se recorta a la arena.
    public void Trayecto(Vector2 destino, float segundos, float alturaArco, Curva c)
    {
        desde = pos;
        hasta = new Vector2(Limitar(destino.x), Mathf.Max(suelo, destino.y));
        duracion = Mathf.Max(0.01f, segundos);
        altura = alturaArco;
        curva = c;
        tt = 0f;
        guiado = true;
        vx = objetivoVx = 0f;
    }

    // Corta el trayecto donde este (golpe de escudo, parry...). Si estaba en el
    // aire, cae al suelo.
    public void CortarTrayecto()
    {
        if (!guiado) return;
        guiado = false;
        if (pos.y > suelo + 0.05f) Trayecto(new Vector2(pos.x, suelo), Mathf.Clamp((pos.y - suelo) * 0.08f, 0.12f, 0.4f), 0f, Curva.Caida);
    }

    // Aparece en otro sitio. Solo bajo un fundido (nunca a la vista).
    public void Colocar(Vector2 p)
    {
        guiado = false;
        vx = objetivoVx = 0f;
        pos = new Vector2(Limitar(p.x), Mathf.Max(suelo, p.y));
        rb.position = pos;
        transform.position = pos;
        anterior = pos;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        anterior = pos;
        if (guiado)
        {
            tt += dt;
            float u = Mathf.Clamp01(tt / duracion);
            float e = Aplicar(curva, u);
            float x = Mathf.LerpUnclamped(desde.x, hasta.x, e);
            float y = Mathf.Lerp(desde.y, hasta.y, curva == Curva.Caida ? u * u : u) + altura * 4f * u * (1f - u);
            pos = new Vector2(Limitar(x), Mathf.Max(suelo, y));
            if (u >= 1f) { guiado = false; pos = new Vector2(Limitar(hasta.x), Mathf.Max(suelo, hasta.y)); }
        }
        else
        {
            bool acelerando = Mathf.Abs(objetivoVx) > Mathf.Abs(vx) && Mathf.Sign(objetivoVx) == Mathf.Sign(vx == 0f ? objetivoVx : vx);
            vx = Mathf.MoveTowards(vx, objetivoVx, (acelerando ? acel : fren) * dt);
            float x = pos.x + vx * dt;
            // Contra el borde: se para (no empuja la pared).
            if (x <= XMin || x >= XMax) vx = 0f;
            pos = new Vector2(Limitar(x), Mathf.Max(suelo, pos.y));
        }
        Velocidad = (pos - anterior) / dt;
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(pos);
    }

    private static float Aplicar(Curva c, float u)
    {
        switch (c)
        {
            // Sale disparada y frena al llegar: rapido pero legible.
            case Curva.Dash: return 1f - (1f - u) * (1f - u) * (1f - u);
            case Curva.Suave: return u * u * (3f - 2f * u);
            case Curva.Caida: return u;
            default: return u;
        }
    }
}
