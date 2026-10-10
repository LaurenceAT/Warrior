using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ronda 22: la Ejecucion doble, el Cerco de ilusiones y el premio por parry
// perfecto. Escribe en las fichas (todo queda editable en el Inspector):
//   - Ajustes Cazadora: avisos de la Ejecucion, frases del cerco y del premio,
//     reintentos por causa (Ejecucion y Cerco como causas propias).
//   - Ficha de la Cazadora (Bitacora): el Cerco sustituye al Tajo del desafio,
//     la Ejecucion nueva y el consejo secreto del cerco.
//   - TextosMuerte: frases por ataque (Ejecucion y Cerco).
//   - Resources/Logros/Desafios_3_silencio_ecos (nuevo, oculto).
// OJO: al ejecutarlo se pisan esos textos. Si ya los retocaste en el Inspector,
// no lo vuelvas a ejecutar.
public static class Ronda22
{
    [MenuItem("Warrior/Actualizar/Ronda 22 (Ejecucion doble, Cerco de ilusiones y parry perfecto)")]
    public static void Aplicar()
    {
        Ajustes();
        Ficha();
        Muerte();
        Logro();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda22] Aplicado.");
    }

    private static AjustesCazadora.ReintentoCausa C(string nombre, string[] ataques, bool estado, params string[] frases) =>
        new AjustesCazadora.ReintentoCausa { nombre = nombre, ataques = ataques, estadoAlterado = estado, frases = frases };

    private static void Ajustes()
    {
        AjustesCazadora a = AssetDatabase.LoadAssetAtPath<AjustesCazadora>("Assets/Data/Jefes/Ajustes Cazadora.asset");
        if (a == null) { Debug.LogError("[Ronda22] No se encuentra Ajustes Cazadora."); return; }
        a.avisoEjecucion = 0.8f;
        a.avisoEjecucionDificil = 0.65f;
        a.avisoMinimoEjecucion = 0.5f;

        a.frasesCerco = new[]
        {
            "Cuatro respiraciones a tu alrededor... y solo una es mía.",
            "Escucha. Así suena un bosque lleno de mí.",
            "Mis ecos ya te rodean. Yo los oigo a todos.",
        };
        a.frasesPremioCerco = new[]
        {
            "...Eso no lo oí venir.",
            "Tus pasos no dudaron ni una vez. Hacía tiempo que no oía algo así.",
            "Mis ecos... callados. Todos.",
        };

        var causas = new List<AjustesCazadora.ReintentoCausa>();
        foreach (AjustesCazadora.ReintentoCausa c in a.reintentoPorCausa ?? new AjustesCazadora.ReintentoCausa[0])
        {
            if (c == null) continue;
            if (c.nombre == "Instakill con aviso rojo")
            {
                // La Ejecucion pasa a ser una causa propia: aqui queda la Sentencia.
                causas.Add(C("Sentencia", new[] { "sentencia" }, false,
                    "Brillaba en rojo, cariño. Hasta yo lo sé, y estoy ciega.",
                    "Te avisé con un sonido precioso, y te quedaste ahí, escuchando.",
                    "Te marqué, y te quedaste donde te marqué."));
                continue;
            }
            if (c.nombre == "Ejecución" || c.nombre == "Cerco de ilusiones") continue;
            if (c.ataques != null) c.ataques = c.ataques.Where(x => x != "tajo_dialogo").ToArray();
            causas.Add(c);
        }
        causas.Insert(0, C("Ejecución", new[] { "ejecucion" }, false,
            "Una línea roja de lado a lado. ¿Qué más querías, un cartel?",
            "Te quedaste en el suelo. Yo no.",
            "Ni la oíste venir, ¿verdad? Yo sí te oí a ti."));
        causas.Insert(1, C("Cerco de ilusiones", new[] { "cerco" }, false,
            "Cuatro de mí y tú con solo dos manos. Mala cuenta.",
            "Te giraste hacia el eco equivocado. Lo oí clarísimo.",
            "Mis ecos te dieron la vuelta. Literalmente."));
        a.reintentoPorCausa = causas.ToArray();
        EditorUtility.SetDirty(a);
    }

    private static void Ficha()
    {
        FichaJefe f = Resources.Load<FichaJefe>("Desafios/Jefe_BlindHuntress");
        if (f == null) return;
        int i = f.ataques.FindIndex(x => x != null && (x.id == "tajo_dialogo" || x.id == "cerco"));
        var cerco = new FichaJefe.Ataque
        {
            id = "cerco",
            nombre = "Cerco de ilusiones",
            fase = 1,
            instakill = false,
            descripcion = "Te rodea con sus ecos y ella misma cierra el círculo. Uno por uno, cada atacante brilla, te apunta con una línea roja y se lanza. Ella siempre va la última.",
            consejo = "Fíjate en quién brilla: ese va ahora. Los de arriba se paran mires donde mires; a los de los lados hay que darles la cara.",
            consejoSecreto = "Si paras a cada copia del cerco sin que ninguna te toque, el cerco se deshace y ella se queda aturdida y herida.",
        };
        if (i >= 0) f.ataques[i] = cerco; else f.ataques.Insert(0, cerco);

        FichaJefe.Ataque e = f.BuscarAtaque("ejecucion");
        if (e != null)
        {
            e.descripcion = "Una línea roja cruza el suelo de punta a punta y ella la recorre en un dash imparable. A veces vuelve. Es mortal.";
            e.consejo = "Salta o rueda cuando la línea se quede quieta y suene el chasquido. Puede volver por el otro lado con otro ritmo; después queda expuesta.";
        }
        EditorUtility.SetDirty(f);
    }

    private static void Muerte()
    {
        TextosMuerte t = Resources.Load<TextosMuerte>("TextosMuerte");
        if (t == null) return;
        t.porAtaque = new List<TextosMuerte.FrasesAtaque>
        {
            new TextosMuerte.FrasesAtaque { jefe = "blind_huntress", ataque = "cerco", frases = new List<string>
            {
                "Te rodearon. Todas eran ella, o casi.",
                "Cuatro direcciones y una sola guardia.",
                "El cerco se cerró. Contigo dentro.",
            } },
            new TextosMuerte.FrasesAtaque { jefe = "blind_huntress", ataque = "ejecucion", frases = new List<string>
            {
                "La línea cruzaba todo. Tú también estabas en todo.",
                "El rojo iba de pared a pared.",
                "De pared a pared, sin un hueco.",
            } },
        };
        EditorUtility.SetDirty(t);
    }

    private static void Logro()
    {
        const string ruta = "Assets/Resources/Logros/Desafios_3_silencio_ecos.asset";
        FichaLogro l = AssetDatabase.LoadAssetAtPath<FichaLogro>(ruta);
        if (l == null)
        {
            l = ScriptableObject.CreateInstance<FichaLogro>();
            AssetDatabase.CreateAsset(l, ruta);
        }
        l.id = "cerco_perfecto";
        l.seccion = FichaLogro.Seccion.Desafios;
        l.orden = 3;
        l.nombre = "Silencio entre ecos";
        l.descripcion = "Haz callar a sus ecos, uno detrás de otro.";
        l.condicion = FichaLogro.Condicion.Especial;
        l.parametro = "cerco_perfecto";
        l.jefeSecreto = "blind_huntress";
        const string icono = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/02-armor/icon_53.png";
        l.icono = AssetDatabase.LoadAllAssetsAtPath(icono).OfType<Sprite>().FirstOrDefault();
        if (l.icono == null) Debug.LogWarning("[Ronda22] No se encontro el icono " + icono);
        EditorUtility.SetDirty(l);
    }
}
