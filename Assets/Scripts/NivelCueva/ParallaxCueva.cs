using UnityEngine;

// Fondo parallax que acompana a la camara. Cada capa tiene varias copias en fila
// y se desplaza a su propio ritmo: las lejanas casi quietas respecto a la camara,
// las cercanas mas rapido. Al pasarse de una copia entera, vuelve a empezar, asi
// el fondo no se acaba nunca por mucho que se ande.
public class ParallaxCueva : MonoBehaviour
{
    [System.Serializable]
    public class Capa
    {
        public Transform raiz;
        // 1 = pegada a la camara (infinitamente lejos), 0 = se mueve con el mundo.
        [Range(0f, 1f)] public float seguimiento = 0.8f;
        // Ancho de una copia, en unidades.
        public float ancho = 10f;
        // Movimiento propio (unidades por segundo): la neblina se desplaza sola.
        public float deriva;
        // Alargan la fila de pixeles del borde para que al alejar la camara no se
        // vea donde acaba la imagen (cielo hacia arriba, montanas hacia abajo).
        public bool rellenarArriba;
        public bool rellenarAbajo;
    }

    public Capa[] capas;
    // En vertical se sigue a la camara casi del todo: la cueva es alta y el
    // fondo se quedaria corto.
    [Range(0f, 1f)] public float seguimientoVertical = 0.97f;
    // Largo de los rellenos de los bordes (unidades).
    [SerializeField] private float largoRelleno = 30f;

    private Transform camara;
    private Vector3 inicioCamara;
    private float[] recorrido;

    private void Start()
    {
        if (capas == null) return;
        recorrido = new float[capas.Length];
        foreach (Capa capa in capas)
        {
            if (capa.raiz == null) continue;
            foreach (SpriteRenderer sr in capa.raiz.GetComponentsInChildren<SpriteRenderer>())
            {
                if (capa.rellenarArriba) Rellenar(sr, true);
                if (capa.rellenarAbajo) Rellenar(sr, false);
            }
        }
    }

    // Un sprite de una sola fila de pixeles (la de arriba o la de abajo de la
    // imagen) estirado en vertical: continua el borde sin que se note.
    private void Rellenar(SpriteRenderer copia, bool arriba)
    {
        Sprite s = copia.sprite;
        if (s == null) return;
        Rect r = s.textureRect;
        Rect fila = new Rect(r.x, arriba ? r.yMax - 1f : r.y, r.width, 1f);
        Sprite borde = Sprite.Create(s.texture, fila, new Vector2(0.5f, arriba ? 0f : 1f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        borde.name = s.name + (arriba ? "_arriba" : "_abajo");

        SpriteRenderer sr = new GameObject(arriba ? "RellenoArriba" : "RellenoAbajo").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(copia.transform, false);
        float altoImagen = s.bounds.size.y;
        float centroY = s.bounds.center.y;
        // Se mete un pixel dentro de la imagen: sin eso queda una raya de hueco.
        float solape = 1f / s.pixelsPerUnit;
        float bordeY = centroY + (arriba ? altoImagen : -altoImagen) * 0.5f + (arriba ? -solape : solape);
        sr.transform.localPosition = new Vector3(s.bounds.center.x, bordeY, 0f);
        // La escala del padre ya esta aplicada: se estira en unidades locales.
        float escalaPadre = Mathf.Max(0.0001f, copia.transform.lossyScale.y);
        sr.transform.localScale = new Vector3(1f, largoRelleno / escalaPadre / (1f / s.pixelsPerUnit), 1f);
        sr.sprite = borde;
        sr.color = copia.color;
        sr.sortingLayerID = copia.sortingLayerID;
        sr.sortingOrder = copia.sortingOrder;
    }

    private void LateUpdate()
    {
        if (camara == null)
        {
            if (Camera.main == null) return;
            camara = Camera.main.transform;
            inicioCamara = camara.position;
        }
        if (recorrido == null || recorrido.Length != capas.Length) recorrido = new float[capas.Length];

        Vector3 c = camara.position;
        for (int i = 0; i < capas.Length; i++)
        {
            Capa capa = capas[i];
            if (capa.raiz == null || capa.ancho <= 0f) continue;

            recorrido[i] += capa.deriva * Time.deltaTime;
            // Centrado en la camara: las copias cubren lo mismo a cada lado.
            float desplazado = c.x * (1f - capa.seguimiento) - recorrido[i];
            float x = c.x - Mathf.Repeat(desplazado + capa.ancho * 0.5f, capa.ancho) + capa.ancho * 0.5f;
            float y = inicioCamara.y + (c.y - inicioCamara.y) * seguimientoVertical;
            capa.raiz.position = new Vector3(x, y, capa.raiz.position.z);
        }
    }
}
