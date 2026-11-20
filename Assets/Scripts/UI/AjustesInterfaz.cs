using UnityEngine;

// Numeros de la interfaz en pantalla (Resources/AjustesInterfaz). Todo en
// unidades de la pantalla de referencia (1920x1080); se escala solo.
//
// MAPA DE ZONAS (nada se pisa):
//   - Arriba a la izquierda: el estado del player (vida, estamina, mana...).
//   - Arriba a la derecha: el objeto deseado del totem (desafios).
//   - Abajo a la derecha, una columna (de abajo arriba): las almas, encima los
//     frascos (sangre y mana) y encima el aviso de "objetos por aplicar".
//   - Abajo, de la izquierda hasta esa columna: la barra del jefe, larga (nombre
//     encima a la izquierda y las marcas I II III encima a la derecha). Si no
//     cabe, sube por encima de la columna.
[CreateAssetMenu(menuName = "Warrior/Ajustes de la interfaz", fileName = "AjustesInterfaz")]
public class AjustesInterfaz : ScriptableObject
{
    [Header("Columna de abajo a la derecha (almas, frascos y aviso)")]
    [Tooltip("Margen con los bordes de la pantalla.")]
    public float margen = 40f;
    [Tooltip("Ancho de la columna (lo que ocupan los dos frascos, que es lo mas ancho).")]
    public float anchoColumna = 410f;
    [Tooltip("Ancho de la caja de almas.")]
    public float anchoAlmas = 250f;
    [Tooltip("Altura (desde abajo) de los frascos: encima de las almas.")]
    public float alturaFrascosColumna = 104f;
    [Tooltip("Altura (desde abajo) del aviso de objetos por aplicar: encima de los frascos.")]
    public float alturaAvisoColumna = 186f;
    [Tooltip("Ancho del aviso de objetos por aplicar.")]
    public float anchoAviso = 330f;

    [Header("Barra del jefe (abajo, larga)")]
    [Tooltip("Donde empieza la barra, en parte del ancho de la pantalla (0.12 = 12 %).")]
    [Range(0f, 0.4f)] public float inicioBarra = 0.12f;
    [Tooltip("Separacion con la columna de la derecha.")]
    public float holgura = 30f;
    public float anchoBarraMinimo = 420f;
    [Tooltip("Altura de la barra (desde abajo).")]
    public float alturaBarra = 82f;
    [Tooltip("Si no cabe junto a la columna, sube a esta altura (por encima de ella).")]
    public float alturaBarraElevada = 270f;

    [Header("Latido del aviso cerca de una hoguera")]
    [Tooltip("Distancia (unidades del mundo) a una hoguera encendida para que el aviso lata.")]
    public float radioLatido = 4f;
    [Tooltip("Latidos por segundo.")]
    public float velocidadLatido = 1.2f;
    [Tooltip("Cuanto crece y brilla (0.08 = un 8 %).")]
    [Range(0f, 0.3f)] public float intensidadLatido = 0.08f;

    [Header("Deseo del totem (arriba a la derecha)")]
    public float anchoDeseo = 330f;
    public float margenArribaDeseo = 30f;

    [Header("Pantalla de carga")]
    [Tooltip("Puntos que aparecen por segundo en \"Cargando.......\".")]
    public float puntosPorSegundo = 5f;
    [Range(1, 12)] public int maximoPuntos = 7;

    [Header("Maquina de escribir (estatuas y dialogo de la Cazadora)")]
    [Tooltip("Segundos de pausa extra tras una coma, punto y coma o dos puntos.")]
    public float pausaComa = 0.12f;
    [Tooltip("Segundos de pausa extra tras un punto, interrogacion, exclamacion o puntos suspensivos.")]
    public float pausaPunto = 0.32f;
    [Tooltip("Cuanto varia la velocidad de letra a letra (0 = todas iguales, 0.3 = hasta un 30 % mas rapida o lenta).")]
    [Range(0f, 0.6f)] public float variacionEscritura = 0.25f;

