using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 21: textos con voz propia. Escribe los textos elegidos en las fichas
// (todo queda editable despues en el Inspector):
//   - Resources/PistasEstatuas (nueva): las 14 estatuas, con dos lecturas y su voz.
//   - Resources/PistasSellos: los mensajes del sello de ensenanza.
//   - Assets/Data/Jefes/Ajustes Cazadora: dialogo burlon, reintentos por causa,
//     "casi vencida" y reacciones durante el combate.
//   - Resources/TextosMuerte, Resources/Bestiario y la ficha de la Cazadora (Bitacora).
// OJO: al ejecutarlo se pisan esos textos con los de aqui. Para retocar un
// texto, editalo en el Inspector y no vuelvas a ejecutar esto.
public static class Ronda21
{
    [MenuItem("Warrior/Actualizar/Ronda 21 (textos: estatuas, Cazadora, muerte, Bestiario)")]
    public static void Aplicar()
    {
        Estatuas();
        Sellos();
        Cazadora();
        Muerte();
        Bestiario();
        BitacoraCazadora();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda21] Textos aplicados.");
    }

    // ------------------------------------------------------------------ Estatuas

    private static PistasEstatuas.Pista P(string escena, string id, string voz, string titulo, string primera, string segunda) =>
        new PistasEstatuas.Pista { escena = escena, id = id, voz = voz, titulo = titulo, primera = primera, segunda = segunda };

    private const string Nieve = "Nivel Nieve", Cueva = "Nivel Cueva";

    private static void Estatuas()
    {
        const string ruta = "Assets/Resources/PistasEstatuas.asset";
        PistasEstatuas f = AssetDatabase.LoadAssetAtPath<PistasEstatuas>(ruta);
        if (f == null)
        {
            f = ScriptableObject.CreateInstance<PistasEstatuas>();
            AssetDatabase.CreateAsset(f, ruta);
        }
        f.pistas = new List<PistasEstatuas.Pista>
        {
            // Tutoriales: claros, dicen como funciona.
            P(Nieve, "estatua_1", "Un instructor viejo (tutorial)", "La espada clavada",
                "Una hoja desnuda corta poco aquí arriba. Lejos de la hoguera, pulsa E y elige con qué vestirla. Cada bestia de estas cumbres teme algo distinto.",
                "Te lo repito: la E abre la rueda lejos de la hoguera. Cada elemento cuesta maná, y la sangre se cobra en vida. No cambies por capricho."),
            P(Nieve, "estatua_claro", "Un peregrino (tutorial)", "La sombra del claro",
                "Los peregrinos dejaban sellos para guardar sus caminos. El color del sello y la runa grabada al lado dicen qué elemento lo abre. Dale a tu hoja ese mismo color y golpea.",
                "Compara el brillo del sello con los colores de tu rueda. Solo uno coincide."),
            P(Nieve, "estatua_2", "Un mensajero con prisa (tutorial)", "La ladera helada",
                "¡Ojo con la ladera! El hielo brilla como un espejo y, si corres, no frenas cuando quieres.",
                "Repito: en el hielo frenas tarde. Afloja antes, no encima del precipicio."),
            P(Nieve, "estatua_3", "Un pastor de las cumbres (tutorial)", "La ventisca",
                "Más allá el viento no descansa. Cuando la nieve se espesa, viene la ráfaga: espera, y avanza cuando afloje.",
                "Avanza a tramos cortos. Cuando la nieve se espesa, para: el empujón llega justo después."),

            // Secretos y caminos.
            P(Nieve, "estatua_ermitano", "Un ermitaño que cuenta inviernos", "El ermitaño",
                "Conté los inviernos en esta roca. El viento del paso tiene un enemigo: las piedras altas. Detrás de ellas, hasta la nieve cae despacio.",
                "Las piedras altas del paso son refugio. Espera a su sombra a que pase la ráfaga."),
            P(Nieve, "estatua_picadores", "Un picador cansado", "Los picadores de hielo",
                "Cavamos bajo la orilla para volver a la lumbre sin dar la vuelta. Cuando se hundió el túmulo, tapamos la boca con piedras. Por dentro, claro.",
                "Ese montón de piedras junto a la orilla esconde un atajo. Se abre desde el otro lado."),
            P(Nieve, "estatua_cripta", "Un guardián que se apaga", "La cripta de los guardianes",
                "Guardianes. Bajo cada bulto de nieve... uno. Se levantan si pasas cerca. Lo que custodian... más adentro.",
                "Los montículos de nieve no son nieve... Acércate con la espada lista. Lo valioso está al final."),

            // Sellos: misteriosos, pero se deducen por el color, la runa y el lugar.
            P(Nieve, "estatua_vigias", "Un vigía que ya no ve bien", "Los vigías",
                "Desde aquí se vigilaba el paso. Lo valioso quedaba en lo alto, tras un hielo que no rompía ningún golpe. Solo el calor de las brasas lo hacía llorar.",
                "Arriba hay un bloque de hielo con resplandor de brasa. Tu hoja tendrá que arder como él."),
            P(Nieve, "estatua_6", "Una voz infantil grabada en la roca", "La grieta sombría",
                "¡Otra sombra que respira! Es como la del claro de la ladera. Esa se fue cuando alguien trajo luz.",
                "Si abriste la grieta del claro, ya sabes la respuesta. Las dos brillan con el mismo oro."),
            P(Nieve, "estatua_4", "Una pescadora", "El lago helado",
                "El agua tiembla, como si quisiera dormirse y no pudiera. Una caricia fría bastaría para que se quedara quieta, y entonces se podría caminar encima.",
                "El agua brilla azul pálido, el color de la escarcha. Golpéala con una hoja igual de fría y podrás cruzar."),
            P(Nieve, "estatua_5", "Un herrero", "El muro de hielo",
                "Ningún martillo dobla este hielo. Pero mira la roca: está negra, chamuscada. Alguien ya pasó, y no lo hizo a golpes.",
                "El muro brilla anaranjado, como metal al rojo. Lleva esa misma brasa en tu hoja."),

            // Antes de un jefe: solo avisan del peligro.
            P(Nieve, "estatua_7", "Un caballero caído", "El guerrero congelado",
                "No pasé de aquí. Lo que vive tras esa niebla pelea mejor que yo. Que tu hoguera esté cerca.",
                "Al otro lado no hay salida hasta que uno de los dos caiga. Elige bien cuándo entrar."),
            P(Cueva, "estatua_cueva_2", "Alguien que cayó", "Marcas de uñas",
                "No pises... lo que cruje. No te fíes de la roca rajada. Y si una pared suena distinta... quizá se pueda atravesar.",
                "Suelo que cruje: no te detengas. Pared que suena hueca: prueba a cruzarla."),
            P(Cueva, "estatua_1", "Un minero que huyó", "Arañazos en la roca",
                "Excavamos demasiado hondo. Algo despertó ahí abajo. Yo corrí. Los demás no.",
                "El fondo de la cueva guarda algo peligroso. Prepárate antes de entrar: después no podrás salir."),
        };
        EditorUtility.SetDirty(f);
    }

    // ------------------------------------------------------------------ Sellos

    private static void Sellos()
    {
        PistasSellos s = Resources.Load<PistasSellos>("PistasSellos");
        if (s == null) return;
        foreach (PistasSellos.Entrada e in s.entradas)
        {
            switch (e.elemento)
            {
                case Elemento.Sagrado:
                    e.textoPista = "La sombra se traga el golpe. Hace falta algo que brille.";
                    e.textoAbrir = "La sombra se deshace en la luz.";
                    break;
                case Elemento.Fuego:
                    e.textoPista = "El hielo ni se inmuta. Necesita calor, no fuerza.";
                    e.textoAbrir = "El hielo cede y se derrite.";
                    break;
                case Elemento.Hielo:
                    e.textoPista = "El agua tiembla... le falta poco frío para quedarse quieta.";
                    e.textoAbrir = "El agua se detiene, helada.";
                    break;
            }
        }
        EditorUtility.SetDirty(s);
    }

    // ------------------------------------------------------------------ Cazadora

    private static AjustesCazadora.ReintentoCausa C(string nombre, string[] ataques, bool estado, params string[] frases) =>
        new AjustesCazadora.ReintentoCausa { nombre = nombre, ataques = ataques, estadoAlterado = estado, frases = frases };

    private static void Cazadora()
    {
        AjustesCazadora a = AssetDatabase.LoadAssetAtPath<AjustesCazadora>("Assets/Data/Jefes/Ajustes Cazadora.asset");
        if (a == null) { Debug.LogError("[Ronda21] No se encuentra Ajustes Cazadora."); return; }

        a.frasesEntrada = new[]
        {
            "Otra persona que se atreve a perturbar mi tranquilidad...",
            "Espero que no vengas a interrumpir mi siesta. Me costó mucho que el bosque se callara.",
            "Así que dime, pequeño... ¿qué vas a hacer ahora?",
        };
        a.respuestaRetirarse = new[] { "Jaja... ¿en serio creíste que dejaría escapar a una presa?", "Qué tierno." };
        a.respuestaDesafiar = new[] { "Espero que dures más de lo que aparentas.", "Aunque, por cómo respiras... no mucho." };
        a.trasParryDialogo = "...Vaya. Tienes buen oído, pequeño.";
        a.finBarra1 = "¿Eso fue todo? Esperaba algo más ruidoso.";
        a.finBarra2 = "No. No. Aún no. Apaguemos la luz, ¿te parece?";
        a.muerteFinal = "...Bah. Por fin... un poco de silencio.";
        a.frasesCasiVencida = new[]
        {
            "Espera, espera... ¿de verdad me estás ganando?",
            "Mmm. Esto no estaba en mis planes.",
            "Vale. Lo admito. Eres un poco molesto.",
        };

        a.reintentoGeneral = new[]
        {
            "¿Otra vez tú? Reconozco esos pasos torpes.",
            "Has vuelto. Empiezo a pensar que te gusto.",
            "Bienvenido de nuevo. La hoguera ya debe de odiarte.",
        };
        a.reintentoPorCausa = new[]
        {
            C("Estocada tras su guardia", new[] { "guardia" }, false,
                "Uno, dos, tres golpes contra mi guardia... ¿no se te ocurrió parar?",
                "Golpeaste mi guardia como quien llama a la puerta. Y yo abrí.",
                "Te encanta pegarle a mi espada. A ella no tanto."),
            C("Instakill con aviso rojo", new[] { "ejecucion", "sentencia" }, false,
                "Brillaba en rojo, cariño. Hasta yo lo sé, y estoy ciega.",
                "¿Viste la línea roja? Ah, perdona. Tú sí podías verla.",
                "Te avisé con un sonido precioso, y te quedaste ahí, escuchando."),
            C("El Silencio", new[] { "silencio" }, false,
                "Te pedí silencio. Uno solo. Y lo rompiste.",
                "Shhh... ¿lo oyes? Era tu última respiración.",
                "En el silencio, hasta un suspiro grita. El tuyo, sobre todo."),
            C("Ilusiones", new[] { "flanqueo", "caen", "espejismo", "espejos", "cruceDoble" }, false,
                "¿Cuál era la de verdad? Ni yo me acuerdo ya.",
                "Le pegaste a mi copia. Te manda saludos.",
                "Peleaste contra un reflejo. Muy valiente. Contra el reflejo."),
            C("Olas y trampas", new[] { "ola", "lluvia", "trampas" }, false,
                "Saltar no es tan difícil. Te lo dice alguien que no ve.",
                "Pisaste justo donde dejé mi trampita. Gracias.",
                "La ola iba por el suelo. Tú también, al final."),
            C("Cuerpo a cuerpo", new[] { "tresLunas", "cruce", "ascendente", "salto", "danza", "fantasma", "tajo_dialogo", "contra", "escudo" }, false,
                "Bailas fatal. Pero gracias por la pieza.",
                "Te pusiste tan cerca que te oí pensar. Mal.",
                "Te quedaste a mi alcance. Qué detalle."),
            C("Estado alterado", new string[0], true,
                "Moriste a cachitos. Qué falta de prisa.",
                "No te maté yo. Te mató esa herida tan pesada.",
                "Te fuiste apagando solito. Yo solo escuchaba."),
        };
        a.reintentoRacha = new[]
        {
            "Si quieres, te bajo la dificultad. Ah, no. No puedo.",
            "¿Otra vez cargando? Ya me sé tu partida de memoria.",
            "Llevas tantas muertes que deberías pagarme alquiler.",
        };

        a.reaccionSangrado = new[]
        {
            "¿Otra vez sangrado? Parece tu imbuición favorita... pero no te va a servir.",
            "Huele a sangre otra vez. ¿Sabes hacer algo más?",
            "Sangre, sangre, sangre... qué monótono.",
        };
        a.reaccionHielo = new[]
        {
            "Jaja. ¿De verdad crees que ralentizándome vas a bloquear mejor mis ataques?",
            "Frío. Qué original. Me tiemblan... de la risa.",
            "¿Hielo? Llevo años en este bosque helado, pequeño.",
        };
        a.reaccionFuego = new[]
        {
            "¿Fuego? ¿En un bosque seco? Qué gran idea.",
            "Calentito. Sigue, que tenía frío.",
            "Huele a chamuscado. Tus cejas, supongo.",
        };
        a.reaccionOscuro = new[]
        {
            "¿Oscuridad? ¿Contra una ciega? Precioso.",
            "Me ofreces oscuridad a mí. Qué amable.",
            "Vivo a oscuras. Esto es como volver a casa.",
        };
        a.reaccionSagrado = new[]
        {
            "Luz. Qué bonita debe de ser. Yo no la veo.",
            "Sagrado... huele a templo. Y a miedo.",
            "¿Me vas a bendecir hasta la muerte?",
        };
        a.reaccionBloqueo = new[]
        {
            "¿Vas a esconderte detrás de eso toda la noche?",
            "Toc, toc. ¿Hay alguien detrás de esa espada?",
            "Bloquear no es pelear, pequeño. Es esperar.",
        };
        a.reaccionCorrer = new[]
        {
            "Corres como un caballo con campanas.",
            "Te oigo llegar desde el otro lado del bosque.",
            "Tanta prisa... ¿tienes algún sitio mejor donde estar?",
        };
        a.reaccionFrasco = new[]
        {
            "Glup, glup. ¿Mejor?",
            "Ese frasco suena casi vacío.",
            "Bebe tranquilo. Te espero.",
        };
        a.reaccionQuieto = new[]
        {
            "¿Te escondes? Respiras como un fuelle.",
            "Muy quieto. Muy valiente. Muy aburrido.",
            "El silencio te queda bien. Lástima que tu corazón no se calle.",
        };
        EditorUtility.SetDirty(a);
    }

    // ------------------------------------------------------------------ Pantalla de muerte

    private static List<string> L(params string[] s) => s.ToList();

    private static void Muerte()
    {
        TextosMuerte t = Resources.Load<TextosMuerte>("TextosMuerte");
        if (t == null) return;
        t.generales = L(
            "Otra vez al suelo. El suelo, al menos, es constante.",
            "Atacaste primero. Pensaste después. Ese orden sale caro.",
            "La hoguera te esperaba. No tan pronto, pero te esperaba.",
            "Hoy no. Mañana, quizá. O dentro de cinco minutos.",
            "Caer también es una forma de aprender. Lenta, pero lo es.",
            "La espada seguía en tu mano. Eso cuenta para algo.",
            "Ni la nieve se sorprende ya.",
            "Faltó poco. O faltó bastante. Desde aquí no se ve bien.",
            "Un mal paso. Solo hace falta uno.",
            "Vuelta a empezar. El camino sigue donde lo dejaste.");
        t.enemigoComun = L(
            "No era la gran amenaza del camino. Bastó igual.",
            "Subestimar cuesta lo mismo que cualquier otro error.",
            "No tenía nombre propio. Tampoco le hizo falta.",
            "Lo pequeño también muerde.",
            "No saldrá en ninguna leyenda. Aun así, ganó.",
            "Lo viste venir. Llegó de todos modos.");
        t.jefe = L(
            "Sigue ahí, esperando. No tiene otra cosa que hacer.",
            "Cada caída te enseña uno de sus pasos.",
            "Ya conoces un poco más su ritmo. A tu costa.",
            "La arena recuerda cada intento. Este también.",
            "Esta vez aguantaste un poco más. ¿O no?");
        t.porJefe = new List<TextosMuerte.FrasesJefe>
        {
            new TextosMuerte.FrasesJefe { jefe = "shadowed_wetlands", frases = L(
                "La ciénaga no tiene prisa. Tú sí la tenías.",
                "El agua negra se cierra igual sobre todos.",
                "La sombra golpeó dos veces. Tú contaste una.",
                "Otro abrigo para la escarcha de los humedales.") },
            new TextosMuerte.FrasesJefe { jefe = "crimson_wraith", frases = L(
                "La cueva bebió un poco más.",
                "Tu sangre ya corría hacia él antes de que entraras.",
                "Algo ahí abajo está un poco más contento.",
                "Lo rojo del suelo no era todo suyo.") },
            new TextosMuerte.FrasesJefe { jefe = "blind_huntress", frases = L(
                "Ella no necesitó verte.",
                "En el bosque, hasta el silencio tiene oídos.",
                "Un sonido de más. Eso fue todo.",
                "Los árboles sin hojas lo escucharon todo.") },
        };
        t.caida = L(
            "El borde estaba ahí desde el principio.",
            "Saltaste con ganas. Faltó suelo.",
            "El vacío no empuja. Solo espera.",
            "Un paso más largo de lo que daba el camino.",
            "Abajo no había nada. Ahora ya lo sabes.");
        t.letal = L(
            "El rojo nunca es un adorno.",
            "Golpeaste una defensa que esperaba justo eso.",
            "Hubo aviso. Uno bien grande.",
            "Hay golpes que se aguantan y golpes que no. Este era de los segundos.",
            "La señal llegó a tiempo. Tú, no tanto.");
        t.rapida = L(
            "Ni siquiera llegaste a entrar en calor.",
            "Rápido. Demasiado rápido.",
            "Unos pasos, y otra vez la hoguera.",
            "Eso no fue una pelea. Fue un saludo.");
        t.racha5 = L(
            "Cinco. La hoguera ya conoce tu cara.",
            "Cinco seguidas. La terquedad es un buen comienzo.");
        t.racha10 = L(
            "Diez veces. La terquedad también es una virtud.",
            "Diez. A estas alturas conoces cada piedra del camino.");
        t.racha20 = L(
            "Veinte veces. Ni la montaña es tan terca.",
            "Veinte. Esto ya es una conversación larga con la muerte.",
            "Has caído veinte veces. Te has levantado veinte.");
        t.almas = L(
            "Una fortuna tirada en el suelo. Todavía es tuya, si llegas.",
            "Llevabas demasiado encima. Aún puedes recuperarlo.",
            "Tus almas te esperan donde caíste. Sin moverse, por suerte.",
            "Mucho que perder. Y lo perdiste. Por ahora.");
        t.sangrado = L(
            "Gota a gota, sin que nadie más te tocara.",
            "Una herida pequeña con mucha paciencia.",
            "Algo seguía sangrando cuando te creías a salvo.");
        t.frio = L(
            "Te quedaste quieto. El frío no.",
            "La escarcha subió despacio. No la sentiste llegar.",
            "Al final, ni los dedos respondían.");
        t.fuego = L(
            "Ardiste. La nieve, alrededor, ni se enteró.",
            "Las llamas no te soltaron.",
            "Te quemó despacio, y tú seguías peleando.");
        EditorUtility.SetDirty(t);
    }

    // ------------------------------------------------------------------ Bestiario

    private static void Bestiario()
    {
        FichaBestiario b = Resources.Load<FichaBestiario>("Bestiario");
        if (b == null) return;
        var textos = new Dictionary<string, (string d, string p)>
        {
            ["RataEscarcha"] = ("Ratas que el frío no mató: les heló la sangre y las dejó con hambre. Nunca van solas. La primera que te ve chilla, y las demás acuden.", null),
            ["OjoVigia"] = ("Un ojo sin párpado que flota sobre el paso nevado. Sigue vigilando para alguien que dejó de mirar hace mucho.", null),
            ["HechiceroSombrio"] = ("Buscó en la sombra algo que lo protegiera del frío. La sombra lo encontró primero. Es lento y pesado, y cuando ataca nada lo detiene.", null),
            ["Mimic"] = (null, "Disfrazado no se mueve. Se abre al acercarte o al golpearlo.\nMordisco: se alza con la boca abierta (aviso largo) y se lanza con un saltito. Duele de verdad.\nLatigazo: echa el cuerpo atrás (aviso corto) y barre hacia delante con más alcance. La media distancia es justo donde llega."),
            ["RataCeniza"] = ("Las que bajaron a la cueva buscando calor, y lo encontraron. Ahora algo les arde por dentro y no deja de arder.", null),
            ["MurcielagoCripta"] = ("Nunca han visto el cielo. Nacen, cazan y mueren en el fondo de la cueva, sin echar de menos la luz.", null),
            ["ArqueraSangre"] = ("Otra centinela que bajó a la cueva. La sangre que empapa la roca le tiñó el arco, y algo más: ya casi no le queda sangre propia.", null),
            ["EliteMadreCarmesi"] = ("Dicen que de su boca nacieron todos los cacodemonios de la cueva. Lo que la engendró sigue ardiendo dentro de ella.", null),
            ["SlimeAbisal"] = ("Más hondo, donde la cueva ya no tiene nombre, los slimes se vuelven oscuros y espesos, como si se hubieran tragado la noche.", null),
        };
        foreach (FichaBestiario.Entrada e in b.entradas)
        {
            if (e == null || e.enemigo == null || !textos.TryGetValue(e.enemigo.name, out var t)) continue;
            if (t.d != null) e.descripcion = t.d;
            if (t.p != null) e.patrones = t.p;
        }
        EditorUtility.SetDirty(b);
    }

    // ------------------------------------------------------------------ Bitacora de la Cazadora

    private static void BitacoraCazadora()
    {
        FichaJefe f = Resources.Load<FichaJefe>("Desafios/Jefe_BlindHuntress");
        if (f == null) return;
        var frag = new Dictionary<string, string>
        {
            ["h1"] = "Nadie recuerda cuándo se quedó ciega. Ella dice que fue el día que se cansó de ver caras tan feas.",
            ["h2"] = "Lo que sí se sabe es que el bosque murió primero: las hojas cayeron una noche y no volvieron a brotar. Ella se quedó, porque alguien tenía que aguantar tanto silencio.",
            ["h3"] = "Aprendió a cazar con los oídos, a leer el miedo en un latido y a distinguir el paso de un ladrón del de un hijo que vuelve a casa. Muchos vinieron a matar a la cazadora ciega. Ninguno la sorprendió, y de todos se acuerda por cómo sonaban al caer.",
            ["h4"] = "En el bosque sin hojas todo se oye. Ella escucha desde hace tanto que se ríe antes de que llegues: ya sabe cómo vas a caer.",
        };
        foreach (FichaJefe.Fragmento x in f.fragmentosHistoria)
            if (x != null && frag.TryGetValue(x.id, out string tx)) x.texto = tx;
        f.historia = string.Join(" ", new[] { "h1", "h2", "h3", "h4" }.Select(k => frag[k]));

        var fases = new Dictionary<int, string>
        {
            [1] = "Es ciega: de lejos ataca a donde oyó tu último ruido (atacar, rodar, aterrizar, beber, imbuir, correr). Si falla, se queda confundida un momento; de cerca te siente siempre. Tiene súper armadura: solo el parry, lo sagrado o romperle el escudo la interrumpen.",
            [2] = "Su espada toma fuego, hielo, luz y sangre, copia tu elemento para resistirlo un rato y se rodea de ilusiones que golpean igual que ella.",
            [3] = "Apaga la luz y solo ves lo que tienes alrededor. Aquí llegan sus cacerías largas: la Danza de la Cacería y la Cacería de Espejos.",
        };
        foreach (FichaJefe.FaseInfo x in f.fases)
            if (x != null && fases.TryGetValue(x.numero, out string tx)) x.descripcion = tx;

        var ataques = new Dictionary<string, (string d, string c)>
        {
            ["tajo_dialogo"] = (null, "Apenas avisa: ten el parry listo en cuanto termine de hablar. Si lo paras, empieza aturdida."),
            ["tresLunas"] = ("Barrido, luna y estocada, uno detrás de otro.", "Esquiva los tres o para la estocada final. Al terminar deja un hueco para castigarla."),
            ["cruce"] = ("Un dash largo que te atraviesa, y un tajo hacia atrás al pasar.", "Rueda a través de ella durante el dash y apártate: el tajo de vuelta cae donde estabas."),
            ["ascendente"] = ("Castiga a quien salta por encima de ella.", "Para pasar al otro lado, mejor rodar por debajo de sus ataques que saltarla."),
            ["salto"] = (null, "En cuanto despegue, rueda hacia delante, por debajo del salto."),
            ["guardia"] = ("Si le pegas sin parar, se pone en guardia: el siguiente golpe lo para, te aturde y te atraviesa con una estocada que quita media vida.", "Cuenta tus golpes. Si ves un brillo fino en su espada, deja de atacar: retrocede o rueda hasta que se apague."),
            ["escudo"] = ("Una vez por barra, con poca vida, ruge, se aleja y se cura dentro de un escudo de luz.", "Rompe el escudo a golpes (10, o 5 con la espada imbuida de oscuridad): cancelas la cura y la dejas aturdida."),
            ["ejecucion"] = ("Una línea roja cruza el suelo de punta a punta y ella la recorre con un dash imparable. Es mortal.", "Salta o rueda justo cuando empiece el dash. Después queda expuesta."),
            ["flanqueo"] = ("Ella a un lado y una ilusión al otro, cortando a la vez; a veces una a ras de suelo y la otra en el aire.", null),
            ["caen"] = (null, "Acércate andando, no corriendo, y sal de las marcas."),
            ["espejismo"] = (null, "Desconfía de la figura que se queda quieta de repente: la real es la que se mueve."),
            ["fantasma"] = (null, "No te quedes quieto: la cuchillada cae donde estabas, no donde estás."),
            ["trampas"] = ("Símbolos tenues en el suelo. Si pisas uno, te ataca al instante desde arriba.", "Mira dónde pisas y rodea o salta los símbolos."),
            ["cruceDoble"] = (null, "Rueda a través de cada cruce en el momento justo; huir en su misma dirección no sirve."),
            ["sentencia"] = ("Una marca roja te sigue, se fija con un sonido y un tajo gigante cae del cielo sobre ella. Es mortal.", null),
            ["danza"] = (null, "Esquiva golpe a golpe, sin prisa: el ritmo cambia a propósito. Tras el golpe final tarda en recuperarse."),
            ["silencio"] = ("Durante unos segundos, cualquier ruido (atacar, rodar, beber, cambiar de imbuición o correr) te delata, y te ejecuta.", null),
        };
        foreach (FichaJefe.Ataque x in f.ataques)
        {
            if (x == null || !ataques.TryGetValue(x.id, out var t)) continue;
            if (t.d != null) x.descripcion = t.d;
            if (t.c != null) x.consejo = t.c;
        }
        EditorUtility.SetDirty(f);
    }
}
