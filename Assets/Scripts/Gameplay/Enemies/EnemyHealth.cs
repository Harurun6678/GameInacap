using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 80;
    public int CurrentHealth
    {
        get; private set;
    }
    public event Action<EnemyHealth> Died;
    public event Action<int> Damaged;

    private Rigidbody2D rb;
    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
    }

    public void ConfigureHealth(int amount)
    {
        maxHealth = Mathf.Max(1, amount);
        CurrentHealth = maxHealth;
    }

    public void RestoreHealth(int amount)
    {
        isDead = false;
        CurrentHealth = Mathf.Clamp(amount, 1, maxHealth);
    }

    public void TakeDamage(int damage, Vector2 knockbackVector)
    {
        if (isDead || damage <= 0)
            return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        Damaged?.Invoke(damage);

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.ApplyHitStun(0.15f);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(knockbackVector, ForceMode2D.Impulse);
        }

        if (CurrentHealth <= 0)
        {
            isDead = true;
            if (ai != null)
                ai.enabled = false;
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
