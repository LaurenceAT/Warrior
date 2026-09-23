using System.Collections;
using UnityEngine;

// Efecto visual del disparo con arco, al estilo del de Nine Sols: un haz de energia
// que sale disparado desde el arco, vibra mientras hace dano y colapsa hacia su eje.
// Solo es visual; el dano lo aplica PlayerControler.
//
// Usa las dos partes de LaserShot.png:
//  - Fotogramas 0-2: el haz formandose. Son cortos y en punta, asi que se dibujan
//    una sola vez, pegados al arco, como fogonazo.
//  - Fotogramas 3-6: el haz completo. Ocupan los 64 px de ancho y encajan entre si,
//    asi que se repiten en mosaico (Draw Mode Tiled) a lo largo de todo el recorrido.
//
// Encima se anaden tres capas hechas por codigo, sin arte extra:
//  - Un nucleo blanco dentro del haz (el mismo sprite, pintado de blanco con el
//    shader Sprites/Flash), que es lo que lo hace parecer al rojo vivo.
//  - Anillos de onda verticales en la boca del arco.
//  - Un estallido en cada enemigo alcanzado.
[RequireComponent(typeof(SpriteRenderer))]
public class BowBeam : MonoBehaviour
{
    [Header("Fotogramas")]
    [SerializeField] private Sprite[] formFrames;
    [SerializeField] private Sprite[] loopFrames;
    [SerializeField] private float formFps = 40f;
    [SerializeField] private float loopFps = 20f;

    [Header("Forma")]
    // Grosor respecto al sprite original. El haz se ve de 0.79 u a tamano natural.
    [SerializeField] private float thickness = 1.8f;
    // Latido del grosor mientras esta activo, para que se note que es energia viva.
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private float pulseSpeed = 28f;

    [Header("Tiempos")]
    // Lo que tarda en recorrer el nivel desde el arco. Rapido, pero se tiene que ver.
    [SerializeField] private float extendTime = 0.06f;
    // Al terminar se hincha un instante y luego colapsa hacia su eje.
    [SerializeField] private float collapseTime = 0.18f;
    [Range(1f, 1.5f)] [SerializeField] private float collapseSwell = 1.15f;

    [Header("Nucleo blanco")]
    // Material con el shader Sprites/Flash. Sin el, no hay nucleo.
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Color coreColor = Color.white;
    // Grosor del nucleo respecto al haz.
    [SerializeField] private float coreThickness = 0.45f;

    [Header("Anillos de onda")]
    [SerializeField] private bool rings = true;
    [SerializeField] private Color ringColor = new Color(0.8f, 1f, 1f, 1f);
    // Dos pixeles a PPU 28.
    [SerializeField] private float ringWidth = 0.07f;
    [SerializeField] private float ringTime = 0.3f;
    // Tamano final del anillo: estrecho y alto, como en Nine Sols.
    [SerializeField] private Vector2 ringSize = new Vector2(0.45f, 1.5f);

    [Header("Chispas de impacto")]
    [SerializeField] private bool sparks = true;
    [SerializeField] private int sparkCount = 12;
    [SerializeField] private Color sparkColorA = new Color(0.6f, 1f, 1f, 1f);
    [SerializeField] private Color sparkColorB = Color.white;
    // Tamano de cada chispa: 2 o 3 pixeles a PPU 28, para que case con el pixel art.
    [SerializeField] private Vector2 sparkSize = new Vector2(0.07f, 0.11f);
    [SerializeField] private Vector2 sparkSpeed = new Vector2(3f, 8f);

    [Header("Dibujo")]
    // Por encima de las paredes: el haz las atraviesa y tiene que verse sobre ellas.
    [SerializeField] private string sortingLayer = "VFX";
    [SerializeField] private int sortingOrder = 5;

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    private SpriteRenderer sr;
    private SpriteRenderer nucleo;
    private int capaId;
    private Vector2 boca;
    private int lado = 1;
    private float largo;
    private float activo;
    // Multiplicador de grosor para el disparo cargado.
    private float grosorExtra = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        capaId = SortingLayer.NameToID(sortingLayer);
        if (!SortingLayer.IsValid(capaId)) capaId = sr.sortingLayerID;

