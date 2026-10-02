using UnityEngine;

// Señal de un sello: una runa grabada (o grietas, o un brillo en el suelo) del
// color del elemento que hace falta. Late despacio para que se note sin
// gritar. Al abrirse su sello se apaga y queda como piedra gris.
public class SimboloSello : MonoBehaviour
{
    public Elemento elemento = Elemento.Sagrado;
    [Tooltip("Lo que late (runa, grietas o brillo). Vacio = los SpriteRenderer de este objeto y sus hijos.")]
    public SpriteRenderer[] piezas = new SpriteRenderer[0];
    [Range(0f, 1f)] public float intensidad = 0.8f;
    public float velocidad = 1.4f;

    private Color[] baseColor;
    private float fase, apagado;
    private bool apagando;

    private void Awake()
    {
        if (piezas == null || piezas.Length == 0) piezas = GetComponentsInChildren<SpriteRenderer>(true);
        baseColor = new Color[piezas.Length];
        for (int i = 0; i < piezas.Length; i++) baseColor[i] = piezas[i] != null ? piezas[i].color : Color.white;
        fase = Random.value * 6f;
    }

    private void Update()
    {
        if (apagando) apagado = Mathf.MoveTowards(apagado, 1f, Time.deltaTime);
        Color tono = PistasSellos.Get().De(elemento).color;
        float k = 0.55f + 0.45f * Mathf.Sin(Time.time * velocidad + fase);
        for (int i = 0; i < piezas.Length; i++)
        {
            SpriteRenderer s = piezas[i];
            if (s == null) continue;
            Color vivo = Color.Lerp(baseColor[i], new Color(tono.r, tono.g, tono.b, baseColor[i].a), intensidad * (0.6f + 0.4f * k));
            Color gris = new Color(0.45f, 0.47f, 0.5f, baseColor[i].a * 0.6f);
            s.color = Color.Lerp(vivo, gris, apagado);
        }
    }

    public void Apagar(bool yaMismo = false)
    {
        apagando = true;
        if (yaMismo) apagado = 1f;
    }

    private void OnDrawGizmos()
    {
        Color c = PistasSellos.Get().De(elemento).color;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.7f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
    }
}
