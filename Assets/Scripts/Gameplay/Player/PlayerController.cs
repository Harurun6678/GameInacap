using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento e Inercia")]
    [SerializeField] private float jogSpeed = 8f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float deceleration = 30f;

    [Header("Buff de Velocidad")]
    [SerializeField] private float speedBuffMultiplier = 1.6f;

    [Header("Salto y Suelo")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private int maxExtraJumps = 1;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCastDistance = 0.1f;
    [Tooltip("Tiempo tras saltar en el que no se detecta suelo, para evitar que el salto extra se recargue al instante.")]
    [SerializeField] private float groundIgnoreAfterJump = 0.1f;
    [Tooltip("Al soltar el salto mientras sube, la velocidad vertical se multiplica por este valor (1 = altura fija).")]
    [SerializeField, Range(.2f, 1f)] private float jumpCutMultiplier = .5f;
    [Tooltip("Gravedad extra al caer: la caída se siente menos flotante. 1 = sin cambio.")]
    [SerializeField, Range(1f, 2f)] private float fallGravityMultiplier = 1.25f;
    [SerializeField, Min(1f)] private float maxFallSpeed = 24f;

    [Header("Dash Evasivo")]
    [SerializeField] private float dashForce = 20f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.8f;
    [Tooltip("El dash esquiva: no se recibe daño mientras dura.")]
    [SerializeField] private bool dashInvulnerable = true;

    [Header("Sensación de combate")]
    [Tooltip("Si se pulsa atacar un poco antes de que termine la espera, el golpe sale igual.")]
    [SerializeField, Range(0f, .3f)] private float attackBuffer = .12f;
    [SerializeField, Range(.05f, .4f)] private float swingDuration = .14f;
    [Tooltip("Empuje horizontal y vertical al recibir un golpe.")]
    [SerializeField] private Vector2 hurtKnockback = new Vector2(7f, 5f);
    [SerializeField, Range(0f, .5f)] private float hurtControlLock = .18f;

    [Header("Sistema de Armas")]
    [SerializeField] private WeaponData startingWeapon;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform weaponVisualPoint;
    [SerializeField] private int activeWeaponSlot;

    private readonly WeaponData[] weaponSlots = new WeaponData[2];
    private SpriteRenderer weaponVisual;
    private readonly Dictionary<Texture, Material> weaponMaterials = new Dictionary<Texture, Material>();

    public WeaponData CurrentWeapon => weaponSlots[Mathf.Clamp(activeWeaponSlot, 0, 1)];
    public WeaponData WeaponSlot1 => weaponSlots[0];
    public WeaponData WeaponSlot2 => weaponSlots[1];
    public int ActiveWeaponSlot => activeWeaponSlot;
    public bool HasSpeedBuff => speedBuffTimer > 0f;
    public float SpeedBuffTimeRemaining => Mathf.Max(0f, speedBuffTimer);
    public bool CanDash => canDash && !isDashing;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    // Velocidad horizontal propia (sin el arrastre de plataformas móviles).
    public float MoveSpeedX => currentSpeedX;
    // Momento del último golpe que salió (lo usa PlayerAnimator para la animación de ataque).
    public float LastAttackTime { get; private set; } = float.NegativeInfinity;
    // Con animaciones de cuerpo, el arma se dibuja en las manos y se oculta el sprite suelto.
    private bool bodyWieldsRuler, bodyWieldsCutters;
    // Superficie que pisa el jugador (null en el aire). Las plataformas inestables la consultan.
    public Collider2D GroundCollider { get; private set; }
    private Collider2D carrierCollider;
    private InverseMover carrier;
    private float carryUp;

    [Header("Dimensiones")]
    [Tooltip("La tecla G solo funciona cerca de una puerta o simbolo de cambio.")]
    [SerializeField] private bool debugDimensionLogs = true;

    private bool inDimensionB;
    private DimensionGate nearbyDimensionGate;

    private Rigidbody2D rb;
    private float originalGravityScale;
    private BoxCollider2D boxCollider;

    private float horizontalInput;
    private float currentSpeedX;
    private bool isFacingRight = true;

    [SerializeField, Range(0, .2f)] private float coyoteTime = .10f;
    [SerializeField, Range(0, .2f)] private float jumpBuffer = .12f;
    private float groundedUntil = float.NegativeInfinity, jumpUntil = float.NegativeInfinity;
    private bool isGrounded;
    private float groundIgnoreTimer;
    private int extraJumps;

    private bool isDashing;
    private bool canDash = true;

    private float speedBuffTimer;
    private float nextAttackTime;
    private float attackUntil = float.NegativeInfinity, swingStart, knockbackUntil = float.NegativeInfinity;
    private bool jumpCutPending, swinging;
    private PlayerHealth playerHealth;
    private int inputBlockedFrame = -1;
    private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>();
    private readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>(8);
    private readonly List<Collider2D> attackHits = new List<Collider2D>(8);
    private readonly HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();
    private readonly HashSet<BreakableBackpack> hitBackpacks = new HashSet<BreakableBackpack>();

    private void OnEnable()
    {
        inputBlockedFrame = Time.frameCount;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;
        boxCollider = GetComponent<BoxCollider2D>();
        extraJumps = maxExtraJumps;
        weaponSlots[0] = startingWeapon;
        activeWeaponSlot = Mathf.Clamp(activeWeaponSlot, 0, 1);
        CreateWeaponVisual();
        CreateAttackPointIfMissing();
        RefreshWeaponVisual();
        if (GetComponent<PlayerAnimator>() == null)
            gameObject.AddComponent<PlayerAnimator>();

        if (!NeonSpiteGame.IsSessionActive && FindAnyObjectByType<CombatHUD>() == null)
        {
            GameObject hudObject = new GameObject("UI_Combate");
            hudObject.AddComponent<CombatHUD>();
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || Time.frameCount <= inputBlockedFrame)
            return;
        if (speedBuffTimer > 0f)
            speedBuffTimer -= Time.deltaTime;

        // La tecla G solo funciona si hay una puerta o simbolo de dimension cerca.
        if (WasDimensionTogglePressed())
            TryToggleDimension();

        bool jumpPressed = WasJumpPressed();
        if (jumpPressed)
            jumpUntil = Time.time + jumpBuffer;
        if (isDashing)
            return;

        horizontalInput = ReadHorizontal();

        FlipCheck();
        if (jumpPressed)
        {
            bool down = GameControls.IsPressed(GameAction.MoveDown) ||
                (Gamepad.current != null && Gamepad.current.leftStick.y.ReadValue() < -.5f);
            if (down && TryDropThrough())
                jumpUntil = float.NegativeInfinity;
        }
        ConsumeBufferedJump();
        UpdateJumpCut();
        if (WasDashPressed() && canDash)
            StartCoroutine(DashRoutine());
        if (WasAttackPressed())
            attackUntil = Time.time + attackBuffer;
        if (Time.time <= attackUntil && TryAttack())
            attackUntil = float.NegativeInfinity;
        if (WasSwitchWeaponPressed())
            SwitchWeapon();
        if (WasSlot1Pressed())
            SelectWeaponSlot(0);
        if (WasSlot2Pressed())
            SelectWeaponSlot(1);
    }

    private void FixedUpdate()
    {
        UpdateGroundState();

        if (isDashing)
            return;

        float maxSpeed = jogSpeed * (speedBuffTimer > 0f ? speedBuffMultiplier : 1f);
        // Tras recibir un golpe, el empuje manda por un instante y luego vuelve el control.
        bool knockedBack = Time.time < knockbackUntil;
        float targetX = knockedBack ? 0f : horizontalInput * maxSpeed;
        float rate = knockedBack ? deceleration * .6f : Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;

        currentSpeedX = Mathf.MoveTowards(currentSpeedX, targetX, rate * Time.fixedDeltaTime);

        // Caída más pesada que la subida, con velocidad máxima para no atravesar suelos finos.
        float vy = rb.linearVelocity.y;
        bool falling = !isGrounded && vy < -.01f;
        rb.gravityScale = originalGravityScale * (falling ? fallGravityMultiplier : 1f);

        // Plataformas móviles: el jugador viaja con la plataforma que pisa.
        Vector2 carry = Vector2.zero;
        if (isGrounded && GroundCollider != null)
        {
            if (GroundCollider != carrierCollider)
            {
                carrierCollider = GroundCollider;
                carrier = GroundCollider.GetComponent<InverseMover>();
            }
            if (carrier != null)
                carry = carrier.Velocity;
        }
        carryUp = Mathf.Max(0f, carry.y);
        if (carry != Vector2.zero && vy <= carry.y + .5f)
            vy = carry.y; // no despega al bajar ni al subir
        rb.linearVelocity = new Vector2(currentSpeedX + carry.x, Mathf.Max(vy, -maxFallSpeed));
    }

    // Salto de altura variable: soltar el botón mientras sube acorta el salto.
    private void UpdateJumpCut()
    {
        if (!jumpCutPending)
            return;
        float vy = rb.linearVelocity.y;
        if (vy <= 0f)
        {
            jumpCutPending = false;
            return;
        }
        if (IsJumpHeld())
            return;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy * jumpCutMultiplier);
        jumpCutPending = false;
    }

    private static bool IsJumpHeld()
    {
        Gamepad pad = Gamepad.current;
        return GameControls.IsPressed(GameAction.Jump) || (pad != null && pad.buttonSouth.isPressed);
    }

    // Lo llama PlayerHealth cuando un golpe viene de una posición concreta.
    public void ApplyKnockback(float directionX)
    {
        if (isDashing || rb == null)
            return;
        knockbackUntil = Time.time + hurtControlLock;
        currentSpeedX = (directionX < 0f ? -1f : 1f) * hurtKnockback.x;
        rb.linearVelocity = new Vector2(currentSpeedX, Mathf.Max(rb.linearVelocity.y, hurtKnockback.y));
        jumpCutPending = false;
    }

    private void UpdateGroundState()
    {
        if (groundIgnoreTimer > 0f)
        {
            groundIgnoreTimer -= Time.fixedDeltaTime;
            isGrounded = false;
            GroundCollider = null;
            return;
        }

        isGrounded = CheckGround();
        if (isGrounded)
        {
            extraJumps = maxExtraJumps;
            groundedUntil = Time.time + coyoteTime;
        }
        if (!isDashing)
            ConsumeBufferedJump();
    }

    private bool CheckGround()
    {
        GroundCollider = null;
        // Sobre un ascensor que sube, la velocidad vertical propia es la del ascensor.
        if (rb.linearVelocity.y > .15f + carryUp)
            return false;
        Bounds b = boxCollider.bounds;
        // Se usa 95% del tamano para no rozar paredes laterales.
        CastGround(b, groundCastDistance);

        foreach (RaycastHit2D hit in groundHits)
        {
            if (hit.collider != null && hit.collider.usedByEffector && b.min.y < hit.collider.bounds.max.y - .08f)
                continue;
            // Ignora el propio collider del jugador.
            if (hit.collider != null && !hit.collider.isTrigger &&
                hit.collider.attachedRigidbody != rb && hit.normal.y > 0.5f)
            {
                GroundCollider = hit.collider;
                return true;
            }
        }
        return false;
    }

    private bool TryDropThrough()
    {
        if (ignoredPlatforms.Count > 0)
            return true;
        CastGround(boxCollider.bounds, .2f);
        foreach (RaycastHit2D hit in groundHits)
        {
            if (hit.collider == null || !hit.collider.usedByEffector || ignoredPlatforms.Contains(hit.collider))
                continue;
            PlatformEffector2D effector = hit.collider.GetComponent<PlatformEffector2D>();
            if (effector == null || !effector.useOneWay)
                continue;
            ignoredPlatforms.Add(hit.collider);
            Physics2D.IgnoreCollision(boxCollider, hit.collider, true);
        }
        if (ignoredPlatforms.Count == 0)
            return false;
        isGrounded = false;
        groundedUntil = float.NegativeInfinity;
        groundIgnoreTimer = .35f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -3f);
        StartCoroutine(RestorePlatforms());
        return true;
    }

    private void CastGround(Bounds bounds, float distance)
    {
        Physics2D.BoxCast(bounds.center, bounds.size * .95f, 0f, Vector2.down,
            QueryFilter(groundLayer), groundHits, distance);
    }

    private static ContactFilter2D QueryFilter(int layerMask)
    {
        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(layerMask);
        return filter;
    }

    private IEnumerator RestorePlatforms()
    {
        yield return new WaitForSeconds(.35f);
        RestorePlatformCollisions();
    }

    private void RestorePlatformCollisions()
    {
        foreach (Collider2D platform in ignoredPlatforms)
            if (platform != null && boxCollider != null)
                Physics2D.IgnoreCollision(boxCollider, platform, false);
        ignoredPlatforms.Clear();
    }

    private void ConsumeBufferedJump()
    {
        if (Time.time > jumpUntil)
            return;
        if (isGrounded || Time.time <= groundedUntil || extraJumps > 0)
        {
            jumpUntil = float.NegativeInfinity;
            TryJump();
        }
    }

    private void TryJump()
    {
        if (isGrounded || Time.time <= groundedUntil)
        {
            ApplyJump();
        }
        else if (extraJumps > 0)
        {
            extraJumps--;
            ApplyJump();
        }
    }

    private void ApplyJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpCutPending = true;
        isGrounded = false;
        GroundCollider = null;
        groundedUntil = float.NegativeInfinity;
        groundIgnoreTimer = groundIgnoreAfterJump;
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        float dir = isFacingRight ? 1f : -1f;

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * dashForce, 0f);
        jumpCutPending = false;
        knockbackUntil = float.NegativeInfinity;
        if (dashInvulnerable)
        {
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.GrantInvulnerability(dashDuration + .05f);
        }

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravityScale;
        // Si se sigue empujando hacia adelante, el dash enlaza con la carrera en vez de frenar en seco.
        horizontalInput = ReadHorizontal();
        currentSpeedX = horizontalInput * dir > .1f ? dir * jogSpeed : 0f;
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Devuelve true si el golpe salió (false si no hay arma o sigue en espera).
    private bool TryAttack()
    {
        WeaponData weapon = CurrentWeapon;
        if (weapon == null || attackPoint == null)
            return false;
        if (Time.time < nextAttackTime)
            return false;

        nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, weapon.attackRate);
        swinging = true;
        swingStart = Time.time;
        LastAttackTime = Time.time;

        int layerMask = enemyLayer.value == 0 ? Physics2D.AllLayers : enemyLayer.value;
        // Incluye triggers: las mochilas usan uno para no estorbar al caminar.
        ContactFilter2D attackFilter = QueryFilter(layerMask);
        attackFilter.useTriggers = true;
        Physics2D.OverlapCircle(attackPoint.position, weapon.attackRange, attackFilter, attackHits);

        // Evita golpear dos veces al mismo enemigo si tiene varios colliders.
        damagedEnemies.Clear();
        hitBackpacks.Clear();

        foreach (Collider2D col in attackHits)
        {
            // Las mochilas (antes cofres) también se rompen a golpes.
            BreakableBackpack pack = col.GetComponent<BreakableBackpack>();
            if (pack != null)
            {
                RaycastHit2D cover = Physics2D.Linecast(attackPoint.position, col.bounds.center, groundLayer);
                if (hitBackpacks.Add(pack) && (cover.collider == null || cover.collider.usedByEffector || cover.collider.isTrigger))
                    pack.Hit();
                continue;
            }
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null)
                continue;
            if (!damagedEnemies.Add(enemy))
                continue;
            RaycastHit2D wall = Physics2D.Linecast(attackPoint.position, col.bounds.center, groundLayer);
            if (wall.collider != null && !wall.collider.usedByEffector && !wall.collider.isTrigger)
                continue;

            Vector2 knockDir = (Vector2)(col.transform.position - transform.position);
            knockDir = knockDir.sqrMagnitude > 0.0001f ? knockDir.normalized : Vector2.up;

            enemy.TakeDamage(weapon.HitDamage, knockDir * weapon.knockbackForce);
        }
        return true;
    }

    // Animación del arma: un arco rápido hacia adelante en cada golpe.
    private void LateUpdate()
    {
        if (!swinging || weaponVisual == null)
            return;
        float t = (Time.time - swingStart) / swingDuration;
        if (t >= 1f)
        {
            ResetSwing();
            return;
        }
        float eased = 1f - (1f - t) * (1f - t);
        weaponVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -60f, eased));
        weaponVisual.transform.localPosition = new Vector3(Mathf.Sin(t * Mathf.PI) * .28f, 0f, 0f);
    }

    private void ResetSwing()
    {
        swinging = false;
        if (weaponVisual == null)
            return;
        weaponVisual.transform.localRotation = Quaternion.identity;
        weaponVisual.transform.localPosition = Vector3.zero;
    }

    private static float ReadHorizontal()
    {
        float x = 0f;

        if (GameControls.IsPressed(GameAction.MoveLeft))
            x -= 1f;
        if (GameControls.IsPressed(GameAction.MoveRight))
            x += 1f;

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            float stick = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.1f)
                x = stick;
        }

        return Mathf.Clamp(x, -1f, 1f);
    }

    private static bool WasJumpPressed()
    {
        Gamepad pad = Gamepad.current;
        return GameControls.WasPressed(GameAction.Jump)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool WasDashPressed()
    {
        Gamepad pad = Gamepad.current;
        return GameControls.WasPressed(GameAction.Dash)
            || (pad != null && pad.rightShoulder.wasPressedThisFrame);
    }

    private static bool WasSwitchWeaponPressed()
    {
        Gamepad pad = Gamepad.current;
        return GameControls.WasPressed(GameAction.SwitchWeapon)
            || (pad != null && pad.leftShoulder.wasPressedThisFrame);
    }

    private static bool WasSlot1Pressed()
    {
        return GameControls.WasPressed(GameAction.Weapon1);
    }

    private static bool WasSlot2Pressed()
    {
        return GameControls.WasPressed(GameAction.Weapon2);
    }

    private static readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private static bool PointerHitsUI(Mouse mouse)
    {
        if (EventSystem.current == null)
            return false;
        PointerEventData point = new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() };
        EventSystem.current.RaycastAll(point, pointerHits);
        bool hits = pointerHits.Count > 0;
        pointerHits.Clear();
        return hits;
    }

    private static bool WasAttackPressed()
    {
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        return GameControls.WasPressed(GameAction.Attack)
            || (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                !PointerHitsUI(mouse))
            || (pad != null && pad.buttonWest.wasPressedThisFrame);
    }

    public bool IsInDimensionB => inDimensionB;

    public void RegisterDimensionGate(DimensionGate gate)
    {
        if (gate != null)
            nearbyDimensionGate = gate;
    }

    public void UnregisterDimensionGate(DimensionGate gate)
    {
        if (nearbyDimensionGate == gate)
            nearbyDimensionGate = null;
    }

    private void TryToggleDimension()
    {
        if (nearbyDimensionGate == null)
            return;

        bool targetDimensionB = !inDimensionB;
        if (!nearbyDimensionGate.CanSwitchTo(targetDimensionB))
        {
            nearbyDimensionGate.ShowUnavailableFeedback();
            return;
        }

        if (!nearbyDimensionGate.TrySwitchDimension(targetDimensionB))
            return;
        inDimensionB = targetDimensionB;

        if (debugDimensionLogs)
        {
            string dimName = inDimensionB ? "Jirai Kei" : "Gyaru";
            Debug.Log($"[PlayerController] Cambio de dimension -> {dimName}", this);
        }
    }

    private static bool WasDimensionTogglePressed()
    {
        return GameControls.WasPressed(GameAction.Dimension);
    }

    private void FlipCheck()
    {
        if (horizontalInput == 0f)
            return;

        bool wantsRight = horizontalInput > 0f;
        if (wantsRight != isFacingRight)
            Flip(wantsRight);
    }

    private void Flip(bool faceRight)
    {
        isFacingRight = faceRight;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f);
        transform.localScale = s;
        RefreshWeaponVisual();
    }

    public void EquipWeapon(WeaponData newWeapon)
    {
        if (newWeapon == null)
            return;

        // Un arma ya equipada conserva su slot.
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != newWeapon)
                continue;
            SelectWeaponSlot(i);
            return;
        }

        int emptySlot = ArrayIndexOfNullWeapon();
        if (emptySlot >= 0)
        {
            weaponSlots[emptySlot] = newWeapon;
            activeWeaponSlot = emptySlot;
        }
        else
        {
            weaponSlots[activeWeaponSlot] = newWeapon;
        }

        // Cambiar de arma conserva la espera del ataque anterior.
        RefreshWeaponVisual();
    }

    public void SelectWeaponSlot(int slot)
    {
        if (slot < 0 || slot >= weaponSlots.Length || weaponSlots[slot] == null)
            return;
        activeWeaponSlot = slot;
        // Cambiar de arma conserva la espera del ataque anterior.
        RefreshWeaponVisual();
    }

    public void SwitchWeapon()
    {
        int otherSlot = activeWeaponSlot == 0 ? 1 : 0;
        SelectWeaponSlot(otherSlot);
    }

    private int ArrayIndexOfNullWeapon()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] == null)
                return i;
        return -1;
    }

    private void CreateAttackPointIfMissing()
    {
        if (attackPoint != null)
            return;
        GameObject point = new GameObject("Punto_Ataque");
        point.transform.SetParent(transform, false);
        point.transform.localPosition = new Vector3(0.7f, 0.05f, 0f);
        attackPoint = point.transform;
    }

    private void CreateWeaponVisual()
    {
        if (weaponVisualPoint == null)
        {
            GameObject point = new GameObject("Punto_Arma");
            point.transform.SetParent(transform, false);
            point.transform.localPosition = new Vector3(0.45f, 0.05f, 0f);
            weaponVisualPoint = point.transform;
        }

        Transform existing = weaponVisualPoint.Find("Arma_Equipada");
        GameObject visualObject = existing != null ? existing.gameObject : new GameObject("Arma_Equipada");
        visualObject.transform.SetParent(weaponVisualPoint, false);
        weaponVisual = visualObject.GetComponent<SpriteRenderer>();
        if (weaponVisual == null)
            weaponVisual = visualObject.AddComponent<SpriteRenderer>();
        weaponVisual.sortingOrder = 30;
    }

    internal void SetBodyWeapons(bool ruler, bool cutters)
    {
        bodyWieldsRuler = ruler;
        bodyWieldsCutters = cutters;
        RefreshWeaponVisual();
    }

    private void RefreshWeaponVisual()
    {
        if (weaponVisual == null)
            return;
        WeaponData weapon = CurrentWeapon;
        weaponVisual.sprite = weapon != null ? weapon.weaponSprite : null;
        bool inHands = weapon != null && (bodyWieldsRuler && weapon.type == WeaponType.Ruler || bodyWieldsCutters && weapon.type == WeaponType.Dagger);
        weaponVisual.enabled = weaponVisual.sprite != null && !inHands;
        if (weaponVisual.sprite != null)
        {
            float size = .8f / Mathf.Max(weaponVisual.sprite.bounds.size.x, weaponVisual.sprite.bounds.size.y);
            weaponVisual.transform.localScale = Vector3.one * size;
            Material material;
            Texture texture = weaponVisual.sprite.texture;
            if (!weaponMaterials.TryGetValue(texture, out material))
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    material = new Material(shader);
                    material.SetTexture("_MainTex", texture);
                    weaponMaterials.Add(texture, material);
                }
            }
            if (material != null)
                weaponVisual.sharedMaterial = material;
        }
    }

    public void BlockInputForCurrentFrame()
    {
        inputBlockedFrame = Time.frameCount;
        jumpUntil = attackUntil = float.NegativeInfinity;
    }

    public void ConfigureGroundLayer(LayerMask layer)
    {
        groundLayer = layer;
    }

    public void ApplySpeedBuff(float duration) => speedBuffTimer = Mathf.Max(speedBuffTimer, Mathf.Max(0f, duration));

    public void ResetInventory(WeaponData weapon)
    {
        weaponSlots[0] = weapon;
        weaponSlots[1] = null;
        activeWeaponSlot = 0;
        nextAttackTime = 0f;
        speedBuffTimer = 0f;
        RefreshWeaponVisual();
    }

    public void RestoreInventory(WeaponData first, WeaponData second, int active, float buff)
    {
        weaponSlots[0] = first;
        weaponSlots[1] = second;
        activeWeaponSlot = Mathf.Clamp(active, 0, 1);
        speedBuffTimer = Mathf.Max(0, buff);
        nextAttackTime = 0f;
        RefreshWeaponVisual();
    }

    public void Teleport(Vector3 position)
    {
        ResetMotion();
        transform.position = position;
        rb.position = position;
        isGrounded = false;
        GroundCollider = null;
        carryUp = 0f;
        groundIgnoreTimer = 0f;
        extraJumps = maxExtraJumps;
        nearbyDimensionGate = null;
    }

    private void ResetMotion()
    {
        StopAllCoroutines();
        RestorePlatformCollisions();
        horizontalInput = 0f;
        groundedUntil = jumpUntil = attackUntil = knockbackUntil = float.NegativeInfinity;
        jumpCutPending = false;
        ResetSwing();
        currentSpeedX = 0f;
        if (rb != null)
        {
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = Vector2.zero;
        }
        isDashing = false;
        canDash = true;
    }

    private void OnDisable()
    {
        ResetMotion();
        nearbyDimensionGate = null;
    }

    private void OnDestroy()
    {
        foreach (Material material in weaponMaterials.Values)
            if (material != null)
                Destroy(material);
        weaponMaterials.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null && CurrentWeapon != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, CurrentWeapon.attackRange);
        }
    }
}
