using System.Collections.Generic;
using UnityEngine;

// Activa tres salas en orden y mueve al jugador entre ellas.
public class LevelGenerator : MonoBehaviour
{
    [Header("Room prefabs (maximum 3 rooms)")]
    [SerializeField] private GameObject startRoomPrefab;
    [SerializeField] private GameObject middleRoomPrefab;
    [SerializeField] private GameObject exitRoomPrefab;

    [Header("Layout")]
    [Min(1f)]
    [SerializeField] private float roomWidth = 18f;

    [Header("Player")]
    [SerializeField] private Transform player;

    private readonly List<GameObject> spawnedRooms = new List<GameObject>();
    private readonly List<RoomController> roomControllers = new List<RoomController>();
    private int activeRoomIndex;

    public int ActiveRoomIndex => activeRoomIndex;
    public int RoomCount => spawnedRooms.Count;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        GenerateLevel();
    }

    private void GenerateLevel()
    {
        if (startRoomPrefab == null || middleRoomPrefab == null || exitRoomPrefab == null)
        {
            Debug.LogError("[LevelGenerator] Asigna los prefabs de inicio, intermedio y final.", this);
            enabled = false;
            return;
        }

        ClearPreviouslyGeneratedRooms();

        GameObject[] prefabs = { startRoomPrefab, middleRoomPrefab, exitRoomPrefab };

        for (int i = 0; i < prefabs.Length; i++)
        {
            Vector3 position = transform.position + Vector3.right * roomWidth * i;
            GameObject room = Instantiate(prefabs[i], position, Quaternion.identity, transform);
            room.name = $"Room_{i + 1}";
            spawnedRooms.Add(room);

            RoomController controller = room.GetComponentInChildren<RoomController>(true);
            // Conserva los indices aunque falte un controlador.
            roomControllers.Add(controller);
            if (controller == null)
                Debug.LogError($"[LevelGenerator] {room.name} no tiene un RoomController en su jerarquia.", room);
            else
                controller.Configure(this, i, i == prefabs.Length - 1);

            if (i > 0)
                room.SetActive(false);
        }

        activeRoomIndex = 0;

        if (player == null)
            Debug.LogWarning("[LevelGenerator] No encontre al jugador. Asignalo en el Inspector o usa el tag 'Player'.", this);
        else if (roomControllers.Count > 0)
            MovePlayerToRoom(roomControllers[0]);
    }

    private void ClearPreviouslyGeneratedRooms()
    {
        foreach (GameObject room in spawnedRooms)
        {
            if (room != null) Destroy(room);
        }

        spawnedRooms.Clear();
        roomControllers.Clear();
    }

    // Avanza solo si la sala activa esta despejada.
    public bool AdvanceFromRoom(int roomIndex)
    {
        if (roomIndex != activeRoomIndex || roomIndex < 0 || roomIndex >= roomControllers.Count ||
            roomControllers[roomIndex] == null || !roomControllers[roomIndex].IsCleared)
            return false;

        if (roomIndex >= spawnedRooms.Count - 1)
        {
            Debug.Log("Gyaru Rogue completado! Has superado las tres salas.", this);
            return true;
        }

        GameObject currentRoom = spawnedRooms[roomIndex];
        int nextIndex = roomIndex + 1;
        GameObject nextRoom = spawnedRooms[nextIndex];

        if (roomControllers[nextIndex] == null)
        {
            Debug.LogError($"[LevelGenerator] Sala {nextIndex + 1} sin RoomController.", this);
            return false;
        }
        nextRoom.SetActive(true);
        activeRoomIndex = nextIndex;

        if (nextIndex < roomControllers.Count)
            MovePlayerToRoom(roomControllers[nextIndex]);

        // Desactiva la sala anterior para evitar colisiones entre encuentros.
        if (currentRoom != null)
            currentRoom.SetActive(false);

        Debug.Log($"Sala {activeRoomIndex + 1} de {spawnedRooms.Count}: adelante!", this);
        return true;
    }

    private void MovePlayerToRoom(RoomController room)
    {
        if (player == null || room == null) return;

        Transform spawn = room.PlayerSpawnPoint;
        if (spawn != null)
        {
            TeleportPlayer(spawn.position);
        }
        else
        {
            // Usa el extremo izquierdo si falta el punto de aparicion.
            TeleportPlayer(room.transform.position + new Vector3(-roomWidth * 0.4f, 1f, 0f));
            Debug.LogWarning($"{room.name}: falta Player Spawn Point; usando una posicion aproximada.", room);
        }
    }
    private void TeleportPlayer(Vector3 position)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.Teleport(position);
            return;
        }
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        player.position = position;
        if (body != null) body.position = position;
    }
}
