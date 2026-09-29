using System.Collections;
using UnityEngine;

// Ilusion de la Cazadora: una copia semitransparente, azulada y sin sombra. Ataca
// a la vez que ella (lo dirige la jefa) pero, al golpearla, se deshace en polvo
// sin quitarle vida a la jefa. Tambien hace de Espejismo: un senuelo quieto que
// explota si lo golpeas.
//
// Sale de una reserva: la plantilla va dentro del prefab de la jefa (apagada) y
// se copia solo cuando hacen falta mas.
[RequireComponent(typeof(EnemyHealth))]
public class IlusionCazadora : MonoBehaviour, IModificadorDano
{
    public AnimCazadora anim;
    [Tooltip("Contorno que brilla en la oscuridad de la fase 3.")]
    public SpriteRenderer aura;

    private JefeCazadora jefe;
    private bool viva, espejismo;
    private float finVida;
    private Color tinte;
    private Vector3 desde, hasta;
    private float tMov, durMov, arco;
    private bool moviendo;
    private int mirada = 1;

    public bool Viva => viva && gameObject.activeSelf;
    public bool Espejismo => espejismo;
    public int Mirada => mirada;
    public Vector2 Posicion => transform.position;

    public void Aparecer(JefeCazadora j, Vector2 pos, int dir, Color color, bool esEspejismo, float vida, bool brillar)
    {
        jefe = j;
        viva = true;
        espejismo = esEspejismo;
        finVida = vida > 0f ? Time.time + vida : float.MaxValue;
        tinte = color;
        moviendo = false;
        transform.position = pos;
        Mirar(dir);
        gameObject.SetActive(true);
        anim.cuerpo.color = color;
        anim.Alfa(color.a);
        anim.Mostrar(true);
        anim.Pose("quieto", 0);
        if (aura != null) aura.enabled = brillar;
        StopAllCoroutines();
        StartCoroutine(Fundido(0f, color.a, 0.18f));
        PolvoCazadora.Soltar(pos + Vector2.up * 0.7f, new Color(0.7f, 0.85f, 1f, 0.8f), 10, 0.8f);
        jefe.SonarPublico("ilusion_aparece", 0.55f);
    }

    public void Mirar(int dir)
    {
        if (dir == 0) return;
        mirada = dir > 0 ? 1 : -1;
        Vector3 e = anim.transform.localScale;
        e.x = Mathf.Abs(e.x) * mirada;
        anim.transform.localScale = e;
    }

    // Movimiento solo visual (no tiene cuerpo): un dash o una caida.
    public void Mover(Vector2 destino, float segundos, float alturaArco = 0f)
    {
        desde = transform.position;
        hasta = destino;
        durMov = Mathf.Max(0.01f, segundos);
        tMov = 0f;
        arco = alturaArco;
        moviendo = true;
    }

    public bool Moviendose => moviendo;

    // Se deshace en polvo (golpeada, o al acabar su ataque).
    public void Deshacer(bool polvo = true)
    {
        if (!viva) return;
        viva = false;
        if (polvo)
        {
            PolvoCazadora.Soltar((Vector2)transform.position + Vector2.up * 0.7f, new Color(0.75f, 0.88f, 1f, 0.9f), 22, 1.2f);
            jefe?.SonarPublico("ilusion_deshace", 0.6f);
        }
        StopAllCoroutines();
        if (isActiveAndEnabled) StartCoroutine(Apagar());
        else gameObject.SetActive(false);
    }

    // Le llegan tus golpes: no hace dano a la jefa; la ilusion se deshace (o el
    // espejismo explota).
    public int Modificar(int dano, TipoArma arma)
    {
        if (!viva) return 0;
        if (espejismo) jefe?.EspejismoGolpeado(this);
        Deshacer();
        return 0;
    }

    private void Update()
    {
        if (!viva) return;
        if (Time.time >= finVida) { Deshacer(); return; }
        if (moviendo)
        {
            tMov += Time.deltaTime;
            float u = Mathf.Clamp01(tMov / durMov);
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            Vector3 p = Vector3.LerpUnclamped(desde, hasta, arco != 0f ? u * u : e);
            p.y += arco * 4f * u * (1f - u);
            transform.position = p;
            if (u >= 1f) moviendo = false;
        }
        if (aura != null && aura.enabled)
        {
            aura.sprite = anim.SpriteCuerpo;
            aura.color = new Color(0.55f, 0.75f, 1f, 0.55f + 0.2f * Mathf.Sin(Time.time * 5f));
        }
    }

    private IEnumerator Fundido(float a, float b, float s)
    {
        for (float t = 0f; t < s; t += Time.deltaTime)
        {
            anim.Alfa(Mathf.Lerp(a, b, t / s));
            yield return null;
        }
        anim.Alfa(b);
    }

    private IEnumerator Apagar()
    {
        float a = anim.AlfaActual;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            anim.Alfa(Mathf.Lerp(a, 0f, t / 0.15f));
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
