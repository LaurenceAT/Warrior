using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 17: interfaz sin solapes, deseo del totem, cofre de la Cazadora,
// objetos por aplicar en todos los niveles, hogueras y textos de muerte.
//   - Resources/AjustesInterfaz (zonas de la pantalla, latido, deseo, carga).
//   - Resources/TextosMuerte (las frases, por categorias). Si ya existe, no se pisa.
//   - Cofre de The Blind Huntress: 4500 almas, piedra, mejora de curacion,
//     mejora de recuperacion de mana, frasco de mana y frasco de sangre (solo si
//     aun tiene el cofre de antes: tus cambios se respetan).
// Warrior > Actualizar > Ronda 17 (o Ronda17.Todo por linea de comandos).
public static class Ronda17
{
    [MenuItem("Warrior/Actualizar/Ronda 17 (interfaz, cofre de la Cazadora y textos de muerte)")]
    public static void Todo()
    {
        if (AssetDatabase.LoadAssetAtPath<AjustesInterfaz>("Assets/Resources/AjustesInterfaz.asset") == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AjustesInterfaz>(), "Assets/Resources/AjustesInterfaz.asset");
        TextosMuerte();
        CofreCazadora();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda17] Listo.");
    }

    private static void CofreCazadora()
    {
        FichaJefe f = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_BlindHuntress.asset");
        if (f == null) return;
        var antes = new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre };
        if (f.almasCofre != 2500 || f.objetosCofre == null || !f.objetosCofre.SequenceEqual(antes))
        {
            Debug.Log("[Ronda17] El cofre de la Cazadora ya estaba cambiado: no se toca.");
            return;
        }
        f.almasCofre = 4500;
        f.objetosCofre = new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.LagrimaCeleste, Equipo.Objeto.FrascoMana, Equipo.Objeto.FrascoSangre };
        EditorUtility.SetDirty(f);
        Debug.Log("[Ronda17] Cofre de la Cazadora: 4500 almas y 5 objetos.");
    }

    private static List<string> L(params string[] s) => s.ToList();

    private static void TextosMuerte()
    {
        const string ruta = "Assets/Resources/TextosMuerte.asset";
        if (AssetDatabase.LoadAssetAtPath<TextosMuerte>(ruta) != null) return;
        TextosMuerte t = ScriptableObject.CreateInstance<TextosMuerte>();
        t.generales = L(
            "Otra vez muerto. Qué sorpresa tan poco sorprendente.",
            "Cada muerte enseña algo. Tú aún no has aprendido nada.",
            "Si morir fuera un deporte, serías campeón.",
            "El suelo te extrañaba.",
            "Hasta los esqueletos se están riendo.",
            "Qué espectáculo. Lástima que nadie aplauda.",
            "Te faltó poco. Bueno, te faltó mucho.",
            "Eso fue valiente. Estúpido, pero valiente.",
            "Tu espada estaba de adorno, ¿verdad?",
            "La hoguera ya ni se sorprende de verte.",
            "Esquivar también es una opción. Por si no lo sabías.",
            "Atacar sin pensar tiene un precio. Hoy lo pagaste entero.");
        t.enemigoComun = L(
            "Perdiste contra un enemigo común. Que no te lo cuenten.",
            "Era el enemigo más fácil de la zona. Era.",
            "Eso no era un jefe. Repítelo hasta creértelo.",
            "Cayó un caballero. Lo mató algo que cabe en una bota.",
            "Hasta las ratas tienen mejor técnica que tú.",
            "Ni siquiera tenía nombre propio. Y aun así ganó.");
        t.jefe = L(
            "Los jefes no necesitan esforzarse contigo.",
            "El jefe ni sudó.",
            "El jefe dice gracias por la práctica.",
            "Le diste un buen calentamiento. Él te dio un final.",
            "Cada intento tuyo es su momento favorito del día.");
        t.porJefe = new List<TextosMuerte.FrasesJefe>
        {
            new TextosMuerte.FrasesJefe { jefe = "shadowed_wetlands", frases = L(
                "La sombra ni se molestó en sacar la escarcha.",
                "Los humedales se tragan a los que dudan. Dudaste.",
                "Saltaste el barrido. El tajo alzado te esperaba arriba.",
                "Otro cuerpo para la ciénaga. Ni siquiera flota.") },
            new TextosMuerte.FrasesJefe { jefe = "crimson_wraith", frases = L(
                "El Espectro bebe tu sangre y aún tiene sed.",
                "Tu sangre ya le pertenecía. Él solo vino a cobrarla.",
                "La cueva sigue creciendo. Gracias por tu aporte.",
                "Ni viste su verdadera forma. Mejor así.") },
            new TextosMuerte.FrasesJefe { jefe = "blind_huntress", frases = L(
                "Te oyó respirar. Siempre se oye.",
                "Ella no ve. Tú tampoco, por lo visto.",
                "Corriste. Ella escuchó. Fin de la historia.",
                "Los árboles del bosque ya tienen un hueso más.") },
        };
        t.caida = L(
            "El suelo existía. Tú decidiste ignorarlo.",
            "Volar no era una opción. Ahora lo sabes.",
            "El abismo no te empujó. Fuiste tú solito.",
            "Mirar dónde pisas: ese gran misterio sin resolver.",
            "Bonito salto. Lástima que no hubiera nada al otro lado.");
        t.letal = L(
            "La señal roja estaba ahí. Hermosa, ¿no? Y no le hiciste caso.",
            "Te pidieron silencio. Ni eso.",
            "Un aviso enorme, brillante y rojo. Y aun así, sorpresa.",
            "Golpeaste a quien estaba en guardia. Brillante idea.",
            "Te avisaron. Con luces. Y te quedaste mirando.");
        t.rapida = L(
            "¿Ya? Ni siquiera calentaste.",
            "Récord personal... de lo rápido que se muere.",
            "Entraste, miraste, moriste. Muy eficiente.",
            "Eso duró menos que la pantalla de carga.");
        t.racha5 = L(
            "Quinta vez. La esperanza es lo último que se pierde, y tú vas por buen camino.",
            "Cinco muertes seguidas. Ya tienes una rutina.");
        t.racha10 = L(
            "Diez muertes. ¿Seguro que no prefieres un juego más tranquilo?",
            "Diez veces. El jefe ya te guarda sitio en la arena.");
        t.racha20 = L(
            "Llevas tantas muertes que el juego ya te conoce por tu nombre.",
            "Veinte y subiendo. Esto ya no es un desafío, es una costumbre.",
            "A estas alturas, morir es tu estilo de juego.");
        t.almas = L(
            "Todas esas almas y no pudiste con ellas. Qué desperdicio.",
            "Tus almas ahora son de otro. Cuídalas mejor, si las recuperas.",
            "Cargabas una fortuna. Ahora la carga el suelo.",
            "Tanto esfuerzo juntando almas para regalarlas así.");
        t.sangrado = L(
            "Te desangraste. Elegante forma de fracasar.",
            "Gota a gota. Y tú pusiste todas las gotas.");
        t.frio = L(
            "Te congelaste. Fría actitud, para ser sinceros.",
            "Quieto como una estatua. Y casi igual de útil.");
        t.fuego = L(
            "Fuego, hielo, sangre... y tú sigues sin esquivar.",
            "Ardiste. Al menos diste un poco de luz.");
        AssetDatabase.CreateAsset(t, ruta);
        Debug.Log($"[Ronda17] Textos de muerte: {t.Total} frases.");
    }
}
