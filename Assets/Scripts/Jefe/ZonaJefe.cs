using System.Linq;
using UnityEngine;

// Lo que tiene que ofrecer la arena de un jefe para usar una ZonaJefe
// (ArenaJefe y ArenaCazadora).
public interface IArenaJefe
{
    // La zona de entrada enlazada (la pone la ZonaJefe al empezar).
    ZonaJefe ZonaEntrada { get; set; }
    // Se puede empezar la pelea (no esta en curso ni vencido).
    bool PuedeEmpezar { get; }
    // Empieza la pelea. La arena llama a ZonaEntrada.Cerrar() cuando empieza de
    // verdad (la Cazadora, tras su dialogo) y a Abrir / Reiniciar al terminar.
    void EmpezarDesdeZona(PlayerControler p);
    // Donde aparece el jefe (para encuadrarlo con la camara).
    Vector2 PuntoJefe { get; }
}

// Prefab "Zona de jefe": la entrada a la pelea con un jefe.
//   - Antes de la pelea no hay niebla: se entra y se sale libremente.
//   - Activador invisible (este collider): al pisarlo (donde ya se ve al jefe)
//     empieza la pelea y la camara encuadra al jefe un momento.
//   - El muro de niebla (hijo MuroNiebla) se forma DETRAS del player y no deja
//     volver. Al vencer se disuelve y ya no vuelve (lo vencido sigue vencido).
//   - Si el player muere, al reaparecer el muro se disuelve y el activador se
//     vuelve a armar.
// Para un jefe nuevo: colocar el prefab en la entrada (a ras de suelo), poner su
// arena en "Arena" y ajustar el activador (se ve en la escena).
[RequireComponent(typeof(BoxCollider2D))]
public class ZonaJefe : MonoBehaviour
{
    [Header("Enlace con el jefe")]
    [Tooltip("La arena del jefe (ArenaJefe o ArenaCazadora). Vacio: la busca en el padre o la mas cercana.")]
    [SerializeField] private MonoBehaviour arena;

    [Header("Activador (invisible)")]
    [Tooltip("Lado de la arena respecto al muro: 1 = derecha, -1 = izquierda.")]
    [SerializeField] private int ladoArena = 1;
    [Tooltip("Distancia desde el muro hasta el activador: el punto desde el que ya se ve al jefe.")]
    public float distanciaActivador = 6f;
    [Tooltip("Largo del activador hacia dentro de la arena.")]
    public float largoActivador = 24f;
    [Tooltip("Alto del activador, desde el suelo.")]
    public float altoActivador = 14f;

    [Header("Camara al empezar")]
    [Tooltip("Mirar hacia el jefe al empezar (la Cazadora ya lo hace en su entrada).")]
    public bool encuadrarAlEmpezar = true;
    public float segundosEncuadre = 2.5f;
    [Range(0f, 1f)] public float pesoEncuadre = 0.55f;

    [Header("Seguridad")]
    [Tooltip("Separacion minima entre el player y el muro al cerrarse.")]
    public float margen = 0.3f;
    [Tooltip("Segundos tras reaparecer en los que el activador no hace caso.")]
    public float esperaTrasReaparecer = 1f;

    public MuroNiebla Muro { get; private set; }
    public IArenaJefe Arena => arena as IArenaJefe;
    public bool Armado { get; private set; } = true;
    public bool Cerrada { get; private set; }
    // Veces que se ha disparado el activador (para las pruebas).
    public int Disparos { get; private set; }
    public int LadoArena => ladoArena >= 0 ? 1 : -1;

    private BoxCollider2D activador;
    private float ignorarHasta;
    private PlayerControler player;
    private Rigidbody2D rbPlayer;
    private Collider2D colPlayer;

    private void Awake()
    {
        activador = GetComponent<BoxCollider2D>();
        activador.isTrigger = true;
        AjustarActivador();
        Muro = GetComponentInChildren<MuroNiebla>(true);
        if (arena == null || !(arena is IArenaJefe)) arena = BuscarArena();
        if (Arena != null) Arena.ZonaEntrada = this;
        else Debug.LogWarning("[ZonaJefe] " + name + " no tiene arena de jefe");
    }

    private void OnValidate()
    {
        ladoArena = ladoArena >= 0 ? 1 : -1;
        if (arena != null && !(arena is IArenaJefe))
            arena = arena.GetComponents<MonoBehaviour>().FirstOrDefault(c => c is IArenaJefe);
        BoxCollider2D b = GetComponent<BoxCollider2D>();
        if (b != null) { activador = b; AjustarActivador(); }
    }

    private void AjustarActivador()
    {
        activador.isTrigger = true;
        activador.size = new Vector2(Mathf.Max(0.5f, largoActivador), Mathf.Max(0.5f, altoActivador));
        activador.offset = new Vector2(LadoArena * (distanciaActivador + largoActivador * 0.5f), altoActivador * 0.5f - 0.5f);
    }

