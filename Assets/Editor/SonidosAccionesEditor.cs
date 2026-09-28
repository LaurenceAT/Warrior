using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Inspector de los sonidos de un personaje: cada accion con su nombre, sus clips,
// su volumen y un boton para escucharla. Y la creacion de los archivos del player
// y de los jefes con lo que suena ahora (Warrior > Crear archivos de sonidos).
[CustomEditor(typeof(SonidosAcciones))]
public class SonidosAccionesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Cambia los clips o el volumen de cada accion. El boton ▶ la hace sonar. " +
                                "Si una accion se queda sin clips, suena la de la biblioteca general.", MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volumenGeneral"));
        EditorGUILayout.Space();

        SerializedProperty lista = serializedObject.FindProperty("acciones");
        for (int i = 0; i < lista.arraySize; i++)
        {
            SerializedProperty a = lista.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(a.FindPropertyRelative("accion").stringValue, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(a.FindPropertyRelative("clave").stringValue, EditorStyles.miniLabel, GUILayout.Width(150));
                    if (GUILayout.Button("▶", GUILayout.Width(28))) Probar(a);
                }
                EditorGUILayout.PropertyField(a.FindPropertyRelative("clips"), true);
                EditorGUILayout.PropertyField(a.FindPropertyRelative("volumen"));
                EditorGUILayout.PropertyField(a.FindPropertyRelative("variacionTono"));
            }
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void Probar(SerializedProperty a)
    {
        SerializedProperty clips = a.FindPropertyRelative("clips");
        if (clips.arraySize == 0) return;
        AudioClip c = clips.GetArrayElementAtIndex(UnityEngine.Random.Range(0, clips.arraySize)).objectReferenceValue as AudioClip;
        if (c == null) return;
        // El reproductor de vista previa del editor (no es API publica).
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        MethodInfo parar = util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        MethodInfo tocar = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                                           new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        parar?.Invoke(null, null);
        tocar?.Invoke(null, new object[] { c, 0, false });
    }

    // ------------------------------------------------------------------ Creacion

    public const string Carpeta = "Assets/Data/Sonidos";

    private static readonly (string clave, string accion)[] Player =
    {
        ("espada_tajo", "Espadazo que no acierta (silbido)"),
        ("espada_impacto", "Espadazo que acierta"),
        ("parry", "Parry (choque de metal)"),
        ("parry_brillo", "Parry (destello)"),
        ("bloqueo", "Bloquear un golpe"),
        ("dano_player", "Recibir dano"),
        ("muerte_player", "Morir"),
        ("esquiva", "Barrido / esquiva (C)"),
        ("salto", "Saltar"),
        ("aterrizaje", "Aterrizar / agarrarse a una cornisa"),
        ("esfuerzo", "Esfuerzo (subir una cornisa)"),
        ("pasos", "Pasos"),
        ("pasos_hielo", "Pasos sobre hielo"),
        ("beber", "Beber un frasco"),
        ("imbuir_0", "Imbuir: fuego"), ("imbuir_1", "Imbuir: hielo"), ("imbuir_2", "Imbuir: oscuridad"),
        ("imbuir_3", "Imbuir: sagrado"), ("imbuir_4", "Imbuir: acido"),
        ("golpe_0", "Golpe imbuido: fuego"), ("golpe_1", "Golpe imbuido: hielo"), ("golpe_2", "Golpe imbuido: oscuridad"),
        ("golpe_3", "Golpe imbuido: sagrado"), ("golpe_4", "Golpe imbuido: acido"),
        ("alma_recoger", "Recoger almas"),
        ("alma_mancha", "Mancha de almas"),
    };

    private static readonly (string clave, string accion)[] Sombra =
    {
        ("jefe_aparicion", "Aparicion"),
        ("jefe_carga_tajo", "Preparar un tajo"),
        ("jefe_tajo", "Tajo"),
        ("jefe_tajo_fuerte", "Tajo fuerte / alzado"),
        ("jefe_medialuna", "Medialunas de sombra"),
        ("jefe_caida", "Caida desde lo alto"),
        ("jefe_impacto_suelo", "Impacto contra el suelo"),
        ("teletransporte", "Esfera / teletransporte"),
        ("jefe_agarre_carga", "Agarre: carga"),
        ("jefe_agarre", "Agarre: atrapa al player"),
        ("jefe_corte_agarre", "Agarre: cada corte"),
        ("jefe_parry", "Le hacen parry"),
        ("jefe_postura", "Postura rota"),
        ("jefe_revivir_carga", "Revivir: la escarcha se junta"),
        ("jefe_revivir", "Revivir: se levanta"),
        ("hielo_conjuro", "Conjuro de hielo (fase 2)"),
        ("ventisca", "Ventisca (bucle)"),
        ("ventisca_rafaga", "Ventisca: rafaga"),
        ("jefe_nova", "Estallido"),
        ("jefe_muerte", "Muerte"),
    };

    private static readonly (string clave, string accion)[] Wraith =
    {
        ("jefe_tajo", "Tajo"),
        ("jefe_golpe_fuerte", "Golpe fuerte"),
        ("jefe_impacto_suelo", "Impacto contra el suelo"),
        ("jefe_nova", "Estallido"),
        ("jefe_transformacion", "Transformacion (fase 2)"),
    };

    [MenuItem("Warrior/Crear archivos de sonidos")]
    public static void Crear()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Data", "Sonidos");
        RecursosRPG rec = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");

        SonidosAcciones player = Archivo("Sonidos Player", Player, rec);
        rec.sonidosPlayer = player;
        EditorUtility.SetDirty(rec);

        Asignar("Assets/Prefabs/Enemies/Nieve/SombraHumedales.prefab", Archivo("Sonidos Jefe Sombra de los Humedales", Sombra, rec));
        Asignar("Assets/Prefabs/Enemies/Cueva/CrimsonWraith.prefab", Archivo("Sonidos Jefe Crimson Wraith", Wraith, rec));
        AssetDatabase.SaveAssets();
        Debug.Log("[Sonidos] Archivos creados en " + Carpeta);
    }

    // Crea el archivo (o completa el que haya, sin tocar lo que se cambio a mano)
    // con los clips y volumenes que suenan ahora.
    private static SonidosAcciones Archivo(string nombre, (string clave, string accion)[] lista, RecursosRPG rec)
    {
        string ruta = $"{Carpeta}/{nombre}.asset";
        SonidosAcciones s = AssetDatabase.LoadAssetAtPath<SonidosAcciones>(ruta);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SonidosAcciones>();
            AssetDatabase.CreateAsset(s, ruta);
        }
        foreach (var (clave, accion) in lista)
        {
            if (s.acciones.Any(a => a.clave == clave)) continue;
            RecursosRPG.GrupoSonido g = rec.sonidos.FirstOrDefault(x => x != null && x.clave == clave);
            s.acciones.Add(new SonidosAcciones.Accion
            {
                accion = accion, clave = clave,
                clips = g != null ? g.clips : new AudioClip[0],
                volumen = g != null ? g.volumen : 0.7f,
                variacionTono = g != null ? g.variacionTono : 0.06f,
            });
        }
        EditorUtility.SetDirty(s);
        return s;
    }

    private static void Asignar(string rutaPrefab, SonidosAcciones s)
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            JefeBase j = raiz.GetComponent<JefeBase>();
            if (j == null) { Debug.LogWarning("[Sonidos] " + rutaPrefab + " no es un jefe"); return; }
            SerializedObject so = new SerializedObject(j);
            so.FindProperty("sonidos").objectReferenceValue = s;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
