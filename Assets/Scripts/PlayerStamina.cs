using UnityEngine;

// Estamina del player, al estilo de Elden Ring.
//
//  - No se regenera mientras se esta gastando (correr, subir por la pared...).
//  - Al dejar de gastar espera un momento (regenDelay) y luego se recupera poco a
//    poco (regenRate por segundo).
//  - Si llega a 0 el player queda "agotado": nada que gaste estamina funciona hasta
//    recuperar una parte (exhaustedRecoverFraction). Nunca baja de 0.
//
// Una accion puede salir aunque cueste mas de lo que queda: si queda algo, sale y
// deja la estamina a 0. Es como funciona en Elden Ring, y evita la sensacion de que
// el boton "falla" por un punto de estamina.
//
// El arco no pasa nunca por aqui: no consume estamina.
public class PlayerStamina : MonoBehaviour
{
    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;

    [Header("Regeneracion")]
    // Unidades por segundo. Con 40 y maximo 100, de vacio a lleno en 2.5 s.
    [SerializeField] private float regenRate = 40f;
    // Espera desde el ultimo gasto hasta que empieza a regenerar.
    [SerializeField] private float regenDelay = 0.8f;
    // Agotado: bloqueado hasta recuperar esta parte del maximo (0.3 = 30 %).
    [Range(0f, 1f)] [SerializeField] private float exhaustedRecoverFraction = 0.3f;

    [Header("Costes: acciones con Shift (por segundo)")]
    [SerializeField] private float sprintCostPerSecond = 18f;
    [SerializeField] private float wallRunCostPerSecond = 30f;
    // Coste unico al engancharse a la pared, ademas del continuo.
    [SerializeField] private float wallRunEntryCost = 8f;

    [Header("Costes: otras acciones")]
    // Los golpes de espada llevan su coste en su propio perfil de ataque
    // (PlayerControler > Attack Profiles > Coste Estamina).
    [SerializeField] private float plungeCost = 25f;
    [SerializeField] private float dodgeCost = 15f;

    [SerializeField] private bool exhausted;
    private float esperaRegen;

    public float Current => currentStamina;
    public float Max => maxStamina;
    public float Fraction => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool Exhausted => exhausted;

    public float SprintCostPerSecond => sprintCostPerSecond;
    public float WallRunCostPerSecond => wallRunCostPerSecond;
    public float WallRunEntryCost => wallRunEntryCost;
    public float PlungeCost => plungeCost;
    public float DodgeCost => dodgeCost;

    private void Awake()
    {
        currentStamina = maxStamina;
        exhausted = false;
    }

    // Si se puede empezar una accion que gasta estamina.
    public bool CanSpend()
    {
        return !exhausted && currentStamina > 0f;
    }

    // Gasto puntual (un golpe, un barrido). Devuelve false si no se puede, y en ese
    // caso no gasta nada.
    public bool Spend(float cantidad)
    {
        if (!CanSpend()) return false;
        Aplicar(cantidad);
        return true;
    }

    // Gasto continuo, para llamar en cada paso mientras dura la accion. Devuelve
    // false cuando ya no se puede seguir.
    public bool SpendOverTime(float porSegundo, float dt)
    {
        if (!CanSpend()) return false;
        Aplicar(porSegundo * dt);
        return true;
    }

    private void Aplicar(float cantidad)
    {
        currentStamina = Mathf.Max(0f, currentStamina - Mathf.Max(0f, cantidad));

        // Cada gasto reinicia la espera: mientras se gasta sin parar, no regenera.
        esperaRegen = regenDelay;

        if (currentStamina <= 0f) exhausted = true;
    }

    private void Update()
    {
        if (esperaRegen > 0f)
        {
            esperaRegen -= Time.deltaTime;
            return;
        }

        if (currentStamina < maxStamina)
            currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * Time.deltaTime);

        if (exhausted && currentStamina >= maxStamina * exhaustedRecoverFraction)
            exhausted = false;
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }
}
