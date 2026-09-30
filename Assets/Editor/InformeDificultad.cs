using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Warrior > Informe de dificultad (solo Editor). Para cada enemigo normal con
// ficha: vida, dano, golpes para matarlo y para morir (con el poder esperado de
// su zona), almas y tamano respecto al player. En rojo lo que se sale de los
// valores de partida de AjustesEnemigos. Es un calculo: no simula combates.
public class InformeDificultad : EditorWindow
{
    public class Fila
    {
        public string prefab;
        public DefinicionEnemigo ficha;
        public float altoDibujo;   // alto visible a escala 1, en unidades
        public int vida, dano, golpesMatar, golpesMorir, almas;
        public Vector2 rangoGolpes, rangoTamano;
        public float tamano;       // alto respecto al player
        public bool golpesMal, tamanoMal;
    }

    private Vector2 scroll;
    private List<Fila> filas;

    [MenuItem("Warrior/Informe de dificultad")]
    public static void Abrir() => GetWindow<InformeDificultad>("Dificultad").Show();

    private void OnEnable() => filas = Calcular();
    private void OnFocus() => filas = Calcular();

    // ------------------------------------------------------------------ Calculo

    private static readonly Dictionary<string, float> altos = new Dictionary<string, float>();

    public static List<Fila> Calcular()
    {
        var lista = new List<Fila>();
        AjustesEnemigos a = AjustesEnemigos.Get();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            EnemyHealth s = go != null ? go.GetComponent<EnemyHealth>() : null;
            if (s == null || s.Definicion == null) continue;
            DefinicionEnemigo d = s.Definicion;
            var f = new Fila
            {
                prefab = Path.GetFileNameWithoutExtension(ruta), ficha = d,
                vida = d.Vida, dano = d.DanoBase, almas = d.Almas,
                golpesMatar = d.GolpesParaMatarlo, golpesMorir = d.GolpesParaMorir,
                rangoGolpes = d.Zona.Golpes(d.rol), rangoTamano = d.Rol.tamano,
                altoDibujo = AltoDibujo(ruta, go),
            };
            f.tamano = f.altoDibujo * d.escala / Mathf.Max(0.01f, a.alturaJugador);
            f.golpesMal = f.golpesMatar < f.rangoGolpes.x - 0.01f || f.golpesMatar > f.rangoGolpes.y + 0.01f;
            f.tamanoMal = f.altoDibujo > 0f && (f.tamano < f.rangoTamano.x - 0.01f || f.tamano > f.rangoTamano.y + 0.01f);
            lista.Add(f);
        }
        lista.Sort((x, y) => x.ficha.zona != y.ficha.zona ? x.ficha.zona.CompareTo(y.ficha.zona) : x.ficha.rol.CompareTo(y.ficha.rol));
        return lista;
    }

    // Alto visible del primer fotograma de reposo (o del ancho, si es mas
    // ancho que alto: una rata o un slime "miden" por lo largo).
    private static float AltoDibujo(string ruta, GameObject go)
    {
        if (altos.TryGetValue(ruta, out float h)) return h;
        h = 0f;
        AnimadorHoja an = go.GetComponentInChildren<AnimadorHoja>(true);
        if (an != null)
        {
            AnimadorHoja.Clip c = null;
            foreach (string n in new[] { "quieto", "vuelo", "andar" })
                if (c == null) c = an.clips.Find(x => x != null && x.nombre == n);
            if (c != null)
            {
                Rect r = default;
                Texture2D tex = null;
                if (c.fotogramas != null && c.fotogramas.Length > 0 && c.fotogramas[0] != null)
                {
                    tex = c.fotogramas[0];
                    r = c.recorte.width > 0 ? new Rect(c.recorte.x, c.recorte.y, c.recorte.width, c.recorte.height) : new Rect(0, 0, tex.width, tex.height);
                }
                else if (c.hoja != null)
                {
                    tex = c.hoja;
                    int columnas = Mathf.Max(1, tex.width / c.anchoCelda);
                    int col = c.primero % columnas, fila = c.fila + c.primero / columnas;
                    r = new Rect(col * c.anchoCelda, tex.height - (fila + 1) * c.altoCelda, c.anchoCelda, c.altoCelda);
                }
                if (tex != null)
                {
                    Vector2Int caja = DiagnosticoEnemigos.Opaco(tex, r);
                    h = Mathf.Max(caja.y, caja.x * 0.6f) / Mathf.Max(1f, c.pixelesPorUnidad) * Mathf.Abs(an.transform.localScale.y);
                }
            }
        }
        altos[ruta] = h;
        return h;
    }

    // Tabla en texto (para las pruebas en batchmode y para copiar).
    public static string Texto()
    {
        var sb = new StringBuilder();
        AjustesEnemigos a = AjustesEnemigos.Get();
        for (int z = 1; z <= a.zonas.Length; z++)
        {
            AjustesEnemigos.Zona zona = a.ZonaN(z);
            sb.AppendLine($"Zona {z} ({zona.nombre}): tu vida {zona.VidaJugador:0}, tu golpe {zona.DanoJugador:0.0}");
        }
        sb.AppendLine("enemigo | zona | rol | vida | dano | golpes matar (rango) | golpes morir | almas | tamano % (rango)");
        foreach (Fila f in Calcular())
            sb.AppendLine($"{f.prefab} | {f.ficha.zona} | {f.ficha.rol} | {f.vida} | {f.dano} | {f.golpesMatar} ({f.rangoGolpes.x:0}-{f.rangoGolpes.y:0}){(f.golpesMal ? " FUERA" : "")} | " +
                          $"{f.golpesMorir} | {f.almas} | {f.tamano * 100f:0} ({f.rangoTamano.x * 100f:0}-{f.rangoTamano.y * 100f:0}){(f.tamanoMal ? " FUERA" : "")}");
        return sb.ToString();
    }

    public static void Escribir()
    {
        string salida = Path.Combine(Path.GetTempPath(), "informe_dificultad.txt");
        File.WriteAllText(salida, Texto());
        Debug.Log("[Informe] " + salida);
    }

    // ------------------------------------------------------------------ Ventana

    private void OnGUI()
    {
        AjustesEnemigos a = AjustesEnemigos.Get();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Recalcular", GUILayout.Width(100))) { altos.Clear(); filas = Calcular(); }
        if (GUILayout.Button("Leer dano del player", GUILayout.Width(150)))
        {
            AjustesEnemigos asset = Ronda13.Ajustes();
            asset.danoGolpeJugador = Ronda13.DanoMedioCombo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            filas = Calcular();
        }
        if (GUILayout.Button("Abrir curva", GUILayout.Width(100))) Selection.activeObject = Ronda13.Ajustes();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Vida x{a.multiplicadorVida:0.##}   Dano x{a.multiplicadorDano:0.##}   Almas x{a.multiplicadorAlmas:0.##}");
        EditorGUILayout.EndHorizontal();

        for (int z = 1; z <= a.zonas.Length; z++)
        {
            AjustesEnemigos.Zona zona = a.ZonaN(z);
            EditorGUILayout.LabelField($"Zona {z} · {zona.nombre}", $"tu vida {zona.VidaJugador:0} · tu golpe {zona.DanoJugador:0.0} (espada +{zona.nivelEspada})");
        }
        EditorGUILayout.Space();

        string[] cab = { "Enemigo", "Zona", "Rol", "Vida", "Daño", "Golpes para matarlo", "Golpes para morir", "Almas", "Tamaño" };
        float[] ancho = { 150, 40, 60, 45, 45, 130, 110, 50, 110 };
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < cab.Length; i++) GUILayout.Label(cab[i], EditorStyles.boldLabel, GUILayout.Width(ancho[i]));
        EditorGUILayout.EndHorizontal();

        var rojo = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.35f, 0.3f) } };
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (filas != null)
            foreach (Fila f in filas)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(f.prefab, EditorStyles.linkLabel, GUILayout.Width(ancho[0]))) Selection.activeObject = f.ficha;
                GUILayout.Label(f.ficha.zona.ToString(), GUILayout.Width(ancho[1]));
                GUILayout.Label(f.ficha.rol.ToString(), GUILayout.Width(ancho[2]));
                GUILayout.Label(f.vida.ToString(), GUILayout.Width(ancho[3]));
                GUILayout.Label(f.dano.ToString(), GUILayout.Width(ancho[4]));
                GUILayout.Label($"{f.golpesMatar}  (objetivo {f.rangoGolpes.x:0}-{f.rangoGolpes.y:0})", f.golpesMal ? rojo : EditorStyles.label, GUILayout.Width(ancho[5]));
                GUILayout.Label(f.golpesMorir.ToString(), GUILayout.Width(ancho[6]));
                GUILayout.Label(f.almas.ToString(), GUILayout.Width(ancho[7]));
                GUILayout.Label(f.altoDibujo > 0f ? $"{f.tamano * 100f:0}%  ({f.rangoTamano.x * 100f:0}-{f.rangoTamano.y * 100f:0})" : "?",
                                f.tamanoMal ? rojo : EditorStyles.label, GUILayout.Width(ancho[8]));
                EditorGUILayout.EndHorizontal();
            }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("Golpes: con la media del combo de espada y las mejoras esperadas de su zona. Golpes para morir: su golpe principal contra tu vida esperada, sin resistencias ni bloqueo.", MessageType.None);
    }
}
