using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de Logros del menu principal: tres secciones (Partida, Desafios y
// Dificil). Los que faltan se ven apagados, con un candado y "???": sin pistas.
// Arriba, cuantos llevas (en total y por seccion). No necesita partida.
public class MenuLogros : MonoBehaviour
{
    private readonly List<Button> pestanas = new List<Button>();
    private TextMeshProUGUI total;
    private RectTransform lista;
    private Button botonVolver;
    private FichaLogro.Seccion seccion;
    private System.Action volverAlMenu;
    private readonly GrupoMarcado grupo = new GrupoMarcado();

    public static MenuLogros Construir(Transform lienzo, System.Action volver)
    {
        RectTransform raiz = new GameObject("Logros", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(lienzo, false);
        EstiloMenu.Estirar(raiz);
        MenuLogros m = raiz.gameObject.AddComponent<MenuLogros>();
        m.volverAlMenu = volver;
        m.Montar(raiz);
        return m;
    }

    public void Abrir()
    {
        total.text = $"{Logros.Desbloqueados()}/{Logros.Total()} desbloqueados";
        Mostrar(FichaLogro.Seccion.Partida);
    }

    private static readonly string[] Nombres = { "Partida", "Desafíos", "Difícil" };

    // La pestana abierta queda marcada (clic o Enter; pasar el raton solo la
    // resalta). Todas dicen cuantos llevas.
    private void PintarPestanas()
    {
        grupo.Marcar(pestanas[(int)seccion].GetComponent<OpcionEstilo>());
        for (int i = 0; i < pestanas.Count; i++)
        {
            FichaLogro.Seccion s = (FichaLogro.Seccion)i;
            pestanas[i].GetComponentInChildren<TextMeshProUGUI>().text =
                $"{Nombres[i]}  <size=80%><color=#8f8780>{Logros.Desbloqueados(s)}/{Logros.Total(s)}</color></size>";
        }
    }

    public GameObject Primero() => pestanas.Count > 0 ? pestanas[(int)seccion].gameObject : botonVolver.gameObject;

    private void Mostrar(FichaLogro.Seccion s)
    {
        seccion = s;
        for (int i = lista.childCount - 1; i >= 0; i--) { GameObject h = lista.GetChild(i).gameObject; h.transform.SetParent(null); Destroy(h); }
        foreach (FichaLogro l in FichaLogro.Visibles().Where(x => x.seccion == s)) Tarjeta(l);
        PintarPestanas();
    }

    private void Tarjeta(FichaLogro l)
    {
        bool tiene = Globales.TieneLogro(l.id);
        Image filo = EstiloMenu.Caja("Logro", lista, tiene ? EstiloMenu.Filo : new Color(0.3f, 0.28f, 0.27f, 0.6f));
        Image fondo = EstiloMenu.Caja("Fondo", filo.transform, tiene ? new Color(0.12f, 0.1f, 0.08f, 1f) : new Color(0.05f, 0.05f, 0.05f, 1f));
        EstiloMenu.Estirar(fondo.rectTransform, 2f);

        Image ico = EstiloMenu.Caja("Icono", fondo.transform, tiene ? Color.white : new Color(1f, 1f, 1f, 0.7f));
        ico.sprite = tiene ? l.icono : IconosDibujados.Candado();
        ico.preserveAspect = true;
        RectTransform ri = ico.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(80f, 80f);
        ri.anchoredPosition = new Vector2(20f, 0f);

        TextMeshProUGUI n = EstiloMenu.Texto(tiene ? l.nombre : "???", fondo.transform, 28, tiene ? EstiloMenu.Titulo : EstiloMenu.TextoApagado);
        n.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform rn = n.rectTransform;
        rn.anchorMin = new Vector2(0f, 0.5f); rn.anchorMax = new Vector2(1f, 1f);
        rn.offsetMin = new Vector2(122f, 2f); rn.offsetMax = new Vector2(-16f, -10f);
        TextMeshProUGUI d = EstiloMenu.Texto(tiene ? l.descripcion : "???", fondo.transform, 21, tiene ? EstiloMenu.TextoElegido : EstiloMenu.TextoApagado);
        d.alignment = TextAlignmentOptions.TopLeft;
        d.enableWordWrapping = true;
        RectTransform rd = d.rectTransform;
        rd.anchorMin = new Vector2(0f, 0f); rd.anchorMax = new Vector2(1f, 0.5f);
        rd.offsetMin = new Vector2(122f, 8f); rd.offsetMax = new Vector2(-16f, -4f);
    }

    private void Montar(RectTransform raiz)
    {
        Image velo = EstiloMenu.Caja("Velo", raiz, new Color(0f, 0f, 0f, 0.9f));
        velo.raycastTarget = true;
        EstiloMenu.Estirar(velo.rectTransform);
        RectTransform panel = EstiloMenu.Panel(raiz, "LOGROS", new Vector2(1500f, 920f));

        total = EstiloMenu.Texto("", panel, 26, new Color(0.82f, 0.76f, 0.68f));
        total.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(total.rectTransform, -110f, 36f);

        RectTransform barra = new GameObject("Pestanas", typeof(RectTransform)).GetComponent<RectTransform>();
        barra.SetParent(panel, false);
        barra.anchorMin = new Vector2(0f, 1f); barra.anchorMax = new Vector2(1f, 1f);
        barra.pivot = new Vector2(0.5f, 1f);
        barra.offsetMin = new Vector2(80f, -220f); barra.offsetMax = new Vector2(-80f, -156f);
        HorizontalLayoutGroup h = barra.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = true;
        for (int i = 0; i < 3; i++)
        {
            FichaLogro.Seccion s = (FichaLogro.Seccion)i;
            Button b = EstiloMenu.Opcion(barra, "", () => Mostrar(s), 60f, 28f);
            grupo.Anadir(b.GetComponent<OpcionEstilo>());
            pestanas.Add(b);
        }

        lista = new GameObject("Lista", typeof(RectTransform)).GetComponent<RectTransform>();
        lista.SetParent(panel, false);
        lista.anchorMin = Vector2.zero; lista.anchorMax = Vector2.one;
        lista.offsetMin = new Vector2(80f, 130f); lista.offsetMax = new Vector2(-80f, -250f);
        GridLayoutGroup g = lista.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(655f, 120f);
        g.spacing = new Vector2(20f, 18f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;

        VerticalLayoutGroup col = EstiloMenu.Columna(panel, 560f, 560f, 790f, 36f, 0f);
        botonVolver = EstiloMenu.Opcion(col.transform, "Volver", () => volverAlMenu?.Invoke(), 54f, 26f);
        botonVolver.GetComponent<OpcionEstilo>().animar = true;
    }
}
