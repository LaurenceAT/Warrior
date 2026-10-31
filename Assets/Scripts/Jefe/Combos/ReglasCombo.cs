using System;
using UnityEngine;

// Peso de un golpe de combo: el ligero avisa en naranja con parpadeo rapido y
// el pesado en rojo con latido lento (asi no depende solo del color).
public enum PesoGolpe { Ligero, Pesado }

// Reglas comunes de los combos con parry continuo (pieza compartida, la usa
// RitmoCombo). Cada jefe la lleva dentro de su ficha: todo se edita alli.
//   - El combo sigue aunque pares sus golpes.
//   - Nunca menos de pausaMinEntreGolpes entre el final de un golpe y el
//     siguiente, ni menos aviso que el minimo de su peso.
//   - Lo que te quita un combo sin parar nada nunca pasa de topeDanoCombo.
//   - Cada parry seguido suena un poco mas agudo y suelta chispas (con reserva).
[Serializable]
public class ReglasCombo
{
    [Header("Avisos")]
    [Tooltip("Aviso minimo de un golpe ligero (segundos).")]
    [Min(0f)] public float avisoMinLigero = 0.35f;
    [Tooltip("Aviso minimo de un golpe pesado (segundos).")]
    [Min(0f)] public float avisoMinPesado = 0.5f;
    [Tooltip("Aviso minimo del ultimo golpe de un combo (segundos).")]
    [Min(0f)] public float avisoMinFinal = 0.5f;
    [Tooltip("Pausa minima entre el final de un golpe y el siguiente (segundos). El jugador machaca si pulsa antes de 0,5 s; con el golpe activo quedan siempre mas de 0,5 s entre golpes.")]
    [Min(0f)] public float pausaMinEntreGolpes = 0.5f;
    public Color colorLigero = new Color(1f, 0.55f, 0.1f, 1f);
    public Color colorPesado = new Color(1f, 0.12f, 0.08f, 1f);
    [Range(0f, 1f)] public float intensidadAviso = 0.6f;
    [Tooltip("Parpadeos por segundo del aviso ligero (rapido).")]
    [Min(0.1f)] public float parpadeoLigero = 9f;
    [Tooltip("Latidos por segundo del aviso pesado (lento y mas fuerte).")]
    [Min(0.1f)] public float parpadeoPesado = 2.5f;

    [Header("Combo")]
    [Tooltip("Golpes seguidos como maximo sin una pausa de recuperacion.")]
    [Min(1)] public int maxGolpesSeguidos = 3;
    [Tooltip("Pausa de recuperacion tras el combo (segundos).")]
    [Min(0f)] public float pausaRecuperacion = 0.8f;
    [Tooltip("Un combo sin parar nada nunca quita mas que esto de tu vida maxima (0,5 = 50 %, ya con el dano de Dificil).")]
    [Range(0.1f, 1f)] public float topeDanoCombo = 0.5f;

    [Header("Parry")]
    [Tooltip("Parada de imagen extra al parar un golpe (segundos, sin escala, menos de 0,08). El jugador ya para la imagen en cada parry: esta solo se nota si la suya ya acabo.")]
    [Range(0f, 0.079f)] public float paradaImagen = 0.04f;
    public Color colorChispas = new Color(1f, 0.85f, 0.45f, 1f);
    public Color colorChispas2 = new Color(1f, 0.45f, 0.15f, 1f);
    [Min(0)] public int chispas = 14;

    [Header("Sonido del parry (sube de tono con cada parry seguido)")]
    [Min(0.1f)] public float tonoInicial = 1f;
    [Min(0f)] public float subidaTono = 0.06f;
    [Min(0.1f)] public float tonoMaximo = 1.5f;
    [Tooltip("Vuelve al tono inicial si te da o bloqueas sin parry.")]
    public bool tonoReiniciaAlFallar = true;
    [Tooltip("Vuelve al tono inicial tras estos segundos sin parry (0 = nunca).")]
    [Min(0f)] public float tonoReiniciaSegundos = 4f;

    public float AvisoMinimo(PesoGolpe p, bool final) =>
        Mathf.Max(p == PesoGolpe.Pesado ? avisoMinPesado : avisoMinLigero, final ? avisoMinFinal : 0f);

    public Color ColorAviso(PesoGolpe p) => p == PesoGolpe.Pesado ? colorPesado : colorLigero;

    public float Parpadeo(PesoGolpe p) => p == PesoGolpe.Pesado ? parpadeoPesado : parpadeoLigero;
}
