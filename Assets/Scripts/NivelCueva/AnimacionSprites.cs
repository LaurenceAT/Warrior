using UnityEngine;

// Anima un SpriteRenderer pasando una lista de sprites a ritmo fijo. Sirve para
// cosas sencillas (la torre de la hoguera, el fuego de las trampas...) sin tener
// que crear un Animator con sus clips para cada una.
[RequireComponent(typeof(SpriteRenderer))]
public class AnimacionSprites : MonoBehaviour
{
    public Sprite[] fotogramas;
    public float fps = 10f;
    public bool bucle = true;
    // Empieza en un fotograma al azar, para que dos iguales no vayan sincronizados.
    public bool inicioAleatorio;

    private SpriteRenderer sr;
    private float t;

    public bool Terminada => !bucle && fotogramas != null && t * fps >= fotogramas.Length;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (inicioAleatorio && fps > 0f && fotogramas != null && fotogramas.Length > 0)
            t = Random.Range(0, fotogramas.Length) / fps;
    }

    public void Reiniciar()
    {
        t = 0f;
    }

    private void Update()
    {
        if (fotogramas == null || fotogramas.Length == 0 || fps <= 0f) return;

        t += Time.deltaTime;
        int i = (int)(t * fps);
        i = bucle ? i % fotogramas.Length : Mathf.Min(i, fotogramas.Length - 1);
        sr.sprite = fotogramas[i];
    }
}
