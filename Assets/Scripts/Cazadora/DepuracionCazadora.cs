using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// Herramientas de prueba de la Cazadora. SOLO en el Editor: en la version final
// este componente no se anade y todo su codigo desaparece (queda solo el
// interruptor de las cajas, siempre apagado).
//
// F10 abre el panel: forzar cada ataque, saltar de fase, poner la vida de la
// barra, encender la oscuridad, ver las cajas de dano y desbloquear o volver a
// bloquear el jefe secreto.
public class DepuracionCazadora : MonoBehaviour
{
    public static bool VerCajas;

#if UNITY_EDITOR
    private bool abierto;
    private Vector2 scroll;
    private bool oscuridad;

    private static readonly string[] Ataques =
    {
        "tresLunas", "cruce", "ola", "ascendente", "salto", "guardia", "escudo", "ejecucion",
        "contra", "flanqueo", "caen", "espejismo", "fantasma", "trampas", "lluvia", "cruceDoble", "sentencia",
        "danza", "espejos", "silencio",
    };

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.f10Key.wasPressedThisFrame) abierto = !abierto;
    }

    private void OnGUI()
    {
        if (!abierto)
        {
            GUI.Label(new Rect(8, Screen.height - 24, 400, 20), "F10: depuración de la Cazadora (solo Editor)");
            return;
        }
        GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>CAZADORA (Editor)</b>");
        JefeCazadora j = JefeCazadora.Actual;
        if (j == null) GUILayout.Label("Entra en la arena para que aparezca.");
        else
        {
            GUILayout.Label($"Fase {j.Fase + 1}   Vida {j.GetComponent<EnemyHealth>().CurrentHealth}/{j.GetComponent<EnemyHealth>().MaxHealth}");
            GUILayout.Label($"Escudo: {j.ContadorEscudo} golpes");
            if (GUILayout.Button("Forzar parry de la Cazadora")) { if (!j.Activa) j.Activar(); j.ForzarAtaque("guardia"); }
            if (GUILayout.Button("Forzar rugido y escudo")) { if (!j.Activa) j.Activar(); j.ForzarAtaque("escudo"); }
            PlayerControler pj = FindFirstObjectByType<PlayerControler>();
            ArmaImbuida arma = pj != null ? pj.GetComponent<ArmaImbuida>() : null;
            if (arma != null && GUILayout.Button(arma.Activo == Elemento.Oscuro ? "Quitar la oscuridad del arma" : "Imbuir el arma de oscuridad"))
            {
                if (arma.Activo == Elemento.Oscuro) arma.Apagar();
                else arma.Activar(Elemento.Oscuro);
            }
            GUILayout.Label("Forzar ataque (el siguiente que haga):");
            foreach (string a in Ataques)
                if (GUILayout.Button(a)) { if (!j.Activa) j.Activar(); j.ForzarAtaque(a); }
            GUILayout.Label("Saltar a la fase:");
            GUILayout.BeginHorizontal();
            for (int f = 0; f < 3; f++) if (GUILayout.Button("Fase " + (f + 1))) j.SaltarAFase(f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Vida de la barra:");
            GUILayout.BeginHorizontal();
            foreach (float v in new[] { 1f, 0.7f, 0.5f, 0.25f, 0.05f })
                if (GUILayout.Button(Mathf.RoundToInt(v * 100) + "%")) j.PonerVida(v);
            GUILayout.EndHorizontal();
        }
        ArenaCazadora arena = ArenaCazadora.Actual;
        if (arena != null && GUILayout.Button(oscuridad ? "Oscuridad: SÍ" : "Oscuridad: NO"))
        {
            oscuridad = !oscuridad;
            arena.ForzarOscuridad(oscuridad);
        }
        if (GUILayout.Button(VerCajas ? "Cajas de daño: SÍ" : "Cajas de daño: NO")) VerCajas = !VerCajas;
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null && GUILayout.Button(p.InvulnerableExterno ? "Tú invulnerable: SÍ" : "Tú invulnerable: NO")) p.InvulnerableExterno = !p.InvulnerableExterno;
        GUILayout.Space(8);
        GUILayout.Label("Jefe secreto:");
        if (GUILayout.Button("Desbloquear")) FichaJefe.ForzarSecreto("blind_huntress", true);
        if (GUILayout.Button("Volver a bloquear")) FichaJefe.ForzarSecreto("blind_huntress", false);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
#endif
}
