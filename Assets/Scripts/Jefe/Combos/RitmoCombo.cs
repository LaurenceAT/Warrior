using System.Collections;
using UnityEngine;

// Pieza compartida de los combos con parry continuo (sus numeros: ReglasCombo,
// dentro de la ficha de cada jefe). El jefe la anade al despertar y la usa en
// cada golpe:
//   Empezar()                      al empezar un combo (cuentas a cero)
//   AvisoObjetivo(...)             cuanto debe durar el aviso de este golpe
//   Avisar(peso, segundos)         aviso de color con ritmo (naranja rapido, rojo lento)
//   DanoPermitido(dano, jugador)   dano recortado al tope del combo
//   AntesDeGolpear/DespuesDeGolpear  alrededor de cada Golpear (tono del parry)
//   Registrar(resultado, ...)      cuenta parrys, chispas, parada de imagen y tono
//   FinGolpe()                     al acabar la caja del golpe (pausa minima)
// El combo sigue aunque pares: es el jefe quien decide que hace con Perfecto.
public class RitmoCombo : MonoBehaviour
{
    private ReglasCombo reglas;
    private SpriteRenderer sr;
    private MaterialPropertyBlock bloque;
    private Coroutine avisoRitmo;
    private ReservaChispas chispas;
    private float finGolpeAnterior = -99f, ultimoParry = -99f;

    // Combo en curso: golpes, parrys, golpes que te tocaron (recibidos o
    // bloqueados) y dano que te ha hecho. Seguidos: golpes sin recuperacion.
    public int Golpes { get; private set; }
    public int Parrys { get; private set; }
    public int Tocados { get; private set; }
    public int DanoCombo { get; private set; }
    public int Seguidos { get; private set; }
    // Paraste todos los golpes del combo (al menos dos).
    public bool Perfecto => Golpes >= 2 && Parrys == Golpes;
    public float TonoParry { get; private set; } = 1f;
    public ReservaChispas Chispas => chispas;
    // Hueco real entre el final del golpe anterior y el aviso de este (lo leen las pruebas).
    public float SegundosDesdeUltimoGolpe => Time.time - finGolpeAnterior;
    // Cada aviso: su peso, su color y su ritmo (parpadeos por segundo).
    public event System.Action<PesoGolpe, Color, float> AlAvisar;

    public void Configurar(ReglasCombo r, SpriteRenderer s)
    {
        reglas = r;
        sr = s;
        bloque = new MaterialPropertyBlock();
        if (chispas == null) chispas = gameObject.AddComponent<ReservaChispas>();
        TonoParry = reglas.tonoInicial;
    }

    public void Empezar()
    {
        Golpes = Parrys = Tocados = DanoCombo = 0;
        finGolpeAnterior = -99f;
    }

    // Tras la pausa de recuperacion vuelve a contar desde cero.
    public void Recuperado() => Seguidos = 0;

    // Haria falta una recuperacion antes de estos golpes (pasarian del maximo seguido).
    public bool PideRecuperacion(int golpes) => Seguidos + golpes > reglas.maxGolpesSeguidos;

    // El aviso de un golpe: el deseado, nunca menos que el minimo de su peso (o
    // del golpe final) ni que lo que falta para la pausa minima con el anterior.
    public float AvisoObjetivo(PesoGolpe p, bool final, float deseado)
    {
        float a = Mathf.Max(deseado, reglas.AvisoMinimo(p, final));
        return Mathf.Max(a, reglas.pausaMinEntreGolpes - (Time.time - finGolpeAnterior));
    }

    // Recorta el dano si el combo ya te quito su tope (con el dano de Dificil).
    public int DanoPermitido(int dano, PlayerControler pc)
    {
        if (pc == null) return dano;
        float queda = reglas.topeDanoCombo * pc.VidaMaxima - DanoCombo;
        float mult = Desafio.MultDano;
        return dano * mult > queda ? Mathf.Max(0, Mathf.FloorToInt(queda / mult)) : dano;
    }

