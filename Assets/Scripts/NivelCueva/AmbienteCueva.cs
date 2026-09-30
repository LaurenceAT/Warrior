using System;
using UnityEngine;

// Ambiente de la cueva nueva, todo ligero y editable en el Inspector:
//   - Cada zona (un rectangulo del mapa) tine el fondo y cambia la bruma: la
//     camara pasa de una a otra con una transicion suave.
//   - Bruma baja: franjas semitransparentes sobre los suelos (las pone el
//     generador) que respiran y se mecen.
//   - Polvo flotando y gotas que caen del techo cerca de la camara, con muy
//     pocas particulas (EmisorCueva, reutilizable).
public class AmbienteCueva : MonoBehaviour
{
    [Serializable]
    public class Zona
    {
        public string nombre = "Zona";
        public Rect area;
        [Tooltip("Tinte del fondo en esta zona (se multiplica por el de cada capa).")]
        public Color tinteFondo = Color.white;
        [Tooltip("Color de la bruma.")]
        public Color colorBruma = new Color(0.6f, 0.7f, 0.8f, 1f);
        [Tooltip("Densidad de la bruma (0 = nada).")]
        [Range(0f, 1f)] public float densidadBruma = 0.35f;
    }

    public Zona[] zonas = new Zona[0];
    [Tooltip("Rapidez con que cambia el tinte al pasar de zona.")]
    public float transicion = 1.5f;

    [Header("Fondo (las capas del parallax, de lejos a cerca)")]
    public SpriteRenderer[] capasFondo = new SpriteRenderer[0];
    [Tooltip("Tinte propio de cada capa (perspectiva: las lejanas mas apagadas).")]
    public Color[] tinteCapa = new Color[0];

    [Header("Bruma")]
    public SpriteRenderer[] bruma = new SpriteRenderer[0];

    [Header("Polvo y gotas")]
    [Tooltip("Motas de polvo por segundo alrededor de la camara (0 = nada).")]
    public float polvo = 2.5f;
    [Tooltip("Gotas por segundo que caen del techo cerca de la camara (0 = nada).")]
    public float gotas = 0.5f;
    public Color colorPolvo = new Color(0.8f, 0.78f, 0.72f, 0.45f);
    public Color colorGota = new Color(0.65f, 0.8f, 0.95f, 0.8f);

    private Color tinteActual = Color.white, brumaActual;
    private float densidadActual;
    private float siguientePolvo, siguienteGota;
    private Vector3[] brumaBase;
    private LayerMask suelo;

    private void Start()
    {
        suelo = LayerMask.GetMask("Ground");
        brumaBase = new Vector3[bruma.Length];
        for (int i = 0; i < bruma.Length; i++) if (bruma[i] != null) brumaBase[i] = bruma[i].transform.position;
        Zona z = ZonaEn(Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero);
        if (z != null) { tinteActual = z.tinteFondo; brumaActual = z.colorBruma; densidadActual = z.densidadBruma; }
    }

    public Zona ZonaEn(Vector2 p)
    {
        foreach (Zona z in zonas) if (z.area.Contains(p)) return z;
        return null;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector2 c = cam.transform.position;
        Zona z = ZonaEn(c);
        if (z != null)
        {
            float k = 1f - Mathf.Exp(-transicion * Time.deltaTime);
            tinteActual = Color.Lerp(tinteActual, z.tinteFondo, k);
            brumaActual = Color.Lerp(brumaActual, z.colorBruma, k);
            densidadActual = Mathf.Lerp(densidadActual, z.densidadBruma, k);
        }

        for (int i = 0; i < capasFondo.Length; i++)
        {
            if (capasFondo[i] == null) continue;
            Color b = i < tinteCapa.Length ? tinteCapa[i] : Color.white;
            capasFondo[i].color = b * tinteActual;
        }

        float t = Time.time;
        for (int i = 0; i < bruma.Length; i++)
        {
            SpriteRenderer s = bruma[i];
            if (s == null) continue;
            // Solo se anima la que esta cerca de la camara.
            if (Mathf.Abs(brumaBase[i].x - c.x) > 20f) continue;
            Color col = brumaActual;
            col.a = densidadActual * (0.7f + 0.3f * Mathf.Sin(t * 0.5f + i * 1.7f));
            s.color = col;
            s.transform.position = brumaBase[i] + Vector3.right * Mathf.Sin(t * 0.15f + i) * 0.6f;
        }

        float alto = cam.orthographicSize, ancho = alto * cam.aspect;
        if (polvo > 0f && t >= siguientePolvo)
        {
            siguientePolvo = t + 1f / polvo * UnityEngine.Random.Range(0.5f, 1.5f);
            Vector2 p = c + new Vector2(UnityEngine.Random.Range(-ancho, ancho), UnityEngine.Random.Range(-alto, alto));
            if (!Physics2D.OverlapPoint(p, suelo))
                EmisorCueva.Emitir(EmisorCueva.Tipo.Polvo, p, new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(-0.08f, 0.08f)),
                                   colorPolvo, UnityEngine.Random.Range(0.025f, 0.045f), UnityEngine.Random.Range(3f, 5f));
        }
        if (gotas > 0f && t >= siguienteGota)
        {
            siguienteGota = t + 1f / gotas * UnityEngine.Random.Range(0.5f, 1.5f);
            Vector2 desde = c + new Vector2(UnityEngine.Random.Range(-ancho, ancho), 0f);
            RaycastHit2D techo = Physics2D.Raycast(desde, Vector2.up, alto + 3f, suelo);
            RaycastHit2D abajo = Physics2D.Raycast(desde, Vector2.down, alto + 6f, suelo);
            if (techo && abajo && !Physics2D.OverlapPoint(desde, suelo))
            {
                float caida = techo.point.y - abajo.point.y;
                // v^2 = 2 g h con la gravedad del emisor (1.2): dura justo hasta el suelo.
                float g = Mathf.Abs(Physics2D.gravity.y) * 1.2f;
                float vida = Mathf.Sqrt(2f * Mathf.Max(0.1f, caida) / g);
                EmisorCueva.Emitir(EmisorCueva.Tipo.Gota, techo.point + Vector2.down * 0.08f, Vector2.zero, colorGota, 0.045f, vida);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        foreach (Zona z in zonas)
        {
            Gizmos.color = new Color(z.tinteFondo.r, z.tinteFondo.g, z.tinteFondo.b, 0.4f);
            Gizmos.DrawWireCube(z.area.center, z.area.size);
        }
    }
}
