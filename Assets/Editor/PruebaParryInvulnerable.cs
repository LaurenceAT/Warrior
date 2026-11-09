using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Parry durante la invulnerabilidad de despues de un golpe (batchmode, sin -quit):
//   ... -executeMethod PruebaParryInvulnerable.Todo
// En la sala de pruebas, con una Rata de escarcha quieta: te golpea, levantas la
// guardia durante el empujon (lo corta), y un golpe dentro de la ventana de parry
// es parry aunque sigas invulnerable; fuera de la ventana, ni entra ni gasta
// estamina. En silencio y sin guardar nada.
[InitializeOnLoad]
public static class PruebaParryInvulnerable
{
    private const string Clave = "PruebaParryInvulnerable.Modo";

    static PruebaParryInvulnerable()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaParryInvulnerable").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(Clave, "si");
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[PARRYINV] OK " + s); } else { fallos++; Debug.LogWarning("[PARRYINV] FALLO " + s); } }
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        private PlayerControler p;
        private T Campo<T>(string n) => (T)typeof(PlayerControler).GetField(n, Priv).GetValue(p);
        private void Poner(string n, object v) => typeof(PlayerControler).GetField(n, Priv).SetValue(p, v);

        private IEnumerator Start()
        {
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[PARRYINV] error visto: " + c + "\n" + st); } };
            for (float t = 0f; t < 10f && SalaPruebas.Actual == null; t += Time.unscaledDeltaTime) yield return null;
            SalaPruebas sala = SalaPruebas.Actual;
            yield return new WaitForSeconds(1f);
            p = FindFirstObjectByType<PlayerControler>();
            sala.Invocar(sala.Catalogo.entradas.First(e => e != null && e.nombre == "Rata de escarcha"));
            yield return new WaitForSeconds(1f);
            EnemyHealth rata = sala.Salud;
            EnemigoBase ia = rata.GetComponent<EnemigoBase>();
            ia.enabled = false;
            // La rata delante del player (el parry es de frente).
            rata.transform.position = p.transform.position + Vector3.right * 1.2f;
            typeof(PlayerControler).GetMethod("SetFacing", Priv).Invoke(p, new object[] { 1 });
            GatherInput entrada = p.GetComponent<GatherInput>();
            PlayerStamina st = p.GetComponent<PlayerStamina>();

            // 1. Golpe: empujado e invulnerable.
            p.CurarCompleto();
            var r1 = p.TakeDamage(20, rata, PlayerControler.TipoDano.Fisico);
            yield return new WaitForFixedUpdate();
            Comprobar(r1 == PlayerControler.ResultadoDano.Recibido && Campo<bool>("isInvincible") && Campo<bool>("isKnocked"), $"el golpe entra: empujado e invulnerable ({r1})");

            // 2. Guardia durante el empujon: sale y corta el empujon.
            entrada.IsKicking = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Comprobar(Campo<bool>("isBlocking") && !Campo<bool>("isKnocked"), "se levanta la guardia durante el empujón (y lo corta)");
            Comprobar(Campo<bool>("isInvincible"), "sigue invulnerable");

            // 3. Golpe dentro de la ventana: parry, aunque siga invulnerable.
            var r2 = p.TakeDamage(20, rata, PlayerControler.TipoDano.Fisico);
            Comprobar(r2 == PlayerControler.ResultadoDano.Parry, $"un golpe dentro de la ventana es parry durante la invulnerabilidad ({r2})");
            Comprobar(Campo<float>("ventanaContra") > 0f, "el parry abre el contraataque");

            // 4. Invulnerable, con la guardia pero fuera de la ventana: ni entra ni gasta estamina.
            yield return new WaitForFixedUpdate();
            entrada.IsKicking = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (Campo<bool>("isInvincible") && Campo<bool>("isBlocking"))
            {
                Poner("tBloqueo", 5f);
                st.Llenar();
                float est = st.Current;
                int vida = p.VidaActual;
                var r3 = p.TakeDamage(20, rata, PlayerControler.TipoDano.Fisico);
                Comprobar(r3 == PlayerControler.ResultadoDano.Ignorado && Mathf.Approximately(st.Current, est) && p.VidaActual == vida,
                          $"invulnerable y fuera de la ventana: el golpe no entra ni gasta estamina ({r3})");
            }
            else Comprobar(false, "no se pudo repetir la guardia mientras seguía invulnerable");

            // 5. Ya sin invulnerabilidad todo sigue igual que antes: bloqueo normal gasta estamina.
            for (float t = 0f; t < 3f && Campo<bool>("isInvincible"); t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.3f);
            p.CurarCompleto();
            entrada.IsKicking = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Poner("tBloqueo", 5f);
            st.Llenar();
            float est2 = st.Current;
            var r4 = p.TakeDamage(20, rata, PlayerControler.TipoDano.Fisico);
            Comprobar(r4 == PlayerControler.ResultadoDano.Bloqueado && st.Current < est2, $"sin invulnerabilidad, el bloqueo normal sigue igual ({r4})");

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[PARRYINV] RESULTADO: {ok} OK, {fallos} FALLOS");
            EditorApplication.ExitPlaymode();
        }
    }
}
