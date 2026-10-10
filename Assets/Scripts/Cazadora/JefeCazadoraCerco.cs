using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cerco de ilusiones: tras "Voy a derrotarte" (apertura) y, desde la barra 2, en
// la pelea (con enfriamiento). Te rodea: una copia a un lado, dos arriba en
// diagonal (y una mas encima desde la barra 2) y ella al otro lado.
//   1. Aparecen todas con un destello.
//   2. Atacan de una en una: las copias en un orden distinto cada vez y ella
//      siempre la ultima. Antes de cada una, un destello marca quien va ahora y
//      suena su aviso con un tono propio (se sigue de oido).
//   3. Su linea roja te apunta y el cerco te sigue si te mueves; un instante
//      antes del golpe la linea se fija: es el momento de bloquear.
//   4. Las de arriba se paran mires hacia donde mires (golpe desde arriba); las
//      de los lados, mirandolas.
// Parar con parry todas las copias: el cerco se deshace, ella pierde parte de su
// barra y queda aturdida (sin golpe de la original). Cada parry seguido a una
// copia suena un poco mas agudo. Todo editable en Ajustes Cazadora.
public partial class JefeCazadora
{
    private float tCerco = -99f;
    private string ordenCerco = "";

    // Para la depuracion y las pruebas.
    public bool EnCerco { get; private set; }
    public Vector2? CercoAtacante { get; private set; }
    public bool CercoLineaFija { get; private set; }
    public int PremiosCerco { get; private set; }
    public int CopiasUltimoCerco { get; private set; }
    public static event System.Action AlPremioCerco;
    // Lo que va pasando en el cerco (para las pruebas): "destello:3", "linea:3",
    // "fija:3", "golpe:3:Parry", "premio"... (3 = puesto; O = ella).
    public readonly List<(float t, string que)> RegistroCerco = new List<(float, string)>();
    public LineaApunte LineaCerco { get; private set; }
    private void Apuntar(string que) => RegistroCerco.Add((Time.time, que));

    private class Puesto
    {
        public Vector2 desplazamiento;
        public bool arriba, original;
        public IlusionCazadora il;
        public int tono;
    }

    // Tras "Voy a derrotarte": el cerco abre la pelea.
    public IEnumerator CercoApertura()
    {
        PlayerControler p = PC;
        if (p == null) yield break;
        noLetalAccion = p.VidaActual >= p.VidaMaxima;
        parado = false;
        AnunciarAtaque("cerco");
        yield return CercoDeIlusiones();
        if (parado) { parado = false; yield return Aturdida(0.75f, "Aturdida", false); }
    }

    private bool CercoDisponible => fase >= 1 && Time.time - tCerco >= ajustes.enfriamientoCerco;

