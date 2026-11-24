using System.Collections.Generic;
using UnityEngine;

// Persigue al jugador y ataca con un intervalo fijo.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Min(0f)] public float speed = 3f;
    [Min(0f)] public float detectionRange = 8f;
    [Min(0.1f)] public float attackRange = 1.2f;
    [Min(0)] public int damage = 20;
    [Min(0.05f)] public float attackCooldown = 1.5f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody2D body;
    private float nextAttackTime;
    private float stunnedUntil;
    private bool useTerritory;
    private Rect territory;
    [Min(.1f)] public float attackWindup = .32f;
    private float strikeAt, reactAfter;
    public bool WindingUp
    {
        get; private set;
    }
    // 0 al empezar a preparar el golpe, 1 justo antes de golpear (para el aviso visual).
    public float WindupProgress => WindingUp ? Mathf.Clamp01(1f - (strikeAt - Time.time) / attackWindup) : 0f;

    // Enemigos activos: sirve para que no se empujen ni se amontonen al perseguir.
    private static readonly List<EnemyAI> active = new List<EnemyAI>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();
    private int groundMask = 1 << 8;

    public void ConfigureTerritory(Rect bounds)
    {
        territory = bounds;
        useTerritory = true;
    }

    public void ApplyHitStun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        WindingUp = false;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        WindingUp = false;
        reactAfter = Time.time + .3f;
        active.Add(this);
    }
    private void OnDisable()
    {
        WindingUp = false;
        active.Remove(this);
    }

    // True si otro enemigo ya ocupa el espacio justo delante, en la dirección de avance.
    private bool BlockedByAlly(float direction)
    {
        Vector2 me = transform.position;
        for (int i = 0; i < active.Count; i++)
        {
            EnemyAI other = active[i];
            if (other == this)
                continue;
            Vector2 offset = (Vector2)other.transform.position - me;
            if (Mathf.Abs(offset.y) < 1f && offset.x * direction > 0f && Mathf.Abs(offset.x) < 1.05f)
                return true;
        }
        return false;
    }

    private void Start()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            player = target.transform;
            playerHealth = target.GetComponent<PlayerHealth>();
        }
    }

    private void FixedUpdate()
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead) || Time.time < stunnedUntil || Time.time < reactAfter)
            return;
        if (useTerritory && !territory.Contains(player.position))
        {
            WindingUp = false;
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            return;
        }
        float dx = player.position.x - transform.position.x;
        float distance = Vector2.Distance(transform.position, player.position);
        float horizontalSpeed = 0f;
        // La línea de visión solo importa dentro del rango de ataque o detección; fuera de él se evita el Linecast.
        bool inRange = distance <= Mathf.Max(attackRange, detectionRange);
        bool canSee = inRange && (!useTerritory || Physics2D.Linecast(transform.position, player.position, groundMask).collider == null);
        if (WindingUp)
        {
            body.linearVelocity = new Vector2(0, body.linearVelocity.y);
            if (Time.time >= strikeAt)
            {
                WindingUp = false;
                if (distance <= attackRange && canSee && playerHealth != null)
                    playerHealth.TakeDamage(damage, transform.position);
            }
            return;
        }
        if (distance <= attackRange && canSee)
        {
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                WindingUp = true;
                strikeAt = Time.time + attackWindup;
            }
        }
        else if (distance <= detectionRange && canSee)
        {
            horizontalSpeed = Mathf.Sign(dx) * speed;
            if (BlockedByAlly(Mathf.Sign(dx)))
                horizontalSpeed = 0f;
        }
        if (useTerritory && horizontalSpeed != 0)
        {
            float direction = Mathf.Sign(horizontalSpeed);
            Vector2 ahead = (Vector2)transform.position + new Vector2(direction * .65f, -.5f);
            RaycastHit2D support = Physics2D.Raycast(ahead, Vector2.down, .65f, groundMask);
            if (support.collider == null || !territory.Contains((Vector2)transform.position + new Vector2(direction * .8f, 0)))
                horizontalSpeed = 0;
        }
        // Conserva la gravedad y detiene la persecucion fuera de rango.
        body.linearVelocity = new Vector2(horizontalSpeed, body.linearVelocity.y);
    }
}
