using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TrainingRoom : MonoBehaviour
{
    [SerializeField] private EnemyHealth trainingDummy;
    [SerializeField] private GameObject dummyArtwork;
    private NeonSpiteGame game;
    private Rigidbody2D playerBody;
    internal int DummyHealth => trainingDummy != null ? trainingDummy.CurrentHealth : 0;

    internal void Initialize(NeonSpiteGame session)
    {
        game = session;
        playerBody = game.Player.GetComponent<Rigidbody2D>();
        if (trainingDummy != null)
        {
            trainingDummy.Died += OnDummyDefeated;
            if (trainingDummy.GetComponent<ActorFeedback>() == null) trainingDummy.gameObject.AddComponent<ActorFeedback>();
        }
    }

    private void OnDisable()
    {
        if (trainingDummy != null) trainingDummy.Died -= OnDummyDefeated;
    }

    private void OnEnable()
    {
        if (game != null && trainingDummy != null) trainingDummy.Died += OnDummyDefeated;
    }

    private void OnDummyDefeated(EnemyHealth dummy)
    {
        dummy.Died -= OnDummyDefeated;
        trainingDummy = null;
        if (dummyArtwork != null) Destroy(dummyArtwork);
        game.Progress.TutorialStep = 5;
        game.Progress.DoorUnlocked = true;
        game.Award(1, "+1 FRAGMENTO DE ESTRELLA   |   PORTAL ABIERTO");
    }

    internal void RestoreDummy(int health)
    {
        if (trainingDummy == null) return;
        if (health > 0) { trainingDummy.RestoreHealth(health); return; }
        trainingDummy.Died -= OnDummyDefeated;
        trainingDummy.gameObject.SetActive(false);
        Destroy(trainingDummy.gameObject);
        trainingDummy = null;
        if (dummyArtwork != null) { dummyArtwork.SetActive(false); Destroy(dummyArtwork); }
    }

    internal string InteractionLabel() => !game.Near(9.05f) ? "" :
        game.Progress.DoorUnlocked ? GameControls.Hint(GameAction.Interact, "IR A LA SALA DE DESCANSO") : "PORTAL CERRADO - VENCE AL MUNECO";

    internal void HandleInput(Keyboard k, bool interact)
    {
        if (game.Progress.TutorialStep == 0 && game.Player.transform.position.x > -5.8f)
        {
            game.Progress.TutorialStep = 1;
            game.ShowToast("BIEN! AHORA SALTA");
        }
        else if (game.Progress.TutorialStep == 1 && (playerBody.linearVelocity.y > 4f || GameControls.WasPressed(GameAction.Jump)))
        {
            game.Progress.TutorialStep = 2;
            game.ShowToast("SALTO COMPLETADO");
        }
        else if (game.Progress.TutorialStep == 2 && (GameControls.WasPressed(GameAction.Dash) || !game.Player.CanDash))
        {
            game.Progress.TutorialStep = 3;
            game.ShowToast("DASH ACTIVADO");
        }
        else if (game.Progress.TutorialStep == 3 && trainingDummy != null && trainingDummy.CurrentHealth < trainingDummy.maxHealth)
        {
            game.Progress.TutorialStep = 4;
            game.ShowToast("DALE UNOS GOLPES MAS");
        }
        if (game.Progress.DoorUnlocked && game.Near(9.05f) && interact)
        {
            game.ShowToast("SALA DE DESCANSO");
            game.Rooms.Enter(RoomKind.Rest);
        }
    }

    internal string Mission()
    {
        switch (game.TutorialStep)
        {
            case 0:
                return GameControls.Cached("tutorial0", () => "01 / MOVERSE   -   " + MoveKeysLabel());
            case 1:
                return GameControls.Cached("tutorial1", () => "02 / SALTAR   -   " + GameControls.Label(GameAction.Jump));
            case 2:
                return GameControls.Cached("tutorial2", () => "03 / DASH   -   " + GameControls.Label(GameAction.Dash));
            case 3:
                return GameControls.Cached("tutorial3", () => "04 / ATACAR AL MUNECO   -   " + GameControls.Label(GameAction.Attack) + " O CLICK");
            case 4:
                return "05 / DERROTA AL MUNECO DE PRACTICA";
            default:
                return GameControls.Cached("tutorial5", () => "06 / ENTRA EN EL PORTAL DORADO   -   " + GameControls.PrimaryLabel(GameAction.Interact));
        }
    }

    // "A / D  O  ← / →" según las teclas asignadas a moverse.
    private static string MoveKeysLabel()
    {
        string main = GameControls.KeyName(GameControls.Get(GameAction.MoveLeft, GameControls.Primary)) + " / " + GameControls.KeyName(GameControls.Get(GameAction.MoveRight, GameControls.Primary));
        Key left = GameControls.Get(GameAction.MoveLeft, GameControls.Alternate), right = GameControls.Get(GameAction.MoveRight, GameControls.Alternate);
        if (left != Key.None && right != Key.None)
            main += "  O  " + GameControls.KeyName(left) + " / " + GameControls.KeyName(right);
        return main.ToUpperInvariant();
    }
}
