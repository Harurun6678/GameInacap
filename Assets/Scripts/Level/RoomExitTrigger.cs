using UnityEngine;

// Activa la salida al detectar al jugador.
[RequireComponent(typeof(Collider2D))]
public class RoomExitTrigger : MonoBehaviour
{
    private RoomController roomController;

    public void Bind(RoomController controller)
    {
        roomController = controller;
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    // Permite salir si el jugador ya estaba en la puerta al despejar la sala.
    private void OnTriggerStay2D(Collider2D other) => OnTriggerEnter2D(other);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || other.GetComponentInParent<PlayerController>() == null) return;

        if (roomController == null)
            roomController = GetComponentInParent<RoomController>();

        if (roomController != null && roomController.IsCleared)
            roomController.TryUseExit();
    }
}
