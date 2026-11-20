using System.IO;
using UnityEditor;
using UnityEngine;

// Menu Warrior > Movil: todo lo de la version de movil que se puede hacer sin
// compilar una APK.
//
//   - Ver los controles tactiles: enciende el modo movil DENTRO del Editor, asi
//     que al darle a Play salen el joystick y los botones y se pueden probar con
//     el raton (el raton cuenta como un dedo).
//   - Probar en la Sala de pruebas: lo enciende y abre la sala jugando.
//   - Crear la ficha: deja Resources/AjustesTactiles.asset con la disposicion de
//     salida. NO pisa lo que ya este editado.
//   - Volver a la disposicion de salida: eso si lo pisa (pregunta antes).
//
// No toca Build Settings ni el progreso del jugador: el interruptor vive en los
// ajustes del Editor (EditorPrefs).
public static class CrearControlesTactiles
{
    private const string Ficha = "Assets/Resources/AjustesTactiles.asset";
    private const string MenuVer = "Warrior/Móvil/Ver los controles táctiles";

    // ------------------------------------------------------------------ Interruptor

    [MenuItem(MenuVer, false, 0)]
    private static void AlternarVer()
    {
        ModoMovil.Forzado = !ModoMovil.Forzado;
        Menu.SetChecked(MenuVer, ModoMovil.Forzado);
        Debug.Log($"[Móvil] Controles táctiles en el Editor: {(ModoMovil.Forzado ? "encendidos" : "apagados")}.");

        // Si ya estamos jugando, aparecen o desaparecen al momento.
        if (!EditorApplication.isPlaying) return;
        if (ModoMovil.Forzado) ControlesTactiles.Montar();
        else ControlesTactiles.Quitar();
    }

    [MenuItem(MenuVer, true)]
    private static bool AlternarVerValidar()
    {
        Menu.SetChecked(MenuVer, ModoMovil.Forzado);
        return true;
    }

    // ------------------------------------------------------------------ Probar

