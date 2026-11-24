using UnityEngine;

// Bubble tea de curación: flota, suelta brillitos rosas y desaparece con un estallido al tomarlo.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class HealingDrink : MonoBehaviour
{
    private struct Sparkle
    {
        public SpriteRenderer Renderer;
        public float Born, Life, Size, Spin;
        public Vector2 Velocity;
        public Color Color;
    }

    private const int IdleSparkles = 8;
    private const int BurstSparkles = 16;
    private const float BobHeight = .07f;
    private const float BobSpeed = 2.3f;
    private const float PopDuration = .32f;
    private static readonly Color[] Pinks =
    {
        new Color(1f, .5f, .8f), new Color(1f, .76f, .91f), new Color(.92f, .6f, 1f), new Color(1f, .93f, .97f)
    };
    private static Sprite pixel;

    private SpriteRenderer drawing;
    private Transform sparkleRoot;
    private Sparkle[] pool;
    private Vector3 basePosition, baseScale;
    private Color baseColor;
    private float phase, nextSparkle, consumedAt = -1f;

    public bool Consumed { get; private set; }

    // Añade el comportamiento a un sprite ya colocado y escalado.
    public static HealingDrink Attach(GameObject obj)
    {
        if (obj == null) return null;
        HealingDrink drink = obj.GetComponent<HealingDrink>();
        return drink != null ? drink : obj.AddComponent<HealingDrink>();
    }

    private void Awake()
    {
        drawing = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
        baseColor = drawing.color;
        phase = Random.value * Mathf.PI * 2f;
        nextSparkle = Time.time + Random.Range(0f, .3f);
        // Los brillos van en un hermano para que no floten ni desaparezcan con el vaso.
        sparkleRoot = new GameObject(name + "_Brillos").transform;
        sparkleRoot.SetParent(transform.parent, false);
        // Escala de mundo 1, para que el tamaño de los brillos no dependa del padre.
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        sparkleRoot.localScale = new Vector3(1f / Mathf.Max(.0001f, parentScale.x), 1f / Mathf.Max(.0001f, parentScale.y), 1f);
        Material material = GamePresentation.Active != null && GamePresentation.Active.WorldMaterial != null ? GamePresentation.Active.WorldMaterial : drawing.sharedMaterial;
        pool = new Sparkle[IdleSparkles + BurstSparkles];
        for (int i = 0; i < pool.Length; i++)
        {
            GameObject obj = new GameObject("Brillo");
            obj.transform.SetParent(sparkleRoot, false);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = Pixel();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = drawing.sortingLayerID;
            renderer.sortingOrder = drawing.sortingOrder + 1;
            renderer.enabled = false;
            pool[i].Renderer = renderer;
        }
    }

    private void OnDestroy()
    {
        if (sparkleRoot != null) Destroy(sparkleRoot.gameObject);
    }

    // Toma el bubble tea: estallido de brillos y el vaso se desvanece.
    public void Consume()
    {
        if (Consumed) return;
        Consumed = true;
        consumedAt = Time.time;
        Bounds bounds = drawing.bounds;
        for (int i = 0; i < BurstSparkles; i++)
        {
            float angle = (i + Random.value * .6f) / BurstSparkles * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Spawn(bounds.center + (Vector3)(direction * bounds.extents.y * .35f), direction * Random.Range(1.4f, 2.6f) + Vector2.up * .6f,
                Random.Range(.45f, .75f), Random.Range(.09f, .15f));
        }
    }

    // Estado ya consumido al cargar una partida: sin animación.
    public void SetConsumed()
    {
        Consumed = true;
        consumedAt = -1f;
        if (drawing == null) drawing = GetComponent<SpriteRenderer>();
        drawing.enabled = false;
        if (pool != null)
            for (int i = 0; i < pool.Length; i++) pool[i].Renderer.enabled = false;
        enabled = false;
    }

    private void Update()
    {
        float now = Time.time;
        if (!Consumed)
        {
            // Flota sobre su base: nunca baja del punto donde se colocó.
            float bob = (Mathf.Sin(now * BobSpeed + phase) + 1f) * .5f * BobHeight;
            transform.localPosition = basePosition + Vector3.up * bob;
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(now * BobSpeed * .5f + phase) * 3f);
            if (now >= nextSparkle)
            {
                nextSparkle = now + Random.Range(.16f, .34f);
                Bounds bounds = drawing.bounds;
                Vector3 at = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y + bounds.extents.y * .2f, bounds.max.y), 0);
                Spawn(at, new Vector2(Random.Range(-.12f, .12f), Random.Range(.25f, .55f)), Random.Range(.7f, 1.1f), Random.Range(.06f, .11f));
            }
        }
        else if (consumedAt >= 0f && drawing.enabled)
        {
            // Se infla un poco y luego se encoge hasta desaparecer.
            float t = Mathf.Clamp01((now - consumedAt) / PopDuration);
            float scale = t < .3f ? Mathf.Lerp(1f, 1.22f, t / .3f) : Mathf.Lerp(1.22f, 0f, (t - .3f) / .7f);
            transform.localScale = baseScale * scale;
            drawing.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t * t));
            if (t >= 1f) drawing.enabled = false;
        }
        bool any = UpdateSparkles(now);
        if (Consumed && !drawing.enabled && !any) enabled = false;
    }

    private void Spawn(Vector3 position, Vector2 velocity, float life, float size)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].Renderer.enabled) continue;
            pool[i].Born = Time.time;
            pool[i].Life = life;
            pool[i].Size = size;
            pool[i].Velocity = velocity;
            pool[i].Spin = Random.Range(-90f, 90f);
            pool[i].Color = Pinks[Random.Range(0, Pinks.Length)];
            Transform t = pool[i].Renderer.transform;
            t.position = new Vector3(position.x, position.y, transform.position.z);
            t.localRotation = Quaternion.Euler(0, 0, 45f);
            t.localScale = Vector3.zero;
            pool[i].Renderer.color = pool[i].Color;
            pool[i].Renderer.enabled = true;
            return;
        }
    }

    private bool UpdateSparkles(float now)
    {
        bool any = false;
        float dt = Time.deltaTime;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].Renderer.enabled) continue;
            float age = (now - pool[i].Born) / pool[i].Life;
            if (age >= 1f)
            {
                pool[i].Renderer.enabled = false;
                continue;
            }
            any = true;
            pool[i].Velocity *= 1f - Mathf.Min(1f, 2.2f * dt);
            Transform t = pool[i].Renderer.transform;
            t.position += (Vector3)(pool[i].Velocity * dt);
            t.Rotate(0, 0, pool[i].Spin * dt);
            // Destello: crece rápido, titila y se apaga.
            float twinkle = Mathf.Sin(age * Mathf.PI) * (.75f + .25f * Mathf.Sin(now * 22f + i));
            t.localScale = Vector3.one * pool[i].Size * twinkle;
            Color c = pool[i].Color;
            c.a = 1f - age * age;
            pool[i].Renderer.color = c;
        }
        return any;
    }

    private static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        pixel.hideFlags = HideFlags.DontSave;
        return pixel;
    }
}
