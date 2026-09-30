using System.IO;
using UnityEditor;
using UnityEngine;

// Decimotercera ronda: los enemigos normales pasan a un sistema de datos
// disenado por golpes.
//   - Resources/AjustesEnemigos: la curva (zonas, roles, ajuste global).
//   - Assets/Data/Enemigos: una ficha por enemigo (zona, rol, elementos, tamano).
//   - Cada prefab lleva su ficha en EnemyHealth.
//   - Variantes de color (Prefabs/Enemies/Variantes) y elites con nombre
//     (Prefabs/Enemies/Elites): prefabs variante de los normales con su propia
//     ficha. No se colocan en los niveles.
// Las fichas que ya existen NO se tocan (son las del Inspector); solo se crean
// las que falten. Warrior > Actualizar > Ronda 13 (o ActualizarProyecto.Ronda13).
public static class Ronda13
{
    public const string RutaAjustes = "Assets/Resources/AjustesEnemigos.asset";
    public const string CarpetaFichas = "Assets/Data/Enemigos";
    private const string CarpetaPrefabs = "Assets/Prefabs/Enemies";

    private class Ficha
    {
        public string archivo, prefab, nombre;
        public int zona;
        public RolEnemigo rol;
        public float escala = 1f;
        public Afinidad fuego, hielo, oscuro, sagrado, sangrado;
        // Variantes y elites: el prefab del que salen y su carpeta.
        public string basePrefab, carpeta;
        public string nombrePropio;
        public float tono, saturacion = 1f, brillo = 1f;
    }

