using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba de lo que hacen los amuletos (batchmode, sin -quit):
//   ... -executeMethod PruebaEfectosAmuletos.Todo
// En la sala de pruebas (una Rata de escarcha quieta de blanco): sin amuletos
// todo vale 1; cada amuleto cambia lo suyo (vida, estamina, frascos, imbuir,
// parry, dano recibido, espinas, calavera, mana, estados, ruido, precios...) y
// al quitarlo vuelve a lo normal. Los 3 de exploracion no hacen nada aun.
// En silencio y sin guardar nada.
[InitializeOnLoad]
public static class PruebaEfectosAmuletos
{
    private const string Clave = "PruebaEfectosAmuletos.Modo";

    static PruebaEfectosAmuletos()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaEfectosAmuletos").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        CrearAmuletos.Crear();
        SessionState.SetString(Clave, "si");
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[EFECTOS] OK " + s); } else { fallos++; Debug.LogWarning("[EFECTOS] FALLO " + s); } }
        private static bool Casi(float a, float b, float tol = 0.02f) => Mathf.Abs(a - b) <= tol * Mathf.Max(1f, Mathf.Abs(b));
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private PlayerControler p;
        private PlayerStamina st;
        private PlayerMana mana;
        private ArmaImbuida arma;
        private EnemyHealth blanco;

        // Espera a que acabe la invulnerabilidad tras un golpe (y a que se levante).
        private IEnumerator SinInvulnerable()
        {
            FieldInfo inv = typeof(PlayerControler).GetField("isInvincible", Priv);
            FieldInfo kn = typeof(PlayerControler).GetField("isKnocked", Priv);
            for (float t = 0f; t < 5f && ((bool)inv.GetValue(p) || (bool)kn.GetValue(p)); t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.1f);
        }

        // Solo ese amuleto puesto (con los 4 huecos abiertos).
        private static void Poner(params string[] ids)
        {
            Amuletos.Reiniciar();
            while (Amuletos.AbrirHueco()) { }
            foreach (string id in ids) { Amuletos.Dar(id); Amuletos.Equipar(id); }
        }