    private IEnumerator CercoDeIlusiones()
    {
        PlayerControler p = PC;
        if (p == null) yield break;
        tCerco = Time.time;
        EnCerco = true;
        // Si empiezas el cerco con la vida llena, ningun golpe del cerco te mata.
        noLetalAccion = p.VidaActual >= p.VidaMaxima;
        RegistroCerco.Clear();
        Apuntar("inicio");
        elementoAtaque = Elemento.Ninguno;
        bool dificil = Desafio.Activo && Desafio.Dificil;
        float apuntar = Mathf.Max(ajustes.avisoMinimoCerco, dificil ? ajustes.apuntarCercoDificil : ajustes.apuntarCerco);
        float fijo = Mathf.Max(ajustes.avisoMinimoCerco, dificil ? ajustes.fijoCercoDificil : ajustes.fijoCerco);
        Vector2 pausa = dificil ? ajustes.pausaCercoDificil : ajustes.pausaCerco;
        int dano = Mathf.Max(1, Mathf.RoundToInt(p.VidaMaxima * ajustes.danoCerco));

        // El lado de ella: a tu derecha, salvo que la pared no deje sitio.
        Vector2 pp = p.transform.position;
        int lado = 1;
        float sitioDer = cuerpo.XMax - pp.x, sitioIzq = pp.x - cuerpo.XMin;
        if (sitioDer < ajustes.distanciaCerco * 0.8f && sitioIzq > sitioDer) lado = -1;

        var puestos = new List<Puesto>();
        float d = ajustes.distanciaCerco;
        Vector2 arriba = ajustes.cercoArriba;
        var copias = new List<Puesto>
        {
            new Puesto { desplazamiento = new Vector2(-lado * d, 0f) },
            new Puesto { desplazamiento = new Vector2(-lado * arriba.x, arriba.y), arriba = true },
            new Puesto { desplazamiento = new Vector2(lado * arriba.x, arriba.y), arriba = true },
        };
        int cuantas = Mathf.Clamp(ajustes.copiasCerco, 1, 3);
        copias = copias.GetRange(0, cuantas);
        if (fase >= 1 && ajustes.copiaExtraFase2) copias.Add(new Puesto { desplazamiento = new Vector2(0f, ajustes.alturaCopiaExtra), arriba = true });
        for (int i = 0; i < copias.Count; i++) copias[i].tono = i;
        Puesto ella = new Puesto { desplazamiento = new Vector2(lado * d, 0f), original = true };
        CopiasUltimoCerco = copias.Count;

        // Entrada: ella se coloca y aparecen las copias con un destello.
        AlFrase?.Invoke(AjustesCazadora.Una(ajustes.frasesCerco));
        yield return Fundido(0f, 0.12f);
        cuerpo.Colocar(new Vector2(PosicionPuesto(ella, pp).x, suelo));
        MirarA(pp.x);
        yield return Fundido(1f, 0.12f);
        foreach (Puesto c in copias)
        {
            c.il = SacarIlusion();
            if (c.il == null) continue;
            Vector2 pos = PosicionPuesto(c, pp);
            c.il.Aparecer(this, pos, pp.x < pos.x ? -1 : 1, ajustes.colorIlusion, false, 60f, Oscuridad);
            c.il.anim.Pose(c.arriba ? "tajoAbajo" : "cruce", 0);
            c.il.anim.Destello(Color.white, 0.9f, 0.18f);
        }
        cuerpoAnim.Destello(Color.white, 0.9f, 0.18f);
        puestos.AddRange(copias);
        yield return Esperar(0.4f);

        // Orden de las copias: al azar, nunca el mismo que la vez anterior.
        List<Puesto> orden = Barajar(copias);
        puestos.Clear();
        puestos.AddRange(orden);
        puestos.Add(ella);

        bool perfecto = true;
        int seguidos = 0;
        for (int i = 0; i < puestos.Count; i++)
        {
            Puesto a = puestos[i];
            if (p == null || p.VidaActual <= 0) break;
            if (!a.original && (a.il == null || !a.il.Viva)) { perfecto = false; seguidos = 0; continue; }

            // Quien va ahora: destello y su aviso con un tono propio.
            string quien = a.original ? "O" : a.tono.ToString();
            Apuntar("destello:" + quien);
            float tonoAviso = a.original ? ajustes.tonoOriginalCerco : Tono(ajustes.tonosCerco, a.tono);
            Sonar(a.original ? "aviso_pesado" : "aviso_ligero", 0.8f, tonoAviso);
            for (float t = 0f; t < ajustes.destelloCerco && p != null; t += Time.deltaTime)
            {
                Seguir(puestos, i, p);
                Destellar(a, Color.white, 0.9f);
                CercoAtacante = PosicionDe(a);
                yield return null;
            }

            // Su linea te apunta y el cerco te sigue...
            if (a.original) cuerpoAnim.Pose("cruce", 0);
            else a.il.anim.Pose(a.arriba ? "tajoAbajo" : "cruce", 0);
            if (p == null) break;
            LineaApunte linea = LineaApunte.Poner(PosicionDe(a) + Vector2.up * 0.8f, (Vector2)p.transform.position + Vector2.up * 0.6f, ajustes.colorInstakill);
            CercoLineaFija = false;
            LineaCerco = linea;
            Apuntar("linea:" + quien);
            for (float t = 0f; t < apuntar && p != null; t += Time.deltaTime)
            {
                Seguir(puestos, i, p);
                if (!a.original) a.il.Mirar(p.transform.position.x < a.il.Posicion.x ? -1 : 1);
                else MirarA(p.transform.position.x);
                CercoAtacante = PosicionDe(a);
                linea.Mover(PosicionDe(a) + Vector2.up * 0.8f, (Vector2)p.transform.position + Vector2.up * 0.6f);
                Destellar(a, new Color(1f, 0.55f, 0.45f), 0.35f);
                yield return null;
            }
            // ...y se fija: ya no te sigue.
            if (p == null) { linea.Quitar(); LineaCerco = null; break; }
            linea.Fijar();
            CercoLineaFija = true;
            Apuntar("fija:" + quien);
            Sonar("marca", 0.55f, tonoAviso);
            Vector2 objetivo = linea.Hasta - Vector2.up * 0.6f;
            for (float t = 0f; t < fijo; t += Time.deltaTime)
            {
                Destellar(a, new Color(1f, 0.4f, 0.35f), 0.55f);
                yield return null;
            }
            linea.Quitar();
            LineaCerco = null;
            CercoLineaFija = false;
            Apuntar("ataca:" + quien);

            if (a.original)
            {
                CercoAtacante = Pos;
                MirarA(objetivo.x);
                int dir = objetivo.x >= Pos.x ? 1 : -1;
                float destino = cuerpo.Limitar(objetivo.x + dir * 1.2f);
                yield return CruceCuerpo(destino, ajustes.golpeCerco, DanoCerco(p, dano), false, false, false);
                Apuntar("golpe:O:" + (ultimoResultado.HasValue ? ultimoResultado.Value.ToString() : "Nada"));
                break;
            }

            // Una copia: su embestida. Cada parry seguido sube un poco el tono.
            float tonoParry = Tono(ajustes.tonosParryCerco, seguidos);
            PlayerControler.ResultadoDano? r = null;
            yield return EmbestidaCopia(a, objetivo, DanoCerco(p, dano), tonoParry, res => r = res);
            Apuntar("golpe:" + quien + ":" + (r.HasValue ? r.Value.ToString() : "Nada"));
            if (r == PlayerControler.ResultadoDano.Parry) seguidos++;
            else { perfecto = false; seguidos = 0; }
            if (a.il != null && a.il.Viva) a.il.Deshacer();

            // Todas las copias paradas: premio, sin golpe de la original.
            if (perfecto && i == orden.Count - 1)
            {
                yield return PremioCerco();
                break;
            }

            float espera = Mathf.Max(ajustes.pausaMinimaCerco, Random.Range(pausa.x, pausa.y));
            for (float t = 0f; t < espera && p != null && p.VidaActual > 0; t += Time.deltaTime)
            {
                Seguir(puestos, i + 1, p);
                yield return null;
            }
        }

        foreach (Puesto c in copias) if (c.il != null && c.il.Viva) c.il.Deshacer();
        CercoAtacante = null;
        EnCerco = false;
        Apuntar("fin");
    }

