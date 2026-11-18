using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Camara de combate contra jefes, como el "fijar objetivo" de Dark Souls: mientras
// dura la pelea de una arena normal (ArenaJefe.EnCombate), la camara encuadra a
// los dos (o a los tres en el duo). Se acerca cuando estas pegado al jefe y se
// aleja poco a poco si os separais, siempre mostrandolo; el centro va entre los
// dos y algo por encima, para que el suelo quede abajo. Al acabar la pelea vuelve
// sola la camara de siempre. Numeros en Resources/AjustesCamaraCombate.
//
// Se enciende y se apaga con el clic central del raton (la rueda), como el
// "fijar objetivo" de los Souls: apagada, manda la camara de siempre (el zoom de
// la arena). Cada pelea empieza apagada.
//
// No toca a la Cazadora (su arena es otra y tiene su propia camara).
// Se crea sola al arrancar el juego (no hay que ponerla en ninguna escena).
public class CamaraCombate : MonoBehaviour
{
    private static CamaraCombate instancia;
    private readonly List<JefeBase> jefes = new List<JefeBase>();
    private float siguienteBusqueda;
    private Transform player;
    private bool combateAnterior;
    private MarcaFijado marca;
    private static bool avisoVisto;

    // El jugador la ha encendido con el clic central (se apaga al acabar la pelea).
    public static bool Fijado { get; private set; }

    // Esta encuadrando un combate ahora (lo leen los jefes y las pruebas).
    public static bool Activo { get; private set; }
    // Cabeis los dos en pantalla con el tamano maximo (lo leen las pruebas).
    public static bool Cabeis { get; private set; }
    // El tamano que pide ahora (lo leen las pruebas).
    public static float TamanoPedido { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (instancia != null) return;
        GameObject go = new GameObject("CamaraCombate");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CamaraCombate>();
    }

    // Enciende o apaga el encuadre de combate (el clic central, y las pruebas).
    public static void Alternar()
    {
        Fijado = !Fijado;
        Sonido.Reproducir(Fijado ? "menu_confirmar" : "menu_cancelar", 0.5f);
    }

    private void LateUpdate()
    {
        AjustesCamaraCombate aj = AjustesCamaraCombate.Get();
        if (!ArenaJefe.EnCombate || !aj.activo)
        {
            // Al acabar la pelea se olvida: la siguiente empieza con la camara de siempre.
            if (combateAnterior) Fijado = false;
            combateAnterior = false;
            Apagar();
            return;
        }

        // Primera pelea de la partida: se recuerda que existe el boton.
        if (!combateAnterior && !avisoVisto)
        {
            avisoVisto = true;
            MensajePantalla.Narrativo("Clic central: cámara de combate", 4f);
        }
        combateAnterior = true;

        // El clic central la enciende y la apaga (no en pausa).
        bool pausa = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (!pausa && Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame) Alternar();
        if (!Fijado) { Apagar(); return; }

        if (Time.unscaledTime >= siguienteBusqueda || jefes.Exists(j => j == null))
        {
            siguienteBusqueda = Time.unscaledTime + 0.5f;
            jefes.Clear();
            foreach (JefeBase j in FindObjectsByType<JefeBase>(FindObjectsSortMode.None))
            {
                EnemyHealth s = j.GetComponent<EnemyHealth>();
                if (s != null && !s.Muerto && !(j is JefeCazadora)) jefes.Add(j);
            }
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                player = p != null ? p.transform : null;
            }
        }
        jefes.RemoveAll(j => j == null || j.GetComponent<EnemyHealth>().Muerto);
        if (player == null || jefes.Count == 0) { Apagar(); return; }

        // La caja que os contiene a todos (tu centro y el centro de cada jefe).
        Vector2 pj = player.position;
        float minX = pj.x, maxX = pj.x, minY = pj.y, maxY = pj.y;
        // Los pies mas bajos de todos (para dejar el suelo abajo en la pantalla).
        float pies = pj.y - aj.alturaJugador;
        foreach (JefeBase j in jefes)
        {
            Vector2 c = (Vector2)j.transform.position + Vector2.up * aj.alturaJefe;
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            pies = Mathf.Min(pies, j.transform.position.y);
        }
        float dx = maxX - minX, dy = maxY - minY;
        Camera cam = Camera.main;
        float aspecto = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;

        // El tamano justo para veros con margen, entre lo mas cerca y lo mas lejos.
        // Ojo: la camara no llega al centro exacto (pesoCentro), asi que se mide
        // desde donde va a quedar de verdad; si no, el jefe se sale por el borde.
        float cx = (minX + maxX) * 0.5f;
        float camX = pj.x + (cx - pj.x) * aj.pesoCentro;
        float ladoX = Mathf.Max(Mathf.Abs(maxX - camX), Mathf.Abs(camX - minX));
        float hace = Mathf.Max((ladoX + aj.margenX) / aspecto, dy * 0.5f + aj.margenY);
        float tamano = Mathf.Clamp(hace, aj.tamanoCerca, aj.tamanoLejos);
        // Os separais tanto que ya no cabeis los dos: manda que no te pierdas tu.
        bool cabeis = hace <= aj.tamanoLejos + 0.01f;

        // El centro, entre los dos; en alto, lo justo para que el suelo quede en su
        // sitio de la pantalla (y nunca por debajo del centro de los dos).
        float yCentro = Mathf.Max((minY + maxY) * 0.5f, pies + 2f * tamano * (0.5f - aj.sueloEnPantalla));
        float xCentro = cx;
        if (!cabeis)
        {
            // Mira hacia el jefe todo lo que puede sin sacarte a ti de la pantalla.
            float mitad = tamano * aspecto;
            float tope = Mathf.Max(0.5f, mitad * aj.margenJugador * 2f);
            float haciaJefe = Mathf.Clamp(xCentro - pj.x, -tope, tope) * aj.tiraHaciaJefe;
            xCentro = pj.x + haciaJefe;
            yCentro = Mathf.Max(pj.y, pies + 2f * tamano * (0.5f - aj.sueloEnPantalla));
        }
        Vector2 centro = new Vector2(xCentro, yCentro);

        Activo = true;
        Cabeis = cabeis;
        TamanoPedido = tamano;
        CamaraDinamica.Combate(tamano, centro, aj.pesoCentro, Mathf.Abs(centro.x - pj.x) + 2f, 0.3f);
        ActualizarMarca(aj, cam);
    }

    // La marca del objetivo: un rombo sobre el jefe fijado (el mas cercano). Si se
    // sale de la pantalla, se queda en el borde apuntando hacia el, como el
    // "fijar objetivo" de los Souls.
    private void ActualizarMarca(AjustesCamaraCombate aj, Camera cam)
    {
        if (!aj.mostrarMarca || cam == null || jefes.Count == 0) { QuitarMarca(); return; }
        JefeBase objetivo = null;
        float mejor = float.MaxValue;
        foreach (JefeBase j in jefes)
        {
            float d = Mathf.Abs(j.transform.position.x - player.position.x);
            if (d < mejor) { mejor = d; objetivo = j; }
        }
        if (objetivo == null) { QuitarMarca(); return; }

        if (marca == null) marca = MarcaFijado.Crear();
        marca.Poner(objetivo.transform.position + Vector3.up * aj.alturaMarca, cam, aj.colorMarca, aj.tamanoMarca, aj.flechaFuera);
    }

    private void QuitarMarca()
    {
        if (marca != null) { Destroy(marca.gameObject); marca = null; }
    }

    private void Apagar()
    {
        Activo = false;
        Cabeis = true;
        QuitarMarca();
        jefes.Clear();
        player = null;
    }
}
