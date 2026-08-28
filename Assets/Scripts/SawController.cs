using UnityEngine;

// Sierra/trampa que patrulla entre un conjunto de waypoints en bucle.
public class SawController : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private Transform[] wayPoints;
    [SerializeField] private int indexWayPoints = 1;

    // Ubica la sierra en el primer waypoint al iniciar.
    void Start()
    {
        transform.position = wayPoints[0].position;
    }

    // Mueve la sierra hacia el waypoint actual y avanza al siguiente (en bucle) al alcanzarlo.
    void Update()
    {
        transform.position = Vector2.MoveTowards(transform.position, wayPoints[indexWayPoints].position, speed * Time.deltaTime);
        if (!(Vector2.Distance(transform.position, wayPoints[indexWayPoints].position) < 0.1f)) return;
        indexWayPoints++;
        if (indexWayPoints >= wayPoints.Length) indexWayPoints = 0;
    }
}
