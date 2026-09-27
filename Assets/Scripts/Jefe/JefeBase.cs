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

    public abstract void Configurar(Rect zona, float alturaSuelo);

    protected void AvisarAterrizaje() => AlAterrizar?.Invoke();
    protected void AvisarCambioFase() => AlCambiarFase?.Invoke();
    protected void AvisarDerrota() => AlDerrotado?.Invoke();
    protected void AvisarSilencio() => AlSilencio?.Invoke();
}
