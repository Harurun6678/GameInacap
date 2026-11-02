using UnityEngine;

// Persigue al jugador y ataca con un intervalo fijo.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Min(0f)] public float speed = 3f;
    [Min(0f)] public float detectionRange = 8f;
    [Min(0.1f)] public float attackRange = 1.2f;
    [Min(0)] public int damage = 15;
    [Min(0.05f)] public float attackCooldown = 1.5f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody2D body;
    private float nextAttackTime;
    private float stunnedUntil;
    private bool useTerritory;
    private Rect territory;
    private int groundMask = 1 << 8;

    public void ConfigureTerritory(Rect bounds) { territory = bounds; useTerritory = true; }

    public void ApplyHitStun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
    }

    private void Awake() { body = GetComponent<Rigidbody2D>(); }

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
        if (player == null || (playerHealth != null && playerHealth.IsDead) || Time.time < stunnedUntil) return;
        if (useTerritory && !territory.Contains(player.position))
        {
            body.linearVelocity = new Vector2(0,body.linearVelocity.y);
            return;
        }
        float dx = player.position.x - transform.position.x;
        float distance = Vector2.Distance(transform.position, player.position);
        float horizontalSpeed = 0f;
        bool canSee = !useTerritory || Physics2D.Linecast(transform.position,player.position,groundMask).collider == null;
        if (distance <= attackRange && canSee)
        {
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                if (playerHealth != null) playerHealth.TakeDamage(damage);
            }
        }
        else if (distance <= detectionRange && canSee)
        {
            horizontalSpeed = Mathf.Sign(dx) * speed;
        }
        if (useTerritory && horizontalSpeed != 0)
        {
            float direction = Mathf.Sign(horizontalSpeed);
            Vector2 ahead = (Vector2)transform.position + new Vector2(direction * .65f,-.5f);
            RaycastHit2D support = Physics2D.Raycast(ahead,Vector2.down,.65f,groundMask);
            if (support.collider == null || !territory.Contains((Vector2)transform.position + new Vector2(direction*.8f,0))) horizontalSpeed = 0;
        }
        // Conserva la gravedad y detiene la persecucion fuera de rango.
        body.linearVelocity = new Vector2(horizontalSpeed, body.linearVelocity.y);
    }
}
