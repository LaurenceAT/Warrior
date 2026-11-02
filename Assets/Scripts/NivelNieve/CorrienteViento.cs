using UnityEngine;

// Corriente de viento en un vacio: sopla hacia arriba, se calma un momento (el
// aviso), sopla hacia abajo, se calma, y vuelta a empezar (numeros en la ficha
// AjustesCorriente). Solo empuja al player en el aire: si salta mientras sopla
// hacia arriba, el viento lo sube; hacia abajo, lo hunde. En el suelo no hace nada.
// No toca el PlayerControler: ajusta la velocidad vertical de su cuerpo, igual
// que el viento de la ventisca se suma a la horizontal.
// Los copos muestran la direccion (y casi se paran en las pausas); al empezar
// cada soplido suena el viento.
[RequireComponent(typeof(BoxCollider2D))]
public class CorrienteViento : MonoBehaviour
{
    public enum Fase { Subida, PausaTrasSubida, Bajada, PausaTrasBajada }

    public AjustesCorriente ajustes;
    // Depuracion y pruebas: false = no empuja.
    public static bool Activa = true;

    private BoxCollider2D zona;
    private PlayerControler player;
    private Rigidbody2D cuerpo;
    private AgarreCornisa cornisa;
    private bool dentro;
    private Fase faseAnterior;
    private float acumuladoCopos;

    public Fase FaseActual => FaseEn(Time.time);

    public Fase FaseEn(float t)
    {
        AjustesCorriente a = ajustes;
        float ciclo = a.duracionSubida + a.pausaTrasSubida + a.duracionBajada + a.pausaTrasBajada;
        float f = Mathf.Repeat(t, Mathf.Max(0.01f, ciclo));
        if (f < a.duracionSubida) return Fase.Subida;
        f -= a.duracionSubida;
        if (f < a.pausaTrasSubida) return Fase.PausaTrasSubida;
        f -= a.pausaTrasSubida;
        return f < a.duracionBajada ? Fase.Bajada : Fase.PausaTrasBajada;
    }

    private void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

    private void Awake()
    {
        zona = GetComponent<BoxCollider2D>();
        zona.isTrigger = true;
        faseAnterior = FaseActual;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!otro.CompareTag("Player")) return;
        player = otro.GetComponent<PlayerControler>();
        if (player == null) return;
        cuerpo = player.GetComponent<Rigidbody2D>();
        cornisa = player.GetComponent<AgarreCornisa>();
        dentro = true;
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (otro.CompareTag("Player")) dentro = false;
    }

    private void FixedUpdate()
    {
        if (!Activa || ajustes == null || !dentro || player == null || cuerpo == null) return;
        if (player.EnSuelo || player.Agarrado || player.Interrumpido || (cornisa != null && cornisa.Activo)) return;
        Fase f = FaseActual;
        float vy = cuerpo.linearVelocityY;
        if (f == Fase.Subida && vy < ajustes.velocidadSubida)
            cuerpo.linearVelocityY = Mathf.MoveTowards(vy, ajustes.velocidadSubida, ajustes.aceleracion * Time.fixedDeltaTime);
        else if (f == Fase.Bajada && vy > -ajustes.velocidadBajada)
            cuerpo.linearVelocityY = Mathf.MoveTowards(vy, -ajustes.velocidadBajada, ajustes.aceleracion * Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (ajustes == null) return;
        Fase f = FaseActual;
        Transform p = player != null ? player.transform : null;
        if (p == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) { player = go.GetComponent<PlayerControler>(); p = go.transform; }
        }
        Bounds b = zona.bounds;
        bool cerca = p != null && Vector2.Distance(b.ClosestPoint(p.position), p.position) < ajustes.distanciaSonido;

        if (f != faseAnterior)
        {
            if (cerca && (f == Fase.Subida || f == Fase.Bajada))
                Sonido.Reproducir(ajustes.sonido, ajustes.volumen, f == Fase.Subida ? ajustes.tonoSubida : ajustes.tonoBajada);
            faseAnterior = f;
        }
        if (!cerca) return;

        // Copos: suben o bajan con el viento; en las pausas casi quietos.
        bool pausa = f == Fase.PausaTrasSubida || f == Fase.PausaTrasBajada;
        float dir = f == Fase.Subida ? 1f : f == Fase.Bajada ? -1f : f == Fase.PausaTrasSubida ? 1f : -1f;
        float vel = ajustes.velocidadCopos * (pausa ? ajustes.coposEnPausa : 1f) * dir;
        acumuladoCopos += ajustes.coposPorSegundo * Time.deltaTime;
        while (acumuladoCopos >= 1f)
        {
            acumuladoCopos -= 1f;
            // Solo cerca del player (la columna es alta): en su franja de pantalla.
            float y = p.position.y + Random.Range(-5f, 5f);
            if (y < b.min.y || y > b.max.y) continue;
            Vector2 pos = new Vector2(Random.Range(b.min.x + 0.2f, b.max.x - 0.2f), y);
            EmisorCueva.Emitir(EmisorCueva.Tipo.Mota, pos, new Vector2(Random.Range(-0.3f, 0.3f), vel), ajustes.colorCopos,
                               Random.Range(0.04f, 0.07f), Random.Range(0.8f, 1.3f));
        }
    }
}
