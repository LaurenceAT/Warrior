using UnityEngine;

// Sierra/trampa que recorre una ruta de waypoints.
//
// La ruta se lee de los hijos de "routeParent": basta con crear o duplicar un waypoint
// dentro de ese objeto para que entre en el recorrido, sin arrastrarlo a ninguna lista.
// Si no se asigna routeParent se sigue usando la lista manual de abajo, para no romper
// las sierras que ya estaban colocadas en las escenas.
[DisallowMultipleComponent]
[ExecuteAlways]
public class SawController : MonoBehaviour
{
    public enum RouteMode
    {
        IdaYVuelta, // recorre la ruta y vuelve por donde vino
        Circuito    // al llegar al último waypoint salta al primero
    }

    [Header("Ruta")]
    // Objeto que contiene los waypoints como hijos. Si está asignado, manda sobre la lista.
    [SerializeField] private Transform routeParent;
    // Se rellena sola cuando hay routeParent. Solo es editable a mano si routeParent está vacío.
    [SerializeField] private Transform[] wayPoints;
    [SerializeField] private RouteMode routeMode = RouteMode.IdaYVuelta;

    [Header("Movimiento")]
    [SerializeField] private float speed = 5f;
    // Margen para dar por alcanzado un waypoint.
    [SerializeField] private float reachDistance = 0.1f;

    [Header("Espera en los extremos")]
    [SerializeField] private float waitTime = 0.6f;
    // Desmarcado, la sierra también se detiene en los waypoints intermedios.
    [SerializeField] private bool waitOnlyAtEnds = true;

    [Header("Giro")]
    // Espeja el sprite al cambiar de sentido, para que la sierra parezca rodar hacia el otro lado.
    [SerializeField] private bool flipOnTurn = true;

    private int index;
    private int step = 1;       // 1 = avanzando por la ruta, -1 = volviendo
    private float waitTimer;
    private int facing = 1;

    #region Ciclo de Unity

    // Coloca la sierra en el primer waypoint y apunta al siguiente.
    private void Start()
    {
        if (!Application.isPlaying) return;

        BuildRoute();

        if (!HasRoute())
        {
            Debug.LogWarning($"La sierra '{name}' no tiene waypoints. Asigna un routeParent con hijos o rellena la lista.", this);
            enabled = false;
            return;
        }

        transform.position = wayPoints[0].position;
        index = wayPoints.Length > 1 ? 1 : 0;
        facing = transform.localScale.x < 0f ? -1 : 1;
    }

