using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Los amuletos (menu Warrior > Amuletos > Crear o actualizar):
//   - Copia sus imagenes (2 anillos del pack de joyas y 38 del pack OBJETOS Y
//     AMULETOS) en Assets/Sprites/Amuletos, como sprites de pixel art. Los
//     originales no se tocan.
//   - La ficha Resources/Amuletos con los 40 (nombre, imagen y precio 1 para
//     probar), en el orden de la lista; se conservan nombres y precios editados.
//   - El Vestigio del Vacio: su icono ("objeto_vestigio" en RecursosRPG) y dos
//     articulos en el totem (Vestigio del Vacio I y II, precio 1 para probar).
public static class CrearAmuletos
{
    private const string Joyas = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/05-jewelry-accessories/";
    private const string Runas = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/16-runes-enchantments/";
    public const string Carpeta = "Assets/Sprites/Amuletos";
    public const string RutaFicha = "Assets/Resources/Amuletos.asset";
    public const string IconoVestigio = Carpeta + "/vestigio_vacio.png";
    private const int PrecioPrueba = 1;

    // Origen de cada amuleto: los dos anillos del pack de joyas (cristal rojo y
    // azul) y los demas del pack OBJETOS Y AMULETOS (Undead loot). En este orden
    // salen en la coleccion.
    private const string Undead = "Assets/SPRITES PARA NUEVOS NIVELES/OBJETOS Y AMULETOS/";
    public static readonly (string id, string origen, string nombre)[] Lista =
    {
        ("amuleto_00", Joyas + "icon_00.png", "Anillo de la brasa"),
        ("amuleto_01", Joyas + "icon_01.png", "Anillo de la marea"),
        U(1, "Cráneo del guardián"), U(3, "Mandíbula del mudo"), U(4, "Hueso nudoso"), U(5, "Mano del osario"),
        U(7, "Costillar hueco"), U(8, "Muelas del glotón"), U(9, "Colmillo curvo"), U(10, "Cráneo de cuervo"),
        U(11, "Cráneo de bestia"), U(12, "Corazón que late"), U(13, "Ojos del vigía"), U(14, "Seso marchito"),
        U(15, "Brazo seco"), U(16, "Pie del peregrino"), U(17, "Dedo anillado"), U(18, "Lazo nocturno"),
        U(19, "Anillo de amatista hueca"), U(20, "Corona rota"), U(23, "Rama de hueso"), U(24, "Garra amarilla"),
        U(26, "Rosario de dientes"), U(27, "Garra de sombra"), U(28, "Cristal de escarcha"), U(29, "Gema de sangre"),
        U(30, "Collar de luto"), U(33, "Calavera votiva"), U(34, "Vela del réquiem"), U(37, "Pluma de fénix"),
        U(38, "Muñeco de trapo"), U(39, "Ojos gemelos"), U(40, "Cuenco del festín"), U(41, "Llave del osario"),
        U(44, "Yelmo abollado"), U(45, "Guante de cuero"), U(46, "Bota del caminante"), U(48, "Tótem de ciervo"),
        U(49, "Raíz retorcida"), U(50, "Colmillo colgante"),
    };

    // Los iconos 49 y 50 estan en la carpeta Bonus.
    private static (string, string, string) U(int n, string nombre) =>
        ("amuleto_u" + n.ToString("00"), Undead + (n >= 49 ? "Bonus/" : "") + $"PNG/Transperent/Icon{n}.png", nombre);

