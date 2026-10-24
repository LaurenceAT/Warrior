using System.Collections.Generic;
using UnityEngine;

// El Impaler (3): se adapta a como juegas, poco a poco y con tope (se reinicia en
// cada pelea; se puede apagar en la ficha), y los golpes extra en Dificil.
//   - Si paras muchos golpes, alarga al azar el retraso entre la senal y el golpe
//     (sosteniendo sus cuadros de pausa: el aviso sigue a la vista y nunca baja
//     del minimo). Solo en los ataques marcados con "admiteRetraso".
//   - Si le pegas seguido en la guardia del contraataque, lo elige mas a menudo;
//     si dejas de hacerlo, vuelve a bajar.
public partial class JefeImpaler
{
    public float RetrasoAprendido { get; private set; }
    public float PesoContraataque { get; private set; } = 1f;
    public float UltimoRetrasoExtra { get; private set; }
    public float TasaParrys { get; private set; }

    // Ultimos ataques o combos: golpes que te alcanzaron y cuantos paraste.
    private readonly Queue<(int alcanzados, int parrys)> observados = new Queue<(int, int)>();

    // Dificil de los desafios (o el de la sala): algun multiplicador por encima de 1.
    public static bool EsDificil => Desafio.MultDano > 1.001f || Desafio.MultVida > 1.001f || Desafio.MultVelocidad > 1.001f;

    // Tras cada ataque o combo.
    private void AprenderParrys()
    {
        if (!ajustes.adaptacionActiva) return;
        int alcanzados = 0, parrys = 0;
        foreach (PlayerControler.ResultadoDano? r in Resultados)
        {
            if (!r.HasValue || r == PlayerControler.ResultadoDano.Ignorado) continue;
            alcanzados++;
            if (r == PlayerControler.ResultadoDano.Parry) parrys++;
        }
        if (alcanzados == 0) return;
        observados.Enqueue((alcanzados, parrys));
        while (observados.Count > ajustes.combosObservados) observados.Dequeue();
        int total = 0, parados = 0;
        foreach (var o in observados) { total += o.alcanzados; parados += o.parrys; }
        TasaParrys = total > 0 ? (float)parados / total : 0f;
        float objetivo = TasaParrys >= ajustes.umbralParrys ? ajustes.topeRetraso : 0f;
        float antes = RetrasoAprendido;
        RetrasoAprendido = Mathf.MoveTowards(RetrasoAprendido, objetivo, ajustes.pasoRetraso);
        if (!Mathf.Approximately(antes, RetrasoAprendido)) AvisarAprendido("retraso");
    }

    // Tras cada guardia del contraataque: los golpes que le diste en ella.
    private void AprenderGuardia(int golpes)
    {
        if (!ajustes.adaptacionActiva) return;
        float antes = PesoContraataque;
        PesoContraataque = golpes >= ajustes.umbralGolpesGuardia
            ? Mathf.Min(ajustes.topePesoContraataque, PesoContraataque + ajustes.pasoPesoContraataque)
            : Mathf.Max(1f, PesoContraataque - ajustes.pasoPesoContraataque);
        if (!Mathf.Approximately(antes, PesoContraataque)) AvisarAprendido("contraataque");
    }

    // Retraso extra de este golpe (al azar hasta lo aprendido).
    private float RetrasoExtra(AjustesImpaler.Ataque a)
    {
        UltimoRetrasoExtra = ajustes.adaptacionActiva && a.admiteRetraso && RetrasoAprendido > 0f ? Random.Range(0f, RetrasoAprendido) : 0f;
        return UltimoRetrasoExtra;
    }

    private float Frecuencia(AjustesImpaler.Ataque a) => a.frecuencia * (a.contraataque ? PesoContraataque : 1f);

    private void AvisarAprendido(string que) =>
        Debug.Log($"[Impaler] Aprende ({que}): parrys {TasaParrys:P0} -> retraso hasta {RetrasoAprendido:0.00} s (tope {ajustes.topeRetraso}); " +
                  $"contraataque x{PesoContraataque:0.0} (tope x{ajustes.topePesoContraataque})");

#if UNITY_EDITOR
    // En la sala de pruebas (con "ver estado"): lo que ha aprendido, en una esquina.
    private GUIStyle estiloAprendido;
    private void OnGUI()
    {
        SalaPruebas sala = SalaPruebas.Actual;
        if (sala == null || !sala.VerEstado || ajustes == null) return;
        if (estiloAprendido == null) estiloAprendido = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperRight };
        string t = !ajustes.adaptacionActiva ? "Impaler: adaptación apagada"
            : $"Impaler aprende:\nparrys {TasaParrys:P0} (umbral {ajustes.umbralParrys:P0}) → retraso hasta {RetrasoAprendido:0.00} s (tope {ajustes.topeRetraso:0.00})\n" +
              $"golpes en su guardia → contraataque x{PesoContraataque:0.0} (tope x{ajustes.topePesoContraataque:0.0})" +
              (EsDificil ? "\nDifícil: golpes extra en los combos" : "");
        Rect r = new Rect(Screen.width - 560f, 60f, 540f, 70f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), t, estiloAprendido);
        GUI.color = Color.white;
        GUI.Label(r, t, estiloAprendido);
    }
#endif
}
