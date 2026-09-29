using UnityEngine;

// Parallax del bosque de la Cazadora. Como el de la cueva, pero cada capa tiene
// su propia altura en el mundo (los arboles apoyan en el suelo y el cielo queda
// arriba), en vez de centrarse en la camara. Todo editable en el Inspector.
public class ParallaxBosque : MonoBehaviour
{
    [System.Serializable]
    public class Capa
    {
        public Transform raiz;
        [Tooltip("1 = pegada a la camara (muy lejos), 0 = se mueve con el mundo.")]
        [Range(0f, 1f)] public float seguimiento = 0.8f;
        [Tooltip("Ancho de una copia, en unidades.")]
        public float ancho = 30f;
        [Tooltip("Altura en el mundo de la base de la imagen.")]
        public float altura = -3f;
    }

    public Capa[] capas = new Capa[0];
    [Tooltip("Cuanto siguen las capas a la camara en vertical (0 = nada).")]
    [Range(0f, 1f)] public float seguimientoVertical = 0.5f;
    [Tooltip("Altura de la camara a la que las capas estan en su sitio.")]
    public float alturaCamara = 3f;

    private Transform camara;

    private void LateUpdate()
    {
        if (camara == null)
        {
            if (Camera.main == null) return;
            camara = Camera.main.transform;
        }
        Vector3 c = camara.position;
        foreach (Capa capa in capas)
        {
            if (capa.raiz == null || capa.ancho <= 0f) continue;
            float desplazado = c.x * (1f - capa.seguimiento);
            float x = c.x - Mathf.Repeat(desplazado + capa.ancho * 0.5f, capa.ancho) + capa.ancho * 0.5f;
            float y = capa.altura + (c.y - alturaCamara) * seguimientoVertical * capa.seguimiento;
            capa.raiz.position = new Vector3(x, y, capa.raiz.position.z);
        }
    }
}