    // Lo que hace cada uno (se ve en el totem y en la hoguera). Sin efecto aun: los de exploracion.
    public static readonly Dictionary<string, string> Descripciones = new Dictionary<string, string>
    {
        { "amuleto_00", "Con fuego imbuido: +12 % de daño y la quemadura es más fuerte.\n<color=#e0776c>Mientras el arma arde, recibes un 10 % más de daño mágico.</color>" },
        { "amuleto_01", "El maná vuelve solo, muy despacio." },
        { "amuleto_u01", "Recibes un 15 % menos de daño físico." },
        { "amuleto_u03", "Haces menos ruido: los enemigos tardan más en verte y la Cazadora te oye peor." },
        { "amuleto_u04", "Una carga más del frasco de sangre." },
        { "amuleto_u05", "Los ataques en el aire y la estocada en picado hacen un 25 % más." },
        { "amuleto_u07", "Un 20 % más de estamina máxima." },
        { "amuleto_u08", "El frasco de sangre cura un 25 % más." },
        { "amuleto_u09", "El golpe justo después de un parry hace un 30 % más." },
        { "amuleto_u10", "Su poder aún duerme." },
        { "amuleto_u11", "El último golpe de cada combo hace un 20 % más." },
        { "amuleto_u12", "Un 15 % más de vida máxima." },
        { "amuleto_u13", "La ventana de parry es un 40 % más amplia." },
        { "amuleto_u14", "Imbuir cuesta la mitad de maná.\n<color=#e0776c>La imbuición dura un 30 % menos.</color>" },
        { "amuleto_u15", "Bloquear gasta un 30 % menos de estamina." },
        { "amuleto_u16", "Correr gasta un 30 % menos y vas un 10 % más rápido." },
        { "amuleto_u17", "Las imbuciones duran un 50 % más." },
        { "amuleto_u18", "Con oscuridad imbuida, drenas un 25 % más de vida.\n<color=#e0776c>El frasco de sangre cura un 15 % menos.</color>" },
        { "amuleto_u19", "Recibes un 15 % menos de daño mágico." },
        { "amuleto_u20", "Con la vida al máximo, +15 % de daño." },
        { "amuleto_u23", "Justo después de esquivar, +20 % de daño." },
        { "amuleto_u24", "Con sagrado imbuido, aturdes un 25 % más a menudo.\n<color=#e0776c>Tras aturdir, tu estamina tarda 1 s más en volver.</color>" },
        { "amuleto_u26", "Con sangrado imbuido: +18 % de sangrado al enemigo e imbuirlo cuesta un 25 % menos de vida.\n<color=#e0776c>El sangrado que te pones tú mismo sube un 25 % más.</color>" },
        { "amuleto_u27", "Con menos de un cuarto de vida, +25 % de daño." },
        { "amuleto_u28", "Con escarcha imbuida, congelas un 18 % antes.\n<color=#e0776c>Cada golpe con escarcha gasta 8 de estamina más.</color>" },
        { "amuleto_u29", "Cada golpe que das te cura un poco (como mucho una vez por segundo)." },
        { "amuleto_u30", "Su poder aún duerme." },
        { "amuleto_u33", "Una vez por cada descanso en la hoguera, un golpe mortal te deja con 1 de vida." },
        { "amuleto_u34", "Sangrado, congelación y quemadura se te llenan un 40 % más despacio." },
        { "amuleto_u37", "La esquiva te protege durante más tiempo (+30 %)." },
        { "amuleto_u38", "El 20 % del daño que recibes vuelve a quien te golpea." },
        { "amuleto_u39", "Su poder aún duerme." },
        { "amuleto_u40", "El frasco de maná devuelve un 30 % más." },
        { "amuleto_u41", "El tótem te cobra un 10 % menos." },
        { "amuleto_u44", "Los golpes leves no te empujan." },
        { "amuleto_u45", "Correr por la pared gasta un 40 % menos de estamina." },
        { "amuleto_u46", "La estocada en picado suelta una onda a los dos lados al tocar el suelo." },
        { "amuleto_u48", "Cada parry justo te devuelve 10 de maná." },
        { "amuleto_u49", "La estamina se recupera un 25 % más rápido." },
        { "amuleto_u50", "Con la espada sin imbuir, +12 % de daño." },
    };

    // Precio en el totem de cada uno (solo para una ficha nueva: lo editado se conserva).
    public static readonly Dictionary<string, int> Precios = new Dictionary<string, int>
    {
        { "amuleto_00", 1100 },
        { "amuleto_01", 1400 },
        { "amuleto_u01", 1500 },
        { "amuleto_u03", 900 },
        { "amuleto_u04", 2200 },
        { "amuleto_u05", 900 },
        { "amuleto_u07", 1100 },
        { "amuleto_u08", 1500 },
        { "amuleto_u09", 1300 },
        { "amuleto_u10", 1000 },
        { "amuleto_u11", 1200 },
        { "amuleto_u12", 1800 },
        { "amuleto_u13", 2000 },
        { "amuleto_u14", 1000 },
        { "amuleto_u15", 900 },
        { "amuleto_u16", 800 },
        { "amuleto_u17", 900 },
        { "amuleto_u18", 1100 },
        { "amuleto_u19", 1500 },
        { "amuleto_u20", 1500 },
        { "amuleto_u23", 1100 },
        { "amuleto_u24", 1000 },
        { "amuleto_u26", 1200 },
        { "amuleto_u27", 1400 },
        { "amuleto_u28", 1000 },
        { "amuleto_u29", 1800 },
        { "amuleto_u30", 1000 },
        { "amuleto_u33", 2500 },
        { "amuleto_u34", 1300 },
        { "amuleto_u37", 1600 },
        { "amuleto_u38", 1400 },
        { "amuleto_u39", 600 },
        { "amuleto_u40", 1000 },
        { "amuleto_u41", 800 },
        { "amuleto_u44", 1500 },
        { "amuleto_u45", 600 },
        { "amuleto_u46", 1000 },
        { "amuleto_u48", 1600 },
        { "amuleto_u49", 1300 },
        { "amuleto_u50", 1000 },
    };

