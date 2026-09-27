using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lago de agua helada que no se puede cruzar a nado (caer es morir). Un tajo
// con la espada imbuida en escarcha congela la superficie y deja un puente de
// hielo, resbaladizo, por el que se puede pasar. Con otro elemento no hace nada.
public class AguaCongelable : MonoBehaviour, IGolpeable
{
    [SerializeField] private Collider2D puenteHielo;
    [SerializeField] private GameObject visualAgua;
    [SerializeField] private GameObject visualHielo;
    [SerializeField] private float anchoLago = 10f;
    private bool congelada;
    private float siguientePista;

    public void Configurar(Collider2D puente, GameObject agua, GameObject hielo, float ancho)
    {
        puenteHielo = puente;
        visualAgua = agua;
        visualHielo = hielo;
        anchoLago = ancho;
    }

    private void Awake()
    {
        if (puenteHielo != null) puenteHielo.enabled = false;
        if (visualHielo != null) visualHielo.SetActive(false);
    }

    public void Golpear(Elemento elemento, Vector2 punto)
    {
        if (congelada) return;
        if (elemento != Elemento.Hielo)
        {
            Sonido.Reproducir("agua_chapoteo", 0.6f);
            ParticulasFx.Rafaga(punto, 8, new Color(0.3f, 0.7f, 0.8f), new Color(0.8f, 1f, 1f), new Vector2(1f, 2.5f), 1.5f,
                                new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.5f), 70f, 90f);
            if (Time.time >= siguientePista)
            {
                siguientePista = Time.time + 4f;
                TextoFlotante.Mostrar("El agua está helada... casi a punto de congelarse", punto + Vector2.up * 1.3f, new Color(0.7f, 0.9f, 1f), 0.8f);
            }
            return;
        }
        StartCoroutine(Congelar(punto));
    }

    private IEnumerator Congelar(Vector2 desde)
    {
        congelada = true;
        Sonido.Reproducir("hielo_congelar_lago");
        CamaraDinamica.Sacudir(0.3f);
        if (puenteHielo != null) puenteHielo.enabled = true;
        if (visualHielo != null) visualHielo.SetActive(true);

        // La escarcha se extiende desde el golpe hacia los dos lados.
        SpriteRenderer[] piezas = visualHielo != null ? visualHielo.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
        foreach (SpriteRenderer p in piezas) { Color c = p.color; c.a = 0f; p.color = c; }
        float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float radio = anchoLago * (t / dur);
            foreach (SpriteRenderer p in piezas)
            {
                float d = Mathf.Abs(p.transform.position.x - desde.x);
                Color c = p.color;
                c.a = Mathf.Clamp01((radio - d) * 2f + 0.2f);
                p.color = c;
            }
            if (Random.value < 0.6f)
                ParticulasFx.Rafaga(new Vector2(desde.x + Random.Range(-radio, radio), transform.position.y), 3, Color.white,
                                    new Color(0.6f, 0.9f, 1f), new Vector2(0.5f, 2f), 0.5f, new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.6f), 90f, 90f);
            yield return null;
        }
        foreach (SpriteRenderer p in piezas) { Color c = p.color; c.a = 1f; p.color = c; }
        if (visualAgua != null)
            foreach (AnimadorHoja a in visualAgua.GetComponentsInChildren<AnimadorHoja>()) a.multiplicador = 0f;
        TextoFlotante.Mostrar("El lago se congela", desde + Vector2.up * 1.5f, new Color(0.7f, 0.95f, 1f), 1f);
    }
}