    // Cada parry seguido suena un poco mas agudo (el sonido lo pone el jugador).
    public void AntesDeGolpear() => PlayerControler.TonoSiguienteParry = TonoParry;
    public void DespuesDeGolpear() => PlayerControler.TonoSiguienteParry = 1f;

    // Lo que pasa tras cada golpe. Devuelve true si fue un parry. Las chispas
    // pueden ir de otro color (p. ej. el jefe inmune al aturdimiento).
    public bool Registrar(PlayerControler.ResultadoDano? r, int danoHecho, Vector2 punto, float direccion, Color? chispaA = null, Color? chispaB = null)
    {
        Golpes++;
        Seguidos++;
        DanoCombo += Mathf.Max(0, danoHecho);
        if (r == PlayerControler.ResultadoDano.Parry)
        {
            Parrys++;
            chispas.Chispas(punto, reglas.chispas, chispaA ?? reglas.colorChispas, chispaB ?? reglas.colorChispas2, direccion);
            if (reglas.paradaImagen > 0f) StartCoroutine(ParadaImagen(reglas.paradaImagen));
            TonoParry = Mathf.Min(reglas.tonoMaximo, TonoParry + reglas.subidaTono);
            ultimoParry = Time.time;
            return true;
        }
        bool tocado = r == PlayerControler.ResultadoDano.Recibido || r == PlayerControler.ResultadoDano.Bloqueado;
        if (tocado) Tocados++;
        if (reglas.tonoReiniciaAlFallar && tocado) TonoParry = reglas.tonoInicial;
        return false;
    }

    public void FinGolpe() => finGolpeAnterior = Time.time;

    // Parada de imagen extra: espera a que acabe la del jugador (si la hay) y para un instante.
    private IEnumerator ParadaImagen(float segundos)
    {
        for (float t = 0f; t < 0.4f && Time.timeScale < 0.99f; t += Time.unscaledDeltaTime) yield return null;
        if (Time.timeScale < 0.99f) yield break;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(segundos);
        if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (reglas != null && reglas.tonoReiniciaSegundos > 0f && Time.time - ultimoParry > reglas.tonoReiniciaSegundos)
            TonoParry = reglas.tonoInicial;
    }

    // ------------------------------------------------------------------ Aviso de color

    // El ligero parpadea rapido; el pesado late lento y mas fuerte.
    public void Avisar(PesoGolpe p, float duracion)
    {
        Color c = reglas.ColorAviso(p);
        float hz = reglas.Parpadeo(p);
        if (avisoRitmo != null) StopCoroutine(avisoRitmo);
        avisoRitmo = StartCoroutine(AvisoRitmo(c, duracion, hz, p == PesoGolpe.Pesado));
        AlAvisar?.Invoke(p, c, hz);
    }

    private IEnumerator AvisoRitmo(Color c, float duracion, float hz, bool pesado)
    {
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float pulso = pesado ? 0.5f - 0.5f * Mathf.Cos(t * hz * Mathf.PI * 2f) : (Mathf.Repeat(t * hz, 1f) < 0.5f ? 1f : 0.2f);
            Destello(c, reglas.intensidadAviso * (0.3f + 0.7f * pulso));
            yield return null;
        }
        Destello(c, 0f);
        avisoRitmo = null;
    }

    // Hay un aviso de color en marcha (lo leen las pruebas).
    public bool AvisoActivo => avisoRitmo != null;

    public void QuitarAviso()
    {
        if (avisoRitmo != null) { StopCoroutine(avisoRitmo); avisoRitmo = null; }
        Destello(Color.white, 0f);
    }

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");
    public void Destello(Color c, float cantidad)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(bloque);
        bloque.SetColor(IdFlashColor, c);
        bloque.SetFloat(IdFlashAmount, cantidad);
        sr.SetPropertyBlock(bloque);
    }
}