    private MonoBehaviour BuscarArena()
    {
        MonoBehaviour m = GetComponentsInParent<MonoBehaviour>(true).FirstOrDefault(c => c is IArenaJefe);
        if (m != null) return m;
        return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c => c is IArenaJefe)
               .OrderBy(c => Vector2.Distance(c.transform.position, transform.position)).FirstOrDefault();
    }

    private void OnEnable() => GameManager.AlReaparecerPlayer += AlReaparecer;
    private void OnDisable() => GameManager.AlReaparecerPlayer -= AlReaparecer;

    private void AlReaparecer()
    {
        ignorarHasta = Time.time + esperaTrasReaparecer;
        player = null;
    }

    // ------------------------------------------------------------------ Activador

    private void OnTriggerEnter2D(Collider2D otro) => Disparar(otro);
    private void OnTriggerStay2D(Collider2D otro) => Disparar(otro);

    private void Disparar(Collider2D otro)
    {
        if (!Armado || Time.time < ignorarHasta || !otro.CompareTag("Player")) return;
        IArenaJefe a = Arena;
        if (a == null || !a.PuedeEmpezar) return;
        PlayerControler p = otro.GetComponentInParent<PlayerControler>();
        if (p == null) return;
        Armado = false;
        Disparos++;
        Recordar(p);
        a.EmpezarDesdeZona(p);
        if (encuadrarAlEmpezar && segundosEncuadre > 0f)
            CamaraDinamica.Encuadrar(a.PuntoJefe, pesoEncuadre, segundosEncuadre, 8f);
    }

    // ------------------------------------------------------------------ Muro

    // La pelea empieza de verdad: la niebla se forma detras del player.
    public void Cerrar()
    {
        Armado = false;
        Cerrada = true;
        if (player == null) Recordar(FindFirstObjectByType<PlayerControler>());
        // Primero el player al lado de la arena; luego el muro (nunca encima de el).
        MantenerDentro(true);
        if (Muro != null) Muro.Cerrar();
    }

    // El jefe ha caido: el muro se disuelve y la zona queda abierta.
    public void Abrir(bool suave = true)
    {
        Cerrada = false;
        if (Muro != null) Muro.Abrir(suave);
    }

    // El player murio: el muro se disuelve y el activador vuelve a estar listo.
    public void Reiniciar()
    {
        Abrir(true);
        Armado = true;
        ignorarHasta = Time.time + esperaTrasReaparecer;
        player = null;
    }

    // Depuracion: cerrar o abrir el muro sin tocar la pelea.
    public void ForzarCierre() { Cerrada = true; MantenerDentro(true); if (Muro != null) Muro.Cerrar(); }
    public void ForzarApertura() => Abrir(true);

    private void Recordar(PlayerControler p)
    {
        player = p;
        rbPlayer = p != null ? p.GetComponent<Rigidbody2D>() : null;
        colPlayer = p != null ? p.GetComponent<Collider2D>() : null;
    }

    // Con el muro cerrado el player no puede estar del otro lado: si un empujon
    // (o un salto, o lo que sea) lo llevaria fuera, se queda pegado al muro.
    private void FixedUpdate()
    {
        if (!Cerrada) return;
        MantenerDentro(false);
    }

    private void MantenerDentro(bool alCerrar)
    {
        if (player == null) Recordar(FindFirstObjectByType<PlayerControler>());
        if (player == null || rbPlayer == null) return;
        Vector2 pos = rbPlayer.position;
        float x0 = transform.position.x;
        // Solo si esta a la altura del muro y cerca (no al otro lado del nivel).
        float alto = Muro != null ? Muro.alto : altoActivador;
        if (pos.y < transform.position.y - 3f || pos.y > transform.position.y + alto + 6f) return;
        if (Mathf.Abs(pos.x - x0) > (alCerrar ? 12f : 6f)) return;
        float medioPlayer = colPlayer != null ? colPlayer.bounds.extents.x : 0.3f;
        float medioMuro = Muro != null ? Muro.anchoBloqueo * 0.5f : 0.4f;
        float minimo = x0 + LadoArena * (medioMuro + medioPlayer + (alCerrar ? margen : 0.02f));
        if ((pos.x - minimo) * LadoArena >= 0f) return;
        rbPlayer.position = new Vector2(minimo, pos.y);
        player.transform.position = new Vector3(minimo, player.transform.position.y, player.transform.position.z);
        Vector2 v = rbPlayer.linearVelocity;
        if (v.x * LadoArena < 0f) rbPlayer.linearVelocity = new Vector2(0f, v.y);
    }

    // ------------------------------------------------------------------ Escena (solo Editor)

    private void OnDrawGizmos()
    {
        BoxCollider2D b = GetComponent<BoxCollider2D>();
        if (b == null) return;
        Vector3 c = transform.position + (Vector3)b.offset;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, Armado ? 0.12f : 0.04f);
        Gizmos.DrawCube(c, b.size);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(c, b.size);
        // La linea donde empieza (donde se ve al jefe).
        float xa = transform.position.x + LadoArena * distanciaActivador;
        Gizmos.DrawLine(new Vector3(xa, transform.position.y - 0.5f), new Vector3(xa, transform.position.y + altoActivador));
        if (Arena != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(Arena.PuntoJefe, 0.6f);
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(xa, transform.position.y + altoActivador + 0.6f), "Activador: " + name);
        UnityEditor.Handles.Label(transform.position + Vector3.up * ((Muro != null ? Muro.alto : 8f) + 0.4f), "Muro de niebla");
#endif
    }
}
