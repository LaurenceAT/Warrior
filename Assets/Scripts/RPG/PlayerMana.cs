using UnityEngine;

// Mana del player. Solo lo gasta imbuir la espada. Se recupera bebiendo el
// frasco de mana (R) y al descansar en la hoguera (se llena, como la vida y la
// estamina); no vuelve al golpear. Al reaparecer tras morir sale lleno, como la
// vida (el player es nuevo y empieza con el maximo).
public class PlayerMana : MonoBehaviour
{
    [SerializeField] private float maximo = 75f;
    [SerializeField] private float actual = 75f;

    public float Actual => actual;
    public float Maximo => maximo;
    public float Fraccion => maximo > 0f ? Mathf.Clamp01(actual / maximo) : 0f;

    public event System.Action AlCambiar;

    private void Awake()
    {
        maximo = Progreso.ManaMax;
        actual = maximo;
    }

    private void OnEnable() { Progreso.AlSubirNivel += Recalcular; Hoguera.AlDescansar += Llenar; }
    private void OnDisable() { Progreso.AlSubirNivel -= Recalcular; Hoguera.AlDescansar -= Llenar; }

    public bool Tiene(float cantidad) => actual + 0.001f >= cantidad;

    public bool Gastar(float cantidad)
    {
        if (!Tiene(cantidad)) return false;
        actual = Mathf.Max(0f, actual - cantidad);
        AlCambiar?.Invoke();
        return true;
    }

    // Devuelve lo que ha subido de verdad (0 si ya estaba lleno).
    public float Recuperar(float cantidad)
    {
        if (cantidad <= 0f) return 0f;
        float antes = actual;
        actual = Mathf.Min(maximo, actual + cantidad);
        AlCambiar?.Invoke();
        return actual - antes;
    }

    // Relee el maximo por si acaba de cambiar (subir de nivel en el mismo menu).
    public void Llenar()
    {
        maximo = Progreso.ManaMax;
        actual = maximo;
        AlCambiar?.Invoke();
    }

    // Tras subir de nivel el maximo cambia: lo ganado se suma a lo que se tenia
    // (antes se conservaba la proporcion y, con la barra a medias, parecia que
    // subir el mana no daba nada).
    private void Recalcular()
    {
        float nuevo = Progreso.ManaMax;
        float diferencia = nuevo - maximo;
        maximo = nuevo;
        actual = Mathf.Clamp(actual + Mathf.Max(0f, diferencia), 0f, maximo);
        AlCambiar?.Invoke();
    }
}
