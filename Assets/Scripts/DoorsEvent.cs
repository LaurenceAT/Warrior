using UnityEngine;

// Dispara la apertura de la puerta de entrada del nivel (usado como evento, ej. desde una animación).
public class DoorsEvent : MonoBehaviour
{
    [SerializeField] private GameObject entranceDoor;
    [SerializeField] private Animator animatorEntranceDoor;
    private int _idOpenDoor;

    // Busca la puerta de entrada de la escena y obtiene su Animator.
    void OnEnable()
    {
        _idOpenDoor = Animator.StringToHash("OpenDoor");
        entranceDoor = GameObject.FindGameObjectWithTag("EntranceDoor");
        animatorEntranceDoor = entranceDoor.GetComponent<Animator>();
    }

    // Dispara la animación de apertura de la puerta de entrada.
    public void DoorOut()
    {
        animatorEntranceDoor.SetTrigger(_idOpenDoor);
    }
}