    [Header("Movil (con los controles en pantalla)")]
    [Tooltip("Lo que sube TODO lo de abajo (almas, frascos, aviso y barra del jefe) para quedar por encima del joystick y los botones.")]
    public float subidaMovil = 560f;
    [Tooltip("Lo que se aparta a la derecha el estado del player, para dejar sitio al boton de menu de arriba a la izquierda.")]
    public float apartarEstadoMovil = 170f;
    [Tooltip("En movil las almas y los frascos pasan a una FILA arriba a la derecha: su altura desde el borde de arriba.")]
    public float filaArribaMovil = 34f;
    [Tooltip("En movil, lo que baja el deseo del totem para no quedar debajo de esa fila.")]
    public float bajarDeseoMovil = 100f;
    [Tooltip("En movil la barra del jefe va abajo del todo, por debajo del boton FIJAR y del joystick: su altura desde el borde de abajo.")]
    public float alturaBarraMovil = 70f;
    [Tooltip("En movil, lo que se le deja libre a la derecha: el boton de atacar llega a 420 px del borde, asi que 440 deja la barra justo a su lado.")]
    public float reservaDerechaMovil = 440f;

    // Valen 0 cuando no hay controles en pantalla, asi que en PC nada cambia.
    public float Subida => ModoMovil.Activo ? subidaMovil : 0f;
    public float ApartarEstado => ModoMovil.Activo ? apartarEstadoMovil : 0f;
    public float ArribaDeseo => margenArribaDeseo + (ModoMovil.Activo ? bajarDeseoMovil : 0f);

    // Donde va una caja de la columna de abajo a la derecha (almas y frascos).
    // En movil los controles ocupan todo lo de abajo, asi que esa columna pasa a
    // ser una FILA arriba a la derecha: "xFila" es cuanto se corre a la izquierda
    // dentro de la fila (0 = pegada al borde derecho).
    public void ColocarColumna(RectTransform r, float xColumna, float alturaAbajo, float xFila)
    {
        if (ModoMovil.Activo)
        {
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-margen - xFila, -filaArribaMovil);
            return;
        }
        r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0f);
        r.anchoredPosition = new Vector2(-margen - xColumna, alturaAbajo + Subida);
    }

    [Header("Subtitulos de la Cazadora")]
    [Tooltip("Separacion entre la barra del jefe y sus frases (para no tapar la barra ni las marcas I II III).")]
    public float separacionSubtitulo = 95f;

    // Segundos de espera tras mostrar la letra c (la maquina de escribir). La
    // pausa de puntuacion va solo al final del grupo: "..." pausa una vez.
    public float Retraso(char c, char siguiente, float letrasPorSegundo)
    {
        float r = 1f / Mathf.Max(1f, letrasPorSegundo) * Random.Range(1f - variacionEscritura, 1f + variacionEscritura);
        if (EsPuntuacion(siguiente)) return r;
        if (c == ',' || c == ';' || c == ':') r += pausaComa;
        else if (c == '.' || c == '?' || c == '!' || c == '…') r += pausaPunto;
        return r;
    }

    private static bool EsPuntuacion(char c) => c == '.' || c == ',' || c == ';' || c == ':' || c == '?' || c == '!' || c == '…' || c == '»' || c == '"';

    private static AjustesInterfaz instancia;

    public static AjustesInterfaz Get()
    {
        if (instancia != null) return instancia;
        instancia = Resources.Load<AjustesInterfaz>("AjustesInterfaz");
        if (instancia == null) instancia = CreateInstance<AjustesInterfaz>();
        return instancia;
    }

    // Donde empieza (x, desde la izquierda), cuanto mide y a que altura va la
    // barra del jefe en un lienzo de este ancho.
    public void Barra(float anchoLienzo, out float x, out float ancho, out float altura)
    {
        x = Mathf.Max(margen, anchoLienzo * inicioBarra);
        // Hasta la columna de la derecha.
        ancho = anchoLienzo - margen - anchoColumna - holgura - x;
        // En movil va abajo del todo: por debajo del joystick (que empieza mas
        // arriba) y del boton FIJAR, y acaba antes del boton grande de atacar.
        if (ModoMovil.Activo)
        {
            altura = alturaBarraMovil;
            // Llega hasta el boton de atacar (no hay columna abajo que esquivar).
            ancho = anchoLienzo - reservaDerechaMovil - x;
            if (ancho < anchoBarraMinimo) ancho = anchoLienzo - 2f * x;
            return;
        }
        altura = alturaBarra;
        if (ancho >= anchoBarraMinimo) return;
        // No cabe: por encima de la columna, con el mismo largo relativo.
        ancho = anchoLienzo - 2f * x;
        altura = alturaBarraElevada;
    }
}
