using Unity.Cinemachine;
using UnityEngine;

// Ajusta el offset vertical de la cámara de Cinemachine al entrar en una zona (ej. un pasillo o zona baja).
public class CustomCameraOffset : MonoBehaviour
{
    public CinemachineCamera CinemachineCamera;
    public CinemachinePositionComposer PositionComposer;

    // Obtiene el Position Composer de la cámara de Cinemachine.
    private void Start()
    {
        PositionComposer = CinemachineCamera.GetComponent<CinemachinePositionComposer>();
    }

    // Cambia el offset vertical objetivo de la cámara al entrar en el trigger.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Enter");
        PositionComposer.TargetOffset.y = -1.8f;
    }
}
