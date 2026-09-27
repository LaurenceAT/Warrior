using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sello de oscuridad: una barrera de sombras que solo la luz sagrada disipa.
// Guarda el camino a un escondite con almas.
public class SelloSombrio : MonoBehaviour, IGolpeable
{
    [SerializeField] private Collider2D solido;
    [SerializeField] private SpriteRenderer[] visuales;
    [SerializeField] private int golpesNecesarios = 2;
    private int golpes;
    private bool roto;
    private float siguientePista;
    private float t;

    public void Configurar(Collider2D solido, SpriteRenderer[] visuales)
    {
        this.solido = solido;
        this.visuales = visuales;
    }

    private void Update()
    {
        if (roto || visuales == null) return;
        t += Time.deltaTime;
        for (int i = 0; i < visuales.Length; i++)
        {
            if (visuales[i] == null) continue;
            Color c = visuales[i].color;
            c.a = 0.65f + 0.25f * Mathf.Sin(t * 2.5f + i);
            visuales[i].color = c;
        }
    }

    public void Golpear(Elemento elemento, Vector2 punto)
    {
        if (roto) return;
        if (elemento != Elemento.Sagrado)
        {
            Sonido.Reproducir("sombra_rebote", 0.7f);
            if (Time.time >= siguientePista)
            {
                siguientePista = Time.time + 4f;
                TextoFlotante.Mostrar("Las sombras absorben el golpe. Solo la luz las disipa", punto + Vector2.up * 1.3f, new Color(0.8f, 0.7f, 1f), 0.8f);
            }
            return;
        }
        golpes++;
        Sonido.Reproducir("golpe_" + (int)Elemento.Sagrado);
        ParticulasFx.Rafaga(punto, 16, new Color(1f, 0.9f, 0.5f), Color.white, new Vector2(1f, 4f), -0.2f,
                            new Vector2(0.05f, 0.1f), new Vector2(0.4f, 0.8f));
        if (golpes < golpesNecesarios) return;
        roto = true;
        if (solido != null) solido.enabled = false;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        Sonido.Reproducir("sello_roto");
        TextoFlotante.Mostrar("La luz disipa las sombras", punto + Vector2.up * 1.5f, new Color(1f, 0.9f, 0.6f), 1f);
        foreach (SpriteRenderer s in visuales) if (s != null) Destroy(s.gameObject);
    }
}
