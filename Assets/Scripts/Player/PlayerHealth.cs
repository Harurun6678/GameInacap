using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controla vida, invulnerabilidad y muerte del jugador.
public class PlayerHealth : MonoBehaviour
{
    [Min(1)] public int maxHealth = 100;
    [Min(0f)] [SerializeField] private float invulnerabilitySeconds = 0.65f;
    public int currentHealth;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    public bool IsDead => dead;
    public bool RestartSceneOnDeath { get; set; } = true;

    private float invulnerableUntil;
    private bool dead;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (dead || damage <= 0 || Time.time < invulnerableUntil) return;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        invulnerableUntil = Time.time + invulnerabilitySeconds;
        HealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0) Die();
    }

    public void Heal(int amount)
    {
        if (dead || amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ResetHealth(int maximum)
    {
        maxHealth = Mathf.Max(1, maximum);
        currentHealth = maxHealth;
        invulnerableUntil = 0f;
        dead = false;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void RestoreHealth(int maximum, int current)
    {
        maxHealth = Mathf.Max(1, maximum);
        currentHealth = Mathf.Clamp(current, 1, maxHealth);
        invulnerableUntil = 0f;
        dead = false;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (dead) return;
        dead = true;
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
        Died?.Invoke();
        if (!RestartSceneOnDeath) return;
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
