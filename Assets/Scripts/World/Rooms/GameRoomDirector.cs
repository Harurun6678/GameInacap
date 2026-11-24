using NeonSpite.Generation;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameRoomDirector : MonoBehaviour
{
    [Header("Salas editables")]
    [SerializeField] private TrainingRoom tutorialPrefab;
    [SerializeField] private ClubRoom clubPrefab;
    [SerializeField] private GameObject initialInstance;
    [SerializeField] private CameraFollow tracking;
    private NeonSpiteGame game;
    private Camera view;
    private GamePresentation presentation;
    private Rigidbody2D body;
    private GameObject activeRoot;
    private TrainingRoom training;
    private ClubRoom club;

    public RoomKind Current { get; private set; }
    public DistrictRuntime District { get; private set; }
    internal TrainingRoom Training => training;

    internal void Initialize(NeonSpiteGame session)
    {
        game = session;
        view = tracking.GetComponent<Camera>();
        presentation = tracking.GetComponent<GamePresentation>();
        body = game.Player.GetComponent<Rigidbody2D>();
        tracking.SetTarget(game.Player.transform);
    }

    internal void Enter(RoomKind next)
    {
        if (activeRoot != null)
        {
            activeRoot.SetActive(false);
            Destroy(activeRoot);
        }
        training = null;
        club = null;
        District = null;
        Current = next;
        tracking.ClearWorldBounds();
        game.Menus.CloseModal();
        game.Player.enabled = true;
        body.simulated = true;
        game.Player.Teleport(new Vector3(next == RoomKind.MainRoom ? -7.5f : -8.25f, -2.42f, 0));

        if (next == RoomKind.Procedural)
        {
            activeRoot = new GameObject("Distrito_Procedural");
            activeRoot.transform.SetParent(transform, false);
            RunProgress run = game.Progress;
            District = activeRoot.AddComponent<DistrictRuntime>();
            District.Initialize(game.Player, game.Health, view, tracking, run.Seed, run.DistrictNumber,
                run.Columns, run.Branches, game.Award, game.ShowToast, run.GeneratorVersion,
                run.Style, run.DistrictNumber >= run.DistrictCount);
            activeRoot.AddComponent<DistrictMapUI>().Initialize(District, game);
        }
        else if (next == RoomKind.MainRoom)
        {
            training = TakeInitialOrInstantiate(tutorialPrefab);
            activeRoot = training.gameObject;
            training.Initialize(game);
            game.Progress.TutorialStep = 0;
            game.Progress.DoorUnlocked = false;
        }
        else
        {
            club = TakeInitialOrInstantiate(clubPrefab);
            activeRoot = club.gameObject;
            club.Initialize(game);
            game.Progress.DoorUnlocked = true;
        }
        if (initialInstance != null)
        {
            initialInstance.SetActive(false);
            Destroy(initialInstance);
            initialInstance = null;
        }
        tracking.SnapToTarget();
        var style = next == RoomKind.Procedural ? game.Progress.Style : DistrictStyle.Industrial;
        string detail = next == RoomKind.MainRoom ? "Aprende a moverte, esquivar y combatir." :
            next == RoomKind.Rest ? (game.PendingDistrict > 0 ? "Próximo destino: " + BiomeRules.For(game.Progress.PendingStyle).Name :
            "Cúrate, elige tus armas y prepara el recorrido.") : null;
        presentation.SetZone(game.ZoneName, BiomeRules.For(style), detail);
        game.Menus.View.RefreshNow();
    }

    private T TakeInitialOrInstantiate<T>(T prefab) where T : Component
    {
        if (initialInstance != null && initialInstance.TryGetComponent(out T placed))
        {
            initialInstance = null;
            return placed;
        }
        T instance = Instantiate(prefab, transform);
        instance.name = prefab.name;
        return instance;
    }

    internal void SnapCamera() => tracking.SnapToTarget();

    internal void HandleInput(Keyboard keyboard, bool interact)
    {
        if (training != null) training.HandleInput(keyboard, interact);
        else if (club != null && interact) club.Interact();
        else if (District != null && interact && District.TryInteract()) game.CompleteDistrict();
    }

    internal string InteractionLabel()
    {
        if (training != null) return training.InteractionLabel();
        if (club != null) return club.InteractionLabel();
        return District != null ? District.InteractionLabel() : "";
    }

    internal string Mission()
    {
        if (training != null) return training.Mission();
        if (club != null) return club.Mission();
        return District != null ? "DISTRITO " + game.ProceduralRoomNumber + "/" + game.DistrictCount + "  |  " + District.LocationLabel : "GENERANDO DISTRITO";
    }
}