    public const string RutaAjustes = "Assets/Resources/AjustesAmuletos.asset";

    [MenuItem("Warrior/Amuletos/Crear o actualizar", false, 70)]
    public static void Crear()
    {
        Directory.CreateDirectory(Carpeta);
        bool nuevos = false;
        foreach (var (id, origen, _) in Lista) nuevos |= Copiar(origen, $"{Carpeta}/{id}.png");
        nuevos |= Copiar($"{Runas}icon_07.png", IconoVestigio);
        if (nuevos) AssetDatabase.Refresh();
        foreach (var (id, _, _) in Lista) Sprite($"{Carpeta}/{id}.png");
        Sprite(IconoVestigio);

        Ficha();
        if (AssetDatabase.LoadAssetAtPath<AjustesAmuletos>(RutaAjustes) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AjustesAmuletos>(), RutaAjustes);
        IconoEnRecursos();
        Tienda();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Amuletos] Listo: {Lista.Length} amuletos en {RutaFicha}, Vestigio del Vacío en el tótem.");
    }

    private static bool Copiar(string origen, string destino)
    {
        if (File.Exists(destino)) return false;
        if (!File.Exists(origen)) { Debug.LogError("[Amuletos] Falta " + origen); return false; }
        File.Copy(origen, destino);
        return true;
    }

    // Sprite suelto de pixel art (32 x 32).
    private static void Sprite(string ruta)
    {
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) return;
        if (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single && ti.filterMode == FilterMode.Point
            && ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    private static Sprite Cargar(string ruta) => AssetDatabase.LoadAssetAtPath<Sprite>(ruta);

    // La lista se rehace en el orden de arriba; de los que ya estaban se
    // conservan el nombre y el precio editados.
    private static void Ficha()
    {
        FichaAmuletos f = AssetDatabase.LoadAssetAtPath<FichaAmuletos>(RutaFicha);
        if (f == null)
        {
            f = ScriptableObject.CreateInstance<FichaAmuletos>();
            AssetDatabase.CreateAsset(f, RutaFicha);
        }
        var nueva = new List<FichaAmuletos.Amuleto>();
        foreach (var (id, _, nombre) in Lista)
        {
            FichaAmuletos.Amuleto viejo = f.Buscar(id);
            nueva.Add(new FichaAmuletos.Amuleto
            {
                id = id, nombre = viejo != null && !string.IsNullOrEmpty(viejo.nombre) ? viejo.nombre : nombre,
                icono = Cargar($"{Carpeta}/{id}.png"), precio = viejo != null ? viejo.precio : Precios.TryGetValue(id, out int pr) ? pr : PrecioPrueba,
                descripcion = viejo != null && !string.IsNullOrEmpty(viejo.descripcion) ? viejo.descripcion
                            : Descripciones.TryGetValue(id, out string d) ? d : "",
            });
        }
        f.amuletos = nueva;
        EditorUtility.SetDirty(f);
    }

    private static void IconoEnRecursos()
    {
        RecursosRPG r = Resources.Load<RecursosRPG>("RecursosRPG");
        if (r == null) { Debug.LogError("[Amuletos] Falta Resources/RecursosRPG"); return; }
        Sprite s = Cargar(IconoVestigio);
        RecursosRPG.EntradaIcono e = r.iconos.FirstOrDefault(i => i != null && i.clave == "objeto_vestigio");
        if (e == null) r.iconos.Add(new RecursosRPG.EntradaIcono { clave = "objeto_vestigio", sprite = s });
        else if (e.sprite == null) e.sprite = s;
        EditorUtility.SetDirty(r);
    }

    private static void Tienda()
    {
        FichaTienda t = Resources.Load<FichaTienda>("Desafios/Tienda");
        if (t == null) { Debug.LogError("[Amuletos] Falta Resources/Desafios/Tienda"); return; }
        void Articulo(string id, string nombre, string requiere, int precio)
        {
            if (t.articulos.Any(a => a != null && a.id == id)) return;
            t.articulos.Add(new FichaTienda.Articulo { id = id, nombre = nombre, objeto = Equipo.Objeto.VestigioVacio, precio = precio, requiere = requiere });
        }
        Articulo("vestigio_1", "Vestigio del Vacío I", "", 1000);
        Articulo("vestigio_2", "Vestigio del Vacío II", "vestigio_1", 1100);
        EditorUtility.SetDirty(t);
    }
}
