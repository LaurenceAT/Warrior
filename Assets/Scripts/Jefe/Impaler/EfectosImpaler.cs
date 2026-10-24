using System.Collections.Generic;
using UnityEngine;

// Efectos del Impaler con reserva: los sistemas de particulas se crean una vez
// (hijos del jefe) y se reutilizan; no se crea ni se destruye nada en cada golpe.
// Se van con el jefe al limpiar o reaparecer.
public class EfectosImpaler : MonoBehaviour
{
    private const int Maximo = 6;
    private readonly List<ParticleSystem> chispas = new List<ParticleSystem>();
    private static Material materialSinLuz;

    public int Creadas => chispas.Count;

    // Rafaga de chispas en un punto (el choque de la lanza en un parry).
    public void Chispas(Vector2 punto, int cantidad, Color a, Color b, float direccion)
    {
        if (cantidad <= 0) return;
        ParticleSystem ps = Libre();
        ps.transform.position = punto;
        ps.transform.rotation = Quaternion.Euler(-direccion, 90f, 0f);
        ParticleSystem.MainModule main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        ps.Emit(cantidad);
    }

    private ParticleSystem Libre()
    {
        foreach (ParticleSystem p in chispas) if (p.particleCount == 0) return p;
        if (chispas.Count >= Maximo) return chispas[0];
        ParticleSystem nuevo = Crear();
        chispas.Add(nuevo);
        return nuevo;
    }

    // Como ParticulasFx.Rafaga (chispas que salen en abanico y se apagan), pero sin destruirse.
    private ParticleSystem Crear()
    {
        GameObject go = new GameObject("Chispas");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 64;
        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 0f;
        ParticleSystem.ShapeModule forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 55f;
        forma.radius = 0.05f;
        ParticleSystem.SizeOverLifetimeModule tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = MaterialSinLuz();
        pr.sortingLayerName = "VFX";
        pr.sortingOrder = 6;
        return ps;
    }

    private static Material MaterialSinLuz()
    {
        if (materialSinLuz != null) return materialSinLuz;
        Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Sprites/Default");
        materialSinLuz = new Material(s);
        return materialSinLuz;
    }
}