    // La embestida de una copia hacia donde quedo fijada la linea (las de arriba
    // caen en diagonal: se paran mires hacia donde mires).
    private IEnumerator EmbestidaCopia(Puesto a, Vector2 objetivo, int dano, float tonoParry, System.Action<PlayerControler.ResultadoDano?> fin)
    {
        IlusionCazadora il = a.il;
        Vector2 inicio = il.Posicion;
        Vector2 dir = (objetivo - inicio).sqrMagnitude > 0.01f ? (objetivo - inicio).normalized : Vector2.right;
        Vector2 hasta = objetivo + dir * 1.2f;
        hasta.y = Mathf.Max(suelo, hasta.y);
        hasta.x = Mathf.Clamp(hasta.x, arena.xMin + 0.4f, arena.xMax - 0.4f);
        il.anim.Pose(a.arriba ? "tajoAbajo" : "cruce", 1);
        il.anim.ColorEfecto(ajustes.colorIlusion);
        il.Mover(hasta, ajustes.golpeCerco);
        Sonar("tajo", 0.75f, Tono(ajustes.tonosCerco, a.tono));
        PlayerControler.ResultadoDano? r = null;
        Vector2 anterior = il.Posicion;
        float siguiente = 0f;
        while (il.Viva && il.Moviendose)
        {
            yield return null;
            CercoAtacante = il.Posicion;
            if (Time.time >= siguiente)
            {
                siguiente = Time.time + ajustes.cadaEstela;
                EstelaFantasma.Lanzar(il.anim.SpriteCuerpo, il.anim.cuerpo.transform.position, il.anim.Volteado, new Color(0.55f, 0.75f, 1f, 0.35f), ajustes.vidaEstela);
            }
            if (r.HasValue) continue;
            // La caja barre desde donde estaba hasta donde esta (no se salta al player).
            Vector2 actual = il.Posicion;
            Vector2 min = Vector2.Min(anterior, actual) - new Vector2(0.45f, 0f);
            Vector2 max = Vector2.Max(anterior, actual) + new Vector2(0.45f, 1.4f);
            anterior = actual;
            Vector2 centro = (min + max) * 0.5f, tam = max - min;
            if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.magenta, 0.03f);
            PlayerControler.TonoSiguienteParry = tonoParry;
            var res = GolpeCazadora.Caja(centro, tam, dano, il, PlayerControler.TipoDano.Fisico, EstadoPlayer.Ninguno, 0f, noLetalAccion,
                                         false, a.arriba, a.arriba ? float.NaN : inicio.x);
            PlayerControler.TonoSiguienteParry = 1f;
            if (!res.HasValue || res.Value == PlayerControler.ResultadoDano.Ignorado) continue;
            r = res;
            if (res.Value == PlayerControler.ResultadoDano.Recibido) CamaraCazadora.Sacudir(ajustes.temblorPesado);
            if (res.Value == PlayerControler.ResultadoDano.Parry) { il.Deshacer(); CamaraCazadora.Lenta(ajustes.lentaParry, ajustes.lentaParryTiempo); }
        }
        fin?.Invoke(r);
    }

    // Parry a todas las copias: el cerco se deshace, ella pierde parte de su barra
    // (dano fijo, sin elemento; nunca la mata ni le vacia la barra) y queda
    // aturdida. Una sola vez por cerco.
    private IEnumerator PremioCerco()
    {
        CercoAtacante = null;
        QuitarIlusiones();
        cuerpo.CortarTrayecto();
        cuerpo.Detener();
        cuerpoAnim.Destello(Color.white, 1f, 0.25f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.8f, new Color(0.85f, 0.92f, 1f, 0.95f), 26, 1.4f);
        CamaraCazadora.Sacudir(ajustes.temblorPesado);
        Sonar("escudo_roto", 0.9f);
        // Con el escudo puesto, el escudo la protege: el aturdimiento si entra.
        if (!escudoActivo) salud.QuitarVidaFija(Mathf.RoundToInt(salud.MaxHealth * ajustes.premioCerco), 1);
        PremiosCerco++;
        AlFrase?.Invoke(AjustesCazadora.Una(ajustes.frasesPremioCerco));
        AlPremioCerco?.Invoke();
        PremioCercoConseguido();
        aturdimientoPendiente = 0f;
        Apuntar("premio");
        yield return Aturdida(ajustes.aturdidaPremioCerco, "Aturdida", false);
        Apuntar("aturdida_fin");
    }

    // El logro (oculto) y el consejo secreto de la Bitacora. Solo con la Cazadora
    // desbloqueada de verdad (no con el codigo "warrior").
    private void PremioCercoConseguido()
    {
        FichaJefe f = FichaJefe.DeId("blind_huntress");
        if (f == null || !f.DesbloqueadoDeVerdad) return;
        Bitacora.ConsejoSecreto(f, "cerco");
        Logros.Comprobar(FichaLogro.Condicion.Especial, "cerco_perfecto");
    }

    // ------------------------------------------------------------------ Ayudas

    private List<Puesto> Barajar(List<Puesto> copias)
    {
        List<Puesto> l = new List<Puesto>(copias);
        for (int intento = 0; intento < 12; intento++)
        {
            for (int i = l.Count - 1; i > 0; i--) { int k = Random.Range(0, i + 1); (l[i], l[k]) = (l[k], l[i]); }
            string clave = "";
            foreach (Puesto x in l) clave += x.tono;
            if (l.Count < 2 || clave != ordenCerco) { ordenCerco = clave; break; }
        }
        return l;
    }

    // Donde va un puesto con el player en "pp" (dentro de la arena y sobre el suelo).
    private Vector2 PosicionPuesto(Puesto a, Vector2 pp)
    {
        float x = Mathf.Clamp(pp.x + a.desplazamiento.x, cuerpo.XMin, cuerpo.XMax);
        float y = a.arriba ? Mathf.Min(arena.yMax - 1.6f, suelo + a.desplazamiento.y) : suelo;
        return new Vector2(x, y);
    }

    private Vector2 PosicionDe(Puesto a) => a.original ? Pos : (a.il != null ? a.il.Posicion : Pos);

    // Los que aun no han atacado siguen al player (suave).
    private void Seguir(List<Puesto> puestos, int desde, PlayerControler p)
    {
        if (p == null) return;
        Vector2 pp = p.transform.position;
        float k = 1f - Mathf.Exp(-ajustes.seguirCerco * Time.deltaTime);
        for (int i = desde; i < puestos.Count; i++)
        {
            Puesto a = puestos[i];
            Vector2 meta = PosicionPuesto(a, pp);
            if (a.original) cuerpo.Colocar(new Vector2(Mathf.Lerp(Pos.x, meta.x, k), suelo));
            else if (a.il != null && a.il.Viva && !a.il.Moviendose) a.il.transform.position = Vector2.Lerp(a.il.Posicion, meta, k);
        }
    }

    private void Destellar(Puesto a, Color c, float fuerza)
    {
        if (a.original) cuerpoAnim.Destello(c, fuerza, 0.05f);
        else if (a.il != null && a.il.Viva) a.il.anim.Destello(c, fuerza, 0.05f);
    }

    // Si empezaste el cerco con la vida llena, ningun golpe suyo te deja por
    // debajo de 1 (tampoco el ultimo, aunque ya te quede 1).
    private int DanoCerco(PlayerControler p, int dano) =>
        noLetalAccion && p != null ? Mathf.Clamp(dano, 0, Mathf.Max(0, p.VidaActual - 1)) : dano;

    private static float Tono(float[] tonos, int i) => tonos == null || tonos.Length == 0 ? 1f : tonos[Mathf.Clamp(i, 0, tonos.Length - 1)];
}
