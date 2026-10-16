using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Diagnostico: al entrar en el desafio de Crimson Wraith, donde aparece el player y si cae.
//   ... -executeMethod PruebaDesafioWraith.Lanzar
// Escribe "[DW] ..." en el log.
[InitializeOnLoad]
public static class PruebaDesafioWraith
{
    private const string Clave = "PruebaDesafioWraith.Modo";

    static PruebaDesafioWraith()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaDesafioWraith").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Lanzar()
    {
        SessionState.SetString(Clave, "si");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) Debug.Log("[DW] error visto: " + c + "\n" + st); };
            Partida.CarpetaPruebas = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "PartidasDW");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            yield return new WaitForSeconds(1f);

            FichaJefe f = FichaJefe.Todas().First(x => x.id == "crimson_wraith");
            Desafio.Empezar(f, false);
            while (SceneManager.GetActiveScene().name != f.escena || PantallaCarga.Cargando) yield return null;
            PlayerControler p = null;
            for (int i = 0; i < 80; i++)
            {
                if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
                if (p != null && (i < 10 || i % 5 == 0))
                {
                    Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                    Debug.Log($"[DW] frame {i} t={Time.time:0.00} player={p.transform.position} vel={(rb ? rb.linearVelocity : Vector2.zero)}");
                }
                if (i == 0)
                {
                    foreach (Hoguera h in Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.InstanceID))
                        Debug.Log($"[DW] hoguera {h.name} pos={h.transform.position} reaparicion={h.PuntoReaparicion} activa={h.gameObject.activeInHierarchy}");
                    ArenaJefe a = Object.FindFirstObjectByType<ArenaJefe>();
                    Collider2D c = a != null ? (a.GetComponent<Collider2D>() ?? a.GetComponentInChildren<Collider2D>()) : null;
                    Debug.Log($"[DW] arena={a?.name} pos={a?.transform.position} collider={c?.name} bounds={c?.bounds}");
                    GameObject muro = GameObject.Find("MuroDesafio");
                    Debug.Log($"[DW] muro={muro?.transform.position}");
                }
                yield return new WaitForFixedUpdate();
            }
            foreach (SueloFalso sf in Object.FindObjectsByType<SueloFalso>(FindObjectsSortMode.None)) Debug.Log($"[DW] SueloFalso {sf.name} pos={sf.transform.position}");
            foreach (GrietaMortal g in Object.FindObjectsByType<GrietaMortal>(FindObjectsSortMode.None)) Debug.Log($"[DW] GrietaMortal {g.name} pos={g.transform.position}");
            foreach (DerrumbeAtajo d in Object.FindObjectsByType<DerrumbeAtajo>(FindObjectsSortMode.None)) Debug.Log($"[DW] Derrumbe {d.name} pos={d.transform.position}");
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            if (z != null) Debug.Log($"[DW] ZonaJefe pos={z.transform.position} activador={z.distanciaActivador}");
            int mask = LayerMask.GetMask("Ground");
            string perfil = "";
            for (float x = 118f; x <= 180f; x += 1f)
            {
                RaycastHit2D h = Physics2D.Raycast(new Vector2(x, 12f), Vector2.down, 40f, mask);
                perfil += h.collider ? $" {x:0}:{h.point.y:0.0}({h.collider.name})" : $" {x:0}:VACIO";
            }
            Debug.Log("[DW] suelo:" + perfil);
            // Simula la web: sin colision de casillas, rehecha con cajas.
            UnityEngine.Tilemaps.Tilemap col = GameObject.Find("Colision").GetComponent<UnityEngine.Tilemaps.Tilemap>();
            col.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>().enabled = false;
            CompositeCollider2D cc = col.GetComponent<CompositeCollider2D>();
            cc.GenerateGeometry();
            Debug.Log($"[DW] sin casillas: caminos={cc.pathCount}");
            int cajas = (int)typeof(ColisionesTilemap).GetMethod("Cajas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { col, true });
            cc.GenerateGeometry();
            RaycastHit2D h2 = Physics2D.Raycast(new Vector2(128.5f, 12f), Vector2.down, 40f, mask);
            Debug.Log($"[DW] con cajas: {cajas} cajas, caminos={cc.pathCount}, suelo en 128.5 = {(h2.collider ? h2.point.y.ToString("0.00") : "NADA")}");
            p.transform.position = new Vector3(150f, 6f, 0f);
            yield return new WaitForSeconds(2f);
            if (p != null) Debug.Log($"[DW] final player={p.transform.position} vivo={p.isActiveAndEnabled}");
            Partida.Descargar();
            EditorApplication.ExitPlaymode();
        }
    }
}