    private static readonly Ficha[] Fichas =
    {
        // ---- Los normales, ya colocados en los niveles.
        new Ficha { archivo = "RataEscarcha", prefab = "Nieve/RataEscarcha", nombre = "Rata de escarcha", zona = 1, rol = RolEnemigo.Debil,
                    fuego = Afinidad.Debil, hielo = Afinidad.Resiste },
        new Ficha { archivo = "MurcielagoCumbres", prefab = "Nieve/MurcielagoCumbres", nombre = "Murciélago de las cumbres", zona = 1, rol = RolEnemigo.Debil,
                    hielo = Afinidad.Debil, sangrado = Afinidad.Resiste },
        new Ficha { archivo = "OjoVigia", prefab = "Nieve/OjoVigia", nombre = "Ojo vigía", zona = 1, rol = RolEnemigo.Debil, escala = 1.143f,
                    sangrado = Afinidad.Debil, fuego = Afinidad.Resiste },
        new Ficha { archivo = "ArqueraArcana", prefab = "Nieve/ArqueraArcana", nombre = "Arquera arcana", zona = 1, rol = RolEnemigo.Comun,
                    oscuro = Afinidad.Debil, sagrado = Afinidad.Resiste },
        new Ficha { archivo = "HechiceroSombrio", prefab = "Nieve/HechiceroSombrio", nombre = "Hechicero sombrío", zona = 1, rol = RolEnemigo.Pesado, escala = 0.82f,
                    sagrado = Afinidad.Debil, oscuro = Afinidad.Resiste, sangrado = Afinidad.Resiste },
        new Ficha { archivo = "Slime", prefab = "Cueva/Slime", nombre = "Slime", zona = 2, rol = RolEnemigo.Comun,
                    fuego = Afinidad.Debil },
        new Ficha { archivo = "Cacodemonio", prefab = "Cueva/Cacodemonio", nombre = "Cacodemonio", zona = 2, rol = RolEnemigo.Pesado,
                    sagrado = Afinidad.Debil },
        new Ficha { archivo = "Mago", prefab = "Cueva/Mago", nombre = "Mago", zona = 2, rol = RolEnemigo.Comun, escala = 0.66f,
                    oscuro = Afinidad.Resiste },
        new Ficha { archivo = "Mimic", prefab = "Cueva/Mimic", nombre = "Mimic", zona = 2, rol = RolEnemigo.Pesado, escala = 1.27f,
                    fuego = Afinidad.Debil },

        // ---- Variantes de color: el mismo dibujo con otro tono, mas vida por su
        // zona y elementos distintos.
        new Ficha { archivo = "RataCeniza", basePrefab = "Nieve/RataEscarcha", carpeta = "Variantes", nombre = "Rata de ceniza", zona = 2, rol = RolEnemigo.Debil,
                    tono = 165f, saturacion = 1.1f, fuego = Afinidad.Resiste, hielo = Afinidad.Debil },
        new Ficha { archivo = "MurcielagoCripta", basePrefab = "Nieve/MurcielagoCumbres", carpeta = "Variantes", nombre = "Murciélago de cripta", zona = 2, rol = RolEnemigo.Debil,
                    tono = 95f, saturacion = 0.8f, oscuro = Afinidad.Resiste, sagrado = Afinidad.Debil },
        new Ficha { archivo = "ArqueraSangre", basePrefab = "Nieve/ArqueraArcana", carpeta = "Variantes", nombre = "Arquera de sangre", zona = 2, rol = RolEnemigo.Comun,
                    tono = 50f, saturacion = 1.1f, sangrado = Afinidad.Resiste, sagrado = Afinidad.Debil },
        new Ficha { archivo = "OjoAbismo", basePrefab = "Nieve/OjoVigia", carpeta = "Variantes", nombre = "Ojo del abismo", zona = 2, rol = RolEnemigo.Debil, escala = 1.143f,
                    tono = -90f, oscuro = Afinidad.Resiste, sagrado = Afinidad.Debil },
        new Ficha { archivo = "SlimeAbisal", basePrefab = "Cueva/Slime", carpeta = "Variantes", nombre = "Slime abisal", zona = 3, rol = RolEnemigo.Comun,
                    tono = 90f, saturacion = 1.15f, fuego = Afinidad.Resiste, hielo = Afinidad.Debil },
        new Ficha { archivo = "MagoHueso", basePrefab = "Cueva/Mago", carpeta = "Variantes", nombre = "Mago de hueso", zona = 3, rol = RolEnemigo.Comun, escala = 0.66f,
                    saturacion = 0.3f, brillo = 1.12f, oscuro = Afinidad.Resiste, sagrado = Afinidad.Debil },

        // ---- Elites con nombre propio (uno en la Nieve, dos en la Cueva).
        new Ficha { archivo = "EliteMorgath", basePrefab = "Nieve/HechiceroSombrio", carpeta = "Elites", nombre = "Hechicero sombrío (élite)", zona = 1, rol = RolEnemigo.Elite, escala = 0.92f,
                    nombrePropio = "Morgath Sin Ojos", tono = 70f, saturacion = 1.2f, sagrado = Afinidad.Debil, oscuro = Afinidad.Resiste },
        new Ficha { archivo = "EliteMadreCarmesi", basePrefab = "Cueva/Cacodemonio", carpeta = "Elites", nombre = "Cacodemonio (élite)", zona = 2, rol = RolEnemigo.Elite, escala = 1.2f,
                    nombrePropio = "La Madre Carmesí", tono = -20f, saturacion = 1.25f, brillo = 0.9f, sagrado = Afinidad.Debil, fuego = Afinidad.Resiste },
        new Ficha { archivo = "EliteAldren", basePrefab = "Cueva/Mago", carpeta = "Elites", nombre = "Mago (élite)", zona = 2, rol = RolEnemigo.Elite, escala = 0.8f,
                    nombrePropio = "Aldren, Hueso Roto", tono = -170f, saturacion = 0.6f, brillo = 1.1f, sagrado = Afinidad.Debil, oscuro = Afinidad.Resiste },
    };

    [MenuItem("Warrior/Actualizar/Ronda 13 (enemigos por golpes)")]
    public static void Todo() => Generar(false);

