using UnityEngine;

// Mensaje en pantalla al pasar por aqui (la pista del peligro del jefe junto a
// su puerta). Sale una vez por partida; al morir se puede volver a leer.
[RequireComponent(typeof(Collider2D))]
public class PistaNarrativa : MonoBehaviour
{
    [TextArea(2, 5)]
    [SerializeField] private string texto =
        "Arañazos profundos cubren la roca. Alguien escribió con sangre: «Su piel carmesí se ríe del acero... solo el puño desnudo quiebra su verdadera forma».";
    [SerializeField] private float duracion = 7f;

    private bool leida;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnEnable() { GameManager.AlReaparecerPlayer += Olvidar; }
    private void OnDisable() { GameManager.AlReaparecerPlayer -= Olvidar; }

    private void Olvidar() { leida = false; }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (leida || !otro.CompareTag("Player")) return;
        leida = true;
        MensajePantalla.Narrativo(texto, duracion);
    }
}