        private IEnumerator Start()
        {
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st2, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[EFECTOS] error visto: " + c + "\n" + st2); } };
            for (float t = 0f; t < 10f && SalaPruebas.Actual == null; t += Time.unscaledDeltaTime) yield return null;
            SalaPruebas sala = SalaPruebas.Actual;
            yield return new WaitForSeconds(1f);
            p = FindFirstObjectByType<PlayerControler>();
            st = p.GetComponent<PlayerStamina>();
            mana = p.GetComponent<PlayerMana>();
            arma = p.GetComponent<ArmaImbuida>();
            sala.Invocar(sala.Catalogo.entradas.First(e => e != null && e.nombre == "Rata de escarcha"));
            yield return new WaitForSeconds(1f);
            blanco = sala.Salud;
            EnemigoBase ia = blanco != null ? blanco.GetComponent<EnemigoBase>() : null;
            if (ia != null) ia.enabled = false;
            Comprobar(blanco != null, "hay un enemigo de blanco");

            AjustesAmuletos A = AjustesAmuletos.Get();
            FichaAmuletos fa = FichaAmuletos.Get();
            Comprobar(fa.amuletos.All(a => !string.IsNullOrEmpty(a.descripcion)), "los 40 tienen descripción");

            // ---- Sin amuletos: todo a 1.
            Amuletos.Reiniciar();
            int vida0 = p.VidaMaxima;
            float est0 = st.Max;
            int frascos0 = ReservaPociones.Get().Maximo;
            float coste0 = arma.CosteMana;
            int sangre0 = ArmaImbuida.CosteVidaSangrado;
            Comprobar(Casi(EfectosAmuletos.MultDano(true, true, true, Elemento.Fuego, 1f, 0f), 1f) && EfectosAmuletos.MultRuido == 1f
                      && EfectosAmuletos.MultPrecios == 1f && EfectosAmuletos.Espinas == 0f, "sin amuletos no cambia nada");

            // ---- Vida, estamina y frascos.
            Poner(EfectosAmuletos.Corazon); yield return null;
            Comprobar(p.VidaMaxima == Mathf.RoundToInt(vida0 * A.corazonVida), $"Corazón que late: vida {vida0} -> {p.VidaMaxima}");
            Poner(EfectosAmuletos.Costillar); yield return null;
            Comprobar(Casi(st.Max, est0 * A.costillarEstamina), $"Costillar hueco: estamina {est0} -> {st.Max}");
            Comprobar(p.VidaMaxima == vida0, "al quitarse el Corazón la vida vuelve a la normal");
            Poner(EfectosAmuletos.Hueso);
            Comprobar(ReservaPociones.Get().Maximo == frascos0 + A.huesoCargas, $"Hueso nudoso: frascos {frascos0} -> {ReservaPociones.Get().Maximo}");
            Poner(EfectosAmuletos.Muelas);
            Comprobar(Casi(EfectosAmuletos.MultFrascoSangre, A.muelasFrasco), "Muelas del glotón: el frasco cura más");
            Poner(EfectosAmuletos.Muelas, EfectosAmuletos.Lazo);
            Comprobar(Casi(EfectosAmuletos.MultFrascoSangre, A.muelasFrasco * A.lazoFrasco), "Lazo nocturno (desventaja): el frasco cura menos");
            Poner(EfectosAmuletos.Cuenco);
            Comprobar(Casi(EfectosAmuletos.MultFrascoMana, A.cuencoFrascoMana), "Cuenco del festín: el frasco de maná da más");

            // ---- Imbuir.
            Poner(EfectosAmuletos.Seso);
            Comprobar(Casi(arma.CosteMana, coste0 * A.sesoCosteMana), $"Seso marchito: imbuir cuesta {arma.CosteMana:0} de maná");
            arma.Activar(Elemento.Fuego);
            float durSeso = arma.Restante;
            arma.Apagar();
            Poner(EfectosAmuletos.Dedo);
            typeof(ArmaImbuida).GetField("siguienteCambio", Priv).SetValue(arma, 0f);
            arma.Activar(Elemento.Fuego);
            float durDedo = arma.Restante;
            arma.Apagar();
            Comprobar(Casi(durDedo / durSeso, A.dedoDuracion / A.sesoDuracion, 0.05f), $"Dedo anillado y Seso: la imbuición dura {durDedo:0} s / {durSeso:0} s");
            Poner(EfectosAmuletos.Rosario);
            Comprobar(ArmaImbuida.CosteVidaSangrado < sangre0 && Casi(EfectosAmuletos.MultSangradoEnemigo, A.rosarioSangrado)
                      && Casi(EfectosAmuletos.MultSangradoPropio, A.rosarioSangradoPropio), $"Rosario de dientes: sangrado cuesta {ArmaImbuida.CosteVidaSangrado} de vida (antes {sangre0}), y su desventaja");
            Poner(EfectosAmuletos.Brasa);
            Comprobar(Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Fuego, 0.5f, 99f), A.brasaDano) && Casi(EfectosAmuletos.MultQuemadura, A.brasaQuemadura)
                      && Casi(EfectosAmuletos.MultDanoRecibido(true, Elemento.Fuego), A.brasaDanoMagicoRecibido) && Casi(EfectosAmuletos.MultDanoRecibido(true, Elemento.Ninguno), 1f),
                      "Anillo de la brasa: más daño y quemadura con fuego, y más daño mágico recibido solo mientras arde");
            Poner(EfectosAmuletos.Escarcha);
            Comprobar(Casi(EfectosAmuletos.MultCongelar, A.escarchaCongelar) && EfectosAmuletos.CosteExtraGolpe(Elemento.Hielo) == A.escarchaEstaminaGolpe
                      && EfectosAmuletos.CosteExtraGolpe(Elemento.Fuego) == 0f, "Cristal de escarcha: congela antes y cada golpe con escarcha cansa más");
            Poner(EfectosAmuletos.GarraAmarilla);
            Comprobar(Casi(EfectosAmuletos.MultSagrado, A.garraSagrado), "Garra amarilla: el sagrado aturde más a menudo");
            st.Llenar();
            st.Spend(50f);
            float antesEst = st.Current;
            EfectosAmuletos.AlAturdirConSagrado();
            yield return new WaitForSeconds(1.3f);
            Comprobar(Casi(st.Current, antesEst), $"Garra amarilla (desventaja): la estamina sigue parada 1 s más ({antesEst:0} -> {st.Current:0})");
            Poner(EfectosAmuletos.Lazo);
            Comprobar(Casi(EfectosAmuletos.MultDrenaje, A.lazoDrenaje), "Lazo nocturno: drena más con oscuridad");

            // ---- Ataque (multiplicadores).
            Poner(EfectosAmuletos.Bestia, EfectosAmuletos.ManoOsario, EfectosAmuletos.ColmilloCurvo, EfectosAmuletos.Rama);
            Comprobar(Casi(EfectosAmuletos.MultDano(true, false, false, Elemento.Hielo, 0.5f, 99f), A.ultimoGolpeCombo)
                      && Casi(EfectosAmuletos.MultDano(false, true, false, Elemento.Hielo, 0.5f, 99f), A.aereoYPicado)
                      && Casi(EfectosAmuletos.MultDano(false, false, true, Elemento.Hielo, 0.5f, 99f), A.trasParry)
                      && Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Hielo, 0.5f, 0.2f), A.trasEsquiva)
                      && Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Hielo, 0.5f, 2f), 1f),
                      "Cráneo de bestia, Mano del osario, Colmillo curvo y Rama de hueso: cada uno en su momento");
            Poner(EfectosAmuletos.Corona, EfectosAmuletos.GarraSombra, EfectosAmuletos.ColmilloColgante);
            Comprobar(Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Hielo, 1f, 99f), A.vidaLlena)
                      && Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Hielo, 0.1f, 99f), A.vidaBaja)
                      && Casi(EfectosAmuletos.MultDano(false, false, false, Elemento.Ninguno, 0.5f, 99f), A.sinImbuir),
                      "Corona rota, Garra de sombra y Colmillo colgante");
            Poner(EfectosAmuletos.Bota);
            Comprobar(EfectosAmuletos.OndaPicado, "Bota del caminante: onda al caer en picado");
            Poner(EfectosAmuletos.Gema);
            int g1 = EfectosAmuletos.CuraAlGolpear(), g2 = EfectosAmuletos.CuraAlGolpear();
            Comprobar(g1 == A.gemaCura && g2 == 0, "Gema de sangre: cura al golpear, con espera");

            // ---- Defensa (con dano de verdad; entre golpe y golpe, hasta que acabe la invulnerabilidad).
            p.CurarCompleto();
            Amuletos.Reiniciar();
            int v = p.VidaActual;
            var r1 = p.TakeDamage(40, blanco, PlayerControler.TipoDano.Fisico);
            int sinGuardian = v - p.VidaActual;
            yield return SinInvulnerable();
            p.CurarCompleto();
            Poner(EfectosAmuletos.Guardian);
            v = p.VidaActual;
            var r2 = p.TakeDamage(40, blanco, PlayerControler.TipoDano.Fisico);
            int conGuardian = v - p.VidaActual;
            Comprobar(r1 == PlayerControler.ResultadoDano.Recibido && r2 == PlayerControler.ResultadoDano.Recibido
                      && conGuardian == Mathf.Max(1, Mathf.RoundToInt(sinGuardian * A.guardianFisico)),
                      $"Cráneo del guardián: golpe físico {sinGuardian} -> {conGuardian} (los dos golpes entran: {r1}, {r2})");
            Poner(EfectosAmuletos.Amatista);
            Comprobar(Casi(EfectosAmuletos.MultDanoRecibido(true, Elemento.Ninguno), A.amatistaMagico) && Casi(EfectosAmuletos.MultDanoRecibido(false, Elemento.Ninguno), 1f),
                      "Anillo de amatista hueca: solo el daño mágico");
            yield return SinInvulnerable();
            p.CurarCompleto();
            Poner(EfectosAmuletos.Muneco);
            int vb = blanco.CurrentHealth;
            var rm = p.TakeDamage(40, blanco, PlayerControler.TipoDano.Fisico);
            Comprobar(rm == PlayerControler.ResultadoDano.Recibido && blanco.CurrentHealth < vb, $"Muñeco de trapo: el atacante pierde {vb - blanco.CurrentHealth} de vida");
            yield return SinInvulnerable();
            p.CurarCompleto();
            Poner(EfectosAmuletos.Yelmo);
            var ry = p.TakeDamage(5, blanco, PlayerControler.TipoDano.Fisico);
            Comprobar(ry == PlayerControler.ResultadoDano.Recibido && !(bool)typeof(PlayerControler).GetField("isKnocked", Priv).GetValue(p), "Yelmo abollado: un golpe leve no te empuja");
            yield return SinInvulnerable();
            p.CurarCompleto();
            Poner(EfectosAmuletos.Calavera);
            p.TakeDamage(9999, blanco, PlayerControler.TipoDano.Fisico);
            Comprobar(p.VidaActual == 1 && EfectosAmuletos.CalaveraGastada, $"Calavera votiva: un golpe mortal te deja con 1 de vida ({p.VidaActual})");
            EfectosAmuletos.RecargarCalavera();
            Poner(EfectosAmuletos.Vela);
            Comprobar(Casi(EfectosAmuletos.MultEstadosRecibidos, A.velaEstados), "Vela del réquiem: los estados te suben más despacio");
            Poner(EfectosAmuletos.Pluma);
            Comprobar(Casi(EfectosAmuletos.MultEsquiva, A.plumaEsquiva), "Pluma de fénix: esquiva más larga");
            yield return SinInvulnerable();
            p.CurarCompleto();

            // ---- Parry y guardia.
            Poner(EfectosAmuletos.Vigia);
            typeof(PlayerControler).GetField("ultimaPulsacionBloqueo", Priv).SetValue(p, -10f);
            typeof(PlayerControler).GetMethod("LevantarGuardia", Priv).Invoke(p, null);
            float ventana = (float)typeof(PlayerControler).GetField("ventanaParryActual", Priv).GetValue(p);
            float base0 = (float)typeof(PlayerControler).GetField("parryWindow", Priv).GetValue(p);
            Comprobar(Casi(ventana, base0 * A.vigiaVentanaParry), $"Ojos del vigía: ventana de parry {base0:0.00} -> {ventana:0.00} s");
            Poner(EfectosAmuletos.BrazoSeco);
            Comprobar(Casi(EfectosAmuletos.MultCosteBloqueo, A.brazoBloqueo), "Brazo seco: bloquear cansa menos");
            Poner(EfectosAmuletos.Ciervo);
            mana.Gastar(30f);
            float m0 = mana.Actual;
            typeof(PlayerControler).GetMethod("Parry", Priv).Invoke(p, new object[] { blanco });
            Comprobar(Casi(mana.Actual - m0, A.ciervoManaParry), $"Tótem de ciervo: un parry devuelve {mana.Actual - m0:0} de maná");
            yield return new WaitForSecondsRealtime(0.5f);

            // ---- Estamina, mana y otros.
            Poner(EfectosAmuletos.Raiz, EfectosAmuletos.Pie, EfectosAmuletos.Guante);
            Comprobar(Casi(EfectosAmuletos.MultRegenEstamina, A.raizRecuperacion) && Casi(EfectosAmuletos.MultCosteCorrer, A.pieCosteCorrer)
                      && Casi(EfectosAmuletos.MultVelocidadCorrer, A.pieVelocidad) && Casi(EfectosAmuletos.MultCosteParedCornisa, A.guanteParedCornisa),
                      "Raíz retorcida, Pie del peregrino y Guante de cuero");
            Poner(EfectosAmuletos.Marea);
            mana.Gastar(30f);
            float m1 = mana.Actual;
            yield return new WaitForSeconds(3.2f);
            Comprobar(mana.Actual - m1 >= 2.5f, $"Anillo de la marea: el maná vuelve solo ({m1:0} -> {mana.Actual:0} en 3 s)");
            Poner(EfectosAmuletos.Llave, EfectosAmuletos.Mandibula);
            Comprobar(Casi(EfectosAmuletos.MultPrecios, A.llavePrecios) && Casi(EfectosAmuletos.MultRuido, A.mandibulaRuido), "Llave del osario y Mandíbula del mudo");
            Poner("amuleto_u10", "amuleto_u30", "amuleto_u39");
            Comprobar(Casi(EfectosAmuletos.MultDano(true, true, true, Elemento.Fuego, 1f, 0f), 1f) && EfectosAmuletos.MultPrecios == 1f && EfectosAmuletos.MultRuido == 1f
                      && p.VidaMaxima == vida0, "los 3 de exploración no hacen nada todavía");

            Amuletos.Reiniciar();
            yield return null;
            Comprobar(p.VidaMaxima == vida0 && Casi(st.Max, est0) && ReservaPociones.Get().Maximo == frascos0, "sin amuletos, todo vuelve a lo normal");
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[EFECTOS] RESULTADO: {ok} OK, {fallos} FALLOS");
            EditorApplication.ExitPlaymode();
        }
    }
}
