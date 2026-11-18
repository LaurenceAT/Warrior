using System.Collections.Generic;
using UnityEngine;

// Camara de combate contra jefes, como el "fijar objetivo" de Dark Souls: mientras
// dura la pelea de una arena normal (ArenaJefe.EnCombate), la camara encuadra a
// los dos (o a los tres en el duo). Se acerca cuando estas pegado al jefe y se
// aleja poco a poco si os separais, siempre mostrandolo; el centro va entre los
// dos y algo por encima, para que el suelo quede abajo. Al acabar la pelea vuelve
// sola la camara de siempre. Numeros en Resources/AjustesCamaraCombate.
//
// No toca a la Cazadora (su arena es otra y tiene su propia camara).
// Se crea sola al arrancar el juego (no hay que ponerla en ninguna escena).
public class CamaraCombate : MonoBehaviour
{
    private static CamaraCombate instancia;
    private readonly List<JefeBase> jefes = new List<JefeBase>();
    private float siguienteBusqueda;
    private Transform player;

    // Esta encuadrando un combate ahora (lo leen los jefes y las pruebas).
    public static bool Activo { get; private set; }
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

    private void LateUpdate()
    {
        AjustesCamaraCombate aj = AjustesCamaraCombate.Get();
        if (!ArenaJefe.EnCombate || !aj.activo) { Apagar(); return; }

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
        float tamano = Mathf.Max((dx * 0.5f + aj.margenX) / aspecto, dy * 0.5f + aj.margenY, aj.tamanoCerca);
        tamano = Mathf.Min(tamano, aj.tamanoLejos);
        // El centro, entre los dos; en alto, lo justo para que el suelo quede en su
        // sitio de la pantalla (y nunca por debajo del centro de los dos).
        float yCentro = Mathf.Max((minY + maxY) * 0.5f, pies + 2f * tamano * (0.5f - aj.sueloEnPantalla));
        Vector2 centro = new Vector2((minX + maxX) * 0.5f, yCentro);

        Activo = true;
        TamanoPedido = tamano;
        CamaraDinamica.Combate(tamano, centro, aj.pesoCentro, dx * 0.5f + 2f, 0.3f);
    }

    private void Apagar()
    {
        Activo = false;
        jefes.Clear();
        player = null;
    }
}
