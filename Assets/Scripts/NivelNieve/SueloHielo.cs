using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Suelo de hielo: el player pierde traccion encima (PlayerControler mira si el
// suelo que pisa tiene este componente). Brilla un poco para que se distinga.
public class SueloHielo : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] brillos;
    private float t;

    public void PonerBrillos(SpriteRenderer[] b) => brillos = b;

    private void Update()
    {
        if (brillos == null) return;
        t += Time.deltaTime;
        for (int i = 0; i < brillos.Length; i++)
        {
            if (brillos[i] == null) continue;
            Color c = brillos[i].color;
            c.a = 0.25f + 0.2f * Mathf.Sin(t * 1.7f + i * 1.3f);
            brillos[i].color = c;
        }
    }
}
