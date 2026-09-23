using UnityEngine;

// Configuracion de las particulas de polvo del player, hecha por codigo para que
// sea igual en todas las escenas y no dependa de ajustar a mano cada Particle System.
//
// Lo que las hace verse vivas, y que antes les faltaba a las tres:
//  - Tamano y transparencia que se apagan con la vida: antes cada particula
//    aparecia y desaparecia de golpe.
//  - Tamano y color aleatorios entre dos valores: antes eran todas iguales.
//  - Velocidad en coordenadas de mundo, orientada cada fotograma segun hacia
//    donde mira el personaje o donde esta la pared.
//  - Emision que se puede ajustar en marcha (por velocidad o distancia) y rafagas
//    puntuales para los momentos fuertes: arrancar, derrapar, aterrizar, pisar.
public static class DustFx
{
    [System.Serializable]
    public class Preset
    {
        public Color colorA = Color.white;
        public Color colorB = Color.white;
        // Minimo y maximo. A PPU 28, 0.036 u es un pixel.
        public Vector2 tamano = new Vector2(0.05f, 0.1f);
        public Vector2 vida = new Vector2(0.25f, 0.4f);
        public float gravedad = 0.3f;
        // Rango de velocidad. La X es "hacia fuera": hacia atras al correr, lejos de
        // la pared en los muros. El signo lo pone el codigo segun el lado.
        public Vector2 velX = new Vector2(0.4f, 1.2f);
        public Vector2 velY = new Vector2(0.3f, 1f);
        public float porSegundo;
        // Particulas por unidad recorrida: con esto el polvo sigue a la velocidad.
        public float porDistancia;
        public int maxParticulas = 80;

        // Correr por el suelo: tierra crema y rosada de tu propio tileset, levantada
        // hacia atras y arriba. Sale por distancia, asi que mas rapido = mas polvo.
        public static Preset Carrera() => new Preset
        {
            colorA = new Color(0.937f, 0.863f, 0.745f, 0.9f),   // EFDCBE
            colorB = new Color(0.820f, 0.525f, 0.475f, 0.7f),   // D18679
            tamano = new Vector2(0.05f, 0.11f),
            vida = new Vector2(0.25f, 0.45f),
            gravedad = 0.25f,
            velX = new Vector2(0.4f, 1.6f),
            velY = new Vector2(0.4f, 1.3f),
            porDistancia = 5f,
        };

        // Deslizarse por la pared: polvo de piedra que salta hacia fuera y hacia
        // arriba al rozar, y cae. El ritmo sube con la velocidad de bajada.
        public static Preset DeslizarPared() => new Preset
        {
            colorA = new Color(0.647f, 0.761f, 0.780f, 0.85f),  // A5C2C7
            colorB = new Color(0.463f, 0.557f, 0.631f, 0.7f),   // 768EA1
            tamano = new Vector2(0.04f, 0.09f),
            vida = new Vector2(0.2f, 0.4f),
            gravedad = 0.6f,
            velX = new Vector2(0.2f, 0.9f),
            velY = new Vector2(0.3f, 1f),
            porSegundo = 14f,
        };

        // Correr por la pared: los pies empujan el polvo hacia abajo y hacia fuera.
        // Ademas del goteo continuo, cada "paso" suelta una rafaga.
        public static Preset CorrerPared() => new Preset
        {
            colorA = new Color(0.647f, 0.761f, 0.780f, 0.9f),   // A5C2C7
            colorB = new Color(0.937f, 0.863f, 0.745f, 0.8f),   // EFDCBE
            tamano = new Vector2(0.05f, 0.1f),
            vida = new Vector2(0.2f, 0.35f),
            gravedad = 0.8f,
            velX = new Vector2(0.6f, 1.6f),
            velY = new Vector2(-1.5f, -0.4f),
            porSegundo = 10f,
        };
    }

    // Deja el sistema listo: en marcha, sin emitir hasta que se active.
    public static void Configurar(ParticleSystem ps, Preset p, string capa, int orden)
    {
        if (ps == null || p == null) return;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(p.vida.x, p.vida.y);
        main.startSize = new ParticleSystem.MinMaxCurve(p.tamano.x, p.tamano.y);
        main.startColor = new ParticleSystem.MinMaxGradient(p.colorA, p.colorB);
        main.startRotation = 0f;
        main.gravityModifier = p.gravedad;
        main.maxParticles = p.maxParticulas;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.enabled = false;
        emision.rateOverTime = p.porSegundo;
        emision.rateOverDistance = p.porDistancia;

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = 0.04f;
        forma.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        Orientar(ps, p, 1);

        // Se encogen al morir, con un final rapido.
        ParticleSystem.SizeOverLifetimeModule tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

        // Y se desvanecen en la ultima mitad.
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        int id = SortingLayer.NameToID(capa);
        if (r != null && SortingLayer.IsValid(id))
        {
            r.sortingLayerID = id;
            r.sortingOrder = orden;
        }

        // Siempre en marcha: la emision continua se abre y cierra aparte, y asi las
        // rafagas funcionan aunque el goteo este apagado.
        ps.Play();
    }

    // Pone la X de la velocidad hacia el lado indicado (1 o -1).
    public static void Orientar(ParticleSystem ps, Preset p, int lado)
    {
        if (ps == null || p == null) return;

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(p.velX.x * lado, p.velX.y * lado);
        vel.y = new ParticleSystem.MinMaxCurve(p.velY.x, p.velY.y);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    // Abre o cierra el goteo continuo. multiplicador escala el ritmo (por ejemplo
    // segun la velocidad de bajada por la pared).
    public static void Emitir(ParticleSystem ps, Preset p, bool activo, float multiplicador = 1f)
    {
        if (ps == null || p == null) return;
        if (!ps.isPlaying) ps.Play();

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.enabled = activo;
        emision.rateOverTime = p.porSegundo * multiplicador;
        emision.rateOverDistance = p.porDistancia * multiplicador;
    }

    // Rafaga puntual con su propia velocidad, sumada a la del sistema.
    public static void Rafaga(ParticleSystem ps, int cantidad, Vector2 velX, Vector2 velY)
    {
        if (ps == null || cantidad <= 0) return;
        if (!ps.isPlaying) ps.Play();

        ParticleSystem.EmitParams parametros = new ParticleSystem.EmitParams();
        parametros.applyShapeToPosition = true;

        for (int i = 0; i < cantidad; i++)
        {
            parametros.velocity = new Vector3(Random.Range(velX.x, velX.y), Random.Range(velY.x, velY.y), 0f);
            ps.Emit(parametros, 1);
        }
    }
}
