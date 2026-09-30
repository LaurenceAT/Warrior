using System.Collections.Generic;
using UnityEngine;

// Particulas reutilizables del ambiente de la cueva: un unico ParticleSystem por
// tipo (motas de las grietas, brasas guia, polvo, gotas), creado una vez, que
// emite particulas sueltas. No crea ni destruye objetos por cada mota.
public static class EmisorCueva
{
    public enum Tipo { Mota, Brasa, Polvo, Gota }

    private static readonly Dictionary<Tipo, ParticleSystem> sistemas = new Dictionary<Tipo, ParticleSystem>();

    public static void Emitir(Tipo tipo, Vector2 pos, Vector2 velocidad, Color color, float tamano, float vida)
    {
        ParticleSystem ps = Sistema(tipo);
        var p = new ParticleSystem.EmitParams
        {
            position = new Vector3(pos.x, pos.y, 0f),
            velocity = new Vector3(velocidad.x, velocidad.y, 0f),
            startColor = color,
            startSize = tamano,
            startLifetime = vida,
            applyShapeToPosition = false,
        };
        ps.Emit(p, 1);
    }

    private static ParticleSystem Sistema(Tipo tipo)
    {
        if (sistemas.TryGetValue(tipo, out ParticleSystem ps) && ps != null) return ps;
        GameObject go = new GameObject("Emisor_" + tipo);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = tipo == Tipo.Polvo ? 60 : 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = tipo == Tipo.Gota ? 1.2f : tipo == Tipo.Polvo ? 0.02f : -0.05f;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = false;

        // Aparecen y se apagan suave.
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        if (tipo == Tipo.Brasa || tipo == Tipo.Mota)
        {
            ParticleSystem.NoiseModule ruido = ps.noise;
            ruido.enabled = true;
            ruido.strength = 0.25f;
            ruido.frequency = 0.6f;
        }

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        pr.sortingLayerName = "VFX";
        pr.sortingOrder = tipo == Tipo.Polvo ? -5 : 5;
        ps.Play();
        sistemas[tipo] = ps;
        return ps;
    }
}
