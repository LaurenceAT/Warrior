using System.Collections.Generic;
using UnityEngine;

// El Black Knight (8): SU CABEZA. Aprende como peleas y cambia de tactica.
//
// No hay nada magico: mira lo que haces (parar, esquivar, encajar, machacar,
// beber, a que distancia te quedas), saca cuatro "perfiles" de 0 a 1 y con ellos
// pesa lo que elige hacer. Lo reciente pesa mas (lo viejo se olvida solo).
//
//   - Paras todo        -> agarres e imparables (no se bloquean), menos combos.
//   - Esquivas mucho    -> golpes rapidos, amagos y retrasos; menos cosas lentas.
//   - Machacas          -> su guardia, paso atras y castigos.
//   - Te quedas lejos   -> especiales a distancia, salto y ataques con avance.
//
// Lo aprendido se guarda EN MEMORIA entre intentos del mismo desafio (no se
// guarda en disco ni en la partida): al reintentar, ya te conoce a medias. Se
// olvida al salir del desafio o al cambiar de escena.
//
// Los avisos de sus golpes no cambian nunca: se adapta eligiendo, no haciendo
// trampas. Todos los numeros estan en la ficha (Ajustes Black Knight).
public partial class JefeBlackKnight
{
    // Categorias en las que reparte lo que sabe hacer.
    public enum Mano { Rapidos, Largos, Agarres, Distancia, Defensivos, Encender }

    // Cuentas con olvido: suben cuando pasa algo y bajan solas con el tiempo.
    private float cParry, cEsquiva, cGolpe, cBloqueo, cMachaque, cFrasco;
    private float distanciaMedia = 2f;
    private float ultimoOlvido;
    private static float[] memoriaPerfiles;   // lo que aprendio en intentos anteriores
    private static int intentosRecordados;

    // Los cuatro perfiles (0 a 1), lo que leen las pruebas y la depuracion.
    public float PerfilParry { get; private set; }
    public float PerfilEsquiva { get; private set; }
    public float PerfilMachaque { get; private set; }
    public float PerfilLejania { get; private set; }
    public float DistanciaTipica => distanciaMedia;
    public static int IntentosRecordados => intentosRecordados;
    public readonly Dictionary<string, int> Elegidas = new Dictionary<string, int>();

    // Olvida lo aprendido (al salir del desafio o empezar de cero).
    public static void OlvidarMemoria()
    {
        memoriaPerfiles = null;
        intentosRecordados = 0;
    }

    // ------------------------------------------------------------------ Presion

    // De 0 a 1. Sube mientras no te toca (y cuando le lees el juego), baja cuando
    // consigue darte. Acorta sus pausas, sus enfriamientos y acelera sus golpes.
    private float presion;
    public float Presion => presion;

    // Lo que dura ahora la pausa entre dos acciones.
    private float ConPresion(float pausa) =>
        ajustes == null || !ajustes.presionActiva ? pausa : pausa * Mathf.Lerp(1f, ajustes.pausaConPresion, presion);

    // Lo que dura ahora un enfriamiento de especial.
    public float EnfriamientoConPresion(float segundos) =>
        ajustes == null || !ajustes.presionActiva ? segundos : segundos * Mathf.Lerp(1f, ajustes.enfriamientoConPresion, presion);

    // Encadena otra accion sin pausa (cuando te tiene cogido el ritmo).
    private bool Encadena() =>
        ajustes != null && ajustes.presionActiva && Random.value < ajustes.probEncadenar * presion;

    private void SubirPresion(float cuanto)
    {
        if (ajustes == null || !ajustes.presionActiva) return;
        presion = Mathf.Clamp01(presion + cuanto);
    }

    private void BajarPresion(float cuanto)
    {
        presion = Mathf.Clamp01(presion - cuanto);
    }

    // Olvida lo que ha leido de ti en esta pelea (pruebas y depuracion).
    public void OlvidarLectura()
    {
        cParry = cEsquiva = cGolpe = cBloqueo = cMachaque = cFrasco = 0f;
        PerfilParry = PerfilEsquiva = PerfilMachaque = PerfilLejania = 0f;
        distanciaMedia = ajustes != null ? ajustes.distanciaCorta : 2f;
        presion = 0f;
        Elegidas.Clear();
        ultimoOlvido = Time.time;
    }

    // ------------------------------------------------------------------ Mirar

    private void EmpezarMente()
    {
        if (memoriaPerfiles != null && ajustes.iaAdaptativa)
        {
            // Lo que aprendio en los intentos anteriores, a medio gas.
            float k = Mathf.Clamp01(ajustes.memoriaEntreIntentos);
            PerfilParry = memoriaPerfiles[0] * k;
            PerfilEsquiva = memoriaPerfiles[1] * k;
            PerfilMachaque = memoriaPerfiles[2] * k;
            PerfilLejania = memoriaPerfiles[3] * k;
            float m = ajustes.ventanaLectura * 0.5f;
            cParry = PerfilParry * m;
            cEsquiva = PerfilEsquiva * m;
            cMachaque = PerfilMachaque * m;
        }
        ultimoOlvido = Time.time;
    }

    private bool memoriaGuardada;

    // Al acabar el intento (muera el o mueras tu) se queda con lo aprendido.
    private void GuardarMemoria()
    {
        if (ajustes == null || !ajustes.iaAdaptativa || memoriaGuardada) return;
        memoriaGuardada = true;
        memoriaPerfiles = new[] { PerfilParry, PerfilEsquiva, PerfilMachaque, PerfilLejania };
        intentosRecordados++;
    }

