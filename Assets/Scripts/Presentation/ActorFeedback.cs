using UnityEngine;

// El destello marca un impacto; la barra de los enemigos aparece solo al recibir daño.
public sealed class ActorFeedback : MonoBehaviour
{
    private EnemyHealth enemy;
    private PlayerHealth player;
    private EnemyAI ai;
    private SpriteRenderer drawing, bar, barBackground;
    private Color baseColor;
    private Vector3 baseScale;
    private float flashUntil, barUntil, shownPulse;
    private void Awake()
    {
        enemy = GetComponent<EnemyHealth>();
        player = GetComponent<PlayerHealth>();
        ai = GetComponent<EnemyAI>();
        drawing = GetComponentInChildren<SpriteRenderer>();
        baseColor = drawing != null ? drawing.color : Color.white;
        baseScale = drawing != null ? drawing.transform.localScale : Vector3.one;
        if (enemy != null && GamePresentation.Active != null)
        {
            barBackground = CreateBar("Vida_Fondo", new Color(.025f, .035f, .06f), .96f, 1.15f);
            bar = CreateBar("Vida_Enemigo", KenneyTheme.Rose, .9f, 1.15f);
        }
    }
    private SpriteRenderer CreateBar(string name, Color color, float width, float y)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = new Vector3(0, y, 0);
        obj.transform.localScale = new Vector3(width, .065f, 1);
        SpriteRenderer r = obj.AddComponent<SpriteRenderer>();
        r.sprite = GamePresentation.Active.Pixel;
        r.sharedMaterial = GamePresentation.Active.WorldMaterial;
        r.color = color;
        r.sortingOrder = 45;
        return r;
    }
    private void OnEnable()
    {
        if (enemy != null)
            enemy.Damaged += Hit;
        if (player != null)
            player.Damaged += Hit;
    }
    private void OnDisable()
    {
        if (enemy != null)
            enemy.Damaged -= Hit;
        if (player != null)
            player.Damaged -= Hit;
    }
    private void Hit(int amount)
    {
        flashUntil = Time.time + .09f;
        barUntil = Time.time + 2.5f;
        if (GamePresentation.Active != null)
            GamePresentation.Active.Impact(transform.position, player != null);
    }
    private void LateUpdate()
    {
        if (Time.timeScale <= 0)
            return;
        if (drawing != null)
        {
            Color color = Time.time < flashUntil ? (player != null ? KenneyTheme.Rose : Color.white) : ai != null && ai.WindingUp ? KenneyTheme.Gold : baseColor;
            // El jugador parpadea mientras dura la invulnerabilidad tras un golpe.
            if (player != null && player.IsRecovering && Time.time >= flashUntil && Mathf.Repeat(Time.time, .16f) < .08f)
                color.a *= .35f;
            drawing.color = color;
            // El enemigo se hincha mientras prepara el golpe: avisa cuándo esquivar.
            if (ai != null)
            {
                float pulse = ai.WindupProgress;
                if (pulse != shownPulse)
                {
                    shownPulse = pulse;
                    drawing.transform.localScale = baseScale * (1f + .16f * pulse);
                }
            }
        }
        if (bar == null)
            return;
        bool show = enemy != null && enemy.CurrentHealth > 0 && Time.time < barUntil;
        bar.enabled = barBackground.enabled = show;
        bar.transform.localScale = new Vector3(.9f * enemy.CurrentHealth / Mathf.Max(1, enemy.maxHealth), .065f, 1);
    }
}
