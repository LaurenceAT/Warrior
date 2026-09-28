using UnityEditor;

// Inspector de la ArenaJefe: si tiene un Config Nivel puesto, la musica del jefe y
// sus frases salen de ahi, asi que aqui se esconden los campos propios (antes se
// veian las pistas viejas y parecia que el cambio no se habia guardado).
[CustomEditor(typeof(ArenaJefe))]
public class ArenaJefeEditor : Editor
{
    private static readonly string[] Musica =
    {
        "m_Script", "musica", "volumen", "inicioFase1", "bucleFase1",
        "musicaFase2", "volumenFase2", "inicioFase2", "bucleFase2",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty config = serializedObject.FindProperty("config");
        ConfigNivel c = config.objectReferenceValue as ConfigNivel;
        if (c == null)
        {
            DrawDefaultInspector();
            return;
        }

        EditorGUILayout.HelpBox(
            $"La música del jefe (fase 1 y 2) se toma de \"{c.name}\" " +
            (c.frasesJefe != null && c.frasesJefe.Length > 0 ? "y también las frases al morir contra el jefe. " : ". ") +
            "Cámbialas allí (Assets/Data/Niveles).", MessageType.Info);
        if (EditorGUILayout.LinkButton("Abrir " + c.name)) Selection.activeObject = c;

        bool frasesDeConfig = c.frasesJefe != null && c.frasesJefe.Length > 0;
        string[] ocultos = frasesDeConfig ? AgregarBurlas() : Musica;
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawPropertiesExcluding(serializedObject, ocultos);
        serializedObject.ApplyModifiedProperties();
    }

    private static string[] AgregarBurlas()
    {
        string[] r = new string[Musica.Length + 1];
        Musica.CopyTo(r, 0);
        r[Musica.Length] = "burlas";
        return r;
    }
}
