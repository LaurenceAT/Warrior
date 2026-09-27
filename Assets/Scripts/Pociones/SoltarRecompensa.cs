using System.Collections;
using UnityEngine;

// Va en el Mimic de la cueva secreta: al morir suelta un frasco que da una carga
// de pocion mas para siempre. Solo una vez: si el Mimic reaparece (hoguera,
// muerte del player) ya no vuelve a soltarlo.
[RequireComponent(typeof(EnemyHealth))]
public class SoltarRecompensa : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<EnemyHealth>().AlMorir += Soltar;
    }

    private void Soltar()
    {
        if (ReservaPociones.Get().RecompensaCogida) return;
        FrascoExtra.Crear((Vector2)transform.position + Vector2.up * 0.6f);
    }
}

// El frasco en el suelo: flota, brilla y, al tocarlo, suma la carga.
public class FrascoExtra : MonoBehaviour
{
    private Vector3 origen;
    private bool cogido;

    public static void Crear(Vector2 pos)
    {
        GameObject go = new GameObject("FrascoExtra");
        go.layer = LayerMask.NameToLayer("Items");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 1.6f;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite icono = RecursosRPG.Get().Icono("pocion");
        // El icono del pack, con el pivote en la base (como el frasco dibujado).
        sr.sprite = icono != null ? Sprite.Create(icono.texture, icono.rect, new Vector2(0.5f, 0f), icono.rect.width * 0.9f)
                                  : ReservaPociones.Frasco();
        sr.sortingLayerName = "Items";
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        CircleCollider2D c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.5f;
        c.offset = new Vector2(0f, 0.6f);
        go.AddComponent<FrascoExtra>();
    }

    private void Start()
    {
        origen = transform.position;
        StartCoroutine(Brillo());
    }

    private void Update()
    {
        transform.position = origen + Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.12f;
    }

    private IEnumerator Brillo()
    {
        while (!cogido)
        {
            ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.5f, 3, new Color(1f, 0.4f, 0.4f), new Color(1f, 0.85f, 0.6f),
                                new Vector2(0.3f, 0.8f), -0.2f, new Vector2(0.04f, 0.08f), new Vector2(0.5f, 1f));
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (cogido || !otro.CompareTag("Player")) return;
        cogido = true;
        ReservaPociones.Get().AumentarMaximo();
        ScreenFlash.Destello(new Color(1f, 0.85f, 0.6f, 0.25f), 0.3f);
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.5f, 24, new Color(1f, 0.3f, 0.3f), new Color(1f, 0.9f, 0.6f),
                            new Vector2(1.5f, 4f), -0.3f, new Vector2(0.05f, 0.12f), new Vector2(0.5f, 1.1f));
        MensajePantalla.Banner("FRASCO DE SANGRE: +1 POCIÓN", new Color(1f, 0.55f, 0.5f), 3f);
        Destroy(gameObject);
    }
}
