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

    // Tipo de tajo de color (Efecto_Tajos) para cada perfil de ataque:
    // 0 horizontal, 1 curvo, 2 ascendente. Indice = perfil (0-2 combo,
    // 3-4 aereos, 7 lanzador).
    private static readonly int[] TipoTajo = { 0, 1, 0, 1, 2, 1, 1, 2 };

    // Tajo de color del elemento, colocado sobre el area que dana de verdad
    // ("centro" y "tamano" salen del perfil del golpe) y un poco mas grande que
    // ella. Solo se ven los fotogramas centrales del tajo (el instante del corte),
    // no la animacion entera.
    public void Tajo(int perfil, int direccion, Elemento elemento, Vector2 centro, float tamano, float angulo)
    {
        if (elemento == Elemento.Ninguno || perfil < 0 || perfil >= TipoTajo.Length) return;
        AnimadorHoja.Clip ce = Recortado(RecursosRPG.Get().Tajo(elemento, TipoTajo[perfil]));
        if (ce == null) return;
        // El fotograma mide 2 unidades (128 px a 64 por unidad); el tajo dibujado
        // ocupa algo menos, por eso el 1.35.
        float escala = Mathf.Clamp(tamano * 1.35f / 2f, 0.5f, 2f);
        float rot = angulo + (TipoTajo[perfil] == 2 ? 20f * direccion : 0f);
        EfectoVisual ef = EfectoVisual.Crear(ce, centro, escala, Color.white, direccion < 0, -1f, "VFX", 20, rot);
        if (ef != null) ef.transform.SetParent(transform, true);
    }

    // Copia del clip solo con los fotogramas del corte, mas rapida.
    private static readonly System.Collections.Generic.Dictionary<AnimadorHoja.Clip, AnimadorHoja.Clip> recortes =
        new System.Collections.Generic.Dictionary<AnimadorHoja.Clip, AnimadorHoja.Clip>();

    private static AnimadorHoja.Clip Recortado(AnimadorHoja.Clip c)
    {
        if (c == null || c.fotogramas == null || c.fotogramas.Length == 0) return null;
        if (recortes.TryGetValue(c, out AnimadorHoja.Clip r)) return r;
        int n = c.fotogramas.Length;
        int desde = Mathf.FloorToInt(n * 0.2f), hasta = Mathf.CeilToInt(n * 0.75f);
        var f = new Texture2D[Mathf.Max(1, hasta - desde)];
        for (int i = 0; i < f.Length; i++) f[i] = c.fotogramas[Mathf.Min(n - 1, desde + i)];
        r = new AnimadorHoja.Clip
        {
            nombre = c.nombre + "_corte", fotogramas = f, cantidad = f.Length, fps = 32f, bucle = false,
            pivote = c.pivote, pixelesPorUnidad = c.pixelesPorUnidad,
        };
        recortes[c] = r;
        return r;
    }

    // Tajos por perfil sin elemento (ahora apagados). Se conserva por si se recuperan.
    public void TajoSinElemento(int perfil, int direccion)
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
