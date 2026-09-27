using System;
using System.Collections.Generic;
using UnityEngine;

// Animaciones sacadas de hojas de sprites por cuadricula, sin Animator.
//
// Los packs de enemigos vienen como tiras de fotogramas del mismo tamano. El
// corte automatico de Unity recorta cada fotograma a su contenido y el personaje
// "baila" al animarse; aqui se corta por celdas fijas y todos los fotogramas
// comparten el mismo punto de apoyo (el pivote en los pies).
//
// La IA reproduce clips por nombre y escucha AlFotograma para sincronizar cosas
// con la animacion (el instante del golpe, el disparo...).
public class AnimadorHoja : MonoBehaviour
{
    [Serializable]
    public class Clip
    {
        public string nombre;
        public Texture2D hoja;
        public int anchoCelda = 64;
        public int altoCelda = 64;
        // Fila de la hoja, contando desde arriba.
        public int fila;
        public int primero;
        public int cantidad = 1;
        public float fps = 10f;
        public bool bucle = true;
        // Pivote en proporcion de la celda: donde apoyan los pies.
        public Vector2 pivote = new Vector2(0.5f, 0.4f);
        public float pixelesPorUnidad = 28f;

        // Alternativa a la hoja: un fotograma por textura (los efectos y el jefe
        // vienen asi). Si hay fotogramas, la hoja y la cuadricula no se usan.
        public Texture2D[] fotogramas;
        // Recorte dentro de cada textura, en pixeles desde abajo a la izquierda.
        // Ancho 0 = la textura entera.
        public RectInt recorte;
        // Pivote de cada fotograma (opcional; si falta, se usa "pivote").
        public Vector2[] pivotes;

        [NonSerialized] public Sprite[] sprites;
        public float Duracion => fps > 0f ? cantidad / fps : 0f;
    }

    public SpriteRenderer destino;
    public List<Clip> clips = new List<Clip>();
    // Clip que empieza solo al arrancar (para decorados animados, como el humo).
    public string clipInicial;

    // clip, fotograma. Se lanza al entrar en cada fotograma (tambien el 0).
    public event Action<string, int> AlFotograma;

    private readonly Dictionary<string, Clip> porNombre = new Dictionary<string, Clip>();
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    private Clip actual;
    private float t;
    private int fotograma = -1;
    private float velocidad = 1f;

    public string Actual => actual != null ? actual.nombre : null;
    public int Fotograma => fotograma;
    public bool Terminado => actual != null && !actual.bucle && t * actual.fps >= actual.cantidad;

    private void Awake()
    {
        Preparar();
    }

    // Corta los sprites de todos los clips. Se llama sola al despertar; hay que
    // llamarla a mano si los clips se ponen despues (efectos creados por codigo).
    public void Preparar()
    {
        if (destino == null) destino = GetComponentInChildren<SpriteRenderer>();
        porNombre.Clear();
        foreach (Clip c in clips)
        {
            if (c == null || string.IsNullOrEmpty(c.nombre)) continue;
            bool sueltos = c.fotogramas != null && c.fotogramas.Length > 0;
            if (!sueltos && c.hoja == null) continue;
            if (sueltos) c.cantidad = c.fotogramas.Length;
            c.sprites = Cortar(c);
            porNombre[c.nombre] = c;
        }
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(clipInicial) && actual == null) Reproducir(clipInicial, true);
    }

    public bool Tiene(string nombre) => porNombre.ContainsKey(nombre);

    public float Duracion(string nombre) => porNombre.TryGetValue(nombre, out Clip c) ? c.Duracion : 0f;

    public float Fps(string nombre) => porNombre.TryGetValue(nombre, out Clip c) ? c.fps : 10f;

    // Reproduce un clip. Si ya esta sonando no lo reinicia, salvo que se pida.
    public void Reproducir(string nombre, bool reiniciar = false, float vel = 1f)
    {
        velocidad = vel;
        if (!reiniciar && actual != null && actual.nombre == nombre) return;
        if (!porNombre.TryGetValue(nombre, out Clip c))
        {
            Debug.LogWarning($"[AnimadorHoja] {name} no tiene el clip '{nombre}'.", this);
            return;
        }

        actual = c;
        t = 0f;
        fotograma = -1;
        Avanzar(0f);
    }

    private void Update()
    {
        Avanzar(Time.deltaTime * velocidad);
    }

    private void Avanzar(float dt)
    {
        if (actual == null || actual.sprites == null || actual.sprites.Length == 0) return;

        t += dt;
        int i = Mathf.FloorToInt(t * actual.fps);
        i = actual.bucle ? i % actual.sprites.Length : Mathf.Min(i, actual.sprites.Length - 1);

        if (i == fotograma) return;

        // Si en un paso largo se salta algun fotograma, se avisan todos: si no,
        // un evento de golpe podria perderse con el juego a tirones.
        int desde = fotograma < 0 ? i : fotograma + 1;
        fotograma = i;
        destino.sprite = actual.sprites[i];
        if (AlFotograma == null) return;
        if (desde <= i)
            for (int k = desde; k <= i; k++) AlFotograma(actual.nombre, k);
        else
            AlFotograma(actual.nombre, i);
    }

    private static Sprite[] Sueltos(Clip c)
    {
        Sprite[] s = new Sprite[c.fotogramas.Length];
        for (int k = 0; k < s.Length; k++)
        {
            Texture2D t = c.fotogramas[k];
            if (t == null) continue;
            Vector2 piv = c.pivotes != null && k < c.pivotes.Length ? c.pivotes[k] : c.pivote;
            Rect r = c.recorte.width > 0
                ? new Rect(c.recorte.x, c.recorte.y, c.recorte.width, c.recorte.height)
                : new Rect(0, 0, t.width, t.height);
            string clave = $"{t.GetInstanceID()}|{r}|{piv}|{c.pixelesPorUnidad}";
            if (!cache.TryGetValue(clave, out Sprite[] hecho) || hecho[0] == null)
            {
                hecho = new[] { Sprite.Create(t, r, piv, c.pixelesPorUnidad, 0, SpriteMeshType.FullRect) };
                hecho[0].name = c.nombre + "_" + k;
                cache[clave] = hecho;
            }
            s[k] = hecho[0];
        }
        return s;
    }

    private static Sprite[] Cortar(Clip c)
    {
        if (c.fotogramas != null && c.fotogramas.Length > 0) return Sueltos(c);

        string clave = $"{c.hoja.GetInstanceID()}|{c.anchoCelda}|{c.altoCelda}|{c.fila}|{c.primero}|{c.cantidad}|{c.pivote}|{c.pixelesPorUnidad}";
        if (cache.TryGetValue(clave, out Sprite[] hechos) && hechos.Length > 0 && hechos[0] != null) return hechos;

        Sprite[] s = new Sprite[c.cantidad];
        int columnas = Mathf.Max(1, c.hoja.width / c.anchoCelda);
        for (int k = 0; k < c.cantidad; k++)
        {
            int indice = c.primero + k;
            int col = indice % columnas;
            int fila = c.fila + indice / columnas;
            float y = c.hoja.height - (fila + 1) * c.altoCelda;
            Rect r = new Rect(col * c.anchoCelda, y, c.anchoCelda, c.altoCelda);
            s[k] = Sprite.Create(c.hoja, r, c.pivote, c.pixelesPorUnidad, 0, SpriteMeshType.FullRect);
            s[k].name = c.nombre + "_" + k;
        }
        cache[clave] = s;
        return s;
    }
}
