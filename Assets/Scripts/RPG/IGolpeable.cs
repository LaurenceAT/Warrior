using UnityEngine;

// Algo del escenario que reacciona a los espadazos del player (no es un
// enemigo): un muro de hielo que solo cede al fuego, agua que se congela con la
// escarcha... Recibe el elemento con el que esta imbuida la espada.
public interface IGolpeable
{
    void Golpear(Elemento elemento, Vector2 punto);
}
