using System.IO;
using UnityEditor;
using UnityEngine;

// Crea la ficha de la camara de combate (Assets/Resources/AjustesCamaraCombate)
// si no existe. Si ya existe no la toca (lo editado no se pisa).
public static class CrearCamaraCombate
{
    public const string Ruta = "Assets/Resources/AjustesCamaraCombate.asset";

    [MenuItem("Warrior/Camara de combate/Crear ficha", false, 70)]
    public static void Crear()
    {
        if (AssetDatabase.LoadAssetAtPath<AjustesCamaraCombate>(Ruta) != null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AjustesCamaraCombate>(), Ruta);
        AssetDatabase.SaveAssets();
        Debug.Log("[CamaraCombate] Ficha creada: " + Ruta);
    }
}
