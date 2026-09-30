using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// Herramientas de prueba de los enemigos normales. SOLO en el Editor: en la
// version final no se crea y su codigo desaparece.
//
// F9 las enciende:
//   - El nombre del estado de la IA sobre cada enemigo cercano.
//   - Sus rangos: deteccion (amarillo), olvido (naranja) y su zona (azul).
//   - Un panel con el enemigo elegido (el mas cercano al player, o el que
//     elijas con < >): vida, rol, golpes que necesitas para matarlo y golpes
//     suyos para matarte (con tu espada y tu vida de ahora), y botones para
//     forzar un estado.
public class DepuracionEnemigos : MonoBehaviour
{
#if UNITY_EDITOR
    private bool activa;
    private int elegido = -1;
    private readonly Dictionary<EnemigoBase, LineRenderer[]> lineas = new Dictionary<EnemigoBase, LineRenderer[]>();
    private static Material materialLineas;
    private GUIStyle etiqueta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        GameObject go = new GameObject("DepuracionEnemigos");
        DontDestroyOnLoad(go);
        go.AddComponent<DepuracionEnemigos>();
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.f9Key.wasPressedThisFrame)
        {
            activa = !activa;
            if (!activa) BorrarLineas();
        }
        if (activa) ActualizarLineas();
    }

    // ------------------------------------------------------------------ Rangos

    private void ActualizarLineas()
    {
        var quitar = new List<EnemigoBase>();
        foreach (var par in lineas) if (par.Key == null) quitar.Add(par.Key);
        foreach (EnemigoBase e in quitar) lineas.Remove(e);

        foreach (EnemigoBase e in EnemigoBase.Activos)
        {
            if (e == null) continue;
            if (!lineas.TryGetValue(e, out LineRenderer[] l))
            {
                l = new[] { NuevaLinea(e, new Color(1f, 1f, 0f, 0.7f)), NuevaLinea(e, new Color(1f, 0.55f, 0f, 0.5f)), NuevaLinea(e, new Color(0.3f, 0.8f, 1f, 0.7f)) };
                lineas[e] = l;
            }
            Circulo(l[0], e.transform.position, e.RadioDeteccion);
            Circulo(l[1], e.transform.position, e.RadioOlvido);
            Vector3 o = e.OrigenZona;
            l[2].positionCount = 4;
            l[2].SetPosition(0, o + new Vector3(-e.RadioZona, 0.6f));
            l[2].SetPosition(1, o + new Vector3(-e.RadioZona, -0.1f));
            l[2].SetPosition(2, o + new Vector3(e.RadioZona, -0.1f));
            l[2].SetPosition(3, o + new Vector3(e.RadioZona, 0.6f));
        }
    }

    private LineRenderer NuevaLinea(EnemigoBase e, Color c)
    {
        if (materialLineas == null) materialLineas = new Material(Shader.Find("Sprites/Default"));
        LineRenderer l = new GameObject("RangoDepuracion").AddComponent<LineRenderer>();
        l.transform.SetParent(e.transform, false);
        l.useWorldSpace = true;
        l.sharedMaterial = materialLineas;
        l.startColor = l.endColor = c;
        l.startWidth = l.endWidth = 0.04f;
        l.sortingLayerName = "VFX";
        l.sortingOrder = 50;
        return l;
    }

    private static void Circulo(LineRenderer l, Vector3 centro, float radio)
    {
        const int n = 48;
        l.positionCount = n + 1;
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            l.SetPosition(i, centro + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radio);
        }
    }

    private void BorrarLineas()
    {
        foreach (var par in lineas)
            if (par.Value != null)
                foreach (LineRenderer l in par.Value) if (l != null) Destroy(l.gameObject);
        lineas.Clear();
    }

    // ------------------------------------------------------------------ Panel

    private void OnGUI()
    {
        if (!activa)
        {
            GUI.Label(new Rect(Screen.width - 260, Screen.height - 24, 250, 20), "F9: depuración de enemigos (Editor)");
            return;
        }
        if (etiqueta == null) etiqueta = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };

        Camera cam = Camera.main;
        PlayerControler pj = FindFirstObjectByType<PlayerControler>();
        List<EnemigoBase> lista = EnemigoBase.Activos;

        // Estado sobre cada enemigo.
        if (cam != null)
            foreach (EnemigoBase e in lista)
            {
                if (e == null) continue;
                EnemyHealth s = e.GetComponent<EnemyHealth>();
                Vector3 p = cam.WorldToScreenPoint((Vector2)e.transform.position + s.OffsetBarra + Vector2.up * 0.45f);
                if (p.z < 0f) continue;
                GUI.Label(new Rect(p.x - 70, Screen.height - p.y - 10, 140, 20), e.EstadoIA, etiqueta);
            }

        // Elegido: el que se haya escogido, o el mas cercano al player.
        EnemigoBase sel = elegido >= 0 && elegido < lista.Count ? lista[elegido] : Cercano(lista, pj);
        GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 330), GUI.skin.box);
        GUILayout.Label("<b>ENEMIGOS (Editor) — F9</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(30)) && lista.Count > 0) elegido = (Mathf.Max(0, elegido) - 1 + lista.Count) % lista.Count;
        if (GUILayout.Button("el más cercano")) elegido = -1;
        if (GUILayout.Button(">", GUILayout.Width(30)) && lista.Count > 0) elegido = (elegido + 1) % lista.Count;
        GUILayout.EndHorizontal();

        if (sel == null) GUILayout.Label("No hay enemigos.");
        else
        {
            EnemyHealth s = sel.GetComponent<EnemyHealth>();
            DefinicionEnemigo d = s.Definicion;
            GUILayout.Label($"{sel.name}  ·  {sel.EstadoIA}");
            GUILayout.Label(d != null ? $"{d.nombre} · zona {d.zona} · {d.rol}" : "Sin ficha");
            GUILayout.Label($"Vida {s.CurrentHealth}/{s.MaxHealth}   Almas {s.Almas}");
            if (d != null && pj != null)
            {
                // Con lo que tienes ahora: tu espada y tu vida y resistencia.
                float golpe = AjustesEnemigos.Get().danoGolpeJugador * Equipo.MultiplicadorEspada;
                int matarlo = Mathf.CeilToInt(s.CurrentHealth / Mathf.Max(1f, golpe) - 0.001f);
                float recibido = Mathf.Max(1f, d.DanoBase * (1f - Progreso.ResGolpes));
                int morir = Mathf.CeilToInt(pj.VidaMaxima / recibido - 0.001f);
                GUILayout.Label($"Te faltan {matarlo} golpes para matarlo (tu golpe ≈ {golpe:0})");
                GUILayout.Label($"Te mata en {morir} golpes suyos ({recibido:0} de {pj.VidaMaxima})");
                GUILayout.Label($"Esperado en su zona: {d.GolpesParaMatarlo} y {d.GolpesParaMorir}");
            }
            GUILayout.Label("Forzar:");
            GUILayout.BeginHorizontal();
            foreach (string est in new[] { "Aturdir", "Alerta", "Olvidar" })
                if (GUILayout.Button(est)) sel.ForzarEstado(est);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (string est in new[] { "Reposicion", "Dormir", "Despertar" })
                if (GUILayout.Button(est)) sel.ForzarEstado(est);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
    }

    private static EnemigoBase Cercano(List<EnemigoBase> lista, PlayerControler pj)
    {
        EnemigoBase mejor = null;
        float dMin = float.MaxValue;
        Vector2 desde = pj != null ? (Vector2)pj.transform.position : Vector2.zero;
        foreach (EnemigoBase e in lista)
        {
            if (e == null) continue;
            float d = Vector2.Distance(desde, e.transform.position);
            if (d < dMin) { dMin = d; mejor = e; }
        }
        return mejor;
    }
#endif
}
