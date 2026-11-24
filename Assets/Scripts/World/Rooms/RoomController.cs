using System.Collections.Generic;
using UnityEngine;

// Abre la salida cuando se derrotan todos los enemigos de la sala.
public class RoomController : MonoBehaviour
{
    [Header("Room references")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject exitVisual;
    [SerializeField] private RoomExitTrigger exitTrigger;

    [Header("Optional: assign a parent containing only this room's enemies")]
    [SerializeField] private Transform enemiesRoot;

    private readonly HashSet<EnemyHealth> livingEnemies = new HashSet<EnemyHealth>();
    private LevelGenerator levelGenerator;
    private int roomIndex;
    private bool isFinalRoom;
    private bool isCleared;
    private bool exitUsed;

    public Transform PlayerSpawnPoint => playerSpawnPoint;
    public bool IsCleared => isCleared;

    public void Configure(LevelGenerator generator, int index, bool finalRoom)
    {
        levelGenerator = generator;
        roomIndex = index;
        isFinalRoom = finalRoom;

        if (exitTrigger == null)
            exitTrigger = GetComponentInChildren<RoomExitTrigger>(true);

        if (exitTrigger != null)
            exitTrigger.Bind(this);
    }

    private void Start()
    {
        if (exitTrigger == null)
            exitTrigger = GetComponentInChildren<RoomExitTrigger>(true);

        if (exitTrigger != null)
        {
            exitTrigger.Bind(this);
            exitTrigger.enabled = false;
        }

        if (exitVisual != null)
            exitVisual.SetActive(false);

        Transform root = enemiesRoot != null ? enemiesRoot : transform;
        EnemyHealth[] enemies = root.GetComponentsInChildren<EnemyHealth>(true);

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null)
                continue;
            livingEnemies.Add(enemy);
            enemy.Died += HandleEnemyDied;
        }

        if (livingEnemies.Count == 0)
            ClearRoom();
        else
            Debug.Log($"Sala {roomIndex + 1}: quedan {livingEnemies.Count} enemigos.", this);
    }

    private void OnDestroy()
    {
        foreach (EnemyHealth enemy in livingEnemies)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy == null || !livingEnemies.Remove(enemy))
            return;

        enemy.Died -= HandleEnemyDied;
        if (livingEnemies.Count == 0)
            ClearRoom();
        else
            Debug.Log($"Sala {roomIndex + 1}: quedan {livingEnemies.Count} enemigos.", this);
    }

    private void ClearRoom()
    {
        if (isCleared)
            return;

        isCleared = true;

        if (exitVisual != null)
            exitVisual.SetActive(true);

        if (exitTrigger != null)
            exitTrigger.enabled = true;

        Debug.Log(isFinalRoom
            ? $"Sala {roomIndex + 1} despejada. Usa la salida para terminar!"
            : $"Sala {roomIndex + 1} despejada. La salida esta abierta!", this);
    }

    public void TryUseExit()
    {
        if (!isCleared || exitUsed)
            return;

        if (levelGenerator != null)
            exitUsed = levelGenerator.AdvanceFromRoom(roomIndex);
        else
            Debug.LogWarning($"{name}: no hay LevelGenerator asignado.", this);
    }
}
