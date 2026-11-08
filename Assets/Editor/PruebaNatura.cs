using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Prueba de la imbuicion de Natura, linea de comandos (sin -quit):
//   ... -executeMethod PruebaNatura.Todo
// Escribe "[NATURA] OK/FALLO" y una captura de la rueda en PruebaNieve/natura_rueda.png.
// En silencio y sin tocar el progreso real.
[InitializeOnLoad]
public static class PruebaNatura
{
    private const string Clave = "PruebaNatura.Modo";

    static PruebaNatura()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaNatura").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(Clave, "todo");
        EditorSceneManager.OpenScene("Assets/Scenes/Nivel Nieve.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[NATURA] OK " + s); } else { fallos++; Debug.LogWarning("[NATURA] FALLO " + s); } }

        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private IEnumerator Start()
        {
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[NATURA] error visto: " + c); } };
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Directory.CreateDirectory(carpeta);
            yield return new WaitForSecondsRealtime(2f);

            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            Comprobar(p != null, "hay player");
            if (p == null) { Terminar(); yield break; }
            PlayerStamina st = p.GetComponent<PlayerStamina>();
            PlayerMana mana = p.GetComponent<PlayerMana>();
            ArmaImbuida arma = p.GetComponent<ArmaImbuida>();
            Comprobar(st != null && mana != null && arma != null, "estamina, mana y arma en el player");
            float regen = (float)typeof(PlayerStamina).GetField("regenRate", Priv).GetValue(st);
            AjustesProgreso aj = AjustesProgreso.Get();
            MethodInfo coste = typeof(PlayerControler).GetMethod("CosteAtaque", Priv);

            // Datos fijos.
            Comprobar(Elementos.Rueda.Length == 6 && Elementos.Rueda[5] == Elemento.Natura, "Natura es el sexto hueco de la rueda");
            Comprobar(Elementos.Todos.Length == 5 && !Elementos.EsDeDano(Elemento.Natura), "Natura no cuenta como elemento de dano (fichas siguen con 5)");
            Comprobar(Elementos.Icono(Elemento.Natura) != null, "Natura tiene icono (la hoja)");
            Comprobar(Mathf.Approximately((float)coste.Invoke(p, new object[] { 10f }), 10f), "sin Natura, los ataques cuestan lo normal");

            // La rueda: 6 huecos. Captura.
            RuedaImbuir.Abrir(Elemento.Ninguno, e => null, e => { });
            yield return new WaitForSecondsRealtime(0.4f);
            RuedaImbuir rueda = FindFirstObjectByType<RuedaImbuir>();
            Image[] fondos = rueda != null ? (Image[])typeof(RuedaImbuir).GetField("fondos", Priv).GetValue(rueda) : null;
            Comprobar(fondos != null && fondos.Length == 6 && System.Array.TrueForAll(fondos, f => f != null), "la rueda tiene 6 huecos");
            typeof(RuedaImbuir).GetField("elegido", Priv).SetValue(rueda, 5);
            typeof(RuedaImbuir).GetMethod("Refrescar", Priv).Invoke(rueda, null);
            yield return Captura(Path.Combine(carpeta, "natura_rueda.png"));
            RuedaImbuir.Cerrar();
            yield return new WaitForSecondsRealtime(0.5f);

            // Imbuir con Natura.
            mana.Llenar();
            st.Llenar();
            float manaAntes = mana.Actual;
            typeof(PlayerControler).GetMethod("EmpezarImbuir", Priv).Invoke(p, new object[] { Elemento.Natura });
            float t0 = Time.realtimeSinceStartup;
            while (arma.Activo != Elemento.Natura && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Comprobar(arma.Activo == Elemento.Natura, "el arma queda imbuida de Natura");
            Comprobar(st.Current < 3f, $"al imbuir se vacia la estamina ({st.Current:0.0})");
            Comprobar(!st.Exhausted, "sin quedar agotado");
            Comprobar(Mathf.Abs(manaAntes - mana.Actual - arma.CosteMana) < 0.5f, $"gasta {arma.CosteMana:0} de mana ({manaAntes:0} -> {mana.Actual:0})");
            Comprobar(p.ElementoActivo == Elemento.Ninguno && arma.ElementoDano == Elemento.Ninguno, "los golpes con Natura son fisicos");
            Comprobar(Mathf.Approximately((float)coste.Invoke(p, new object[] { 10f }), 10f * aj.naturaCosteAtaque),
                      $"con Natura los ataques cuestan x{aj.naturaCosteAtaque}");

            // Recuperacion: medida en medio segundo, sin gastar.
            yield return new WaitForSeconds(0.3f);
            float a = st.Current; float ta = Time.time;
            yield return new WaitForSeconds(0.5f);
            float ritmo = (st.Current - a) / Mathf.Max(0.01f, Time.time - ta);
            Comprobar(Mathf.Abs(ritmo / (regen * aj.naturaRecuperacion) - 1f) < 0.15f,
                      $"recupera {ritmo:0}/s (normal {regen:0}/s, con Natura {regen * aj.naturaRecuperacion:0}/s)");

            // Al apagarse, todo vuelve a lo normal.
            arma.Apagar();
            st.Spend(60f);
            yield return new WaitForSeconds(1f);
            a = st.Current; ta = Time.time;
            yield return new WaitForSeconds(0.4f);
            ritmo = (st.Current - a) / Mathf.Max(0.01f, Time.time - ta);
            Comprobar(Mathf.Abs(ritmo / regen - 1f) < 0.15f, $"sin Natura recupera lo normal ({ritmo:0}/s)");
            Comprobar(Mathf.Approximately((float)coste.Invoke(p, new object[] { 10f }), 10f), "sin Natura los ataques vuelven a costar lo normal");

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Terminar();
        }

        private void Terminar()
        {
            Debug.Log($"[NATURA] RESULTADO: {ok} OK, {fallos} FALLOS");
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Captura(string ruta)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            rt.Release();
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
