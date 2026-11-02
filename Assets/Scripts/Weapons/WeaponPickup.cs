using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponData weaponToGive;

    [Header("Mensaje de Interaccion")]
    [Tooltip("Posicion del mensaje respecto al centro del arma.")]
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Color promptColor = Color.white;

    [Header("Depuracion")]
    [Tooltip("Muestra en consola cada paso de la interaccion.")]
    [SerializeField] private bool debugLogs = false;

    private TextMesh prompt;
    private PlayerController playerController;
    private bool picked;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        CreatePrompt();
        SetPromptVisible(false);
        Log("Awake: mensaje creado. Arma asignada: " + (weaponToGive != null ? weaponToGive.weaponName : "NINGUNA"));
    }

    private void CreatePrompt()
    {
        GameObject promptObject = new GameObject("PickupPrompt");
        promptObject.transform.SetParent(transform, false);
        promptObject.transform.localPosition = promptOffset;

        prompt = promptObject.AddComponent<TextMesh>();
        prompt.text = weaponToGive != null
            ? $"Presiona E para tomar: {weaponToGive.weaponName}"
            : "Presiona E para tomar";
        prompt.characterSize = 0.1f;
        prompt.fontSize = 48;
        prompt.anchor = TextAnchor.MiddleCenter;
        prompt.alignment = TextAlignment.Center;
        prompt.color = promptColor;

        // Se dibuja por encima del sprite del arma.
        promptObject.GetComponent<MeshRenderer>().sortingOrder = 10;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (picked) return;

        Log($"OnTriggerEnter2D con '{other.name}' (tag: {other.tag})");

        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            LogWarning("El objeto con tag Player no tiene PlayerController.");
            return;
        }

        SetPromptVisible(true);
        Log("Jugador en rango. Mensaje mostrado.");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != playerController) return;

        playerController = null;
        SetPromptVisible(false);
        Log("Jugador salio del rango. Mensaje oculto.");
    }

    private void Update()
    {
        if (picked) return;
        if (!WasInteractPressed()) return;

        if (playerController == null || !playerController.isActiveAndEnabled)
        {
            Log("E presionado, pero el jugador no esta en rango.");
            return;
        }

        Log("E presionado con el jugador en rango. Recogiendo arma.");
        PickUp();
    }

    private void PickUp()
    {
        if (weaponToGive == null)
        {
            LogWarning("No hay WeaponData asignado. No se puede recoger el arma.");
            return;
        }

        picked = true;
        playerController.EquipWeapon(weaponToGive);
        SetPromptVisible(false);

        Log($"Arma '{weaponToGive.weaponName}' equipada. Eliminando objeto.");
        Destroy(gameObject);
    }

    private void SetPromptVisible(bool visible)
    {
        if (prompt != null) prompt.gameObject.SetActive(visible);
    }

    private static bool WasInteractPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.eKey.wasPressedThisFrame)
            || (pad != null && pad.buttonNorth.wasPressedThisFrame);
    }

    private void Log(string message)
    {
        if (debugLogs) Debug.Log($"[WeaponPickup:{name}] {message}", this);
    }

    private void LogWarning(string message)
    {
        // Las advertencias siempre se muestran, aunque los logs esten desactivados.
        Debug.LogWarning($"[WeaponPickup:{name}] {message}", this);
    }
}