    [MenuItem("Warrior/Móvil/Probar en la Sala de pruebas", false, 1)]
    private static void ProbarEnLaSala()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Móvil] Sal del Play primero."); return; }
        CrearFicha();
        ModoMovil.Forzado = true;
        Menu.SetChecked(MenuVer, true);
        CrearSalaPruebas.AbrirYJugar();
    }

    // ------------------------------------------------------------------ Ficha

    [MenuItem("Warrior/Móvil/Crear la ficha de los controles", false, 20)]
    public static void CrearFicha()
    {
        if (File.Exists(Ficha))
        {
            Debug.Log("[Móvil] La ficha ya existe: Resources/AjustesTactiles (no se toca).");
            return;
        }
        AjustesTactiles a = AjustesTactiles.PorDefecto();
        AssetDatabase.CreateAsset(a, Ficha);
        AssetDatabase.SaveAssets();
        AjustesTactiles.Olvidar();
        Debug.Log($"[Móvil] Ficha creada: {Ficha} ({a.botones.Count} botones).");
    }

    [MenuItem("Warrior/Móvil/Volver a la disposición de salida", false, 21)]
    private static void Rehacer()
    {
        if (!EditorUtility.DisplayDialog("Controles táctiles",
            "Esto borra las posiciones y tamaños que hayas tocado a mano en la ficha y vuelve a dejar la disposición de salida.\n\n¿Seguir?",
            "Volver a la de salida", "Dejarlo como está")) return;

        AjustesTactiles vieja = AssetDatabase.LoadAssetAtPath<AjustesTactiles>(Ficha);
        if (vieja == null) { CrearFicha(); return; }

        AjustesTactiles nueva = AjustesTactiles.PorDefecto();
        vieja.botones = nueva.botones;
        vieja.centroJoystick = nueva.centroJoystick;
        vieja.tamanoBase = nueva.tamanoBase;
        vieja.tamanoPomo = nueva.tamanoPomo;
        vieja.zonaJoystick = nueva.zonaJoystick;
        vieja.seguirDedo = nueva.seguirDedo;
        vieja.margenJoystick = nueva.margenJoystick;
        vieja.fotogramasObjetivo = nueva.fotogramasObjetivo;
        vieja.bajarSolo = nueva.bajarSolo;
        vieja.fotogramasMinimos = nueva.fotogramasMinimos;
        vieja.escalaBaja = nueva.escalaBaja;
        vieja.posicionVolver = nueva.posicionVolver;
        vieja.tamanoVolver = nueva.tamanoVolver;
        vieja.camaraConDedo = nueva.camaraConDedo;
        vieja.alcanceCamara = nueva.alcanceCamara;
        vieja.arrastreMinimo = nueva.arrastreMinimo;
        vieja.camaraVertical = nueva.camaraVertical;
        EditorUtility.SetDirty(vieja);
        AssetDatabase.SaveAssets();
        AjustesTactiles.Olvidar();
        Debug.Log("[Móvil] Ficha devuelta a la disposición de salida.");
    }

    // ------------------------------------------------------------------ Informe

    // Dice si los controles pisan alguna zona del HUD, que es lo que hay que
    // decidir antes de dar la disposicion por buena.
    [MenuItem("Warrior/Móvil/Informe de la disposición", false, 40)]
    private static void Informe()
    {
        AjustesTactiles a = AssetDatabase.LoadAssetAtPath<AjustesTactiles>(Ficha) ?? AjustesTactiles.PorDefecto();
        AjustesInterfaz hud = Resources.Load<AjustesInterfaz>("AjustesInterfaz");

        string t = "[Móvil] Disposición de los controles (pantalla de 1920x1080):\n";
        t += $"  Joystick: centro a ({a.centroJoystick.x}, {a.centroJoystick.y}) desde abajo a la izquierda, aro de {a.tamanoBase} px";
        t += a.seguirDedo ? " (aparece donde pongas el dedo).\n" : " (fijo).\n";
        t += a.camaraConDedo
            ? $"  Cámara: arrastrando donde no hay botones, hasta {a.alcanceCamara} unidades a media pantalla.\n"
            : "  Cámara: apagada (no se mueve con el dedo).\n";

        int chicos = 0;
        foreach (AjustesTactiles.Boton b in a.botones)
        {
            if (b == null || !b.activo) continue;
            t += $"  {b.accion}: {b.tamano} px, a ({b.posicion.x}, {b.posicion.y}) de {b.anclaje}";
            t += b.mantener ? " [mantener]\n" : "\n";
            if (b.tamano < 110f) chicos++;
        }
        if (chicos > 0) t += $"  AVISO: {chicos} botón(es) por debajo de 110 px; con el dedo cuesta acertar.\n";

        if (hud != null)
        {
            t += "  HUD en móvil (AjustesInterfaz):\n";
            t += $"   - Las almas y los frascos van en fila arriba a la derecha, a {hud.filaArribaMovil} px del borde.\n";
            t += $"   - El aviso de objetos por aplicar sube {hud.subidaMovil} px.\n";
            t += $"   - La barra del jefe va abajo, a {hud.alturaBarraMovil} px del borde (el nombre encima, hasta {hud.alturaBarraMovil + 66f}).\n";
            t += $"   - El estado del player se aparta {hud.apartarEstadoMovil} px para dejar sitio al botón de menú.\n";

            // La barra ocupa una franja abajo: se avisa de los circulos que la pisen.
            hud.Barra(1920f, out float bx, out float bancho, out float balto);
            float btecho = balto + 66f;          // la barra (22) y el nombre encima (44)
            foreach (AjustesTactiles.Boton b in a.botones)
            {
                if (b == null || !b.activo) continue;
                if (!Abajo(b.anclaje)) continue;
                float cx = EnX(b.anclaje, b.posicion.x);
                float r = b.tamano * 0.5f;
                if (b.posicion.y - r > btecho) continue;              // por encima de la franja
                if (cx + r < bx || cx - r > bx + bancho) continue;    // a un lado
                t += $"   AVISO: el botón {b.accion} pisa la barra del jefe (sube su Y por encima de {btecho} o baja 'alturaBarraMovil').\n";
            }
            if (a.centroJoystick.y - a.tamanoBase * 0.5f < btecho && a.centroJoystick.x + a.tamanoBase * 0.5f > bx)
                t += $"   AVISO: el joystick pisa la barra del jefe (su borde de abajo está a {a.centroJoystick.y - a.tamanoBase * 0.5f} px).\n";
        }
        Debug.Log(t);
    }

    private static bool Abajo(AjustesTactiles.Esquina e) =>
        e == AjustesTactiles.Esquina.AbajoIzquierda || e == AjustesTactiles.Esquina.AbajoDerecha
        || e == AjustesTactiles.Esquina.AbajoCentro;

    // La x del centro del boton en la pantalla de referencia (1920 de ancho).
    private static float EnX(AjustesTactiles.Esquina e, float x) =>
        e == AjustesTactiles.Esquina.AbajoDerecha ? 1920f - x
        : e == AjustesTactiles.Esquina.AbajoCentro ? 960f + x : x;
}
