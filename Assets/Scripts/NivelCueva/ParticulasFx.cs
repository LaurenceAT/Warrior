using UnityEngine;

// Rafagas de particulas montadas por codigo: brasas de la hoguera, esquirlas de
// las estalactitas... Asi no hace falta un prefab por cada efecto.
public static class ParticulasFx
{
    private static Material materialSinLuz;

    // velocidad: rango de rapidez. gravedad: negativa sube (brasas), positiva cae.
    // arco: apertura en grados alrededor de "direccion" (360 = en todas direcciones).
    public static void Rafaga(Vector2 punto, int cantidad, Color colorA, Color colorB,
                              Vector2 velocidad, float gravedad, Vector2 tamano,
                              Vector2 vida, float arco = 360f, float direccion = 90f)
    {
        GameObject go = new GameObject("Particulas");
        go.transform.position = punto;
        // El cono de Unity apunta a +Z; se gira para que apunte a "direccion" en 2D.
        go.transform.rotation = Quaternion.Euler(-direccion, 90f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida.x, vida.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad.x, velocidad.y);
        main.startSize = new ParticleSystem.MinMaxCurve(tamano.x, tamano.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravedad;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cantidad) });

        ParticleSystem.ShapeModule forma = ps.shape;
        if (arco >= 359f)
        {
            forma.shapeType = ParticleSystemShapeType.Circle;
            forma.radius = 0.1f;
            go.transform.rotation = Quaternion.identity;
        }
        else
        {
            forma.shapeType = ParticleSystemShapeType.Cone;
            forma.angle = arco * 0.5f;
            forma.radius = 0.1f;
        }

        ParticleSystem.SizeOverLifetimeModule tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = MaterialSinLuz();
        pr.sortingLayerName = "VFX";
        pr.sortingOrder = 5;

        ps.Play();
    }

    // Sin luces 2D: asi se ven igual en las zonas oscuras de la cueva.
    private static Material MaterialSinLuz()
    {
        if (materialSinLuz != null) return materialSinLuz;
        Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Sprites/Default");
        materialSinLuz = new Material(s);
        return materialSinLuz;
    }
}
