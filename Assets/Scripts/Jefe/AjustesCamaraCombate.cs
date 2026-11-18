using UnityEngine;

// Numeros de la camara de combate contra jefes (CamaraCombate). Ficha en
// Assets/Resources/AjustesCamaraCombate (la crea Warrior > Camara de combate).
// Si falta la ficha, se usan estos valores.
[CreateAssetMenu(fileName = "AjustesCamaraCombate", menuName = "Warrior/Ajustes camara de combate")]
public class AjustesCamaraCombate : ScriptableObject
{
    [Tooltip("Apagado: la camara de los jefes vuelve a ser la de antes (el zoom fijo de la arena).")]
    public bool activo = true;

    [Header("Zoom (tamano de la camara: menos = mas cerca; el normal fuera de combate es 3,33)")]
    [Tooltip("Lo mas cerca que llega cuando estas pegado al jefe.")]
    [Min(1f)] public float tamanoCerca = 3.8f;
    [Tooltip("Lo mas lejos que llega cuando os separais mucho.")]
    [Min(1f)] public float tamanoLejos = 9f;

    [Header("Encuadre")]
    [Tooltip("Margen a los lados de los dos (unidades): cuanto mas, mas aire alrededor.")]
    [Min(0f)] public float margenX = 3f;
    [Tooltip("Margen arriba y abajo (unidades).")]
    [Min(0f)] public float margenY = 1.8f;
    [Tooltip("Cuanto se va la camara hacia el centro del combate (0 = sigue solo a ti, 1 = el centro exacto entre los dos).")]
    [Range(0f, 1f)] public float pesoCentro = 0.9f;
    [Tooltip("Donde queda el suelo en la pantalla (fraccion desde abajo: 0,22 = un poco por encima del borde). Asi no sobra pantalla bajo los pies.")]
    [Range(0.05f, 0.5f)] public float sueloEnPantalla = 0.22f;
    [Tooltip("Altura del centro del jefe sobre sus pies (unidades): lo que se mira de el.")]
    [Min(0f)] public float alturaJefe = 1f;
    [Tooltip("Altura de tu centro sobre tus pies (unidades).")]
    [Min(0f)] public float alturaJugador = 0.62f;

    [Header("Cuando no cabeis los dos (os separais mucho)")]
    [Tooltip("Hasta donde puede acercarse al borde de la pantalla el jugador (fraccion del ancho desde el centro: 0,38 = bastante al borde, pero dentro).")]
    [Range(0.15f, 0.48f)] public float margenJugador = 0.36f;
    [Tooltip("Cuanto mira hacia el jefe cuando no cabe en pantalla (0 = nada, 1 = todo lo que puede sin perderte).")]
    [Range(0f, 1f)] public float tiraHaciaJefe = 1f;

    [Header("Marca del objetivo (el jefe fijado)")]
    [Tooltip("Apagado: no se dibuja ninguna marca sobre el jefe.")]
    public bool mostrarMarca = true;
    [Tooltip("Tamano de la marca sobre el jefe (unidades) y su altura sobre sus pies.")]
    [Min(0.2f)] public float tamanoMarca = 0.75f;
    [Min(0f)] public float alturaMarca = 2.6f;
    [Tooltip("Cuando el jefe se sale de la pantalla, la marca se queda en el borde apuntando hacia el.")]
    public bool flechaFuera = true;
    public Color colorMarca = new Color(1f, 0.92f, 0.75f, 0.9f);

    private static AjustesCamaraCombate cache;

    public static AjustesCamaraCombate Get()
    {
        if (cache == null) cache = Resources.Load<AjustesCamaraCombate>("AjustesCamaraCombate");
        if (cache == null) cache = CreateInstance<AjustesCamaraCombate>();
        return cache;
    }
}
