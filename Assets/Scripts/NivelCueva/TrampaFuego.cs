using System.Collections;
using UnityEngine;

// Respiradero de fuego que se enciende y se apaga en ciclo. Antes de encenderse
// chisporrotea un momento con llamas pequenas (el aviso) y solo hace dano con la
// llamarada entera. Las llamas son hijos con AnimacionSprites y la zona de dano
// es un trigger con ZonaDano que se activa solo mientras arde.
public class TrampaFuego : MonoBehaviour
{
    [SerializeField] private float tiempoApagada = 2f;
    [SerializeField] private float tiempoAviso = 0.6f;
    [SerializeField] private float tiempoEncendida = 1.3f;
    // Retraso del primer ciclo, para que dos respiraderos seguidos no vayan a la par.
    [SerializeField] private float desfase;

    [SerializeField] private Transform llamas;
    [SerializeField] private Collider2D zonaDano;
    // Altura de las llamas encendidas (escala Y); en el aviso salen a un cuarto.
    [SerializeField] private float escalaEncendida = 1f;

    private SpriteRenderer[] renderersLlamas;
    private Vector3 escalaBase;

    private void Start()
    {
        if (llamas != null)
        {
            renderersLlamas = llamas.GetComponentsInChildren<SpriteRenderer>();
            escalaBase = llamas.localScale;
        }
        Poner(0f, false);
        StartCoroutine(Ciclo());
    }

    private IEnumerator Ciclo()
    {
        if (desfase > 0f) yield return new WaitForSeconds(desfase);

        while (true)
        {
            Poner(0f, false);
            yield return new WaitForSeconds(tiempoApagada);

            // Aviso: llamas bajas que parpadean.
            for (float t = 0f; t < tiempoAviso; t += Time.deltaTime)
            {
                Poner(0.25f + 0.1f * Mathf.Sin(t * 40f), false, 0.6f);
                yield return null;
            }

            // Llamarada: sube rapido y quema.
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                Poner(Mathf.Lerp(0.3f, 1f, t / 0.12f), true);
                yield return null;
            }
            Poner(1f, true);
            yield return new WaitForSeconds(tiempoEncendida);
        }
    }

    private void Poner(float altura, bool quema, float alfa = 1f)
    {
        if (zonaDano != null) zonaDano.enabled = quema;
        if (llamas == null) return;

        llamas.gameObject.SetActive(altura > 0.01f);
        llamas.localScale = new Vector3(escalaBase.x, escalaBase.y * altura * escalaEncendida, escalaBase.z);
        if (renderersLlamas == null) return;
        foreach (SpriteRenderer r in renderersLlamas)
        {
            Color c = r.color;
            c.a = alfa;
            r.color = c;
        }
    }
}
