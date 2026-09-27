using UnityEngine;

// Zona que cambia el zoom de la camara mientras el player este dentro (la arena
// del jefe, un pozo alto...). Necesita un trigger. Ver CamaraDinamica.
[RequireComponent(typeof(Collider2D))]
public class ZonaCamara : MonoBehaviour
{
    // Tamano ortografico dentro de la zona (el normal es 3.33).
    [SerializeField] private float tamano = 4.5f;
    public float Tamano => tamano;
    // Mirada extra hacia arriba dentro de la zona (la arena: ver al jefe en el aire).
    [SerializeField] private float desplazamientoY;
    public float DesplazamientoY => desplazamientoY;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (otro.CompareTag("Player")) CamaraDinamica.EntrarZona(this);
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (otro.CompareTag("Player")) CamaraDinamica.SalirZona(this);
    }

    private void OnDisable()
    {
        CamaraDinamica.SalirZona(this);
    }
}
