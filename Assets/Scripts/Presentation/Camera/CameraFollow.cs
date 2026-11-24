using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Si esta vacio, busca el objeto con tag Player al iniciar.")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";

    [Header("Seguimiento")]
    [Tooltip("Desplazamiento de la camara respecto al jugador.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);
    [Tooltip("Tiempo aproximado que tarda la camara en alcanzar al jugador. Valores bajos = mas pegada.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Limites del nivel (opcional)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds = new Vector2(-10f, -5f);
    [SerializeField] private Vector2 maxBounds = new Vector2(10f, 5f);

    private bool useRoomBounds;
    private Rect roomBounds;
    private bool useWorldBounds;
    private Rect worldBounds;
    private Camera view;
    private Vector3 velocity;
    private float fixedZ;

    // Sacudida breve de cámara (golpes). Se suma al seguimiento sin alterarlo.
    private Vector3 shakeOffset;
    private float shakeAmplitude, shakeDuration, shakeUntil;

    public void Shake(float amplitude, float duration)
    {
        if (duration <= 0f)
            return;
        // Una sacudida débil no interrumpe a una fuerte que sigue activa.
        if (amplitude < CurrentShake())
            return;
        shakeAmplitude = amplitude;
        shakeDuration = duration;
        shakeUntil = Time.unscaledTime + duration;
    }

    private float CurrentShake()
    {
        float remaining = shakeUntil - Time.unscaledTime;
        return remaining <= 0f || shakeDuration <= 0f ? 0f : shakeAmplitude * (remaining / shakeDuration);
    }

    private void Awake()
    {
        fixedZ = transform.position.z;
        view = GetComponent<Camera>();
    }

    public void SetWorldBounds(Rect bounds)
    {
        worldBounds = bounds;
        useWorldBounds = true;
        useBounds = false;
    }

    public void ClearWorldBounds()
    {
        useWorldBounds = false;
        useRoomBounds = false;
        useBounds = false;
    }

    public void SetRoomBounds(Rect bounds)
    {
        roomBounds = bounds;
        useRoomBounds = true;
    }

    public void SetTarget(Transform actor)
    {
        target = actor;
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        velocity = Vector3.zero;
        shakeOffset = Vector3.zero;
        if (target != null)
            transform.position = ComputeDesiredPosition();
    }

    private void Start()
    {
        if (target == null && !string.IsNullOrEmpty(targetTag))
        {
            GameObject player = GameObject.FindGameObjectWithTag(targetTag);
            if (player != null)
                target = player.transform;
        }

        if (target == null)
        {
            Debug.LogWarning("CameraFollow: no se encontro un objetivo para seguir.", this);
            return;
        }

        // Posiciona la camara de una vez para evitar un deslizamiento inicial.
        transform.position = ComputeDesiredPosition();
    }

    private void LateUpdate()
    {
        if (target == null || Time.timeScale <= 0f)
            return;

        Vector3 desired = ComputeDesiredPosition();
        // Se quita la sacudida anterior antes de suavizar, para que no desvíe el seguimiento.
        Vector3 follow = Vector3.SmoothDamp(transform.position - shakeOffset, desired, ref velocity, smoothTime);
        float strength = CurrentShake();
        if (strength > 0f)
        {
            float t = Time.unscaledTime * 45f;
            shakeOffset = new Vector3((Mathf.PerlinNoise(t, 0f) * 2f - 1f) * strength, (Mathf.PerlinNoise(0f, t) * 2f - 1f) * strength, 0f);
        }
        else
            shakeOffset = Vector3.zero;
        transform.position = follow + shakeOffset;
    }

    private Vector3 ComputeDesiredPosition()
    {
        Vector3 pos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            fixedZ);

        if (useBounds)
        {
            pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
            pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        }

        if (useRoomBounds && view != null)
        {
            float halfY = view.orthographicSize, halfX = halfY * view.aspect;
            pos.x = roomBounds.width < halfX * 2 ? roomBounds.center.x : Mathf.Clamp(pos.x, roomBounds.xMin + halfX, roomBounds.xMax - halfX);
            pos.y = roomBounds.height < halfY * 2 ? roomBounds.center.y : Mathf.Clamp(pos.y, roomBounds.yMin + halfY, roomBounds.yMax - halfY);
        }
        if (useWorldBounds && view != null)
        {
            float halfY = view.orthographicSize, halfX = halfY * view.aspect;
            pos.x = worldBounds.width < halfX * 2 ? worldBounds.center.x : Mathf.Clamp(pos.x, worldBounds.xMin + halfX, worldBounds.xMax - halfX);
            pos.y = worldBounds.height < halfY * 2 ? worldBounds.center.y : Mathf.Clamp(pos.y, worldBounds.yMin + halfY, worldBounds.yMax - halfY);
        }
        return pos;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds)
            return;

        Gizmos.color = Color.green;
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
