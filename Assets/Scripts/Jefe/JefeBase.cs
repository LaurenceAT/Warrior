using UnityEngine;

// Lo comun de los jefes para su arena (ArenaJefe): la arena les dice sus limites
// y ellos avisan de su aparicion, del cambio de fase, de la derrota y de los
// momentos en que la musica debe callarse (una transicion dramatica).
public abstract class JefeBase : EnemigoBase
{
    public event System.Action AlAterrizar;
    public event System.Action AlCambiarFase;
    public event System.Action AlDerrotado;
    // La musica baja casi del todo (el jefe cae y parece muerto antes de revivir).
    public event System.Action AlSilencio;

    [Header("Sonidos")]
    [Tooltip("Los sonidos de este jefe, accion por accion (Assets/Data/Sonidos). Lo que no este aqui suena de la biblioteca general.")]
    [SerializeField] protected SonidosAcciones sonidos;

    // Los jefes no usan lo de los enemigos normales (dormir lejos, alerta, giro
    // con retraso, aceleracion, parry que interrumpe...): ver EnemigoBase.EsJefe.
    protected override bool EsJefe => true;

    public abstract void Configurar(Rect zona, float alturaSuelo);

    // Un sonido del jefe: el de su archivo si lo tiene, si no el de la biblioteca.
    protected void Sonar(string clave, float volumen = 1f, float tono = 1f)
    {
        RecursosRPG.GrupoSonido g = sonidos != null ? sonidos.Buscar(clave) : null;
        if (g != null) Sonido.ReproducirGrupo(g, "jefe:" + clave, volumen, tono);
        else Sonido.Reproducir(clave, volumen, tono);
    }

    // Modo Dificil de los desafios (fuera de ellos todo vale 1): mas vida y
    // menos pausa entre ataques. El dano extra lo pone el player al recibirlo.
    protected static int VidaDesafio(int vida) => Mathf.Max(1, Mathf.RoundToInt(vida * Desafio.MultVida));
    protected static float PausaDesafio(float segundos) => segundos / Desafio.MultVelocidad;

    protected void AvisarAterrizaje() => AlAterrizar?.Invoke();

    // Bitacora (fichas de Desafios): el jefe empieza un ataque (los instakills,
    // al mostrar su aviso) y cambia de fase. El id es el de su ficha.
    private FichaJefe fichaBitacora;
    private int faseBitacora = 1;
    protected FichaJefe FichaBitacora => fichaBitacora != null ? fichaBitacora : fichaBitacora = FichaJefe.DeEscena(gameObject.scene.name);
    protected void AnunciarAtaque(string ataque) => Bitacora.Ataque(FichaBitacora, ataque);

    protected void AvisarCambioFase()
    {
        AlCambiarFase?.Invoke();
        Bitacora.Fase(FichaBitacora, ++faseBitacora);
    }
    protected void AvisarDerrota() => AlDerrotado?.Invoke();
    protected void AvisarSilencio() => AlSilencio?.Invoke();
}
