using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 24: crea la ficha Resources/GuiaControles ("Controles y guia" y la guia
// del primer desafio) con las categorias, tarjetas, textos e imagenes aprobados.
//   Menu Warrior > Controles y guia > Crear ficha    (no pisa una ficha que ya exista)
//   Menu Warrior > Controles y guia > Rehacer ficha  (la vuelve a crear: se pierde lo editado)
//   Linea de comandos: -executeMethod Ronda24.Crear
public static class Ronda24
{
    private const string Ruta = "Assets/Resources/GuiaControles.asset";
    private const string K = "Assets/Sprites/PJ_Knight/1 - Adventurer 1.5 (RECOMENDADA)/";
    private const string O = "Assets/Sprites/PJ_Knight/2 - Otras versiones/Combate cuerpo a cuerpo/";
    private const string Torre = "Assets/SPRITES PARA NUEVOS NIVELES/HOGUERA O CHECKPOINT/Checkpoint_TorreLunaSangre/RedMoonTower_free_idle_animation..png";

    [MenuItem("Warrior/Controles y guia/Crear ficha")]
    public static void Crear()
    {
        if (AssetDatabase.LoadAssetAtPath<GuiaControles>(Ruta) != null)
        {
            Debug.Log("[Ronda24] La ficha ya existe: no se toca (" + Ruta + ").");
            Salir();
            return;
        }
        Hacer();
    }

    [MenuItem("Warrior/Controles y guia/Rehacer ficha")]
    public static void Rehacer()
    {
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rehacer ficha", "Se perderá lo editado a mano en GuiaControles. ¿Seguir?", "Rehacer", "Cancelar")) return;
        AssetDatabase.DeleteAsset(Ruta);
        Hacer();
    }

    private static void Salir()
    {
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void Hacer()
    {
        GuiaControles g = ScriptableObject.CreateInstance<GuiaControles>();
        g.nombresTeclas = new[]
        {
            N("Space", "Espacio"), N("Shift", "Mayús"), N("Left Shift", "Mayús"), N("Right Shift", "Mayús"),
            N("Left Button", "clic izquierdo"), N("Right Button", "clic derecho"), N("Escape", "Esc"),
        };
        RecursosRPG r = RecursosRPG.Get();
        Color apagada = new Color(0.65f, 0.6f, 0.65f, 1f), encendida = new Color(1f, 0.82f, 0.82f, 1f);
        Sprite torre = AssetDatabase.LoadAllAssetsAtPath(Torre).OfType<Sprite>().OrderBy(s => s.name, new OrdenNatural()).FirstOrDefault();

        GuiaControles.Tarjeta I(string id, string titulo, string texto, params string[] iconos) =>
            T(id, titulo, texto, false, iconos.Select(c => Q(r.Icono(c))).ToArray());

        g.categorias = new[]
        {
            Cat("Moverse",
                T("caminar", "Caminar", "Pulsa {Move/left} o {Move/right} para moverte a la izquierda o a la derecha.", true,
                  S(K + "PlayerRun/PlayerRun_00.png"), S(K + "PlayerRun/PlayerRun_02.png"), S(K + "PlayerRun/PlayerRun_04.png")),
                T("saltar", "Saltar", "Pulsa {Jump} para saltar.", true,
                  S(K + "PlayerJump/PlayerJump_00.png"), S(K + "PlayerJump/PlayerJump_01.png"), S(K + "PlayerJump/PlayerJump_02.png")),
                T("correr", "Correr", "Mantén {Sprint} mientras te mueves para correr. Correr gasta estamina.", true,
                  S(O + "Correr2/Correr2_00.png"), S(O + "Correr2/Correr2_02.png"), S(O + "Correr2/Correr2_04.png")),
                T("doble_salto", "Doble salto", "Pulsa {Jump} otra vez en el aire para saltar de nuevo. Se recarga al tocar el suelo o una pared.", false,
                  S(K + "Acciones nuevas/Voltereta/Voltereta_00.png"), S(K + "Acciones nuevas/Voltereta/Voltereta_01.png"), S(K + "Acciones nuevas/Voltereta/Voltereta_02.png")),
                T("pared", "Pared", "En el aire, pégate a una pared para deslizarte. Pulsa {Move/down} para bajar más rápido y {Jump} para saltar desde la pared.", false,
                  S(K + "PlayerWallSlide/PlayerWallSlide_00.png"), S(K + "PlayerWallSlide/PlayerWallSlide_01.png")),
                T("correr_pared", "Correr por la pared", "Mantén {Sprint} contra una pared para subir un tramo corriendo. Gasta estamina.", false,
                  S(O + "CorrerPorPared/CorrerPorPared_00.png"), S(O + "CorrerPorPared/CorrerPorPared_02.png"), S(O + "CorrerPorPared/CorrerPorPared_04.png")),
                T("cornisa", "Agarre de cornisa", "Salta hacia un borde alto: el personaje se agarra y sube solo.", false,
                  S(K + "Acciones nuevas/Cornisa-Agarrar/Cornisa-Agarrar_00.png"), S(K + "Acciones nuevas/Cornisa-Trepar/Cornisa-Trepar_01.png"), S(K + "Acciones nuevas/Cornisa-Trepar/Cornisa-Trepar_03.png"))),
            Cat("Combate",
                T("atacar", "Atacar", "Para realizar los ataques, presiona el {Attack}. Esto también funciona si estás en el aire. Cada golpe gasta estamina.", true,
                  S(K + "PlayerAttack/PlayerAttack_00.png"), S(K + "PlayerAttack/PlayerAttack_02.png"), S(K + "PlayerAttack/PlayerAttack_04.png")),
                T("ataque_aereo", "Ataque aéreo", "Pulsa {Attack} en el aire. Si aciertas, te quedas suspendido un momento y puedes seguir golpeando.", false,
                  S(K + "Acciones nuevas/AtaqueAereo1/AtaqueAereo1_00.png"), S(K + "Acciones nuevas/AtaqueAereo1/AtaqueAereo1_01.png"), S(K + "Acciones nuevas/AtaqueAereo1/AtaqueAereo1_02.png")),
                T("lanzador", "Lanzador", "Mantén {Move/up} y pulsa {Attack} para lanzar al enemigo hacia arriba.", false,
                  S(K + "PlayerAttack/PlayerAttack_02.png"), S(K + "PlayerAttack/PlayerAttack_03.png"), S(K + "PlayerAttack/PlayerAttack_04.png")),
                T("estocada", "Estocada hacia abajo", "En el aire, mantén {Move/down} y pulsa {Attack}. Caes con fuerza y rebotas en los enemigos. Gasta estamina.", false,
                  S(K + "Acciones nuevas/AtaqueAereo3-Inicio/AtaqueAereo3-Inicio_00.png"), S(K + "Acciones nuevas/AtaqueAereo3-Bucle/AtaqueAereo3-Bucle_00.png"), S(K + "Acciones nuevas/AtaqueAereo3-Final/AtaqueAereo3-Final_01.png")),
                T("bloquear", "Bloquear y parry", "Mantén {Kick} para bloquear: recibes menos daño a cambio de estamina. Pulsa justo antes del golpe para hacer parry y aturdir al enemigo.", true,
                  S(K + "Acciones nuevas/Attack2/Attack2_00.png"), S(K + "Acciones nuevas/Attack2/Attack2_01.png"), S(K + "Acciones nuevas/Attack2/Attack2_02.png")),
                T("esquivar", "Esquivar", "Pulsa {Dodge} para hacer un barrido. Durante un instante nada te hace daño. Gasta estamina.", true,
                  S(K + "Acciones nuevas/Barrido/Barrido_00.png"), S(K + "Acciones nuevas/Barrido/Barrido_01.png"))),
            Cat("Frascos y objetos",
                Con(T("frasco_sangre", "Frasco de sangre", "Pulsa [Q] para beber y curarte. Sus cargas se rellenan al descansar en la hoguera.", true,
                  S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_00.png"), S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_01.png"), S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_02.png")), r.Icono("pocion")),
                Con(T("frasco_mana", "Frasco de maná", "Pulsa [R] para recuperar maná. El maná también se llena al descansar en la hoguera.", true,
                  S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_00.png"), S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_01.png"), S(K + "Acciones nuevas/UsarObjeto/UsarObjeto_02.png")), r.Icono("pocion_mana")),
                Tipo(T("imbuiciones", "Imbuiciones", "Para elegir una imbuición, pulsa la tecla {ToggleWeapon}. Cada imbuición gasta maná y algunas gastan otra cosa.", true), GuiaControles.TipoImagen.Rueda),
                Tipo(T("usar_rueda", "Usar la rueda", "Elige con el ratón, con [W][A][S][D] o con [1] a [5]. Confirma con [clic izquierdo] o [E]. Cancela con [clic derecho] o [Esc].", false), GuiaControles.TipoImagen.Rueda),
                I("piedra", "Piedra de forja", "Mejora la espada. Se usa en la hoguera, en \"Mejorar equipamiento\".", "objeto_piedra"),
                I("lagrimas", "Lágrimas", "La Lágrima carmesí mejora el frasco de sangre y la Lágrima celeste, el de maná. Se usan en la hoguera.", "objeto_lagrima_curacion", "objeto_lagrima_mana"),
                I("cofres", "Cofres con brillo", "Los cofres con un brillo dorado suave guardan objetos de mejora.", "cofre_especial")),
            Cat("Interactuar",
                T("interactuar", "Interactuar", "Pulsa [F] cerca de hogueras, cofres, estatuas, antorchas y el tótem.", true,
                  new GuiaControles.Cuadro { sprite = torre, color = apagada }, new GuiaControles.Cuadro { sprite = torre, color = encendida }),
                I("menu_hoguera", "Menú de la hoguera", "Pulsa [F] junto a una hoguera. Puedes descansar (te cura, rellena los frascos y guarda), subir de nivel y mejorar el equipo.", "ctrl_hoguera")),
            Cat("Progreso",
                I("almas", "Almas", "Los enemigos sueltan almas al morir. Se gastan en la hoguera para subir de nivel.", "almas"),
                I("al_morir", "Al morir", "Tus almas se quedan donde caíste. Recógelas antes de volver a morir o se pierden.", "ctrl_mancha"),
                I("resistencias", "Resistencias", "La resistencia a golpes reduce el daño físico y la resistencia a hechizos, el mágico. Las dos tienen un tope.", "stat_resgolpes", "stat_reshechizos")),
            Cat("Barras y estados",
                I("vida", "Vida", "La barra roja es tu vida. Se alarga al subir su estadística.", "ctrl_vida"),
                I("estamina", "Estamina", "La barra verde es tu estamina. La gastan correr, atacar, esquivar y bloquear, y se recupera sola.", "stat_estamina"),
                I("mana", "Maná", "La barra azul es tu maná. Lo gastan las imbuiciones.", "stat_mana"),
                Tipo(T("anillo", "Anillo de imbuición", "Con la espada imbuida, el anillo se vacía según el tiempo que le queda.", false), GuiaControles.TipoImagen.Anillo),
                I("tus_estados", "Tus estados", "Algunos ataques de los jefes llenan una barra de sangrado, congelación o quemadura. Si se llena, te afecta. Si dejas de recibirlos, baja sola.", "jugador_sangrado"),
                I("quemado", "Quemado", "El enemigo pierde vida durante un rato.", "estado_quemado"),
                I("lento", "Lento", "El enemigo se mueve más despacio. Si lo golpeas varias veces seguidas, se congela.", "estado_lento"),
                I("aturdido", "Aturdido", "A veces el enemigo se queda quieto un momento.", "estado_aturdido"),
                I("sangrado", "Sangrado", "Cada golpe acumula sangrado. Al llenarse, el enemigo pierde de golpe parte de su vida.", "estado_sangrado"),
                I("drenado", "Drenado", "Te curas con parte del daño que haces.", "estado_drenado"),
                I("estados_enemigo", "Estados del enemigo", "Los iconos bajo su barra de vida muestran sus estados. Parpadean cuando están por acabar.", "ctrl_estados")),
            Cat("Sistema",
                Teclas(T("pausa", "Pausa", "Pulsa [Esc] o [Tab] para abrir la pausa. Desde ahí entras en \"Controles y guía\".", true), "Esc", "Tab")),
        };

        AssetDatabase.CreateAsset(g, Ruta);
        AssetDatabase.SaveAssets();
        // Aviso de lo que falte (sprites que no se encuentren).
        int sinImagen = g.Todas().Count(t => t.tipo == GuiaControles.TipoImagen.Cuadros && (t.cuadros.Length == 0 || t.cuadros.Any(c => c.sprite == null)));
        Debug.Log($"[Ronda24] Ficha creada: {g.Todas().Count()} tarjetas, {g.Esenciales().Count} esenciales, {sinImagen} sin imagen completa.");
        Salir();
    }

    private static GuiaControles.NombreTecla N(string a, string b) => new GuiaControles.NombreTecla { original = a, espanol = b };
    private static GuiaControles.Categoria Cat(string n, params GuiaControles.Tarjeta[] t) => new GuiaControles.Categoria { nombre = n, tarjetas = t };
    private static GuiaControles.Cuadro Q(Sprite s) => new GuiaControles.Cuadro { sprite = s };

    private static GuiaControles.Cuadro S(string ruta)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ruta) ?? AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
        if (s == null) Debug.LogWarning("[Ronda24] No se encuentra el sprite " + ruta);
        return new GuiaControles.Cuadro { sprite = s };
    }

    private static GuiaControles.Tarjeta T(string id, string titulo, string texto, bool esencial, params GuiaControles.Cuadro[] cuadros) =>
        new GuiaControles.Tarjeta { id = id, titulo = titulo, texto = texto, esencial = esencial, tipo = GuiaControles.TipoImagen.Cuadros, cuadros = cuadros };

    private static GuiaControles.Tarjeta Con(GuiaControles.Tarjeta t, Sprite icono) { t.icono = icono; return t; }
    private static GuiaControles.Tarjeta Tipo(GuiaControles.Tarjeta t, GuiaControles.TipoImagen tipo) { t.tipo = tipo; return t; }
    private static GuiaControles.Tarjeta Teclas(GuiaControles.Tarjeta t, params string[] teclas) { t.tipo = GuiaControles.TipoImagen.Teclas; t.teclas = teclas; return t; }

    // "idle_2" antes que "idle_10".
    private class OrdenNatural : IComparer<string>
    {
        public int Compare(string a, string b) => EditorUtility.NaturalCompare(a, b);
    }
}
