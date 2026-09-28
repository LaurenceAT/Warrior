using System.Collections.Generic;
using UnityEngine;

// Siluetas oscuras delante del jugador (pinos, troncos, hierba) que pasan mas
// rapido que el escenario: dan sensacion de que estan pegadas a la camara.
// Para no estorbar:
//   - Solo asoman por la franja de abajo de la pantalla.
//   - Se vuelven casi transparentes si tapan al jugador, a un enemigo, un cofre
//     o la hoguera.
//   - Con el jefe desaparecen (nunca esconden sus ataques ni el agarre).
// Son de adorno: no tienen colisiones.
public class PrimerPlanoNieve : MonoBehaviour
{
    public bool activo = true;
    [Tooltip("Siluetas que se usan (se eligen al azar).")]
    public Sprite[] sprites;
    [Tooltip("Siluetas por cada 10 unidades de recorrido.")]
    [Range(0f, 3f)] public float densidad = 0.45f;
    [Tooltip("1 = como el escenario. Mas = mas rapido y mas cerca de la camara.")]
    [Range(1f, 3f)] public float velocidadParallax = 1.45f;
    [Tooltip("0 = colores del sprite, 1 = silueta del todo.")]
    [Range(0f, 1f)] public float oscuridad = 0.93f;
    public Color colorSilueta = new Color(0.03f, 0.05f, 0.09f);
    [Range(0f, 1f)] public float opacidad = 0.95f;
    [Tooltip("Opacidad cuando tapa algo importante.")]
    [Range(0f, 1f)] public float opacidadAlTapar = 0.2f;
    [Tooltip("Pixeles por unidad (28 = pixeles como los del personaje). Menos = mas grandes.")]
    public float pixelesPorUnidad = 28f;
    [Tooltip("Tamano extra: las cosas cercanas se ven algo mas grandes.")]
    public float escala = 1.3f;
    [Tooltip("Hasta donde suben desde el borde de abajo (fraccion del alto de la pantalla, min y max).")]
    public Vector2 alturaMaxima = new Vector2(0.14f, 0.3f);
    [Tooltip("Desaparecen durante la pelea con el jefe.")]
    public bool ocultarConJefe = true;
    public int semilla = 7;

    private const float AnchoFranja = 60f;

    private class Silueta
    {
        public SpriteRenderer sr;
        public float u;       // posicion en la franja
        public float altura;  // fraccion de la pantalla
        public float alfa;
    }

    private readonly List<Silueta> siluetas = new List<Silueta>();
    private readonly List<Bounds> protegidos = new List<Bounds>();
    private readonly List<Renderer> importantes = new List<Renderer>();
    private Camera cam;
    private float densidadHecha = -1f;
    private int semillaHecha;
    private float siguienteBusqueda;

    private void Start()
    {
        cam = Camera.main;
    }

    private void Crear()
    {
        foreach (Silueta s in siluetas) if (s.sr != null) Destroy(s.sr.gameObject);
        siluetas.Clear();
        densidadHecha = densidad;
        semillaHecha = semilla;
        if (sprites == null || sprites.Length == 0) return;

        Random.State antes = Random.state;
        Random.InitState(semilla);
        int n = Mathf.RoundToInt(densidad * AnchoFranja / 10f);
        for (int i = 0; i < n; i++)
        {
            Sprite sp = sprites[Random.Range(0, sprites.Length)];
            if (sp == null) continue;
            SpriteRenderer sr = new GameObject("Silueta").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sp;
            sr.flipX = Random.value < 0.5f;
            sr.sortingLayerName = "VFX";
            sr.sortingOrder = 30;
            siluetas.Add(new Silueta
            {
                sr = sr,
                // Repartidas con algo de azar, sin amontonarse.
                u = (i + Random.Range(0.15f, 0.85f)) * AnchoFranja / n,
                altura = Random.Range(alturaMaxima.x, alturaMaxima.y),
                alfa = 0f,
            });
        }
        Random.state = antes;
    }

    private void LateUpdate()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        if (!Mathf.Approximately(densidadHecha, densidad) || semillaHecha != semilla) Crear();

        bool jefe = ocultarConJefe && ArenaJefe.EnCombate;
        if (Time.time >= siguienteBusqueda) BuscarImportantes();
        protegidos.Clear();
        foreach (Renderer r in importantes)
            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
            {
                Bounds b = r.bounds;
                b.Expand(0.4f);
                protegidos.Add(b);
            }

        float medioAlto = cam.orthographicSize, medioAncho = medioAlto * cam.aspect;
        Vector3 c = cam.transform.position;
        float factor = pixelesPorUnidad > 0f ? escala / pixelesPorUnidad : escala / 28f;
        Color tinte = Color.Lerp(Color.white, colorSilueta, oscuridad);

        foreach (Silueta s in siluetas)
        {
            Sprite sp = s.sr.sprite;
            float esc = sp.pixelsPerUnit * factor;
            s.sr.transform.localScale = new Vector3(esc, esc, 1f);
            Vector2 tam = sp.bounds.size * esc;

            // Se mueve mas rapido que el mundo y da la vuelta al salir de la franja.
            float x = Mathf.Repeat(s.u - c.x * velocidadParallax + AnchoFranja * 0.5f, AnchoFranja) - AnchoFranja * 0.5f;
            bool visible = activo && Mathf.Abs(x) < medioAncho + tam.x;
            // La punta de arriba queda a "altura" de la pantalla; el resto, por debajo.
            float arriba = c.y - medioAlto + s.altura * medioAlto * 2f;
            float centroY = arriba - tam.y * 0.5f;
            Vector3 centroSprite = sp.bounds.center * esc;
            s.sr.transform.position = new Vector3(c.x + x - centroSprite.x * (s.sr.flipX ? -1f : 1f), centroY - centroSprite.y, 0f);

            float objetivo = !visible || jefe ? 0f : Tapa(s.sr.bounds) ? opacidadAlTapar : opacidad;
            s.alfa = Mathf.MoveTowards(s.alfa, objetivo, Time.deltaTime * 2.5f);
            s.sr.enabled = s.alfa > 0.001f;
            s.sr.color = new Color(tinte.r, tinte.g, tinte.b, s.alfa);
        }
    }

    private bool Tapa(Bounds b)
    {
        foreach (Bounds p in protegidos) if (b.Intersects(p)) return true;
        return false;
    }

    // Lo que nunca se debe tapar: jugador, enemigos, cofres y hogueras. Se busca
    // de vez en cuando (aparecen y mueren enemigos).
    private void BuscarImportantes()
    {
        siguienteBusqueda = Time.time + 1f;
        importantes.Clear();
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null) Anadir(p);
        foreach (EnemigoBase e in FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None)) Anadir(e);
        foreach (Hoguera h in FindObjectsByType<Hoguera>(FindObjectsSortMode.None)) Anadir(h);
        foreach (CofreMejora cm in FindObjectsByType<CofreMejora>(FindObjectsSortMode.None)) Anadir(cm);
        foreach (CofreAlmas ca in FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None)) Anadir(ca);
    }

    private void Anadir(Component c)
    {
        Renderer r = c.GetComponentInChildren<SpriteRenderer>();
        if (r != null) importantes.Add(r);
    }
}
