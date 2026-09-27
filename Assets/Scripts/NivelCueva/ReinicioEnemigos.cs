using System.Collections.Generic;
using UnityEngine;

// Hace reaparecer a los enemigos comunes al descansar en una hoguera y al morir
// el player, como en los Souls. Al empezar guarda una copia dormida de cada
// enemigo de la escena tal como esta colocado; al reiniciar destruye los que
// queden y crea otra vez todos desde esas copias.
//
// Los que llevan NoReaparece (el jefe) se dejan en paz.
public class ReinicioEnemigos : MonoBehaviour
{
    private class Plantilla
    {
        public GameObject copia;
        public GameObject viva;
        public Transform padre;
    }

    private readonly List<Plantilla> plantillas = new List<Plantilla>();
    private Transform almacen;

    private void Start()
    {
        // Contenedor apagado: lo que se crea dentro no se despierta (ni Awake, ni
        // Start, ni IA) hasta que se saca.
        almacen = new GameObject("PlantillasEnemigos").transform;
        almacen.SetParent(transform, false);
        almacen.gameObject.SetActive(false);

        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (e.GetComponentInParent<NoReaparece>() != null) continue;

            GameObject go = e.gameObject;
            GameObject copia = Instantiate(go, go.transform.position, go.transform.rotation, almacen);
            copia.name = go.name;
            plantillas.Add(new Plantilla { copia = copia, viva = go, padre = go.transform.parent });
        }

        Hoguera.AlDescansar += Reiniciar;
        GameManager.AlReaparecerPlayer += Reiniciar;
    }

    private void OnDestroy()
    {
        Hoguera.AlDescansar -= Reiniciar;
        GameManager.AlReaparecerPlayer -= Reiniciar;
    }

    public void Reiniciar()
    {
        foreach (Plantilla p in plantillas)
        {
            if (p.viva != null) Destroy(p.viva);

            p.viva = Instantiate(p.copia, p.copia.transform.position, p.copia.transform.rotation, p.padre);
            p.viva.name = p.copia.name;
            p.viva.SetActive(true);
        }
    }
}
