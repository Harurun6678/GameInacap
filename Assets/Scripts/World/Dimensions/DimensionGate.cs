using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DimensionGate : MonoBehaviour
{
    [Header("Mundos")]
    [Tooltip("Raiz de escenario visible en la dimension Gyaru.")]
    [SerializeField] private GameObject gyaruWorldRoot;
    [Tooltip("Raiz de escenario visible en la dimension Jirai Kei.")]
    [SerializeField] private GameObject jiraiWorldRoot;

    [Header("Simbolo y feedback")]
    [SerializeField] private GameObject symbolVisual;
    [SerializeField] private GameObject unavailableVisual;
    [Tooltip("Si esta activo, el jugador puede cambiar en ambas direcciones.")]
    [SerializeField] private bool allowSwitchToBothDimensions = true;
    [Tooltip("Permite entrar a Jirai Kei, pero no volver a Gyaru desde esta puerta.")]
    [SerializeField] private bool allowSwitchToGyaru = true;

    private PlayerController nearbyPlayer;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (unavailableVisual != null)
            unavailableVisual.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        nearbyPlayer = player;
        player.RegisterDimensionGate(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (nearbyPlayer == player)
            nearbyPlayer = null;
        player.UnregisterDimensionGate(this);
        if (unavailableVisual != null)
            unavailableVisual.SetActive(false);
    }

    public bool CanSwitchTo(bool targetDimensionB)
    {
        if (!allowSwitchToBothDimensions && targetDimensionB)
            return false;
        if (!allowSwitchToGyaru && !targetDimensionB)
            return false;
        return true;
    }

    public bool TrySwitchDimension(bool targetDimensionB)
    {
        if (nearbyPlayer == null || !CanSwitchTo(targetDimensionB))
            return false;

        if (gyaruWorldRoot == null || jiraiWorldRoot == null || gyaruWorldRoot == jiraiWorldRoot)
        {
            Debug.LogWarning("[DimensionGate] Configura dos raices de mundo diferentes.", this);
            return false;
        }
        if (nearbyPlayer.transform.IsChildOf(gyaruWorldRoot.transform) ||
            nearbyPlayer.transform.IsChildOf(jiraiWorldRoot.transform) ||
            transform.IsChildOf(gyaruWorldRoot.transform) || transform.IsChildOf(jiraiWorldRoot.transform))
        {
            Debug.LogWarning("[DimensionGate] El jugador y la puerta deben estar fuera de las raices de mundo.", this);
            return false;
        }
        // Solo se activan/desactivan las raices del escenario, nunca la raiz del jugador.
        if (gyaruWorldRoot != null)
            gyaruWorldRoot.SetActive(!targetDimensionB);
        if (jiraiWorldRoot != null)
            jiraiWorldRoot.SetActive(targetDimensionB);
        if (unavailableVisual != null)
            unavailableVisual.SetActive(false);
        return true;
    }

    private void OnDisable()
    {
        if (nearbyPlayer != null)
            nearbyPlayer.UnregisterDimensionGate(this);
        nearbyPlayer = null;
    }

    public void ShowUnavailableFeedback()
    {
        if (unavailableVisual != null)
            unavailableVisual.SetActive(true);
        Debug.Log("[DimensionGate] No puedes cambiar a esa dimension desde aqui.", this);
    }
}
