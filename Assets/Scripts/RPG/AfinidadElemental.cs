using UnityEngine;

// Debilidades y resistencias de un enemigo comun a los elementos. 1 = normal,
// mas de 1 = debil (mas dano, y sufre el estado), 0.5 o menos = resiste (menos
// dano y no sufre el estado). Los jefes calculan la suya segun la fase.
public class AfinidadElemental : MonoBehaviour, IAfinidadElemental
{
    public float fuego = 1f;
    public float hielo = 1f;
    public float oscuro = 1f;
    public float sagrado = 1f;
    [UnityEngine.Serialization.FormerlySerializedAs("acido")]
    public float sangrado = 1f;

    public float Multiplicador(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return fuego;
            case Elemento.Hielo: return hielo;
            case Elemento.Oscuro: return oscuro;
            case Elemento.Sagrado: return sagrado;
            case Elemento.Sangrado: return sangrado;
            default: return 1f;
        }
    }

    public void Poner(float fuego, float hielo, float oscuro, float sagrado, float sangrado)
    {
        this.fuego = fuego; this.hielo = hielo; this.oscuro = oscuro; this.sagrado = sagrado; this.sangrado = sangrado;
    }
}
