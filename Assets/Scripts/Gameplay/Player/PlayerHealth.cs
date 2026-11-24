using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controla vida, invulnerabilidad y muerte del jugador.
public class PlayerHealth : MonoBehaviour
{
    [Min(1)] public int maxHealth = 100;
    [Min(0f)][SerializeField] private float invulnerabilitySeconds = 0.65f;
    public int currentHealth;
    public event Action<int, int> HealthChanged;
    public event Action Died;
    public event Action<int> Damaged;

    public bool IsDead => dead;
    public bool RestartSceneOnDeath { get; set; } = true;

    private float invulnerableUntil;
    private bool dead;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    // True mientras dura la invulnerabilidad por un golpe reciente (se usa para el parpadeo).
    public bool IsRecovering => !dead && Time.time < recoveringUntil;
    private float recoveringUntil;

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage);
    }

    // Igual que TakeDamage, pero además empuja al jugador lejos del origen del golpe.
    public void TakeDamage(int damage, Vector2 sourcePosition)
    {
        // El arma equipada puede volver más frágil al jugador (Twin Cutters).
        PlayerController controller = GetComponent<PlayerController>();
        WeaponData weapon = controller != null ? controller.CurrentWeapon : null;
        if (weapon != null && damage > 0)
            damage = Mathf.CeilToInt(damage * weapon.DamageTakenScale);
        if (!ApplyDamage(damage) || dead)
            return;
        if (controller != null && controller.enabled)
            controller.ApplyKnockback(transform.position.x - sourcePosition.x);
    }

    // Invulnerabilidad breve sin daño (por ejemplo, durante el dash).
    public void GrantInvulnerability(float seconds)
    {
        if (seconds > 0f)
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    private bool ApplyDamage(int damage)
    {
        if (dead || damage <= 0 || Time.time < invulnerableUntil)
            return false;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        invulnerableUntil = recoveringUntil = Time.time + invulnerabilitySeconds;
        Damaged?.Invoke(damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0)
            Die();
        return true;
    }

    public void Heal(int amount)
    {
        if (dead || amount <= 0)
            return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ResetHealth(int maximum)
    {
        maxHealth = Mathf.Max(1, maximum);
        currentHealth = maxHealth;
        invulnerableUntil = recoveringUntil = 0f;
        dead = false;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void RestoreHealth(int maximum, int current)
    {
        maxHealth = Mathf.Max(1, maximum);
        currentHealth = Mathf.Clamp(current, 1, maxHealth);
        invulnerableUntil = recoveringUntil = 0f;
        dead = false;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (dead)
            return;
        dead = true;
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;
        Died?.Invoke();
        if (!RestartSceneOnDeath)
            return;
        Scene scene = SceneManager.GetActiveScene();
        // En el editor tambien permite reiniciar escenas fuera del perfil de compilacion.
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(scene.path))
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        Debug.LogError("[PlayerHealth] No se puede reiniciar esta escena: guarda la escena y agregala a Build Settings.", this);
    }
}
