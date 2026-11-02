using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Dash Evasivo")]
    [SerializeField] private float dashForce = 20f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.8f;

    [Header("Sistema de Armas")]
    [SerializeField] private WeaponData startingWeapon;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform weaponVisualPoint;
    [SerializeField] private int activeWeaponSlot;

    private readonly WeaponData[] weaponSlots = new WeaponData[2];
    private SpriteRenderer weaponVisual;

    public WeaponData CurrentWeapon => weaponSlots[Mathf.Clamp(activeWeaponSlot, 0, 1)];
    public WeaponData WeaponSlot1 => weaponSlots[0];
    public WeaponData WeaponSlot2 => weaponSlots[1];
    public int ActiveWeaponSlot => activeWeaponSlot;
    public bool HasSpeedBuff => speedBuffTimer > 0f;
    public float SpeedBuffTimeRemaining => Mathf.Max(0f, speedBuffTimer);
    public bool CanDash => canDash && !isDashing;

    [Header("Dimensiones") ]
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

    private bool isGrounded;
    private float groundIgnoreTimer;
    private int extraJumps;

    private bool isDashing;
    private bool canDash = true;

    private float speedBuffTimer;
    private float nextAttackTime;
    private int inputBlockedFrame = -1;
    private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>();

    private void OnEnable() { inputBlockedFrame = Time.frameCount; }

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


        if (!GyaruDemoBootstrap.IsDemoActive && FindObjectOfType<CombatHUD>() == null)
        {
            GameObject hudObject = new GameObject("UI_Combate");
            hudObject.AddComponent<CombatHUD>();
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || Time.frameCount <= inputBlockedFrame) return;
        if (speedBuffTimer > 0f) speedBuffTimer -= Time.deltaTime;

        // La tecla G solo funciona si hay una puerta o simbolo de dimension cerca.
        if (WasDimensionTogglePressed()) TryToggleDimension();

        if (isDashing) return;

        horizontalInput = ReadHorizontal();

        FlipCheck();
        if (WasJumpPressed())
        {
            bool down = (Keyboard.current != null && (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)) ||
                (Gamepad.current != null && Gamepad.current.leftStick.y.ReadValue() < -.5f);
            if (!down || !TryDropThrough()) TryJump();
        }
        if (WasDashPressed() && canDash) StartCoroutine(DashRoutine());
        if (WasAttackPressed()) TryAttack();
        if (WasSwitchWeaponPressed()) SwitchWeapon();
        if (WasSlot1Pressed()) SelectWeaponSlot(0);
        if (WasSlot2Pressed()) SelectWeaponSlot(1);

        FlipCheck();
    }

    private void FixedUpdate()
    {
        UpdateGroundState();

        if (isDashing) return;

        float maxSpeed = jogSpeed * (speedBuffTimer > 0f ? speedBuffMultiplier : 1f);
        float targetX = horizontalInput * maxSpeed;
        float rate = Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;

        currentSpeedX = Mathf.MoveTowards(currentSpeedX, targetX, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
    }

    private void UpdateGroundState()
    {
        if (groundIgnoreTimer > 0f)
        {
            groundIgnoreTimer -= Time.fixedDeltaTime;
            isGrounded = false;
            return;
        }

        isGrounded = CheckGround();
        if (isGrounded) extraJumps = maxExtraJumps;
    }

    private bool CheckGround()
    {
        if (rb.linearVelocity.y > .15f) return false;
        Bounds b = boxCollider.bounds;
        // Se usa 95% del tamano para no rozar paredes laterales.
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            b.center, b.size * 0.95f, 0f, Vector2.down, groundCastDistance, groundLayer);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider.usedByEffector && b.min.y < hit.collider.bounds.max.y - .08f) continue;
            // Ignora el propio collider del jugador.
            if (hit.collider != null && !hit.collider.isTrigger &&
                hit.collider.attachedRigidbody != rb && hit.normal.y > 0.5f) return true;
        }
        return false;
    }

    private bool TryDropThrough()
    {
        if (ignoredPlatforms.Count > 0) return true;
        Bounds bounds = boxCollider.bounds;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(bounds.center,bounds.size*.95f,0,Vector2.down,.2f,groundLayer);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || !hit.collider.usedByEffector || ignoredPlatforms.Contains(hit.collider)) continue;
            PlatformEffector2D effector = hit.collider.GetComponent<PlatformEffector2D>();
            if (effector == null || !effector.useOneWay) continue;
            ignoredPlatforms.Add(hit.collider);
            Physics2D.IgnoreCollision(boxCollider,hit.collider,true);
        }
        if (ignoredPlatforms.Count == 0) return false;
        isGrounded = false; groundIgnoreTimer = .35f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x,-3f);
        StartCoroutine(RestorePlatforms());
        return true;
    }

    private IEnumerator RestorePlatforms()
    {
        yield return new WaitForSeconds(.35f);
        RestorePlatformCollisions();
    }

    private void RestorePlatformCollisions()
    {
        foreach (Collider2D platform in ignoredPlatforms)
            if (platform != null && boxCollider != null) Physics2D.IgnoreCollision(boxCollider,platform,false);
        ignoredPlatforms.Clear();
    }

    private void TryJump()
    {
        if (isGrounded)
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
        isGrounded = false;
        groundIgnoreTimer = groundIgnoreAfterJump;
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        float originalGravity = rb.gravityScale;
        float dir = isFacingRight ? 1f : -1f;

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * dashForce, 0f);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        currentSpeedX = 0f;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void TryAttack()
    {
        WeaponData weapon = CurrentWeapon;
        if (weapon == null || attackPoint == null) return;
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, weapon.attackRate);

        int layerMask = enemyLayer.value == 0 ? Physics2D.AllLayers : enemyLayer.value;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position, weapon.attackRange, layerMask);

        // Evita golpear dos veces al mismo enemigo si tiene varios colliders.
        HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();

        foreach (Collider2D col in hits)
        {
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null) continue;
            if (!damaged.Add(enemy)) continue;

            Vector2 knockDir = (Vector2)(col.transform.position - transform.position);
            knockDir = knockDir.sqrMagnitude > 0.0001f ? knockDir.normalized : Vector2.up;

            enemy.TakeDamage(weapon.damage, knockDir * weapon.knockbackForce);
        }
    }

    private static float ReadHorizontal()
    {
        float x = 0f;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            float stick = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.1f) x = stick;
        }

        return Mathf.Clamp(x, -1f, 1f);
    }

    private static bool WasJumpPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.spaceKey.wasPressedThisFrame)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool WasDashPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.leftShiftKey.wasPressedThisFrame)
            || (pad != null && pad.rightShoulder.wasPressedThisFrame);
    }

    private static bool WasSwitchWeaponPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.qKey.wasPressedThisFrame)
            || (pad != null && pad.leftShoulder.wasPressedThisFrame);
    }

    private static bool WasSlot1Pressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.digit1Key.wasPressedThisFrame;
    }

    private static bool WasSlot2Pressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.digit2Key.wasPressedThisFrame;
    }

    private static bool WasAttackPressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.jKey.wasPressedThisFrame)
            || (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            || (pad != null && pad.buttonWest.wasPressedThisFrame);
    }

    public bool IsInDimensionB => inDimensionB;

    public void RegisterDimensionGate(DimensionGate gate)
    {
        if (gate != null) nearbyDimensionGate = gate;
    }

    public void UnregisterDimensionGate(DimensionGate gate)
    {
        if (nearbyDimensionGate == gate) nearbyDimensionGate = null;
    }

    private void TryToggleDimension()
    {
        if (nearbyDimensionGate == null) return;

        bool targetDimensionB = !inDimensionB;
        if (!nearbyDimensionGate.CanSwitchTo(targetDimensionB))
        {
            nearbyDimensionGate.ShowUnavailableFeedback();
            return;
        }

        if (!nearbyDimensionGate.TrySwitchDimension(targetDimensionB)) return;
        inDimensionB = targetDimensionB;

        if (debugDimensionLogs)
        {
            string dimName = inDimensionB ? "Jirai Kei" : "Gyaru";
            Debug.Log($"[PlayerController] Cambio de dimension -> {dimName}", this);
        }
    }

    private static bool WasDimensionTogglePressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.gKey.wasPressedThisFrame;
    }

    private void FlipCheck()
    {
        if (horizontalInput == 0f) return;

        bool wantsRight = horizontalInput > 0f;
        if (wantsRight != isFacingRight) Flip(wantsRight);
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
        if (newWeapon == null) return;

        // Un arma ya equipada conserva su slot.
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != newWeapon) continue;
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
        if (slot < 0 || slot >= weaponSlots.Length || weaponSlots[slot] == null) return;
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
            if (weaponSlots[i] == null) return i;
        return -1;
    }

    private void CreateAttackPointIfMissing()
    {
        if (attackPoint != null) return;
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
        if (weaponVisual == null) weaponVisual = visualObject.AddComponent<SpriteRenderer>();
        weaponVisual.sortingOrder = 30;
    }

    private void RefreshWeaponVisual()
    {
        if (weaponVisual == null) return;
        WeaponData weapon = CurrentWeapon;
        weaponVisual.sprite = weapon != null ? weapon.weaponSprite : null;
        weaponVisual.enabled = weaponVisual.sprite != null;
    }

    public void BlockInputForCurrentFrame() { inputBlockedFrame = Time.frameCount; }

    public void ConfigureGroundLayer(LayerMask layer) { groundLayer = layer; }

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
        groundIgnoreTimer = 0f;
        extraJumps = maxExtraJumps;
        nearbyDimensionGate = null;
    }

    private void ResetMotion()
    {
        StopAllCoroutines();
        RestorePlatformCollisions();
        horizontalInput = 0f;
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

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null && CurrentWeapon != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, CurrentWeapon.attackRange);
        }
    }
}
