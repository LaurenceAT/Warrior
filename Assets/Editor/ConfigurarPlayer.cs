using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Anade al prefab del Player el agarre de cornisa con sus fotogramas
// (Acciones nuevas/Cornisa-Agarrar y Cornisa-Trepar). Warrior > Configurar player.
public static class ConfigurarPlayer
{
    private const string Prefab = "Assets/Prefabs/Player.prefab";
    private const string Acciones = "Assets/Sprites/PJ_Knight/1 - Adventurer 1.5 (RECOMENDADA)/Acciones nuevas/";

    [MenuItem("Warrior/Configurar player")]
    public static void Configurar()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            AgarreCornisa a = raiz.GetComponent<AgarreCornisa>();
            if (a == null) a = raiz.AddComponent<AgarreCornisa>();
            SerializedObject so = new SerializedObject(a);
            Poner(so.FindProperty("fotogramasAgarre"), Fotogramas("Cornisa-Agarrar"));
            Poner(so.FindProperty("fotogramasSubida"), Fotogramas("Cornisa-Trepar"));
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log("[Player] Agarre de cornisa listo.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static Sprite[] Fotogramas(string carpeta)
    {
        return Directory.GetFiles(Acciones + carpeta, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
    }

    private static void Poner(SerializedProperty p, Sprite[] s)
    {
        p.arraySize = s.Length;
        for (int i = 0; i < s.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = s[i];
    }
}
