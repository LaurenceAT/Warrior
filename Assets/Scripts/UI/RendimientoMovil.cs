using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rendimiento en un movil DE VERDAD (en PC y en el Editor no hace nada).
//
// Dos cosas:
//   1. Pone un tope de fotogramas. Sin el, en un movil de 120 Hz el juego
//      intenta ir a 120: se calienta, gasta bateria y va a tirones igual.
//   2. Si aun asi va mal, baja una vez la resolucion a la que se dibuja (el
//      juego se ve igual de grande, solo un poco menos fino). Una sola vez: no
//      se pasa el rato subiendo y bajando.
//
// Los numeros estan en Resources/AjustesTactiles ("Rendimiento").
public class RendimientoMovil : MonoBehaviour
{
    private static RendimientoMovil instancia;

    private AjustesTactiles aj;
    private float espera;        // los primeros segundos no cuentan (carga)
    private float acumulado;
    private int cuadros;
    private int malas;           // tandas seguidas yendo mal
    private bool bajado;

    private const float Tanda = 3f;      // segundos de cada medida
    private const int TandasMalas = 2;   // hacen falta dos seguidas

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arranque()
    {
        if (!ModoMovil.EnMovilDeVerdad || instancia != null) return;
        AjustesTactiles aj = AjustesTactiles.Cargar();

        // El tope de fotogramas solo manda si el vsync esta apagado.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Max(30, aj.fotogramasObjetivo);

        if (!aj.bajarSolo) return;
        GameObject go = new GameObject("RendimientoMovil");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<RendimientoMovil>();
        instancia.aj = aj;
        instancia.espera = 5f;
    }

    private void Update()
    {
        if (bajado) { enabled = false; return; }
        float dt = Time.unscaledDeltaTime;
        if (espera > 0f) { espera -= dt; return; }

        acumulado += dt;
        cuadros++;
        if (acumulado < Tanda) return;

        float fps = cuadros / acumulado;
        acumulado = 0f;
        cuadros = 0;
        if (fps >= aj.fotogramasMinimos) { malas = 0; return; }
        if (++malas < TandasMalas) return;
        Bajar(fps);
    }

    private void Bajar(float fps)
    {
        bajado = true;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.renderScale > aj.escalaBaja)
        {
            urp.renderScale = aj.escalaBaja;
            Debug.Log($"[Movil] Iba a {fps:0} fotogramas: se baja la resolucion de dibujado a {aj.escalaBaja:0.00}.");
        }
    }
}
