#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Herramientas de prueba de la Bitacora, el inventario y el reinicio de los
// desafios (F7, solo en el Editor: no entra en la version final).
//   - Desbloquear / bloquear toda la informacion de un jefe (< > para elegirlo).
//   - Desbloquear / bloquear todo el Bestiario.
//   - Reiniciar todos los descubrimientos (los logros y tiempos se quedan).
//   - En un desafio: anadir objetos sin aplicar, vaciarlos y forzar el reinicio.
// Los menus que ya estan abiertos se ven al dia al volver a abrirlos.
public class DepuracionBitacora : MonoBehaviour
{
    private bool visible;
    private int jefe;
    private string mensaje = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (FindFirstObjectByType<DepuracionBitacora>() != null) return;
        GameObject go = new GameObject("DepuracionBitacora");
        DontDestroyOnLoad(go);
        go.AddComponent<DepuracionBitacora>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible) return;
        FichaJefe[] jefes = FichaJefe.Todas();
        GUILayout.BeginArea(new Rect(20, 20, 380, 470), GUI.skin.box);
        GUILayout.Label("<b>Bitácora (F7, solo Editor)</b>", new GUIStyle(GUI.skin.label) { richText = true });

        if (jefes.Length > 0)
        {
            jefe = Mathf.Clamp(jefe, 0, jefes.Length - 1);
            FichaJefe f = jefes[jefe];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30))) jefe = (jefe + jefes.Length - 1) % jefes.Length;
            GUILayout.Label($"{f.nombre}{(f.Desbloqueado ? "" : " (secreto bloqueado)")}");
            if (GUILayout.Button(">", GUILayout.Width(30))) jefe = (jefe + 1) % jefes.Length;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Desbloquear todo")) { Bitacora.DesbloquearTodo(f); mensaje = f.nombre + ": todo desbloqueado"; }
            if (GUILayout.Button("Bloquear todo")) { Bitacora.BloquearTodo(f); mensaje = f.nombre + ": todo bloqueado"; }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUILayout.Label("Bestiario");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desbloquear todo")) { Bitacora.DesbloquearBestiario(); mensaje = "Bestiario desbloqueado"; }
        if (GUILayout.Button("Bloquear todo")) { Bitacora.BloquearBestiario(); mensaje = "Bestiario bloqueado"; }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("Reiniciar TODOS los descubrimientos")) { Bitacora.Reiniciar(); mensaje = "Descubrimientos a cero"; }

        GUILayout.Space(6);
        GUILayout.Label(Desafio.Activo ? $"Desafío: {Desafio.Ficha?.nombre} ({(Desafio.Dificil ? "Difícil" : "Normal")})" : "Sin desafío en curso");
        GUI.enabled = Desafio.Activo;
        if (GUILayout.Button("Añadir objetos sin aplicar (uno de cada)"))
        {
            foreach (Equipo.Objeto o in Desafio.ObjetosMejora) Desafio.GuardarObjeto(o);
            mensaje = $"Por aplicar: {Desafio.TotalPorAplicar}";
        }
        if (GUILayout.Button("Vaciar objetos sin aplicar")) { Desafio.VaciarInventario(); mensaje = "Inventario vacío"; }
        if (GUILayout.Button("Forzar reinicio del desafío")) { Desafio.Reiniciar(); mensaje = "Reiniciando..."; }
        GUI.enabled = true;

        GUILayout.Space(6);
        GUILayout.Label(mensaje);
        GUILayout.EndArea();
    }
}
#endif
