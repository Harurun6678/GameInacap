using NeonSpite.Generation;
using UnityEngine;

// Plataforma de una sala inversa: se mueve según su definición y, si es inestable,
// cede cuando el jugador se queda quieto encima. Reaparece al cabo de unos segundos.
[DefaultExecutionOrder(-100)] // se mueve antes de que el jugador calcule su velocidad
[RequireComponent(typeof(Rigidbody2D))]
public sealed class InverseMover : MonoBehaviour
{
    private const float RespawnSeconds = 2.2f;
    private static readonly Color Warning = new Color(1f, .3f, .45f);

    private InversePlatform spec;
    private Vector2 roomOrigin;
    private PlayerController player;
    private Rigidbody2D body;
    private Collider2D solid;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private float crumbleSeconds, standing, back;
    private bool gone;

    // Velocidad actual: el jugador la suma a la suya mientras está encima.
    public Vector2 Velocity { get; private set; }

    public void Configure(InversePlatform platform, Vector2 origin, PlayerController actor, float crumbleTime)
    {
        spec = platform; roomOrigin = origin; player = actor; crumbleSeconds = crumbleTime;
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        solid = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        transform.position = Target(Time.fixedTime);
        body.position = transform.position;
    }

    private Vector2 Target(float time)
    {
        // El centro del collider queda media altura (0.125) por debajo de la cara superior.
        return roomOrigin + new Vector2(spec.CenterAt(time), spec.TopAt(time) - .125f);
    }

    private void OnEnable()
    {
        // Al recargar la sala, la plataforma vuelve entera y sin aviso pendiente.
        if (gone) Restore();
        standing = 0f;
        // Mientras la sala estuvo descargada el tiempo siguió corriendo: se coloca donde corresponde
        // en vez de recorrer la distancia de golpe (eso arrastraría al jugador).
        Velocity = Vector2.zero;
        if (body != null) { transform.position = Target(Time.fixedTime); body.position = transform.position; }
    }

    private void FixedUpdate()
    {
        if (body == null) return;
        if (spec.Motion != InverseMotion.None)
        {
            Vector2 target = Target(Time.fixedTime + Time.fixedDeltaTime);
            Velocity = (target - body.position) / Time.fixedDeltaTime;
            body.MovePosition(target);
        }
        if (!spec.Crumbles) return;
        if (gone)
        {
            if (Time.time >= back) Restore();
            return;
        }
        bool onTop = player != null && player.GroundCollider == solid;
        // Quedarse encima carga el contador; bajarse lo descarga a la mitad de velocidad.
        standing = onTop ? standing + Time.fixedDeltaTime : Mathf.Max(0f, standing - Time.fixedDeltaTime * .5f);
        if (standing >= crumbleSeconds) Collapse();
    }

    private void Update()
    {
        if (!spec.Crumbles || renderers == null || gone) return;
        float ratio = crumbleSeconds > 0f ? standing / crumbleSeconds : 0f;
        // Se enrojece mientras aguanta y parpadea al final: aviso para saltar.
        bool blink = ratio > .55f && Mathf.Repeat(Time.time, .12f) < .06f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color color = Color.Lerp(baseColors[i], Warning, ratio);
            if (blink) color.a *= .45f;
            renderers[i].color = color;
        }
    }

    private void Collapse()
    {
        gone = true; standing = 0f; back = Time.time + RespawnSeconds;
        solid.enabled = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) { Color c = baseColors[i]; c.a *= .12f; renderers[i].color = c; }
    }

    private void Restore()
    {
        gone = false; standing = 0f;
        if (solid != null) solid.enabled = true;
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = baseColors[i];
    }
}
