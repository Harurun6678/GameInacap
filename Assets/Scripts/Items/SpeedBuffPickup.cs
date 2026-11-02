using UnityEngine;

public class SpeedBuffPickup : MonoBehaviour
{
    public float buffDuration = 6f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.ApplySpeedBuff(buffDuration);
                Destroy(gameObject);
            }
        }
    }
}
