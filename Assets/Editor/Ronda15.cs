using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 15: fichas que se desbloquean peleando (jefes) y el Bestiario.
//   - Iconos nuevos en RecursosRPG (pestanas de la ficha, candado, calavera),
//     del pack ICONOS.
//   - Fichas de jefe: ataques (con descripcion y consejo), fases y fragmentos
//     de historia (la historia de siempre, partida en trozos). Solo se rellena
//     lo que este vacio: si ya lo has editado, no se pisa.
//   - Resources/Bestiario: una entrada por ficha de Assets/Data/Enemigos, con su
//     retrato (recortado de su sprite), su enemigo base y un borrador de texto.
//     Las entradas que ya existen no se tocan (salvo el retrato si falta).
// Warrior > Actualizar > Ronda 15 (o Ronda15.Todo por linea de comandos).
public static class Ronda15
{
    private const string Iconos = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/";
    private const string Retratos = "Assets/Sprites/Bestiario/";
    private const string RutaBestiario = "Assets/Resources/Bestiario.asset";

    [MenuItem("Warrior/Actualizar/Ronda 15 (fichas y Bestiario)")]
    public static void Todo()
    {
        RecursosRPG r = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        IconosNuevos(r);
        FichasJefes();
        Bestiario();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda15] Listo.");
    }

    // ------------------------------------------------------------------ Iconos

    private static void IconosNuevos(RecursosRPG r)
    {
        if (r == null) { Debug.LogWarning("[Ronda15] Sin RecursosRPG"); return; }
        (string clave, string ruta)[] nuevos =
        {
            ("ficha_ataques", "14-dungeon-objects/icon_47.png"),
            ("ficha_debilidades", "16-runes-enchantments/icon_20.png"),
            ("ficha_fases", "14-dungeon-objects/icon_30.png"),
            ("ficha_historia", "14-dungeon-objects/icon_36.png"),
            ("ficha_estadisticas", "14-dungeon-objects/icon_42.png"),
            ("candado", "14-dungeon-objects/icon_12.png"),
            ("bestiario", "14-dungeon-objects/icon_00.png"),
        };
        foreach ((string clave, string ruta) in nuevos)
        {
            string p = Iconos + ruta;
            ConfigurarRecursosRPG.PrepararPixel(p);
            Sprite s = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().FirstOrDefault();
            if (s == null) { Debug.LogWarning("[Ronda15] Sin sprite en " + p); continue; }
            RecursosRPG.EntradaIcono e = r.iconos.FirstOrDefault(i => i.clave == clave);
            if (e == null) r.iconos.Add(e = new RecursosRPG.EntradaIcono { clave = clave });
            e.sprite = s;
        }
        EditorUtility.SetDirty(r);
    }

    // ------------------------------------------------------------------ Jefes

    private static FichaJefe.Ataque A(string id, string nombre, int fase, string descripcion, string consejo, bool instakill = false) =>
        new FichaJefe.Ataque { id = id, nombre = nombre, fase = fase, descripcion = descripcion, consejo = consejo, instakill = instakill };

    private static FichaJefe.FaseInfo F(int n, string nombre, string descripcion) => new FichaJefe.FaseInfo { numero = n, nombre = nombre, descripcion = descripcion };

    private static FichaJefe.Fragmento H(string id, FichaJefe.Cuando c, string texto) => new FichaJefe.Fragmento { id = id, cuando = c, texto = texto };

    private static void FichasJefes()
    {
        Rellenar("Assets/Resources/Desafios/Jefe_ShadowedWetlands.asset",
            new List<FichaJefe.Ataque>
            {
                A("barrido", "Barrido", 1, "Un tajo a ras de suelo con mucho alcance.", "Sáltalo, o páralo con un parry justo antes del impacto."),
                A("barrido_alzado", "Barrido y tajo alzado", 1, "Tras el barrido, con un retraso que cambia cada vez, un tajo vertical que castiga a quien saltó el primero.",
                  "No saltes el barrido por reflejo: páralo y guarda otro parry para el tajo alzado. Son dos ventanas distintas."),
                A("reves", "Revés", 1, "Si te pones a su espalda, corta hacia atrás sin girarse.", "No te quedes pegado a su espalda: rueda a través de él y sal de su alcance, o para el revés."),
                A("paso", "Paso sombrío", 1, "Se convierte en una esfera de sombra, cruza al otro lado y sale con un tajo circular.",
                  "Cuando se haga esfera, aléjate o prepara la esquiva: el tajo sale justo al reaparecer."),
                A("medialunas", "Medialunas", 1, "Lanza medialunas que vuelan hacia ti y te hacen sangrar (en la fase de hielo, te congelan).",
                  "Rueda a través de ellas o sáltalas; acercarte entre dos le quita espacio."),
                A("caida", "Caída desde lo alto", 1, "Salta fuera de la vista y cae sobre una marca en el suelo; al impacto, dos ondas recorren el suelo.",
                  "Sal de la marca en cuanto aparezca y salta las ondas que salen a los lados."),
                A("agarre", "Agarre", 1, "Pose de carga muy visible. Aparece junto a ti y, si te atrapa, te lanza al aire y te corta: con menos del 80 % de vida, mata.",
                  "Al aparecer hay un instante a cámara lenta: rueda justo entonces. Si ves la pose de carga, prepárate para esquivar, no para atacar."),
                A("triple", "Barrido triple", 2, "Barrido, barrido y tajo alzado, sin respiro.", "Tres ventanas de parry seguidas: si las paras todas, le rompes la postura."),
                A("lanzas", "Lanzas de escarcha", 2, "Conjura lanzas de hielo que caen sobre ti.", "Muévete sin parar mientras las conjura; nunca te quedes donde apuntan."),
                A("estacas", "Estacas de hielo", 2, "Estacas que brotan del suelo en cadena, una detrás de otra, hacia ti.", "Salta la cadena o rueda a través de ella; cuando pase, tienes un momento para golpear."),
                A("ventisca", "Ventisca", 2, "Una ventisca que te arrastra hacia él.", "Camina contra el viento sin gastar la estamina; si llegas a su lado, espera su golpe para pararlo."),
            },
            new List<FichaJefe.FaseInfo>
            {
                F(1, "Sombra", "Pequeño y rápido, con una cola larga y una espada que deja tajos blancos enormes. Castiga a quien salta a destiempo y a quien se queda a su espalda. " +
                               "Cada golpe de un combo se puede parar: parar todos le rompe la postura."),
                F(2, "Hielo", "Al vaciar su vida no muere: cae, la escarcha se junta en su cuerpo y se levanta imbuido en hielo. Más rápido y con pausas más cortas; " +
                              "sus tajos dejan escarcha que te ralentiza y suma hechizos de hielo."),
            },
            new List<FichaJefe.Fragmento>
            {
                H("h1", FichaJefe.Cuando.PrimerIntento, "Antes de la nevada eterna, estos humedales alimentaban a un pueblo entero."),
                H("h2", FichaJefe.Cuando.Fase2, "Luego llegó el frío, y con él la sombra: algo que no caza por hambre, sino por costumbre."),
                H("h3", FichaJefe.Cuando.PrimeraVictoria, "Los que cruzaron la ciénaga helada dicen que el silencio llega antes que el agarre. Quien lo ve venir aún puede huir. Quien duda, no vuelve."),
                H("h4", FichaJefe.Cuando.VictoriaDificil, "Lo que queda es una presencia paciente, hecha de agua negra y hielo roto, que espera a quien todavía cree que el camino sigue."),
            });

        Rellenar("Assets/Resources/Desafios/Jefe_CrimsonWraith.asset",
            new List<FichaJefe.Ataque>
            {
                A("caida_entrada", "Caída del techo", 1, "Al empezar, una sombra carmesí aparece bajo tus pies y el jefe cae en picado sobre ella.",
                  "Sal de la sombra antes de que caiga, o recibe el impacto con un parry: si lo paras, queda aturdido un buen rato."),
                A("tajos", "Zarpazos", 1, "Alza la garra y encadena dos zarpazos; si te pones a su espalda, añade un revés.",
                  "Para los dos zarpazos o aléjate tras el primero. No lo rodees: el revés castiga a quien se queda detrás."),
                A("embestida", "Embestida", 1, "Se agacha y cruza el escenario dejando una estela.", "Cuando se agache, sáltalo o rueda a través de él en el último momento."),
                A("zigzag", "Zigzag", 1, "Una embestida de ida y, casi sin respiro, la de vuelta.", "Esquiva la primera y prepárate enseguida para la segunda: vuelve por el mismo camino."),
                A("onda", "Onda carmesí", 1, "Conjura y lanza dos ondas de sangre por el suelo.", "Salta cada onda; tras la segunda queda un momento abierto."),
                A("pilares", "Pilares de sangre", 1, "Marca el suelo con glifos y brotan pilares de sangre que causan sangrado.",
                  "Sal de los glifos en cuanto aparezcan: los pilares brotan justo donde brillan."),
                A("orbes", "Orbes", 1, "Flota y suelta orbes que te persiguen y causan sangrado.", "Un parry devuelve cada orbe contra él. Si no, sigue moviéndote hasta que se deshagan."),
                A("salto", "Salto aplastante", 1, "Se agacha, salta en arco hasta ti y cae con una onda de choque. El sitio de caída se marca en cuanto despega.",
                  "Mira la marca y sal de ella; la onda sale a los dos lados."),
                A("medialuna", "Media luna", 1, "Un zarpazo a distancia que vuela recto a la altura del pecho.", "Sáltala o rueda a través de ella en el momento justo."),
                A("raices", "Raíces carmesí", 1, "Tentáculos que brotan del suelo por donde pasaste, uno tras otro.", "Cambia de dirección: las raíces siguen tu rastro, no a ti."),
                A("cosecha", "Cosecha de sangre", 1, "Si tu barra de sangrado va por la mitad, cierra el puño y la hace estallar: un aro se cierra sobre ti y el daño depende de lo acumulado.",
                  "Vigila tu sangrado. Cuando el aro se cierre, rueda justo antes de que termine: la barra se vacía sin daño."),
                A("zarpazo", "Zarpazo gigante", 2, "Un zarpazo enorme que suelta ondas por el suelo.", "Páralo o aléjate, y salta las ondas que salen de él."),
                A("guadana", "Guadaña doble", 2, "Un tajo enorme hacia delante y, sin girarse, otro hacia atrás. A veces lo encadena tras el zarpazo, el salto o el teletransporte.",
                  "No te pongas a su espalda: el segundo tajo castiga ahí. Aléjate después del primero."),
                A("teletransporte", "Teletransporte", 2, "Desaparece y reaparece a tu espalda con un golpe.", "Cuando desaparezca, date la vuelta o rueda hacia delante: sale detrás de ti."),
                A("cadena", "Pilares en cadena", 2, "Pilares de sangre que brotan en fila, uno detrás de otro, hacia ti.", "Salta la fila o rueda a través de ella; no corras en su misma dirección."),
                A("lluvia", "Lluvia de fuego", 2, "Llueve fuego por toda la arena. Después su corazón queda expuesto.",
                  "Busca los huecos entre las marcas y no dejes de moverte. Al terminar, golpéalo: recibe x1.5."),
                A("vortice", "Vórtice", 2, "Un remolino de sangre que te atrae hacia su centro.", "Aléjate del centro en cuanto aparezca; si te atrapa, rueda para salir."),
                A("nova", "Nova", 2, "Se carga con un aro que marca su alcance y estalla alrededor. Después su corazón queda expuesto.",
                  "Sal del aro antes de que estalle y vuelve enseguida: tras la Nova recibe x1.5."),
                A("semillas", "Semillas del vacío", 2, "Lanza orbes morados en arco que caen al suelo y, un momento después, brotan como pilares.",
                  "Mira dónde caen las semillas y aléjate de ellas antes de que broten."),
                A("transfusion", "Transfusión", 2, "Un hilo de sangre te une a él; cada pulso que te llega le cura.", "Aléjate para romper el hilo, o para un pulso con un parry."),
                A("frenesi", "Frenesí", 2, "Con poca vida, ruge, la arena se oscurece y encadena ataques casi sin pausa. Después queda agotado y con el corazón expuesto.",
                  "No ataques durante el frenesí: solo esquiva. Cuando quede agotado, es tu momento."),
            },
            new List<FichaJefe.FaseInfo>
            {
                F(1, "Forma humanoide", "Se mueve con aceleración y frenada, y avisa de sus golpes: un destello corto para los ligeros y uno largo para los pesados. " +
                                        "Zarpazos, embestidas, ondas y pilares de sangre."),
                F(2, "Forma grande", "A mitad de vida toma su forma grande y la arena se tiñe de rojo. Ataca más rápido y con menos pausa. " +
                                     "Tras la Nova, la Lluvia de fuego y el Frenesí su corazón queda expuesto: todo lo que le hagas entra x1.5."),
            },
            new List<FichaJefe.Fragmento>
            {
                H("h1", FichaJefe.Cuando.PrimerIntento, "Nadie recuerda su rostro, porque no tiene uno que la mente acepte."),
                H("h2", FichaJefe.Cuando.Fase2, "Crimson Wraith fue algo más antiguo que la cueva, y la montaña se cerró a su alrededor como una herida que cicatriza sobre una astilla."),
                H("h3", FichaJefe.Cuando.PrimeraVictoria, "Se alimenta de la sangre que baja hasta él: la de los mineros, la de los que vinieron a matarlo, la tuya. Sus garras arrancan lo que tocan. " +
                                                          "Sus tentáculos buscan en la oscuridad lo que las garras no alcanzan. Y sus hechizos no piden permiso: la sangre simplemente obedece."),
                H("h4", FichaJefe.Cuando.VictoriaDificil, "Quienes lo vieron no saben describirlo. Solo dicen que sigue creciendo."),
            });

        Rellenar("Assets/Resources/Desafios/Jefe_BlindHuntress.asset",
            new List<FichaJefe.Ataque>
            {
                A("tajo_dialogo", "Tajo del desafío", 1, "Si aceptas su desafío al llegar, abre con un tajo horizontal muy rápido.",
                  "Tiene un aviso mínimo: ten el parry listo en cuanto termine de hablar. Si lo paras, empieza aturdida."),
                A("tresLunas", "Tres Lunas", 1, "Barrido, luna y estocada seguidos.", "Esquiva los tres o para la estocada final: al terminar deja un hueco para castigarla."),
                A("cruce", "Cruce", 1, "Un dash largo que te atraviesa y un tajo hacia atrás al pasar.", "Rueda a través de ella en el dash y aléjate: el tajo de vuelta sale donde estabas."),
                A("ola", "Ola de luz", 1, "El barrido sale como una ola baja por el suelo. Si no sabe dónde estás, la lanza a ciegas.",
                  "Salta la ola. Si no te oye, quédate quieto: la lanza hacia donde mira, no hacia ti."),
                A("ascendente", "Tajo ascendente", 1, "Castiga al que salta encima de ella.", "No saltes sobre ella para pasar al otro lado: rueda por debajo de sus ataques."),
                A("salto", "Salto descendente", 1, "Salta hacia ti y cae cortando.", "Rueda hacia delante, por debajo del salto, en cuanto despegue."),
                A("guardia", "Guardia", 1, "Si le pegas sin parar, adopta la guardia: si la golpeas, te para, te aturde y te atraviesa con una estocada que quita media vida.",
                  "Cuenta tus golpes. Si ves un brillo fino en su espada, deja de atacar, retrocede o rueda hasta que se apague."),
                A("escudo", "Rugido y escudo", 1, "Una vez por barra, con poca vida: ruge, se aleja y se cura dentro de un escudo de luz.",
                  "Rompe el escudo a golpes (10, o 5 con la espada imbuida de oscuridad): cancela la cura y la deja aturdida."),
                A("ejecucion", "Ejecución", 1, "Una línea roja cruza el suelo de punta a punta y ella la recorre con un dash imparable. Mata.",
                  "Salta o rueda cuando empiece el dash. Después queda expuesta.", true),
                A("flanqueo", "Flanqueo", 2, "Ella a un lado y una ilusión al otro, y cortan a la vez; a veces una a ras de suelo y otra en el aire.",
                  "Fíjate en la altura de cada corte: salta el bajo y rueda bajo el alto. Golpear a la ilusión la deshace."),
                A("caen", "Ilusiones que caen", 2, "Si te acercas corriendo, caen ilusiones del cielo con un tajo descendente; una marca en el suelo avisa del impacto.",
                  "No corras hacia ella: acércate andando, y sal de las marcas."),
                A("espejismo", "Espejismo", 2, "Deja un señuelo quieto donde estaba y reaparece en otro sitio. Si golpeas el señuelo, explota.",
                  "Desconfía de la figura que se queda quieta de repente: busca a la que se mueve."),
                A("fantasma", "Cuchillada fantasma", 2, "Un tajo aparece donde estabas hace un segundo, con una marca antes.", "No te detengas: la cuchillada llega a donde estabas, no a donde estás."),
                A("trampas", "Trampas de sonido", 2, "Símbolos tenues en el suelo; si pisas uno, te ataca al instante desde arriba.", "Mira el suelo y rodea o salta los símbolos."),
                A("lluvia", "Lluvia de tajos", 2, "Salta fuera de la vista y cae con un tajo descendente, soltando olas de luz a los dos lados.",
                  "Aléjate de donde vaya a caer y salta las olas."),
                A("cruceDoble", "Cruce doble", 2, "Ella cruza hacia un lado y una ilusión hacia el otro.", "Rueda a través de los cruces en el momento justo; no huyas en su dirección."),
                A("contra", "Contraataque elemental", 2, "Copia la imbuición de tu espada y la resiste unos segundos.", "Cuando copie tu elemento, cámbialo o espera a que se le pase."),
                A("sentencia", "Sentencia del Cazador", 2, "Una marca roja te sigue, se fija con un sonido y un tajo gigante cae del cielo sobre ella. Mata.",
                  "Cuando la marca se fije, sal de ella. Las ilusiones de alrededor no importan: la marca siempre se ve encima.", true),
                A("danza", "Danza de la Cacería", 3, "De ocho a doce golpes con dashes y un ritmo engañoso, cada uno con su aviso; termina con un golpe pesado.",
                  "Esquiva golpe a golpe sin prisa: el ritmo cambia a propósito. Tras el golpe final, su recuperación es larga."),
                A("espejos", "Cacería de Espejos", 3, "Tres o cuatro figuras, solo una real, que cruzan una tras otra.", "Esquiva cada cruce por separado y no ataques hasta que pase el último."),
                A("silencio", "El Silencio", 3, "Durante unos segundos, cualquier ruido (atacar, rodar, beber, cambiar de imbuición o correr) te delata y te ejecuta.",
                  "Quédate quieto o camina despacio. Si no oye nada, pierde tu rastro y queda vulnerable.", true),
            },
            new List<FichaJefe.FaseInfo>
            {
                F(1, "La cazadora", "Es ciega: de lejos ataca a donde oyó tu último ruido (atacar, rodar, aterrizar, beber, imbuir, correr). Si fallas donde golpea, queda confundida; " +
                                    "de cerca te siente siempre. Tiene super armadura: solo el parry, lo sagrado o romperle el escudo la interrumpen."),
                F(2, "Las ilusiones", "Imbuye su espada (fuego, hielo, luz y sangre), copia tu elemento para resistirlo y se apoya en ilusiones que golpean como ella."),
                F(3, "La oscuridad", "La luz se apaga y solo ves a tu alrededor. Llegan sus cacerías más largas: la Danza de la Cacería y la Cacería de Espejos."),
            },
            new List<FichaJefe.Fragmento>
            {
                H("h1", FichaJefe.Cuando.PrimerIntento, "Nadie recuerda cuándo se quedó ciega ni quién la dejó así."),
                H("h2", FichaJefe.Cuando.Fase3, "Lo que se sabe es que el bosque murió primero: las hojas cayeron una noche y no volvieron a brotar. Ella se quedó."),
                H("h3", FichaJefe.Cuando.PrimeraVictoria, "Aprendió a cazar con los oídos, a leer el miedo en un latido, a distinguir el paso de un ladrón del de un hijo que vuelve a casa. " +
                                                          "Muchos vinieron a matar a la cazadora ciega. Ninguno la sorprendió."),
                H("h4", FichaJefe.Cuando.VictoriaDificil, "En el bosque sin hojas todo se oye, y ella escucha desde hace tanto que ya no recuerda otra cosa que el sonido de quien viene a morir."),
            });
    }

    private static void Rellenar(string ruta, List<FichaJefe.Ataque> ataques, List<FichaJefe.FaseInfo> fases, List<FichaJefe.Fragmento> historia)
    {
        FichaJefe f = AssetDatabase.LoadAssetAtPath<FichaJefe>(ruta);
        if (f == null) { Debug.LogWarning("[Ronda15] No existe " + ruta); return; }
        if (f.ataques == null || f.ataques.Count == 0) f.ataques = ataques;
        if (f.fases == null || f.fases.Count == 0) f.fases = fases;
        if (f.fragmentosHistoria == null || f.fragmentosHistoria.Count == 0) f.fragmentosHistoria = historia;
        EditorUtility.SetDirty(f);
        Debug.Log($"[Ronda15] {f.nombre}: {f.ataques.Count} ataques, {f.fases.Count} fases, {f.fragmentosHistoria.Count} fragmentos");
    }

    // ------------------------------------------------------------------ Bestiario

    private class Texto
    {
        public string descripcion, patrones, base_;
        public int derrotas = 5;
    }

    private static readonly Dictionary<string, Texto> Textos = new Dictionary<string, Texto>
    {
        ["RataEscarcha"] = new Texto
        {
            descripcion = "Ratas que el frío no mató: les heló la sangre y las dejó con hambre. Nunca van solas. La que te ve primero chilla, y las demás acuden.",
            patrones = "Mordisco: pegada a ti, un aviso muy corto y una dentellada.\nSalto: a media distancia se agacha y salta encima de ti.\nGolpea y huye: tras atacar se aparta. Espérala con un parry o castígala al aterrizar.",
        },
        ["MurcielagoCumbres"] = new Texto
        {
            descripcion = "Anidan en las grietas de las cumbres, donde ya no llega el sol. Vuelan sin rumbo hasta que huelen algo caliente.",
            patrones = "Vuelo errático: te rodea a distinta altura cada vez.\nPicado en arco: chilla, se lanza en curva hacia ti y vuelve a subir por el otro lado. A veces encadena otro.\nChillido: si te acercas mucho, suelta una onda que te empuja.",
        },
        ["OjoVigia"] = new Texto
        {
            descripcion = "Un ojo sin párpado que flota sobre el paso nevado. Vigila para algo que dejó de mirar hace mucho tiempo.",
            patrones = "Se mantiene lejos y alto, y se aparta si te acercas.\nEscupitajo: uno apuntado o una ráfaga de tres en abanico. Un parry lo devuelve.\nEmbestida giratoria: si estás cerca, gira y embiste.",
        },
        ["ArqueraArcana"] = new Texto
        {
            descripcion = "Fue centinela del paso cuando aún había algo que guardar. Sigue disparando a todo lo que se mueve, sin preguntarse a quién sirve.",
            patrones = "Disparo: tensa el arco y suelta una flecha recta y rápida.\nLluvia: dispara al cielo; unas marcas avisan de dónde caerán las flechas.\nVoltereta: si te acercas, salta hacia atrás sin darte la espalda.\nDestello: arrinconada contra una pared, se teletransporta lejos.",
        },
        ["HechiceroSombrio"] = new Texto
        {
            descripcion = "Buscó en la sombra un poder que lo protegiera del frío. La sombra lo encontró primero. Lento, pesado, y nada lo detiene cuando ataca.",
            patrones = "Armadura: mientras ataca, tus golpes le quitan vida pero no lo paran. Un parry sí.\nGarras de sombra: proyecta una sombra hacia delante, a media distancia.\nGolpe de báculo: un barrido de cerca que a veces encadena con las garras, con un retraso que cambia.\nSalto sombrío: salta por encima de ti y cae a tu espalda.",
        },
        ["Slime"] = new Texto
        {
            descripcion = "Lo que queda cuando la cueva digiere algo. No piensa: se arrastra hacia el calor y se lanza sobre él.",
            patrones = "Patrulla dando la vuelta en paredes y bordes.\nEmbestida: se encoge (aviso) y se lanza hacia delante; después queda un momento expuesto.\nSi falla, a veces rebota y repite la embestida enseguida.",
        },
        ["Cacodemonio"] = new Texto
        {
            descripcion = "Una boca con alas que llena la cueva con su rugido. Hasta la roca le tiene miedo: se desprende a su paso.",
            patrones = "Flota sobre ti, a un lado, meciéndose.\nRugido: se para, abre la boca y ruge; las estalactitas cercanas caen sobre quien esté debajo.\nPicado: se lanza recto hacia donde estabas, con armadura. Un parry lo para en seco; si se estrella contra la roca, queda aturdido.",
        },
        ["Mago"] = new Texto
        {
            descripcion = "Bajaron a la cueva a estudiar la sangre que empapa la roca. Algunos aprendieron demasiado y ya no saben salir.",
            patrones = "Mantiene la distancia y lanza bolas mágicas: se pueden bloquear, esquivar o devolver con un parry.\nSi te echas encima, retrocede sin darte la espalda.\nEstallido: arrinconado contra una pared o un borde, suelta un estallido a su alrededor con un aviso claro.",
        },
        ["Mimic"] = new Texto
        {
            descripcion = "Un cofre que espera. La cueva enseña pronto que no todo lo que brilla guarda un tesoro: a veces guarda dientes.",
            patrones = "Disfrazado no se mueve. Se abre al acercarte o al golpearlo.\nMordisco: se alza con la boca abierta (aviso largo) y se lanza con un saltito. Mucho daño.\nLatigazo: echa el cuerpo atrás (aviso corto) y barre hacia delante con más alcance. No te quedes a media distancia.",
        },
        ["RataCeniza"] = new Texto
        {
            base_ = "RataEscarcha",
            descripcion = "Las que bajaron a la cueva buscando calor, y lo encontraron. Ahora arden por dentro y ya no temen al fuego.",
            patrones = "Como la rata de escarcha: mordisco rápido, salto desde media distancia y huida tras atacar. Caza en manada.",
        },
        ["MurcielagoCripta"] = new Texto
        {
            base_ = "MurcielagoCumbres",
            descripcion = "Nunca han visto el cielo. Nacen, cazan y mueren en la oscuridad de la cueva, y la oscuridad no les hace daño.",
            patrones = "Como el murciélago de las cumbres: vuelo errático, picado en arco (a veces dos seguidos) y un chillido que empuja si te acercas.",
        },
        ["ArqueraSangre"] = new Texto
        {
            base_ = "ArqueraArcana",
            descripcion = "Otra centinela que bajó a la cueva. La sangre que empapa la roca le tiñó el arco, y ya no sangra cuando la hieres.",
            patrones = "Como la arquera arcana: disparo recto, lluvia de flechas con marcas en el suelo, voltereta hacia atrás y destello si la arrinconas.",
        },
        ["OjoAbismo"] = new Texto
        {
            base_ = "OjoVigia",
            descripcion = "Un ojo que miró demasiado tiempo al fondo de la cueva. Lo que vio le dio su color.",
            patrones = "Como el ojo vigía: se mantiene lejos, escupe (uno o tres en abanico; un parry los devuelve) y embiste girando si estás cerca.",
        },
        ["SlimeAbisal"] = new Texto
        {
            base_ = "Slime",
            descripcion = "Más hondo, donde la cueva ya no tiene nombre, los slimes se vuelven oscuros y el fuego resbala sobre ellos.",
            patrones = "Como el slime: se encoge y embiste, queda expuesto después y, si falla, a veces repite la embestida.",
        },
        ["MagoHueso"] = new Texto
        {
            base_ = "Mago",
            descripcion = "De los que se quedaron a estudiar la sangre, solo quedan los huesos. Siguen lanzando hechizos, por costumbre.",
            patrones = "Como el mago: bolas mágicas a distancia, retrocede sin darte la espalda y suelta un estallido si lo arrinconas.",
        },
        ["EliteMorgath"] = new Texto
        {
            base_ = "HechiceroSombrio", derrotas = 3,
            descripcion = "Se arrancó los ojos para no ver lo que la sombra le mostraba. Sigue viéndolo. Guarda el paso nevado como guardaría una tumba.",
            patrones = "Como el hechicero sombrío, pero más duro: armadura al atacar, garras de sombra, golpe de báculo encadenado y salto a tu espalda.",
        },
        ["EliteMadreCarmesi"] = new Texto
        {
            base_ = "Cacodemonio", derrotas = 3,
            descripcion = "Dicen que de su boca nacieron todos los cacodemonios de la cueva. El fuego que la engendró ya no le hace daño.",
            patrones = "Como el cacodemonio, pero más dura: rugido que derrumba las estalactitas y picado con armadura. Un parry la para en seco.",
        },
        ["EliteAldren"] = new Texto
        {
            base_ = "Mago", derrotas = 3,
            descripcion = "El primero que bajó a estudiar la sangre, y el último en marcharse. Nunca se marchó.",
            patrones = "Como el mago, pero más duro: bolas mágicas, retirada sin darte la espalda y un estallido si lo arrinconas.",
        },
    };

    // Orden: primero los de la Nieve y luego los de la Cueva (y lo que venga).
    private static readonly string[] Orden =
    {
        "RataEscarcha", "MurcielagoCumbres", "OjoVigia", "ArqueraArcana", "HechiceroSombrio", "EliteMorgath",
        "Slime", "Cacodemonio", "Mago", "Mimic", "RataCeniza", "MurcielagoCripta", "ArqueraSangre", "OjoAbismo", "EliteMadreCarmesi", "EliteAldren",
        "SlimeAbisal", "MagoHueso",
    };

    private static void Bestiario()
    {
        Directory.CreateDirectory(Retratos);
        FichaBestiario b = AssetDatabase.LoadAssetAtPath<FichaBestiario>(RutaBestiario);
        bool nuevo = b == null;
        if (nuevo) b = ScriptableObject.CreateInstance<FichaBestiario>();

        Dictionary<string, DefinicionEnemigo> defs = AssetDatabase.FindAssets("t:DefinicionEnemigo", new[] { "Assets/Data/Enemigos" })
            .Select(g => AssetDatabase.LoadAssetAtPath<DefinicionEnemigo>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null).ToDictionary(d => d.name);
        // Prefab de cada ficha (el primero que la usa).
        var prefabs = new Dictionary<DefinicionEnemigo, GameObject>();
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
            EnemyHealth h = go != null ? go.GetComponentInChildren<EnemyHealth>(true) : null;
            if (h != null && h.Definicion != null && !prefabs.ContainsKey(h.Definicion)) prefabs[h.Definicion] = go;
        }

        foreach (string id in Orden.Concat(defs.Keys.Except(Orden).OrderBy(x => x)))
        {
            if (!defs.TryGetValue(id, out DefinicionEnemigo d)) continue;
            FichaBestiario.Entrada e = b.entradas.FirstOrDefault(x => x != null && x.enemigo == d);
            if (e == null)
            {
                Textos.TryGetValue(id, out Texto t);
                e = new FichaBestiario.Entrada
                {
                    enemigo = d,
                    descripcion = t?.descripcion ?? "",
                    patrones = t?.patrones ?? "",
                    derrotasParaPatrones = t?.derrotas ?? 5,
                    relacionado = t != null && t.base_ != null && defs.TryGetValue(t.base_, out DefinicionEnemigo db) ? db : null,
                };
                b.entradas.Add(e);
            }
            // Los retratos hechos aqui se rehacen; uno puesto a mano (en otra carpeta) se respeta.
            bool propio = e.retrato != null && !AssetDatabase.GetAssetPath(e.retrato).StartsWith(Retratos);
            if (!propio && prefabs.TryGetValue(d, out GameObject p)) e.retrato = Retrato(p, id);
            if (e.retrato == null) Debug.LogWarning("[Ronda15] Sin retrato para " + id);
        }
        if (nuevo) AssetDatabase.CreateAsset(b, RutaBestiario);
        EditorUtility.SetDirty(b);
        Debug.Log($"[Ronda15] Bestiario: {b.entradas.Count} entradas, {b.entradas.Count(x => x.retrato != null)} con retrato");
    }

    // El primer fotograma de su animacion de reposo, recortado a lo pintado.
    private static Sprite Retrato(GameObject prefab, string id)
    {
        Texture2D tex = null;
        RectInt rect = default;
        AnimadorHoja an = prefab.GetComponentInChildren<AnimadorHoja>(true);
        // La pose de combate ("quieto"; el Mimic disfrazado es otro clip), si no la inicial.
        AnimadorHoja.Clip c = null;
        if (an != null)
            foreach (string n in new[] { "quieto", "idle", "reposo", an.clipInicial })
                if (c == null && !string.IsNullOrEmpty(n)) c = an.clips.FirstOrDefault(x => x != null && x.nombre == n);
        if (an != null && c == null) c = an.clips.FirstOrDefault(x => x != null);
        if (c != null && c.fotogramas != null && c.fotogramas.Length > 0 && c.fotogramas[0] != null)
        {
            tex = c.fotogramas[0];
            rect = c.recorte.width > 0 ? c.recorte : new RectInt(0, 0, tex.width, tex.height);
        }
        else if (c != null && c.hoja != null)
        {
            tex = c.hoja;
            int columnas = Mathf.Max(1, tex.width / c.anchoCelda);
            int col = c.primero % columnas, fila = c.fila + c.primero / columnas;
            rect = new RectInt(col * c.anchoCelda, tex.height - (fila + 1) * c.altoCelda, c.anchoCelda, c.altoCelda);
        }
        else
        {
            SpriteRenderer sr = prefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s => s.sprite != null);
            if (sr == null) return null;
            tex = sr.sprite.texture;
            Rect r = sr.sprite.rect;
            rect = new RectInt((int)r.x, (int)r.y, (int)r.width, (int)r.height);
        }

        // Se lee el PNG del disco (la textura importada puede no ser legible).
        string ruta = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return null;
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(ruta));
        if (t.width != tex.width && tex.width > 0)
        {
            // La importada esta reducida: se escala el rectangulo al tamano real.
            float k = t.width / (float)tex.width;
            rect = new RectInt(Mathf.RoundToInt(rect.x * k), Mathf.RoundToInt(rect.y * k), Mathf.RoundToInt(rect.width * k), Mathf.RoundToInt(rect.height * k));
        }
        rect.x = Mathf.Clamp(rect.x, 0, t.width - 1);
        rect.y = Mathf.Clamp(rect.y, 0, t.height - 1);
        rect.width = Mathf.Clamp(rect.width, 1, t.width - rect.x);
        rect.height = Mathf.Clamp(rect.height, 1, t.height - rect.y);
        Color[] px = t.GetPixels(rect.x, rect.y, rect.width, rect.height);

        // Recorte a lo pintado.
        int x0 = rect.width, x1 = -1, y0 = rect.height, y1 = -1;
        for (int y = 0; y < rect.height; y++)
            for (int x = 0; x < rect.width; x++)
                if (px[y * rect.width + x].a > 0.05f) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
        if (x1 < 0) return null;
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        Texture2D salida = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                salida.SetPixel(x, y, px[(y + y0) * rect.width + x + x0]);
        salida.Apply();
        string destino = Retratos + id + ".png";
        File.WriteAllBytes(destino, salida.EncodeToPNG());
        AssetDatabase.ImportAsset(destino);
        TextureImporter ti = AssetImporter.GetAtPath(destino) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(destino);
    }
}
