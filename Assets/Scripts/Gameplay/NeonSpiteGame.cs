using System;
using NeonSpite.Generation;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
public sealed class NeonSpiteGame : MonoBehaviour
{
    [Header("Inicio de esta escena")]
    [SerializeField] private RoomKind initialRoom;
    [SerializeField] private bool startInMainMenu = true;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private DistrictStyle startingStyle = DistrictStyle.Industrial;
    [UnityEngine.Serialization.FormerlySerializedAs("combatRoomsPerRun")]
    [SerializeField, Range(1, 5)] private int districtsPerRun = 3;
    [SerializeField, Range(4, 8)] private int mainPathColumns = 5;
    [SerializeField, Range(1, 6)] private int optionalBranches = 3;

    [Header("Objetos de la escena")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private GameRoomDirector rooms;
    [SerializeField] private GameMenuController menus;
    [SerializeField] private RunSaveController saves;
    [SerializeField] private GameArsenal arsenal;

    internal RunProgress Progress { get; } = new RunProgress();
    internal GameArsenal Arsenal => arsenal;
    internal GameRoomDirector Rooms => rooms;
    public GameMenuController Menus => menus;
    public RunSaveController Saves => saves;
    public static bool IsSessionActive { get; private set; }
    public RoomKind CurrentRoom => rooms.Current;
    public PlayerController Player => player;
    public PlayerHealth Health => health;
    public DistrictRuntime District => rooms.District;
    public int ProceduralRoomNumber => Progress.DistrictNumber;
    public int Fragments => Progress.Fragments;
    public int RunSeed => Progress.Seed;
    public int DistrictCount => Progress.DistrictCount;
    public int TutorialStep => Progress.TutorialStep;
    public int PendingDistrict => Progress.PendingDistrict;
    public float Elapsed => Progress.Elapsed;
    public bool InMainMenu => menus.InMainMenu;
    public bool IsModalOpen => menus.IsModalOpen;
    public bool IsPaused => menus.IsPaused;
    public string ZoneName => CurrentRoom == RoomKind.Procedural ? BiomeRules.For(Progress.Style).Name :
        CurrentRoom == RoomKind.Rest ? "Refugio del club" : "Main Room";
    public string Toast => Time.unscaledTime < toastUntil ? toast : "";
    private float toastUntil;
    private string toast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsSessionActive = false;

    private void Awake() => IsSessionActive = true;

    private void Start()
    {
        GameSettings.Load();
        arsenal.Initialize();
        health.RestartSceneOnDeath = false;
        health.Died += OnPlayerDied;
        rooms.Initialize(this);
        menus.Initialize(this);
        saves.Initialize(this);
        Progress.DistrictCount = Mathf.Clamp(districtsPerRun, 1, 5);
        Progress.Columns = mainPathColumns;
        Progress.Branches = optionalBranches;
        if (startInMainMenu) menus.ReturnToMainMenu();
        else StartNewGame();
    }

    public void StartNewGame()
    {
        menus.LeaveMainMenu();
        arsenal.ResetDamage();
        health.ResetHealth(100);
        player.ResetInventory(arsenal.Ruler);
        int seed = useRandomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : fixedSeed;
        Progress.Start(seed, startingStyle, initialRoom);
        rooms.Enter(initialRoom);
    }

    private void Update()
    {
        if (menus.HandleNavigation()) return;
        Progress.Elapsed += Time.deltaTime;
        Keyboard keyboard = Keyboard.current;
        bool interact = GameControls.WasPressed(GameAction.Interact)
            || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
        rooms.HandleInput(keyboard, interact);
    }

    internal void StartDistrict(int number, DistrictStyle style)
    {
        Progress.EnterDistrict(number, style);
        rooms.Enter(RoomKind.Procedural);
    }

    internal void CompleteDistrict()
    {
        if (ProceduralRoomNumber >= DistrictCount)
        {
            menus.OpenModal("RECORRIDO COMPLETADO", "HAS LLEGADO AL FINAL.\nDISTRITOS: " + DistrictCount
                + "   ·   FICHAS: " + Fragments + "\nSEMILLA: " + RunSeed,
                new[] { "Nueva partida", "Menú principal" }, new Action[] { StartNewGame, menus.ReturnToMainMenu }, allowBack: false);
            return;
        }
        int next = ProceduralRoomNumber + 1;
        DistrictStyle[] options = RunRoute.Choices(next, DistrictCount);
        uint index = unchecked((uint)RunSeed + (uint)next) % (uint)options.Length;
        StartDistrict(next, options[index]);
    }

    private void OnPlayerDied()
    {
        menus.OpenModal("FIN DE LA PARTIDA", "PUEDES VOLVER A INTENTARLO DESDE EL INICIO.",
            new[] { "Reintentar", "Cargar partida", "Menú principal" },
            new Action[] { StartNewGame, () => { menus.ReturnToMainMenu(); menus.View.ShowSlots(false); }, menus.ReturnToMainMenu });
    }

    internal void Award(int amount, string message)
    {
        Progress.Fragments += amount;
        ShowToast(message);
    }

    internal void ShowToast(string message)
    {
        toast = message;
        toastUntil = Time.unscaledTime + 2.2f;
    }

    internal bool Near(float x)
    {
        Vector3 position = player.transform.position;
        return Mathf.Abs(position.x - x) < 1.5f && Mathf.Abs(position.y + 2.42f) < 1.8f;
    }

    public string RouteStageName(int index) => Progress.StageName(index, startingStyle);
    internal DistrictStyle StartingStyle => startingStyle;
    public string CurrentInteractLabel() => rooms.InteractionLabel();
    public string CurrentMission() => rooms.Mission();

    private void OnDestroy()
    {
        IsSessionActive = false;
        if (health != null) health.Died -= OnPlayerDied;
    }
}
