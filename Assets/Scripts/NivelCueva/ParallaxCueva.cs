using UnityEngine;

// Fondo parallax que acompana a la camara. Cada capa tiene tres copias en fila y
// se desplaza a su propio ritmo: las lejanas casi quietas respecto a la camara,
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
    }

    public Capa[] capas;
    // En vertical se sigue a la camara casi del todo: la cueva es alta y el
    // fondo se quedaria corto.
    [Range(0f, 1f)] public float seguimientoVertical = 0.97f;

    private Transform camara;
    private Vector3 inicioCamara;

    private void LateUpdate()
    {
        if (camara == null)
        {
            if (Camera.main == null) return;
            camara = Camera.main.transform;
            inicioCamara = camara.position;
        }

        Vector3 c = camara.position;
        foreach (Capa capa in capas)
        {
            if (capa.raiz == null || capa.ancho <= 0f) continue;

            float desplazado = c.x * (1f - capa.seguimiento);
            float x = c.x - Mathf.Repeat(desplazado, capa.ancho);
            float y = inicioCamara.y + (c.y - inicioCamara.y) * seguimientoVertical;
            capa.raiz.position = new Vector3(x, y, capa.raiz.position.z);
        }
    }
}
