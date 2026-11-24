using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NeonSpite.Generation;

// Una base opaca siempre cubre la cámara, incluso al cambiar la resolución.
[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GamePresentation : MonoBehaviour
{
    public static GamePresentation Active
    {
        get; private set;
    }
    private Camera view;
    private Sprite pixel, vignetteSprite;
    private Material spriteMaterial;
    private Transform backing;
    private CanvasGroup zone;
    private Text zoneName, zoneDetail;
    private float zoneUntil;
    private Color accent;
    private sealed class Spark
    {
        public SpriteRenderer Renderer; public Vector2 Velocity; public float Life;
    }
    private readonly List<Spark> sparks = new List<Spark>();
    public Material WorldMaterial => spriteMaterial;
    public Sprite Pixel => pixel;
    private void Awake()
    {
        Active = this;
        view = GetComponent<Camera>();
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            spriteMaterial = new Material(shader);
            spriteMaterial.SetTexture("_MainTex", texture);
        }
        GameObject fill = new GameObject("Fondo_Negro_PantallaCompleta");
        fill.transform.SetParent(transform, false);
        backing = fill.transform;
        SpriteRenderer black = fill.AddComponent<SpriteRenderer>();
        black.sprite = pixel;
        black.color = Color.black;
        black.sortingOrder = -32760;
        black.sharedMaterial = spriteMaterial;
        backing.localPosition = new Vector3(0, 0, 10);
        BuildOverlay();
        FitBackground();
    }
    private void BuildOverlay()
    {
        RectTransform root = KenneyTheme.Rect(transform, "Atmosfera_Pantalla", 0, 0, 1600, 900);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        CanvasScaler scale = root.gameObject.AddComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1600, 900);
        scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = Mathf.Abs(x / 63f * 2 - 1), dy = Mathf.Abs(y / 63f * 2 - 1);
                float edge = Mathf.Clamp01((Mathf.Max(dx, dy) - .60f) / .4f);
                texture.SetPixel(x, y, new Color(0, 0, 0, edge * edge * .25f));
            }
        texture.Apply();
        vignetteSprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100);
        Image vignette = KenneyTheme.Image(root, "Vineta_Sutil", 0, 0, 1600, 900, Color.white);
        vignette.sprite = vignetteSprite;
        KenneyTheme.Stretch(vignette.rectTransform);
        RectTransform card = KenneyTheme.Rect(root, "Entrada_Zona", 0, 140, 880, 118);
        zone = card.gameObject.AddComponent<CanvasGroup>();
        zone.blocksRaycasts = false;
        zoneName = KenneyTheme.Label(card, "Nombre", "", 0, 20, 860, 55, 34, KenneyTheme.Paper, TextAnchor.MiddleCenter);
        zoneDetail = KenneyTheme.Label(card, "Detalle", "", 0, -29, 860, 35, 17, KenneyTheme.Gold, TextAnchor.MiddleCenter);
        zone.alpha = 0;
    }
    public void SetZone(string name, BiomeRules rules, string description = null)
    {
        accent = Hex(rules.Accent);
        zoneName.text = name.ToUpperInvariant();
        zoneDetail.text = description ?? rules.Description;
        zoneDetail.color = accent;
        zoneUntil = Time.time + 3f;
    }
    public static Color Hex(uint hex, float alpha = 1)
    {
        return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, alpha);
    }
    private void FitBackground()
    {
        if (view == null || backing == null)
            return;
        backing.localScale = new Vector3(view.orthographicSize * 2 * view.aspect + 2, view.orthographicSize * 2 + 2, 1);
    }
    private void LateUpdate()
    {
        FitBackground();
        if (hitStopActive && Time.unscaledTime >= hitStopUntil)
            EndHitStop();
        bool paused = Time.timeScale <= 0;
        zone.alpha = paused ? 0 : Mathf.MoveTowards(zone.alpha, Time.time < zoneUntil ? 1 : 0, Time.deltaTime * 3);
        if (paused)
            return;
        foreach (Spark spark in sparks)
        {
            if (spark.Life <= 0)
                continue;
            spark.Life -= Time.deltaTime;
            if (spark.Life <= 0)
            {
                spark.Renderer.enabled = false;
                continue;
            }
            spark.Velocity += Vector2.down * 8 * Time.deltaTime;
            spark.Renderer.transform.position += (Vector3)(spark.Velocity * Time.deltaTime);
            Color color = spark.Renderer.color;
            color.a = Mathf.Clamp01(spark.Life / .25f);
            spark.Renderer.color = color;
        }
    }
    // ---------- Pausa de impacto ----------
    // El juego se ralentiza unas centésimas en cada golpe para que se sienta el contacto.
    private const float HitStopScale = .05f;
    private bool hitStopActive;
    private float hitStopUntil;
    private CameraFollow follow;

    private void HitStop(float seconds)
    {
        // Nunca se aplica sobre una pausa ni sobre otro cambio de velocidad del juego.
        if (!hitStopActive && Time.timeScale != 1f)
            return;
        hitStopActive = true;
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        Time.timeScale = HitStopScale;
    }

    private void EndHitStop()
    {
        if (!hitStopActive)
            return;
        hitStopActive = false;
        hitStopUntil = 0;
        if (Mathf.Approximately(Time.timeScale, HitStopScale))
            Time.timeScale = 1f;
    }

    // Los menús llaman a esto antes de pausar, para no guardar la velocidad ralentizada.
    public static void CancelHitStop()
    {
        if (Active != null)
            Active.EndHitStop();
    }

    public void Impact(Vector2 position, bool playerHit)
    {
        if (follow == null)
            follow = GetComponent<CameraFollow>();
        if (follow != null)
            follow.Shake(playerHit ? .2f : .08f, playerHit ? .22f : .12f);
        HitStop(playerHit ? .08f : .045f);
        Color color = playerHit ? KenneyTheme.Rose : accent.maxColorComponent > 0 ? accent : KenneyTheme.Gold;
        for (int i = 0; i < 6; i++)
        {
            Spark spark = sparks.Find(s => s.Life <= 0);
            if (spark == null)
            {
                if (sparks.Count >= 60)
                    break;
                GameObject obj = new GameObject("Impacto_Pixel");
                obj.transform.SetParent(transform.parent, false);
                SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = pixel;
                renderer.sharedMaterial = spriteMaterial;
                renderer.sortingOrder = 42;
                spark = new Spark { Renderer = renderer };
                sparks.Add(spark);
            }
            float angle = (i * 60 + Time.frameCount % 35) * Mathf.Deg2Rad;
            spark.Velocity = new Vector2(Mathf.Cos(angle) * 2.5f, Mathf.Sin(angle) * 2.5f + 1);
            spark.Life = .28f;
            spark.Renderer.enabled = true;
            spark.Renderer.color = color;
            spark.Renderer.transform.position = position;
            spark.Renderer.transform.localScale = new Vector3(.07f, .07f, 1);
        }
    }
    private void OnDestroy()
    {
        EndHitStop();
        if (Active == this)
            Active = null;
        foreach (Spark spark in sparks)
            if (spark.Renderer != null)
                Destroy(spark.Renderer.gameObject);
        if (spriteMaterial != null)
            Destroy(spriteMaterial);
        if (pixel != null)
        {
            Destroy(pixel.texture);
            Destroy(pixel);
        }
        if (vignetteSprite != null)
        {
            Destroy(vignetteSprite.texture);
            Destroy(vignetteSprite);
        }
    }
}
