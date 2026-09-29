using System;
using UnityEngine;

// Particulas pequenas que salen de la hoja mientras la espada esta imbuida:
//   - Sangrado: gotas rojas que caen.
//   - Fuego: brasas y chispas naranjas que suben.
//   - Escarcha: cristalitos claros que se desprenden.
//   - Sagrado: destellos amarillos y blancos alrededor.
//   - Oscuridad: jirones violetas que suben despacio.
// Salen de un punto de la hoja en el fotograma actual (MapaHoja), asi siguen al
// arma en los ataques y se voltean con el player. Un solo sistema de particulas
// para todo, con muy pocas a la vez. Se apagan al acabar la imbuicion y al morir.
public class EfectoArma : MonoBehaviour
{
    [Serializable]
    public class Ajuste
    {
        public bool activo = true;
        [Tooltip("Particulas por segundo.")]
        public float cantidad = 6f;
        [Tooltip("Tamano (min y max, en unidades; 0.036 es un pixel del personaje).")]
        public Vector2 tamano = new Vector2(0.035f, 0.055f);
        public Color colorA = Color.white, colorB = Color.white;
        [Tooltip("Velocidad al salir (x a los lados, y hacia arriba).")]
        public Vector2 velocidad = new Vector2(0.2f, 0.3f);
        [Tooltip("Gravedad: positiva caen, negativa suben.")]
        public float gravedad;
        [Tooltip("Segundos que dura cada particula (min y max).")]
        public Vector2 vida = new Vector2(0.35f, 0.6f);
    }

    public Ajuste sangrado = new Ajuste
    {
        cantidad = 5f, colorA = new Color(0.8f, 0.04f, 0.08f), colorB = new Color(0.45f, 0f, 0.03f),
        velocidad = new Vector2(0.15f, -0.2f), gravedad = 0.7f, vida = new Vector2(0.4f, 0.7f),
    };
    public Ajuste fuego = new Ajuste
    {
        cantidad = 8f, tamano = new Vector2(0.03f, 0.05f), colorA = new Color(1f, 0.62f, 0.15f), colorB = new Color(1f, 0.85f, 0.35f),
        velocidad = new Vector2(0.15f, 0.55f), gravedad = -0.12f, vida = new Vector2(0.3f, 0.55f),
    };
    public Ajuste hielo = new Ajuste
    {
        cantidad = 5f, colorA = new Color(0.8f, 0.95f, 1f), colorB = new Color(0.55f, 0.85f, 1f),
        velocidad = new Vector2(0.3f, 0.1f), gravedad = 0.15f, vida = new Vector2(0.35f, 0.6f),
    };
    public Ajuste sagrado = new Ajuste
    {
        cantidad = 6f, tamano = new Vector2(0.03f, 0.06f), colorA = new Color(1f, 0.92f, 0.45f), colorB = Color.white,
        velocidad = new Vector2(0.25f, 0.15f), gravedad = 0f, vida = new Vector2(0.25f, 0.45f),
    };
    public Ajuste oscuro = new Ajuste
    {
        cantidad = 5f, colorA = new Color(0.62f, 0.3f, 1f), colorB = new Color(0.3f, 0.1f, 0.5f),
        velocidad = new Vector2(0.1f, 0.3f), gravedad = -0.05f, vida = new Vector2(0.4f, 0.7f),
    };

    private ArmaImbuida arma;
    private PlayerControler player;
    private SpriteRenderer sr;
    private ParticleSystem ps;
    private float pendientes;
    private Elemento ultimo = Elemento.Ninguno;

    private void Awake()
    {
        arma = GetComponent<ArmaImbuida>();
        player = GetComponent<PlayerControler>();
        sr = GetComponent<SpriteRenderer>();
    }

    public Ajuste De(Elemento e)
    {
        switch (e)
        {
            case Elemento.Sangrado: return sangrado;
            case Elemento.Fuego: return fuego;
            case Elemento.Hielo: return hielo;
            case Elemento.Sagrado: return sagrado;
            case Elemento.Oscuro: return oscuro;
            default: return null;
        }
    }

    private void LateUpdate()
    {
        // El player anade ArmaImbuida al arrancar: puede no estar aun en Awake.
        if (arma == null) arma = GetComponent<ArmaImbuida>();
        Elemento e = arma != null ? arma.Activo : Elemento.Ninguno;
        bool vivo = player == null || player.VidaActual > 0;
        if (!vivo)
        {
            if (ps != null && ps.particleCount > 0) ps.Clear();
            return;
        }
        Ajuste a = De(e);
        if (a == null || !a.activo || sr == null || !sr.enabled) { pendientes = 0f; return; }
        if (ps == null) Crear();
        if (e != ultimo)
        {
            ultimo = e;
            var main = ps.main;
            main.gravityModifier = a.gravedad;
        }

        pendientes += a.cantidad * Time.deltaTime;
        while (pendientes >= 1f)
        {
            pendientes -= 1f;
            Emitir(a);
        }
    }

    private void Emitir(Ajuste a)
    {
        MapaHoja mapa = MapaHoja.Get();
        Vector2 local;
        if (mapa == null || !mapa.Punto(sr.sprite, out local))
        {
            // Sin mapa (o sin hoja en este fotograma): delante del pecho.
            local = new Vector2(0.3f, 0.1f) + UnityEngine.Random.insideUnitCircle * 0.15f;
        }
        if (sr.flipX) local.x = -local.x;
        Vector3 pos = sr.transform.TransformPoint(local);

        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = new Vector3(UnityEngine.Random.Range(-a.velocidad.x, a.velocidad.x), a.velocidad.y * UnityEngine.Random.Range(0.5f, 1f), 0f),
            startSize = UnityEngine.Random.Range(a.tamano.x, a.tamano.y),
            startLifetime = UnityEngine.Random.Range(a.vida.x, a.vida.y),
            startColor = Color.Lerp(a.colorA, a.colorB, UnityEngine.Random.value),
            applyShapeToPosition = false,
        };
        ps.Emit(ep, 1);
    }

    private void Crear()
    {
        GameObject go = new GameObject("EfectoArma");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.startSpeed = 0f;
        var em = ps.emission;
        em.enabled = false;
        var sh = ps.shape;
        sh.enabled = false;
        // Se apagan poco a poco al final de su vida.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = EfectoVisual.MaterialSinLuz();
        r.sortingLayerName = "VFX";
        r.sortingOrder = 21;
        ps.Play();
    }
}