    private void Update()
    {
        // Fuera de Play solo refrescamos la lista, para que un waypoint nuevo aparezca al instante.
        if (!Application.isPlaying)
        {
            SyncRouteInEditor();
            return;
        }

        if (!HasRoute()) return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        Transform target = wayPoints[index];
        if (target == null) return;

        transform.position = Vector2.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target.position) > reachDistance) return;

        Advance();
    }

    // Mantiene los valores dentro de rangos con sentido y recoge la ruta al tocar el Inspector.
    private void OnValidate()
    {
        if (speed < 0f) speed = 0f;
        if (reachDistance < 0.01f) reachDistance = 0.01f;
        if (waitTime < 0f) waitTime = 0f;

        BuildRoute();
    }

    #endregion

    #region Ruta

    private bool HasRoute()
    {
        return wayPoints != null && wayPoints.Length > 0;
    }

    // Copia los hijos de routeParent a la lista de waypoints.
    private void BuildRoute()
    {
        if (routeParent == null) return;

        int count = routeParent.childCount;
        Transform[] found = new Transform[count];
        for (int i = 0; i < count; i++) found[i] = routeParent.GetChild(i);
        wayPoints = found;
    }

    // Igual que BuildRoute pero sin generar basura si no ha cambiado nada.
    private void SyncRouteInEditor()
    {
        if (routeParent == null) return;

        if (wayPoints != null && wayPoints.Length == routeParent.childCount)
        {
            bool igual = true;
            for (int i = 0; i < wayPoints.Length; i++)
            {
                if (wayPoints[i] == routeParent.GetChild(i)) continue;
                igual = false;
                break;
            }
            if (igual) return;
        }

        BuildRoute();
    }

    // Pasa al siguiente waypoint, da la vuelta si toca y programa la espera.
    private void Advance()
    {
        if (wayPoints.Length < 2) return;

        bool haDadoLaVuelta = false;

        if (routeMode == RouteMode.Circuito)
        {
            index++;
            if (index >= wayPoints.Length)
            {
                index = 0;
                haDadoLaVuelta = true;
            }
        }
        else
        {
            int siguiente = index + step;
            if (siguiente >= wayPoints.Length || siguiente < 0)
            {
                step = -step;
                siguiente = index + step;
                haDadoLaVuelta = true;
            }
            index = siguiente;
        }

        if (waitTime > 0f && (haDadoLaVuelta || !waitOnlyAtEnds)) waitTimer = waitTime;

        if (flipOnTurn) ApplyFlip(haDadoLaVuelta);
    }

    #endregion

    #region Giro

    // Espeja el sprite según hacia dónde va el siguiente tramo.
    private void ApplyFlip(bool haDadoLaVuelta)
    {
        Transform target = wayPoints[index];
        if (target == null) return;

        float dx = target.position.x - transform.position.x;

        if (Mathf.Abs(dx) > 0.01f)
        {
            SetFacing(dx > 0f ? 1 : -1);
        }
        else if (haDadoLaVuelta)
        {
            // Ruta vertical: no hay lado al que mirar, así que espejamos al dar la vuelta.
            SetFacing(-facing);
        }
    }

    private void SetFacing(int nuevo)
    {
        if (nuevo == facing) return;

        facing = nuevo;
        Vector3 escala = transform.localScale;
        escala.x = Mathf.Abs(escala.x) * facing;
        transform.localScale = escala;
    }

    #endregion

    #region Editor

    // Dibuja el recorrido en la escena para verlo sin entrar en Play.
    private void OnDrawGizmos()
    {
        if (!HasRoute()) return;

        for (int i = 0; i < wayPoints.Length; i++)
        {
            if (wayPoints[i] == null) continue;

            bool esExtremo = routeMode == RouteMode.IdaYVuelta && (i == 0 || i == wayPoints.Length - 1);
            Gizmos.color = esExtremo ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(wayPoints[i].position, esExtremo ? 0.22f : 0.15f);

            Transform siguiente = null;
            if (i < wayPoints.Length - 1) siguiente = wayPoints[i + 1];
            else if (routeMode == RouteMode.Circuito) siguiente = wayPoints[0];

            if (siguiente == null) continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(wayPoints[i].position, siguiente.position);
        }
    }

#if UNITY_EDITOR
    // Crea un waypoint nuevo al final de la ruta, separado del anterior para verlo enseguida.
    // Se usa desde el menú de los tres puntos del componente en el Inspector.
    [ContextMenu("Añadir waypoint al final de la ruta")]
    private void AddWaypointAtEnd()
    {
        if (routeParent == null)
        {
            Debug.LogWarning($"La sierra '{name}' no tiene routeParent asignado: no sé dónde crear el waypoint.", this);
            return;
        }

        GameObject nuevo = new GameObject($"WP{routeParent.childCount}");
        UnityEditor.Undo.RegisterCreatedObjectUndo(nuevo, "Añadir waypoint");
        nuevo.transform.SetParent(routeParent, false);

        Vector3 referencia = routeParent.childCount > 1
            ? routeParent.GetChild(routeParent.childCount - 2).position
            : transform.position;
        nuevo.transform.position = referencia + Vector3.right * 2f;

        BuildRoute();
        UnityEditor.Selection.activeGameObject = nuevo;
    }
#endif

    #endregion
}
