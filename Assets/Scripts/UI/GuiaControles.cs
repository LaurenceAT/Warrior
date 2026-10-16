using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// Ficha de "Controles y guia" (Resources/GuiaControles): categorias, tarjetas,
// textos, imagenes, tarjetas esenciales (las de la guia del primer desafio) y
// los numeros de la tarjeta pequena. La genera Ronda24; se edita en el Inspector.
//
// En los textos:
//   {Accion} o {Accion/parte}  -> la tecla real de Controls.inputactions
//                                 (p. ej. {Jump}, {Move/left}), con su nombre en espanol.
//   [texto]                    -> se dibuja como una tecla (Q, R, F, Esc... que no
//                                 estan en las acciones).
[CreateAssetMenu(menuName = "Warrior/Guia de controles")]
public class GuiaControles : ScriptableObject
{
    public enum TipoImagen { Cuadros, Rueda, Anillo, Teclas }

    [Serializable]
    public class Cuadro
    {
        public Sprite sprite;
        public Color color = Color.white;
    }

    [Serializable]
    public class Tarjeta
    {
        public string id;
        public string titulo;
        [TextArea(2, 5)] public string texto;
        [Tooltip("Sale en la guia del primer desafio.")]
        public bool esencial;
        public TipoImagen tipo;
        [Tooltip("Cuadros de la imagen (1 = fija; 2 o 3 = se anima en bucle).")]
        public Cuadro[] cuadros = new Cuadro[0];
        [Tooltip("Icono pequeno sobre la imagen (frascos). Opcional.")]
        public Sprite icono;
        [Tooltip("Solo para el tipo Teclas: las teclas que se dibujan.")]
        public string[] teclas = new string[0];
    }

    [Serializable]
    public class Categoria
    {
        public string nombre;
        public Tarjeta[] tarjetas = new Tarjeta[0];
    }

    [Serializable]
    public class NombreTecla
    {
        [Tooltip("Nombre que da Unity (Space, Shift, Left Button...).")]
        public string original;
        public string espanol;
    }

    public string titulo = "CONTROLES Y GUÍA";
    public Categoria[] categorias = new Categoria[0];
    public NombreTecla[] nombresTeclas = new NombreTecla[0];

    [Header("Imagenes")]
    [Tooltip("Cuadros por segundo de las imagenes animadas (tiempo real: funciona en pausa).")]
    public float cuadrosPorSegundo = 3f;

    [Header("Guia del primer desafio")]
    [Tooltip("Tamano de la tarjeta pequena (unidades de una pantalla de 1920x1080).")]
    public Vector2 tamanoPequena = new Vector2(760f, 600f);
    [Tooltip("Posicion de la tarjeta pequena respecto al centro.")]
    public Vector2 posicionPequena = new Vector2(0f, 30f);

    private static GuiaControles cache;
    public static GuiaControles Get()
    {
        if (cache == null) cache = Resources.Load<GuiaControles>("GuiaControles");
        return cache;
    }

    public IEnumerable<Tarjeta> Todas()
    {
        foreach (Categoria c in categorias)
            if (c?.tarjetas != null)
                foreach (Tarjeta t in c.tarjetas) if (t != null) yield return t;
    }

    public List<Tarjeta> Esenciales()
    {
        var l = new List<Tarjeta>();
        foreach (Tarjeta t in Todas()) if (t.esencial) l.Add(t);
        return l;
    }

    // ------------------------------------------------------------------ Teclas

    private static InputActionAsset acciones;

    // Nombre de la tecla de una accion real ("Jump" o "Move/left"), en espanol.
    public string NombreAccion(string accion)
    {
        if (acciones == null) acciones = new Controls().asset;
        string parte = null;
        int barra = accion.IndexOf('/');
        if (barra >= 0) { parte = accion.Substring(barra + 1); accion = accion.Substring(0, barra); }
        InputAction a = acciones.FindAction(accion);
        if (a == null) return accion;
        foreach (InputBinding b in a.bindings)
        {
            if (b.isComposite) continue;
            if (parte != null && !string.Equals(b.name, parte, StringComparison.OrdinalIgnoreCase)) continue;
            if (parte == null && b.isPartOfComposite) continue;
            if (string.IsNullOrEmpty(b.effectivePath) || b.effectivePath.StartsWith("<Gamepad>")) continue;
            return Espanol(InputControlPath.ToHumanReadableString(b.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice));
        }
        return accion;
    }

    public string Espanol(string nombre)
    {
        foreach (NombreTecla n in nombresTeclas)
            if (n != null && string.Equals(n.original, nombre, StringComparison.OrdinalIgnoreCase)) return n.espanol;
        return nombre;
    }

    // Texto listo para mostrar: las acciones con su tecla y las teclas dibujadas.
    // "llano": sin formato (para las pruebas).
    public string Resolver(string texto, bool llano = false)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        string s = Regex.Replace(texto, @"\{([^}]+)\}", m => "[" + NombreAccion(m.Groups[1].Value) + "]");
        return Regex.Replace(s, @"\[([^\]]+)\]", m => llano ? m.Groups[1].Value : Tecla(m.Groups[1].Value));
    }

    // Una tecla dibujada como tecla: caja clara con el nombre en negrita.
    public static string Tecla(string nombre) =>
        "<nobr><mark=#E8D9B833 padding=\"8,8,4,4\"><b><color=#F4E6C8> " + nombre + " </color></b></mark></nobr>";
}
