using System;
using UnityEngine;

// Mochila que reemplaza a los cofres: se rompe a golpes y suelta su premio.
// Flota sobre el suelo y gira sobre su eje vertical; como el juego es 2D, el giro
// se simula escalando el ancho con el coseno del ángulo (igual que una moneda que da vueltas).
// Cuando le queda un solo golpe, parpadea para avisar que está por romperse.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BreakableBackpack : MonoBehaviour
{
    [Header("Giro")]
    [Tooltip("Segundos por vuelta completa.")]
    [SerializeField, Min(.2f)] private float spinPeriod = 2.6f;
    [Tooltip("Oscurece la cara de atrás para que se note el giro (1 = sin cambio).")]
    [SerializeField, Range(.4f, 1f)] private float backShade = .78f;

    [Header("Flotación")]
    [Tooltip("Altura media sobre su posición inicial, en unidades.")]
    [SerializeField, Min(0f)] private float hoverHeight = .2f;
    [Tooltip("Amplitud del vaivén vertical, en unidades.")]
    [SerializeField, Min(0f)] private float bobAmplitude = .08f;
    [SerializeField, Min(.2f)] private float bobPeriod = 1.8f;

    private SpriteRenderer drawing;
    private BoxCollider2D area;
    private Vector2 areaSize;
    private Vector3 baseScale, basePosition, shadowScale;
    private Color baseColor;
    private Transform shadow;
    private GameObject companion;
    private int hitsLeft, maxHits;
    private float phase, squashUntil, lockedNoticeAt = float.NegativeInfinity;
    private Func<bool> canBreak;
    private Action lockedNotice, broken;
    private bool done;

    public bool AboutToBreak => !done && hitsLeft == 1;

    // canBreakNow: false mientras la mochila esté protegida (por ejemplo, por un guardia).
    public void Configure(int hits, Func<bool> canBreakNow, Action onLocked, Action onBroken)
    {
        drawing = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale; basePosition = transform.position; baseColor = drawing.color;
        maxHits = hitsLeft = Mathf.Max(1, hits);
        canBreak = canBreakNow; lockedNotice = onLocked; broken = onBroken;
        // Collider de disparo: el arma la detecta, pero no estorba al caminar.
        area = GetComponent<BoxCollider2D>();
        if (area == null) area = gameObject.AddComponent<BoxCollider2D>();
        area.isTrigger = true; areaSize = area.size;
        // Cada mochila arranca en otro punto del giro para que no se muevan todas a la vez.
        phase = Mathf.Repeat(basePosition.x * .37f + basePosition.y * .21f, 1f);
    }

    // Sombra en el suelo: se achica y aclara cuando la mochila sube.
    public void SetShadow(Transform floorShadow)
    {
        shadow = floorShadow;
        if (shadow != null) shadowScale = shadow.localScale;
    }

    // Objeto que acompaña a la mochila (por ejemplo, su brillo) y desaparece con ella.
    public void SetCompanion(GameObject other) { companion = other; }

    // Lo llama el ataque del jugador. Devuelve true si el golpe contó.
    public bool Hit()
    {
        if (done) return false;
        if (canBreak != null && !canBreak())
        {
            // Un aviso cada tanto, no en cada golpe.
            if (lockedNotice != null && Time.time - lockedNoticeAt > 1.5f) { lockedNoticeAt = Time.time; lockedNotice(); }
            squashUntil = Time.time + .08f;
            return false;
        }
        hitsLeft--;
        squashUntil = Time.time + .14f;
        if (GamePresentation.Active != null) GamePresentation.Active.Impact(transform.position, false);
        if (hitsLeft <= 0) Break();
        return true;
    }

    private void Break()
    {
        done = true;
        if (GamePresentation.Active != null) GamePresentation.Active.Impact(transform.position + Vector3.up * .3f, false);
        if (broken != null) broken();
        Hide();
    }

    // Para partidas cargadas: la mochila ya estaba rota.
    public void SetBroken() { done = true; Hide(); }

    private void Hide()
    {
        if (shadow != null) shadow.gameObject.SetActive(false);
        if (companion != null) companion.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (drawing == null || done) return;
        float time = Time.time;

        // Flotación: sube y baja sobre una altura fija.
        float bob = Mathf.Sin((time / bobPeriod + phase) * Mathf.PI * 2f);
        transform.position = basePosition + new Vector3(0f, hoverHeight + bob * bobAmplitude, 0f);

        // Giro: el ancho sigue al coseno del ángulo. Con el coseno negativo se ve la cara de atrás (reflejada).
        float turn = Mathf.Cos((time / spinPeriod + phase) * Mathf.PI * 2f);
        float width = Mathf.Max(.06f, Mathf.Abs(turn));  // nunca del todo plana, para que no desaparezca
        drawing.flipX = turn < 0f;
        // Al recibir un golpe se aplasta un instante.
        Vector3 squash = time < squashUntil ? new Vector3(1.12f, .86f, 1f) : Vector3.one;
        transform.localScale = new Vector3(baseScale.x * width * squash.x, baseScale.y * squash.y, baseScale.z);
        // El área de golpe no se adelgaza con el giro: se compensa el ancho del collider.
        if (area != null) area.size = new Vector2(areaSize.x / width, areaSize.y);

        if (shadow != null)
        {
            float lift = (bob + 1f) * .5f; // 0 abajo, 1 arriba
            shadow.localScale = new Vector3(shadowScale.x * Mathf.Lerp(1f, .75f, lift) * Mathf.Lerp(.7f, 1f, width), shadowScale.y, shadowScale.z);
        }

        // Color: cara de atrás más oscura, rojiza tras un golpe y parpadeo cuando le queda uno.
        Color color = baseColor;
        if (turn < 0f) color = new Color(color.r * backShade, color.g * backShade, color.b * backShade, color.a);
        if (AboutToBreak)
        {
            bool flash = Mathf.Repeat(time, .18f) < .09f;
            color = flash ? Color.Lerp(color, Color.white, .65f) : new Color(color.r, color.g, color.b, .45f);
        }
        else if (hitsLeft < maxHits) color = Color.Lerp(color, new Color(1f, .75f, .7f), .25f);
        drawing.color = color;
    }
}
