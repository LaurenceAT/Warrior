using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Para la linea de comandos: aplica los cambios de esta ronda sin regenerar la
// cueva (sonidos nuevos, player, nivel nevado, portales y configuraciones).
public static class ActualizarProyecto
{
    public static void Todo()
    {
        ConfigurarRecursosRPG.Configurar();
        ConfigurarPlayer.Configurar();
        CrearNivelNieve.Crear();
        Portales();
        Ajustes();
    }

    // Los numeros de la subida de nivel, en un archivo editable.
    public static void Ajustes()
    {
        const string ruta = "Assets/Resources/AjustesProgreso.asset";
        if (AssetDatabase.LoadAssetAtPath<AjustesProgreso>(ruta) != null) return;
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AjustesProgreso>(), ruta);
        AssetDatabase.SaveAssets();
    }

    // Segunda ronda: nieve lista para pintar y borrado aprobado.
    // Tercera ronda: tiles automaticos al dia y colisiones y bordes rehechos.
    // Cuarta ronda: icono de la pocion de mana y paletas de decoracion.
    // Quinta ronda: iconos, sonidos editables, menu principal y orden de niveles.
    // Sexta ronda: sonidos del menu, sangrado, iconos, cofres de mejora y
    // recompensas de los jefes.
    public static void Ronda6() => global::Ronda6.Todo();
    // Septima ronda: fondo de la nieve con profundidad y ambiente.
    public static void Ronda7() => global::Ronda7.Todo();
    // Octava ronda: estatuas con pistas, decoracion, imbuiciones, muerte en el aire y sangre.
    public static void Ronda8() => global::Ronda8.Todo();
    // Novena ronda: mejoras de frasco separadas, Desafios, totem-tienda y Logros.
    public static void Ronda9() => global::Ronda9.Todo();
    // Decima ronda: The Blind Huntress (jefe secreto de los Desafios) y su arena.
    public static void Ronda10() => global::Ronda10.Todo();
    // Undecima ronda: parry de la Cazadora, escudo a golpes y sonidos nuevos.
    public static void Ronda11() => global::Ronda11.Todo();
    // Duodecima ronda: Crimson Wraith mas fluido, poderes nuevos y resistencias.
    public static void Ronda12() => global::Ronda12.Todo();

    public static void Ronda5()
    {
        ConfigurarRecursosRPG.Configurar();
        SonidosAccionesEditor.Crear();
        CrearMenuPrincipal.Crear();
    }

    public static void Ronda4()
    {
        ConfigurarRecursosRPG.Configurar();
        PaletasDecoracion.Todo();
    }

    public static void Ronda3()
    {
        PaletaNieve.CrearPaleta();
        NivelesPintables.ArreglarColisiones();
    }

    public static void Ronda2()
    {
        PaletaNieve.Preparar();
        LimpiezaAprobada.Borrar();
    }

    public static void Portales()
    {
        var cueva = EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaCueva);
        ConfigurarPortales.Instalar(cueva);
        EditorSceneManager.MarkSceneDirty(cueva);
        EditorSceneManager.SaveScene(cueva);
        ConfigNivelEditor.Asegurar(ConfigNivelEditor.EscenaCueva, ConfigNivelEditor.RutaCueva, ConfigNivel.TipoTerreno.Piezas);
        AssetDatabase.SaveAssets();
        Debug.Log("[Actualizar] Listo.");
    }
}
