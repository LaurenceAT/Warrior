using UnityEngine;

// El totem de los desafios, junto a la hoguera: flota despacio, sus runas
// brillan un poco y con la F abre la tienda (TiendaTotem).
public class TotemTienda : MonoBehaviour, IInteractuable
{
    private SpriteRenderer sr, contorno;
    private Material material;
    private Vector3 baseLocal;

    public Vector2 PuntoInteraccion => transform.position;
    public float RadioInteraccion => 1.6f;
    public bool PuedeInteractuar => !TiendaTotem.Abierta;
    public string TextoInteraccion => "Presiona F para comerciar";

    // "suelo": punto del suelo donde se pone.
    public static TotemTienda Crear(Vector3 suelo)
    {
        FichaTienda f = FichaTienda.Get();
        GameObject go = new GameObject("Totem");
        go.transform.position = suelo;
        TotemTienda t = go.AddComponent<TotemTienda>();

        if (f != null && f.sombraTotem != null)
        {
            SpriteRenderer sombra = new GameObject("Sombra").AddComponent<SpriteRenderer>();
            sombra.transform.SetParent(go.transform, false);
            sombra.sprite = f.sombraTotem;
            float es = 1.1f / Mathf.Max(0.01f, f.sombraTotem.bounds.size.x);
            sombra.transform.localScale = new Vector3(es, es, 1f);
            sombra.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            sombra.sortingLayerName = "Middleground";
            sombra.sortingOrder = 2;
            sombra.color = new Color(1f, 1f, 1f, 0.6f);
        }

        t.sr = new GameObject("Obelisco").AddComponent<SpriteRenderer>();
        t.sr.transform.SetParent(go.transform, false);
        t.sr.sortingLayerName = "Middleground";
        t.sr.sortingOrder = 3;
        if (f != null && f.spriteTotem != null)
        {
            t.sr.sprite = f.spriteTotem;
            float e = f.altoTotem / Mathf.Max(0.01f, f.spriteTotem.bounds.size.y);
            t.sr.transform.localScale = new Vector3(e, e, 1f);
            // La base del obelisco un poco por encima del suelo.
            t.sr.transform.localPosition = new Vector3(0f, f.alturaVuelo - f.spriteTotem.bounds.min.y * e, 0f);
        }
        t.baseLocal = t.sr.transform.localPosition;
        AvisoInteraccion.Crear(go.transform, t, (f != null ? f.altoTotem + f.alturaVuelo : 2.8f) + 0.4f);
        return t;
    }

    private void OnEnable() => Interacciones.Registrar(this);
    private void OnDisable() => Interacciones.Quitar(this);

    public void Interactuar(PlayerControler p) => TiendaTotem.Abrir(p);

    private void LateUpdate()
    {
        FichaTienda f = FichaTienda.Get();
        if (sr == null || f == null) return;
        // Flota despacio.
        sr.transform.localPosition = baseLocal + Vector3.up * Mathf.Sin(Time.time * f.ritmoVaiven) * f.vaivenTotem;

        // Brillo suave de las runas: un contorno de un pixel que late.
        if (contorno == null)
        {
            Shader s = RecursosRPG.Get().shaderAura;
            if (s == null) s = Shader.Find("Sprites/Aura");
            if (s == null) return;
            material = new Material(s);
            material.SetFloat("_Width", 1f);
            material.SetFloat("_Inner", 0.05f);
            contorno = new GameObject("Brillo").AddComponent<SpriteRenderer>();
            contorno.transform.SetParent(sr.transform, false);
            contorno.sharedMaterial = material;
            contorno.sortingLayerID = sr.sortingLayerID;
            contorno.sortingOrder = sr.sortingOrder + 1;
        }
        contorno.sprite = sr.sprite;
        material.SetColor("_AuraColor", f.brilloRunas);
        material.SetFloat("_Amount", 0.25f + 0.2f * Mathf.Sin(Time.time * 2f));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
