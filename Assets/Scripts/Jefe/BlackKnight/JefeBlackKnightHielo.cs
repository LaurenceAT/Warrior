using System.Collections;
using UnityEngine;

// El Black Knight (9): ESTALLIDO DE ESCARCHA.
//
// Congelarlo ya no es un regalo. En cuanto se queda helado, aguanta un instante
// y revienta el hielo: una onda que manda por los aires a quien este cerca y le
// quita la congelacion de encima. Si estas lejos cuando revienta, no te toca.
//
// No se puede parar ni bloquear (es una explosion): hay que no estar al lado.
// Numeros en la ficha (Ajustes Black Knight).
public partial class JefeBlackKnight
{
    private EstadosEnemigo estados;
    private bool estallando;
    private float finEnfriamientoEstallido;

    public bool Estallando => estallando;
    public int EstallidosHielo { get; private set; }

    // Cada fotograma: si le han congelado, se prepara para reventar.
    private void ActualizarHielo()
    {
        if (estallando || salud.Muerto || cambiando || !ajustes.estallidoAlCongelar) return;
        if (estados == null) estados = GetComponent<EstadosEnemigo>();
        if (estados == null || !estados.Congelado) return;
        if (Time.time < finEnfriamientoEstallido) return;
        StartCoroutine(EstallidoHielo());
    }

    private IEnumerator EstallidoHielo()
    {
        estallando = true;
        finEnfriamientoEstallido = Time.time + ajustes.enfriamientoEstallido;

        // 1. Un instante quieto, con el hielo crujiendo y creciendo: te da tiempo
        //    justo a apartarte si estas encima.
        float aviso = Mathf.Max(0.1f, ajustes.avisoEstallido);
        Sonar("hielo_congelar", 0.8f, 0.7f);
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            float k = t / aviso;
            ritmo.Destello(ajustes.colorEstallido, 0.3f + 0.6f * k * (0.5f + 0.5f * Mathf.Sin(t * (10f + 30f * k))));
            if (Random.value < 0.35f)
                ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * Random.Range(0.3f, 1.6f), 2,
                                    ajustes.colorEstallido, Color.white, new Vector2(0.8f, 2.5f), 1.5f,
                                    new Vector2(0.05f, 0.1f), new Vector2(0.2f, 0.45f));
            yield return null;
        }
        ritmo.Destello(ajustes.colorEstallido, 0f);

        // 2. Revienta: se quita el hielo y empuja a quien tenga al lado.
        if (estados != null) estados.RomperCongelacion();
        EstallidosHielo++;
        Sonar("hielo_romper", 1f, 0.85f);
        CamaraDinamica.Sacudir(1.2f);
        ScreenFlash.Destello(new Color(ajustes.colorEstallido.r, ajustes.colorEstallido.g, ajustes.colorEstallido.b, 0.25f), 0.3f);
        TextoFlotante.Mostrar(ajustes.textoEstallido, (Vector2)transform.position + Vector2.up * 2.4f, ajustes.colorEstallido, 1f);
        OndaEscarchaBK.Crear(transform.position, ajustes.radioEstallido * Esc, ajustes.colorEstallido);
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.9f, 26, ajustes.colorEstallido, Color.white,
                            new Vector2(3f, 8f), 0.8f, new Vector2(0.06f, 0.13f), new Vector2(0.3f, 0.7f));

        Vector2 caja = new Vector2(ajustes.radioEstallido * 2f, ajustes.radioEstallido * 1.6f) * Esc;
        PlayerControler p = GolpeCazadora.PlayerEn((Vector2)transform.position + Vector2.up * 0.8f, caja);
        if (p != null)
        {
            int dano = Mathf.RoundToInt(ajustes.danoEstallido * Desafio.MultDano);
            PlayerControler.ResultadoDano r = GolpeCazadora.Aplicar(p, dano, this, PlayerControler.TipoDano.Magico,
                                                                   EstadoPlayer.Ninguno, 0f, true, true);
            if (r != PlayerControler.ResultadoDano.Ignorado)
            {
                int lado = p.transform.position.x >= transform.position.x ? 1 : -1;
                p.Derribar(new Vector2(lado * ajustes.empujonEstallido.x, ajustes.empujonEstallido.y), true);
            }
        }
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar((Vector2)transform.position + Vector2.up * 0.8f, caja, ajustes.colorEstallido, 0.5f);

        // 3. Sigue a lo suyo: la congelacion ya no le frena.
        anim.Reproducir(ClipQuieto, true);
        yield return EsperarCielo(0.2f);
        estallando = false;
    }
}

// La onda del estallido: un aro de escarcha que se abre de golpe y se apaga.
// Dibujada por codigo, como el resto de sus efectos.
public class OndaEscarchaBK : MonoBehaviour
{
    private SpriteRenderer sr;
    private float edad, radio;
    private const float Vida = 0.45f;

    public static OndaEscarchaBK Crear(Vector2 centro, float radio, Color color)
    {
        GameObject go = new GameObject("OndaEscarchaBK");
        go.transform.position = centro + Vector2.up * 0.6f;
        OndaEscarchaBK o = go.AddComponent<OndaEscarchaBK>();
        o.sr = DibujosCazadora.Renderer(go, "VFX", 50);
        o.sr.sprite = DibujosCazadora.Anillo();
        o.sr.color = color;
        o.radio = Mathf.Max(0.5f, radio);
        PeligrosJefe.Registrar(go);
        return o;
    }

    private void Update()
    {
        edad += Time.deltaTime;
        float k = Mathf.Clamp01(edad / Vida);
        float r = Mathf.Lerp(0.2f, radio * 2f, Mathf.Sqrt(k));
        transform.localScale = new Vector3(r, r * 0.85f, 1f);
        Color c = sr.color;
        c.a = 1f - k;
        sr.color = c;
        if (k >= 1f) Destroy(gameObject);
    }
}
