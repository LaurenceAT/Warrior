using System.Collections;
using UnityEngine;

// El Black Knight (7): ARMA ENCENDIDA (desde la fase 1).
//
// Hace su pose y la espada (y su silueta) se tinen de naranja rojizo. El
// siguiente golpe que te acierte te prende del todo: quemadura al instante, no
// poco a poco, y pega algo mas fuerte. Con parry no pasa nada y ademas se le
// apaga el arma: el parry es la respuesta. Si no llega a golpear, se le apaga
// sola al cabo de unos segundos.
public partial class JefeBlackKnight
{
    private bool armaEncendida;
    private float finArmaEncendida, finEnfriamientoEncender;

    public bool ArmaEncendida => armaEncendida;
    public int VecesEncendida { get; private set; }
    public int GolpesEncendidos { get; private set; }
    public int ApagadasPorParry { get; private set; }

    private IEnumerator EncenderArma()
    {
        finEnfriamientoEncender = Time.time + EnfriamientoConPresion(ajustes.enfriamientoEncender);
        BuscarPlayer();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipBuff, true);
        Sonar("jefe_revivir", 0.7f, 1.3f);
        float aviso = ajustes.avisoEncender / Mathf.Max(0.5f, Desafio.MultVelocidad);
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            ritmo.Destello(ajustes.colorEncendida, 0.2f + 0.5f * (t / aviso));
            if (anim.Terminado) anim.Reproducir(ClipBuff, true);
            Parar();
            MirarYa();
            yield return null;
        }
        armaEncendida = true;
        VecesEncendida++;
        finArmaEncendida = Time.time + ajustes.duracionEncendida;
        Sonar("jefe_tajo_fuerte", 0.7f, 1.3f);
        ScreenFlash.Destello(new Color(ajustes.colorEncendida.r, ajustes.colorEncendida.g, ajustes.colorEncendida.b, 0.18f), 0.25f);
        TextoFlotante.Mostrar(ajustes.textoEncendida, (Vector2)transform.position + Vector2.up * 2.2f, ajustes.colorEncendida, 1f);
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 1.2f, 16, ajustes.colorEncendida, Color.white,
                            new Vector2(1.5f, 4f), -0.7f, new Vector2(0.05f, 0.11f), new Vector2(0.3f, 0.7f));
        while (!anim.Terminado) yield return null;
        yield return Quieto(0.25f / Vel);
    }

    // Mientras dure, late en naranja; al pasarse el tiempo, se apaga sola.
    private void ActualizarArma()
    {
        if (!armaEncendida) return;
        if (Time.time >= finArmaEncendida || salud.Muerto || cambiando) { ApagarArma(); return; }
        // El latido no pisa el aviso de un golpe (ese lo maneja RitmoCombo).
        if (ritmo != null && GolpesActivos == 0 && GolpeEnCurso == null)
            ritmo.Destello(ajustes.colorEncendida, 0.3f + 0.2f * Mathf.Sin(Time.time * 7f));
    }

    private void ApagarArma()
    {
        if (!armaEncendida) return;
        armaEncendida = false;
        if (ritmo != null) ritmo.Destello(ajustes.colorEncendida, 0f);
    }

    // Lo que pasa con un golpe encendido: si te alcanza, te prende del todo; si
    // lo paras con parry, se le apaga y te libras.
    private void GolpeConArma(PlayerControler.ResultadoDano? r)
    {
        if (!armaEncendida || !r.HasValue) return;
        if (r.Value == PlayerControler.ResultadoDano.Parry)
        {
            ApagadasPorParry++;
            Sonar("jefe_parry", 0.8f, 1.4f);
            TextoFlotante.Mostrar("Apagada", (Vector2)transform.position + Vector2.up * 2.2f, new Color(0.7f, 0.85f, 1f), 1f);
            ApagarArma();
            return;
        }
        if (r.Value == PlayerControler.ResultadoDano.Ignorado) return;
        GolpesEncendidos++;
        ApagarArma();
    }
}