        sr.sortingLayerID = capaId;
        sr.sortingOrder = sortingOrder;

        if (coreMaterial != null) CrearNucleo();
    }

    // boca: punta del arco. direccion: 1 o -1. longitud: lo que recorre el haz.
    // tiempoActivo: cuanto se mantiene a plena potencia (lo que duran los ticks).
    // grosor: multiplicador extra para el disparo cargado (1 = normal).
    public void Disparar(Vector2 bocaDelArco, int direccion, float longitud, float tiempoActivo, float grosor = 1f)
    {
        grosorExtra = Mathf.Max(0.1f, grosor);
        boca = bocaDelArco;
        lado = direccion >= 0 ? 1 : -1;
        largo = Mathf.Max(0.1f, longitud);
        activo = Mathf.Max(0f, tiempoActivo);

        // Los fotogramas tienen el pivote en el centro: se voltean con flipX y se
        // colocan a mano en cada fase.
        sr.flipX = lado < 0;
        if (nucleo != null) nucleo.flipX = sr.flipX;

        StartCoroutine(Efecto());
    }

    // Estallido en el punto donde el haz alcanza a un enemigo.
    public void Impacto(Vector2 punto)
    {
        StartCoroutine(Anillo(punto, new Vector2(0.9f, 0.9f) * grosorExtra, 0.22f, 0f));
        if (sparks) Chispas(punto);
    }

    // Rafaga de chispas cuadradas que salen despedidas en la direccion del haz.
    // El Particle System se monta por codigo, asi no hace falta ningun prefab.
    private void Chispas(Vector2 punto)
    {
        GameObject go = new GameObject("Chispas");
        go.transform.position = punto;
        // Apuntando hacia donde va el haz.
        go.transform.rotation = Quaternion.Euler(0f, lado > 0 ? 90f : -90f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeed.x, sparkSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sparkSize.x, sparkSize.y);
        main.startColor = new ParticleSystem.MinMaxGradient(sparkColorA, sparkColorB);
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Tiempo real: salen durante el hit stop y se tienen que ver moverse.
        main.useUnscaledTime = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 40f;
        forma.radius = 0.05f;

        // Se encogen al morir, en vez de desaparecer de golpe.
        ParticleSystem.SizeOverLifetimeModule tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = sr.sharedMaterial;
        pr.sortingLayerID = capaId;
        pr.sortingOrder = sortingOrder + 3;

        ps.Play();
    }

    private IEnumerator Efecto()
    {
        // 1) Fogonazo en la boca del arco, a tamano natural pero con el grosor final.
        sr.drawMode = SpriteDrawMode.Simple;
        EscalaVertical(1f);
        if (formFrames != null)
        {
            foreach (Sprite s in formFrames)
            {
                if (s == null) continue;
                sr.sprite = s;
                transform.position = boca + Vector2.right * (lado * s.bounds.size.x * 0.5f);
                yield return new WaitForSeconds(1f / Mathf.Max(1f, formFps));
            }
        }

        if (loopFrames == null || loopFrames.Length == 0) { Destroy(gameObject); yield break; }

        float alto = loopFrames[0].bounds.size.y;
        sr.drawMode = SpriteDrawMode.Tiled;
        if (nucleo != null) nucleo.drawMode = SpriteDrawMode.Tiled;

        if (rings)
        {
            StartCoroutine(Anillo(boca, ringSize, ringTime, 0f));
            StartCoroutine(Anillo(boca + Vector2.right * (lado * 0.45f), ringSize * 0.8f, ringTime, 0.07f));
        }

        // 2) Sale disparado: crece desde el arco hasta el final del recorrido.
        float t = 0f;
        while (t < extendTime)
        {
            float k = t / extendTime;
            float suave = 1f - (1f - k) * (1f - k);
            ColocarHaz(Mathf.Max(0.1f, largo * suave), alto, t);
            EscalaVertical(1f);
            t += Time.deltaTime;
            yield return null;
        }

        // 3) A plena potencia, latiendo mientras duran los ticks de dano.
        t = 0f;
        while (t < activo)
        {
            ColocarHaz(largo, alto, extendTime + t);
            EscalaVertical(1f + Mathf.Sin(t * pulseSpeed) * pulseAmount);
            t += Time.deltaTime;
            yield return null;
        }

        // 4) Colapso: se hincha un instante y se aplasta hacia su eje, apagandose.
        Color color = sr.color;
        t = 0f;
        while (t < collapseTime)
        {
            float k = Mathf.Clamp01(t / collapseTime);

            // Primer quinto: hinchazon. Resto: caida acelerada hasta cero.
            float grosor = k < 0.2f
                ? Mathf.Lerp(1f, collapseSwell, k / 0.2f)
                : collapseSwell * (1f - Mathf.Pow((k - 0.2f) / 0.8f, 2f));

            ColocarHaz(largo, alto, extendTime + activo + t);
            EscalaVertical(Mathf.Max(0f, grosor));

            // La transparencia solo al final, para que se vea encogerse y no fundirse.
            float alfa = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            AplicarAlfa(color, alfa);

            t += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // Coloca el haz con su extremo en la boca del arco y el largo dado.
    private void ColocarHaz(float longitud, float alto, float tiempo)
    {
        Sprite s = loopFrames[(int)(tiempo * loopFps) % loopFrames.Length];
        sr.sprite = s;
        sr.size = new Vector2(longitud, alto);
        transform.position = boca + Vector2.right * (lado * longitud * 0.5f);

        if (nucleo == null) return;
        nucleo.sprite = s;
        nucleo.size = sr.size;
    }

    // La escala, no el size: en modo Tiled el size recorta en vez de escalar, y al
    // encogerlo se comia la parte de arriba (parecia que el haz se iba hacia arriba).
    // Con la escala se aplasta hacia el centro.
    private void EscalaVertical(float factor)
    {
        transform.localScale = new Vector3(1f, thickness * grosorExtra * factor, 1f);
    }

    private void AplicarAlfa(Color baseColor, float alfa)
    {
        Color c = baseColor;
        c.a = baseColor.a * alfa;
        sr.color = c;

        if (nucleo == null) return;
        Color n = nucleo.color;
        n.a = alfa;
        nucleo.color = n;
    }

    // Copia del haz, mas fina y pintada de blanco con el shader de destello.
    private void CrearNucleo()
    {
        GameObject go = new GameObject("Nucleo");
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(1f, coreThickness, 1f);

        nucleo = go.AddComponent<SpriteRenderer>();
        nucleo.sharedMaterial = coreMaterial;
        nucleo.sortingLayerID = capaId;
        nucleo.sortingOrder = sortingOrder + 1;

        MaterialPropertyBlock bloque = new MaterialPropertyBlock();
        bloque.SetColor(IdFlashColor, coreColor);
        bloque.SetFloat(IdFlashAmount, 0.9f);
        nucleo.SetPropertyBlock(bloque);
    }

    // Elipse que crece y se desvanece. Va con LineRenderer para no necesitar sprite.
    private IEnumerator Anillo(Vector2 centro, Vector2 tamanoFinal, float duracion, float retraso)
    {
        if (retraso > 0f) yield return new WaitForSeconds(retraso);

        GameObject go = new GameObject("Anillo");
        go.transform.position = centro;
        // Red de seguridad: si el haz se destruye antes, el anillo no se queda huerfano.
        Destroy(go, duracion + 0.1f);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 36;
        lr.widthMultiplier = ringWidth;
        lr.numCornerVertices = 0;
        lr.sharedMaterial = sr.sharedMaterial;
        lr.sortingLayerID = capaId;
        lr.sortingOrder = sortingOrder + 2;

        float t = 0f;
        while (t < duracion)
        {
            float k = t / duracion;
            float suave = 1f - (1f - k) * (1f - k);
            Vector2 radio = Vector2.Lerp(tamanoFinal * 0.15f, tamanoFinal, suave) * 0.5f;

            for (int i = 0; i < lr.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / lr.positionCount;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radio.x, Mathf.Sin(a) * radio.y, 0f));
            }

            Color c = ringColor;
            c.a = ringColor.a * (1f - k);
            lr.startColor = c;
            lr.endColor = c;
            lr.widthMultiplier = ringWidth * (1f - k * 0.5f);

            t += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }
}
