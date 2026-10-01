using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Monta el nivel del jefe en modo desafio (se crea solo al cargar la escena si
// hay un desafio en curso). El nivel no se toca: solo se apaga lo que sobra.
//   - Queda una sola hoguera: la mas cercana antes de la arena.
//   - Fuera los enemigos comunes, cofres, estatuas y portales.
//   - Un muro invisible detras de la hoguera: no se puede volver atras.
//   - Junto a la hoguera: el totem (la tienda) y un cofre con lo de la ficha.
//   - El player aparece en la hoguera, y ahi reaparece al morir.
// Cuenta el tiempo y las muertes, y al vencer al jefe completa el desafio.
public class ModoDesafio : MonoBehaviour
{
    private Hoguera hoguera;
    // La ArenaJefe de siempre o la de la Cazadora (su propia arena).
    private Component arena;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (Desafio.Activo && Desafio.Ficha != null && s.name == Desafio.Ficha.escena)
                new GameObject("ModoDesafio").AddComponent<ModoDesafio>();
        };
    }

    private void Awake()
    {
        Desafio.Reiniciando = false;
        IndicadorInventario.Asegurar();
        IndicadorDeseo.Asegurar();
        arena = FindFirstObjectByType<ArenaJefe>();
        if (arena == null) arena = FindFirstObjectByType<ArenaCazadora>();
        if (arena == null) { Debug.LogWarning("[Desafio] La escena no tiene ArenaJefe"); return; }
        Bounds zona = LimitesArena();

        // Una sola hoguera: la ultima antes de la arena (o la mas cercana).
        Hoguera[] hogueras = FindObjectsByType<Hoguera>(FindObjectsSortMode.None);
        hoguera = hogueras.Where(h => h.transform.position.x < zona.min.x).OrderByDescending(h => h.transform.position.x).FirstOrDefault()
                  ?? hogueras.OrderBy(h => Mathf.Abs(h.transform.position.x - zona.center.x)).FirstOrDefault();
        foreach (Hoguera h in hogueras) if (h != hoguera) h.gameObject.SetActive(false);

        // El cofre del desafio sale de uno del nivel (mismo aspecto).
        CofreMejora plantilla = FindFirstObjectByType<CofreMejora>();
        CofreMejora cofre = null;
        if (plantilla != null && hoguera != null)
        {
            cofre = Instantiate(plantilla.gameObject).GetComponent<CofreMejora>();
            cofre.name = "CofreDesafio";
            FichaJefe f = Desafio.Ficha;
            Equipo.Objeto[] objetos = f.objetosCofre != null && f.objetosCofre.Length > 0 ? f.objetosCofre : new[] { Equipo.Objeto.PiedraForja };
            cofre.Configurar("desafio", objetos[0], objetos.Skip(1).ToArray(), f.almasCofre);
            cofre.transform.position = EnSuelo(hoguera.transform.position.x - 2.6f, hoguera.transform.position.y, cofre.transform.position.y - Pie(cofre.gameObject));
        }

        // Fuera lo demas (apagado ya, para que la reaparicion de enemigos no lo copie).
        foreach (EnemigoBase e in FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None))
            if (!(e is JefeBase)) Quitar(e.gameObject);
        foreach (CofreMejora c in FindObjectsByType<CofreMejora>(FindObjectsSortMode.None)) if (c != cofre) Quitar(c.gameObject);
        foreach (CofreAlmas c in FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None)) Quitar(c.gameObject);
        foreach (EstatuaPista s in FindObjectsByType<EstatuaPista>(FindObjectsSortMode.None)) Quitar(s.gameObject);
        foreach (PortalNivel p in FindObjectsByType<PortalNivel>(FindObjectsSortMode.None)) Quitar(p.gameObject);
        foreach (PortalEntrada p in FindObjectsByType<PortalEntrada>(FindObjectsSortMode.None)) Quitar(p.gameObject);

        if (hoguera == null) return;
        Vector2 hp = hoguera.transform.position;

        // Muro invisible detras de la hoguera.
        GameObject muro = new GameObject("MuroDesafio");
        muro.layer = LayerMask.NameToLayer("Ground");
        muro.transform.position = new Vector3(hp.x - 5.5f, hp.y + 10f, 0f);
        BoxCollider2D bc = muro.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(1f, 40f);

        // El totem, al otro lado de la hoguera.
        TotemTienda.Crear(EnSuelo(hp.x + 3f, hp.y, 0f));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.hasCheckPointActive = true;
            GameManager.Instance.checkpointRespawnPosition = hoguera.PuntoReaparicion;
        }
    }

    private IEnumerator Start()
    {
        // El player aparece en la hoguera (y la camara con el, sin barrido).
        yield return null;
        if (hoguera == null) yield break;
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null)
        {
            Vector2 pos = hoguera.PuntoReaparicion;
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
            p.transform.position = pos;
            CinemachineCamera cam = FindFirstObjectByType<CinemachineCamera>();
            if (cam != null) cam.PreviousStateIsValid = false;
        }
    }

    private void OnEnable()
    {
        PlayerControler.AlMorir += SumarMuerte;
        ArenaJefe.AlVencer += Vencido;
    }

    private void OnDisable()
    {
        PlayerControler.AlMorir -= SumarMuerte;
        ArenaJefe.AlVencer -= Vencido;
    }

    private void SumarMuerte() { if (!Desafio.Completado && !Desafio.Reiniciando) Desafio.Muertes++; }
    private void Vencido() => Desafio.Completar();

    private void Update()
    {
        // El tiempo del desafio corre con el juego (no en pausa, en menus ni cargando).
        if (Desafio.Activo && !Desafio.Completado && !Desafio.Reiniciando && !PantallaCarga.Cargando) Desafio.Segundos += Time.deltaTime;
    }

    // ------------------------------------------------------------------ Ayudas

    private static void Quitar(GameObject g)
    {
        g.SetActive(false);
        Destroy(g);
    }

    private Bounds LimitesArena()
    {
        Collider2D c = arena.GetComponent<Collider2D>();
        if (c == null) c = arena.GetComponentInChildren<Collider2D>();
        return c != null ? c.bounds : new Bounds(arena.transform.position, new Vector3(30f, 10f, 1f));
    }

    // Cuanto hay del centro del objeto a su pie (su collider o su dibujo).
    private static float Pie(GameObject g)
    {
        Collider2D c = g.GetComponent<Collider2D>();
        if (c != null) return c.bounds.min.y;
        Renderer r = g.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.min.y : g.transform.position.y;
    }

    // Punto a la altura del suelo bajo x (buscando desde "y" hacia abajo), mas
    // lo que haya del centro del objeto a su pie.
    private static Vector3 EnSuelo(float x, float y, float desvio)
    {
        bool antes = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;
        RaycastHit2D h = Physics2D.Raycast(new Vector2(x, y + 1.5f), Vector2.down, 12f, LayerMask.GetMask("Ground"));
        Physics2D.queriesStartInColliders = antes;
        float suelo = h.collider != null ? h.point.y : y;
        return new Vector3(x, suelo + desvio, 0f);
    }
}
