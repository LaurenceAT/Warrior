using UnityEngine;

// Zona oscura de la cueva: con el player dentro, OscuridadCueva se funde a
// negro (solo se ven las luces). Necesita un trigger. Si lleva ademas una
// ZonaCamara, la camara cambia un poco el encuadre al entrar.
[RequireComponent(typeof(Collider2D))]
public class ZonaOscura : MonoBehaviour
{
    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (otro.CompareTag("Player") && OscuridadCueva.Instancia != null) OscuridadCueva.Instancia.Entrar(this);
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (otro.CompareTag("Player") && OscuridadCueva.Instancia != null) OscuridadCueva.Instancia.Salir(this);
    }

    private void OnDisable()
    {
        if (OscuridadCueva.Instancia != null) OscuridadCueva.Instancia.Salir(this);
    }

    private void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0.3f, 0.2f, 0.6f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