    // Cada fotograma: olvida un poco y apunta a que distancia te quedas.
    private void ActualizarMente()
    {
        if (ajustes == null || !ajustes.iaAdaptativa) return;
        float dt = Time.time - ultimoOlvido;
        if (dt < 0.1f) return;
        ultimoOlvido = Time.time;

        // Olvido suave: en "ventanaLectura" segundos una cuenta baja a la mitad.
        float olvido = Mathf.Pow(0.5f, dt / Mathf.Max(1f, ajustes.ventanaLectura));
        cParry *= olvido; cEsquiva *= olvido; cGolpe *= olvido;
        cBloqueo *= olvido; cMachaque *= olvido; cFrasco *= olvido;

        // Mientras no te toque, aprieta mas.
        SubirPresion(ajustes.presionPorSegundo * dt);

        if (player != null && !cambiando)
        {
            float d = Mathf.Abs(DxPlayer);
            distanciaMedia = Mathf.Lerp(distanciaMedia, d, 1f - Mathf.Pow(0.5f, dt / 2.5f));
        }
        RehacerPerfiles();
    }

    // Lo que le pasa a cada golpe suyo: asi sabe como te defiendes.
    private void ApuntarResultado(PlayerControler.ResultadoDano? r)
    {
        if (!r.HasValue || ajustes == null || !ajustes.iaAdaptativa) return;
        switch (r.Value)
        {
            case PlayerControler.ResultadoDano.Parry: cParry += 1f; SubirPresion(ajustes.presionPorLeerte); break;
            case PlayerControler.ResultadoDano.Bloqueado: cBloqueo += 1f; cParry += 0.35f; SubirPresion(ajustes.presionPorLeerte * 0.6f); break;
            case PlayerControler.ResultadoDano.Ignorado: cEsquiva += 1f; SubirPresion(ajustes.presionPorLeerte); break;
            default: cGolpe += 1f; BajarPresion(ajustes.presionAlDarte); break;
        }
        RehacerPerfiles();
    }

    private void ApuntarMachaque() { if (ajustes != null && ajustes.iaAdaptativa) { cMachaque += 1f; RehacerPerfiles(); } }
    private void ApuntarFrasco() { if (ajustes != null && ajustes.iaAdaptativa) cFrasco += 1f; }

    // De las cuentas a los cuatro perfiles (0 a 1).
    private void RehacerPerfiles()
    {
        float defensa = cParry + cBloqueo + cEsquiva + cGolpe;
        PerfilParry = defensa < 1.5f ? 0f : Mathf.Clamp01((cParry + cBloqueo) / defensa);
        PerfilEsquiva = defensa < 1.5f ? 0f : Mathf.Clamp01(cEsquiva / defensa);
        PerfilMachaque = Mathf.Clamp01(cMachaque / Mathf.Max(1f, ajustes.machaqueParaPerfil));
        float lejos = Mathf.InverseLerp(ajustes.distanciaCorta, ajustes.distanciaLarga, distanciaMedia);
        PerfilLejania = Mathf.Clamp01(lejos);
    }

    // ------------------------------------------------------------------ Decidir

    // En que categoria cae cada cosa que sabe hacer.
    private Mano ManoDe(string id)
    {
        if (id == AccCielo || id == AccDiezmo || id == AccSello) return Mano.Agarres;
        if (id == AccOndas || id == AccErupciones || id == AccSombras || id == AccSalto) return Mano.Distancia;
        if (id == AccGuardia || id == AccRetroceder || id == AccRodadaEspalda || id == AccProvocar) return Mano.Defensivos;
        if (id == AccEncender) return Mano.Encender;
        if (id == AccDesenvaine) return Mano.Rapidos;
        AjustesBlackKnight.Ataque a = ajustes.BuscarAtaque(id);
        if (a == null) return Mano.Rapidos;
        return a.golpes.Count >= 3 ? Mano.Largos : Mano.Rapidos;
    }

    // Lo que multiplica la probabilidad de cada cosa segun como peleas.
    public float Peso(string id)
    {
        if (ajustes == null || !ajustes.iaAdaptativa) return 1f;
        Mano m = ManoDe(id);
        float f = Mathf.Max(0f, ajustes.fuerzaAdaptacion);
        float mult = 1f;
        mult *= 1f + PerfilParry * f * (ajustes.contraParry.De(m) - 1f);
        mult *= 1f + PerfilEsquiva * f * (ajustes.contraEsquiva.De(m) - 1f);
        mult *= 1f + PerfilMachaque * f * (ajustes.contraMachaque.De(m) - 1f);
        mult *= 1f + PerfilLejania * f * (ajustes.contraLejania.De(m) - 1f);
        // Y lo que ya ha usado mucho pierde fuerza: no se vuelve monotono.
        if (Elegidas.TryGetValue(id, out int veces) && veces > 0)
            mult *= Mathf.Pow(ajustes.castigoRepetir, veces);
        return Mathf.Clamp(mult, ajustes.topeMultiplicador.x, ajustes.topeMultiplicador.y);
    }

    private void ApuntarElegida(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        // Se olvida poco a poco: solo cuentan las ultimas.
        var claves = new List<string>(Elegidas.Keys);
        foreach (string k in claves) if (Elegidas[k] > 0 && k != id) Elegidas[k]--;
        Elegidas[id] = Mathf.Min(ajustes != null ? ajustes.topeRepetir : 3, Elegidas.TryGetValue(id, out int v) ? v + 1 : 1);
    }

    // Lo que ve la depuracion y las pruebas, en una linea.
    public string Resumen() =>
        $"parry {PerfilParry:0.00} | esquiva {PerfilEsquiva:0.00} | machaque {PerfilMachaque:0.00} | lejania {PerfilLejania:0.00} (a {distanciaMedia:0.0} u) | presion {presion:0.00}";
}
