using UnityEngine;

// El frasco de sangre que el Black Knight te arranca en el Diezmo: sale de tu
// cinturon, vuela a su mano, se inclina mientras bebe (y se vacia) y acaba
// tirado por los aires. Es solo imagen; el robo y la curacion los hace el jefe.
public class FrascoRobadoBK : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 desde;
    private Color color;
    private bool tirado;
    private Vector2 vel;

    public static FrascoRobadoBK Crear(Sprite frasco, Vector2 pos, Color color)
    {
        if (frasco == null) return null;
        GameObject go = new GameObject("FrascoRobadoBK");
        go.transform.position = pos;
        FrascoRobadoBK f = go.AddComponent<FrascoRobadoBK>();
        f.sr = DibujosCazadora.Renderer(go, "VFX", 75);
        f.sr.sprite = frasco;
        f.desde = pos;
        f.color = color;
        go.transform.localScale = Vector3.one * 1.4f;
        PeligrosJefe.Registrar(go);
        return f;
    }

    // Va de tu cinturon a su mano, dando vueltas.
    public void Volar(Vector2 mano, float k)
    {
        if (tirado) return;
        k = Mathf.Clamp01(k);
        Vector2 pos = Vector2.Lerp(desde, mano, k * k);
        pos.y += Mathf.Sin(k * Mathf.PI) * 0.9f;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, 0f, -540f * k);
        transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1.1f, k);
    }

    // En su mano, inclinandose cada vez mas mientras se vacia.
    public void Beber(Vector2 mano, float k)
    {
        if (tirado) return;
        k = Mathf.Clamp01(k);
        transform.position = mano + new Vector2(0f, 0.15f * Mathf.Sin(Time.time * 8f) * 0.1f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -115f, k));
        sr.color = Color.Lerp(Color.white, new Color(0.45f, 0.4f, 0.42f, 1f), k);
        if (Random.value < 0.12f)
            ParticulasFx.Rafaga(mano, 1, color, Color.white, new Vector2(0.4f, 1.2f), 1.2f,
                                new Vector2(0.04f, 0.07f), new Vector2(0.2f, 0.4f));
    }

    // Lo tira por encima del hombro: cae y se va.
    public void Tirar(int dir)
    {
        tirado = true;
        vel = new Vector2(-dir * Random.Range(2.5f, 4f), Random.Range(4f, 6f));
    }

    private void Update()
    {
        if (!tirado) return;
        vel.y -= 18f * Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);
        transform.Rotate(0f, 0f, -420f * Time.deltaTime);
        Color c = sr.color;
        c.a -= Time.deltaTime * 1.2f;
        sr.color = c;
        if (c.a <= 0f) Destroy(gameObject);
    }
}
