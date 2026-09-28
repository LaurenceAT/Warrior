using UnityEngine;

// Mana del player. Solo lo gasta imbuir la espada. Durante la partida solo se
// recupera bebiendo pociones de mana (R): no vuelve al golpear ni al descansar (la
// hoguera rellena las pociones). Al reaparecer tras morir sale lleno, como la vida.
public class PlayerMana : MonoBehaviour
{
    [SerializeField] private float maximo = 60f;
    [SerializeField] private float actual = 60f;
    // (Ya no se usa: el mana no vuelve al golpear.)
    [SerializeField] private float porGolpe = 0f;

    public float Actual => actual;
    public float Maximo => maximo;
    public float Fraccion => maximo > 0f ? actual / maximo : 0f;
    public float PorGolpe => porGolpe;

    public event System.Action AlCambiar;

    private void Awake()
    {
        maximo = Progreso.ManaMax;
        actual = maximo;
    }

    private void OnEnable() { Progreso.AlSubirNivel += Recalcular; }
    private void OnDisable() { Progreso.AlSubirNivel -= Recalcular; }

    public bool Tiene(float cantidad) => actual >= cantidad;

    public bool Gastar(float cantidad)
    {
        if (actual < cantidad) return false;
        actual -= cantidad;
        AlCambiar?.Invoke();
        return true;
    }

    public void Recuperar(float cantidad)
    {
        if (cantidad <= 0f) return;
        actual = Mathf.Min(maximo, actual + cantidad);
        AlCambiar?.Invoke();
    }

    public void Llenar()
    {
        actual = maximo;
        AlCambiar?.Invoke();
    }

    // Tras subir de nivel el maximo cambia; se conserva la proporcion.
    private void Recalcular()
    {
        float f = Fraccion;
        maximo = Progreso.ManaMax;
        actual = maximo * f;
        AlCambiar?.Invoke();
    }
}
