using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tramo con ventisca: mientras el player esta dentro, la nieve tapa la vista
// (la hoja animada de ventisca del pack Mapa_BosqueGandalf, delante de todo, y
// una neblina blanca) y el viento le empuja. Cada poco llega una rafaga mas
// fuerte, avisada: la nieve se espesa y el viento silba antes de que empuje.
[RequireComponent(typeof(Collider2D))]
public class ZonaVentisca : MonoBehaviour
{
    [SerializeField] private float direccion = -1f;
    [SerializeField] private float fuerzaBase = 1.4f;
    [SerializeField] private float fuerzaRafaga = 4.2f;
    [SerializeField] private Vector2 esperaRafagas = new Vector2(3.5f, 6f);
    [SerializeField] private float avisoRafaga = 1.1f;
    [SerializeField] private float duracionRafaga = 1.6f;
    [Range(0f, 1f)] [SerializeField] private float neblina = 0.32f;
    public AnimadorHoja.Clip clipVentisca;

    private PlayerControler player;
    private bool dentro;
    private float siguienteRafaga;
    private float inicioRafaga = -99f;
    private float intensidad;

    private static AudioSource sonido;

    private void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!otro.CompareTag("Player")) return;
        player = otro.GetComponent<PlayerControler>();
        dentro = true;
        siguienteRafaga = Time.time + Random.Range(esperaRafagas.x, esperaRafagas.y) * 0.6f;
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (otro.CompareTag("Player")) dentro = false;
    }

    private void Update()
    {
        if (player == null) dentro = false;

        // Fase de la rafaga: 0 nada, aviso (sube), rafaga (maximo), y se calma.
        float ahora = Time.time;
        if (dentro && ahora >= siguienteRafaga)
        {
            inicioRafaga = ahora;
            siguienteRafaga = ahora + avisoRafaga + duracionRafaga + Random.Range(esperaRafagas.x, esperaRafagas.y);
            Sonido.Reproducir("ventisca_rafaga", 0.8f);
        }
        float enRafaga = RafagaActual();

        float objetivo = dentro ? 0.55f + 0.45f * enRafaga : 0f;
        intensidad = Mathf.MoveTowards(intensidad, objetivo, Time.deltaTime * (dentro ? 0.8f : 0.6f));
        OverlayVentisca.Poner(this, intensidad, neblina, clipVentisca);

        if (sonido == null) sonido = Sonido.Bucle("ventisca", 0f);
        if (sonido != null) sonido.volume = Mathf.Lerp(0f, 0.55f, OverlayVentisca.IntensidadMaxima) * Mathf.Max(0.3f, DatosNivel.FactorAmbiente) * ControlVolumen.Efectos;
    }

    // 0 sin rafaga, sube durante el aviso y vale 1 durante la rafaga.
    private float RafagaActual()
    {
        float t = Time.time - inicioRafaga;
        if (t < 0f) return 0f;
        if (t < avisoRafaga) return Mathf.SmoothStep(0f, 0.6f, t / avisoRafaga);
        if (t < avisoRafaga + duracionRafaga) return 1f;
        if (t < avisoRafaga + duracionRafaga + 0.8f) return 1f - (t - avisoRafaga - duracionRafaga) / 0.8f;
        return 0f;
    }

    private void FixedUpdate()
    {
        if (!dentro || player == null) return;
        float t = Time.time - inicioRafaga;
        bool soplando = t >= avisoRafaga && t < avisoRafaga + duracionRafaga;
        float fuerza = soplando ? fuerzaRafaga : fuerzaBase * (0.7f + 0.3f * Mathf.Sin(Time.time * 1.3f));
        player.AplicarViento(new Vector2(direccion * fuerza, 0f));
    }

    private void OnDisable()
    {
        OverlayVentisca.Poner(this, 0f, 0f, null);
    }
}
