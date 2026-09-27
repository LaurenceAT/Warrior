using UnityEngine;

// Efectos de los golpes del player, en el instante exacto del golpe (el Animation
// Event del ataque llama a AplicarGolpe, y este pide el tajo):
//   - Un tajo por cada perfil de ataque de la espada (se ve aunque falle).
//   - Un impacto donde el golpe conecta: distinto para espada, punos y el
//     contraataque del parry (mas grande y rojo).
public class EfectosGolpePlayer : MonoBehaviour
{
    // Indice = perfil de ataque (0-2 combo, 3-4 aereos, 7 lanzador). Vacio = sin tajo.
    public AnimadorHoja.Clip[] tajos;
    public float[] escalaTajo;
    // Desplazamiento del tajo respecto al centro del player (x hacia donde mira).
    public Vector2 desvioTajo = new Vector2(0.85f, 0.05f);

    public AnimadorHoja.Clip impactoEspada;
    public AnimadorHoja.Clip impactoPunos;
    public AnimadorHoja.Clip impactoContra;
    public Color colorImpacto = new Color(0.75f, 0.92f, 1f, 1f);

    public void Tajo(int perfil, int direccion)
    {
        if (tajos == null || perfil < 0 || perfil >= tajos.Length) return;
        AnimadorHoja.Clip c = tajos[perfil];
        if (c == null || c.fotogramas == null || c.fotogramas.Length == 0) return;

        float escala = escalaTajo != null && perfil < escalaTajo.Length && escalaTajo[perfil] > 0f ? escalaTajo[perfil] : 1f;
        Vector2 pos = (Vector2)transform.position + new Vector2(desvioTajo.x * direccion, desvioTajo.y);
        EfectoVisual e = EfectoVisual.Crear(c, pos, escala, Color.white, direccion < 0, -1f, "VFX", 20);
        // Sigue al player durante el tajo, que no se quede atras al avanzar.
        if (e != null) e.transform.SetParent(transform, true);
    }

    public void Impacto(Vector2 punto, TipoArma arma, bool contra)
    {
        AnimadorHoja.Clip c = contra ? impactoContra : arma == TipoArma.Punos ? impactoPunos : impactoEspada;
        if (c == null) return;
        float escala = contra ? 1.5f : 0.9f;
        Color color = contra ? Color.white : colorImpacto;
        EfectoVisual.Crear(c, punto, escala, color, Random.value < 0.5f, -1f, "VFX", 22, Random.Range(-20f, 20f));
    }
}