    // Con "forzar", vuelve a escribir las fichas con los valores de aqui
    // (pisa lo editado en el Inspector).
    public static void Generar(bool forzar)
    {
        AjustesEnemigos a = Ajustes();
        a.danoGolpeJugador = DanoMedioCombo();
        EditorUtility.SetDirty(a);

        if (!AssetDatabase.IsValidFolder(CarpetaFichas)) AssetDatabase.CreateFolder("Assets/Data", "Enemigos");
        foreach (Ficha f in Fichas)
        {
            string ruta = $"{CarpetaFichas}/{f.archivo}.asset";
            DefinicionEnemigo d = AssetDatabase.LoadAssetAtPath<DefinicionEnemigo>(ruta);
            bool nueva = d == null;
            if (nueva)
            {
                d = ScriptableObject.CreateInstance<DefinicionEnemigo>();
                AssetDatabase.CreateAsset(d, ruta);
            }
            if (nueva || forzar)
            {
                d.nombre = f.nombre; d.zona = f.zona; d.rol = f.rol; d.escala = f.escala;
                d.fuego = f.fuego; d.hielo = f.hielo; d.oscuro = f.oscuro; d.sagrado = f.sagrado; d.sangrado = f.sangrado;
                d.almas = -1;
                d.nombrePropio = f.nombrePropio ?? "";
                d.tono = f.tono; d.saturacion = f.saturacion; d.brillo = f.brillo;
                EditorUtility.SetDirty(d);
            }

            if (f.basePrefab == null) AsignarAPrefab($"{CarpetaPrefabs}/{f.prefab}.prefab", d);
            else CrearVariante(f, d);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda13] Listo.");
    }

    public static void Forzar() => Generar(true);

    public static AjustesEnemigos Ajustes()
    {
        AjustesEnemigos a = AssetDatabase.LoadAssetAtPath<AjustesEnemigos>(RutaAjustes);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<AjustesEnemigos>();
        AssetDatabase.CreateAsset(a, RutaAjustes);
        return a;
    }

    private static void AsignarAPrefab(string ruta, DefinicionEnemigo d)
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        EnemyHealth salud = raiz.GetComponent<EnemyHealth>();
        if (salud == null) { Debug.LogError("[Ronda13] Sin EnemyHealth: " + ruta); PrefabUtility.UnloadPrefabContents(raiz); return; }
        var so = new SerializedObject(salud);
        so.FindProperty("definicion").objectReferenceValue = d;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        PrefabUtility.UnloadPrefabContents(raiz);
    }

    // Prefab variante del normal (hereda todo lo que se cambie en el) con su ficha.
    private static void CrearVariante(Ficha f, DefinicionEnemigo d)
    {
        string carpeta = $"{CarpetaPrefabs}/{f.carpeta}";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(CarpetaPrefabs, f.carpeta);
        string ruta = $"{carpeta}/{f.archivo}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
        {
            GameObject origen = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/{f.basePrefab}.prefab");
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(origen);
            inst.name = f.archivo;
            PrefabUtility.SaveAsPrefabAsset(inst, ruta);
            Object.DestroyImmediate(inst);
        }
        AsignarAPrefab(ruta, d);
    }

    // Media del combo de espada en el suelo (perfiles 0, 1 y 2 del player).
    public static float DanoMedioCombo()
    {
        GameObject p = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        float media = 17.3f;
        PlayerControler pc = p.GetComponent<PlayerControler>();
        if (pc != null)
        {
            SerializedProperty lista = new SerializedObject(pc).FindProperty("attackProfiles");
            if (lista != null && lista.arraySize >= 3)
            {
                float suma = 0f;
                for (int i = 0; i < 3; i++) suma += lista.GetArrayElementAtIndex(i).FindPropertyRelative("dano").intValue;
                media = suma / 3f;
            }
        }
        PrefabUtility.UnloadPrefabContents(p);
        return Mathf.Round(media * 10f) / 10f;
    }
}
