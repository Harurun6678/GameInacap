using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(GameMenuView))]
public sealed class GameMenuController : MonoBehaviour
{
    private NeonSpiteGame game;
    private GameMenuView view;
    private Rigidbody2D playerBody;
    private bool modalOpen, pauseMenuOpen, allowModalBack = true, previousAudioPause;
    private float previousTimeScale = 1f;
    private Action[] choices = Array.Empty<Action>();
    public bool InMainMenu { get; private set; }
    public bool IsModalOpen => modalOpen;
    public bool IsPaused => pauseMenuOpen;
    internal GameMenuView View => view;

    internal void Initialize(NeonSpiteGame session)
    {
        game = session;
        playerBody = game.Player.GetComponent<Rigidbody2D>();
        view = GetComponent<GameMenuView>();
        view.Initialize(game);
    }
    internal void LeaveMainMenu() => InMainMenu = false;

    internal bool HandleNavigation()
    {
        Keyboard k = Keyboard.current;
        if (modalOpen)
        {
            bool closePressed = (k != null && k.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));
            // Mientras se reasigna una tecla, Esc/B cancela la captura y no debe cerrar la pantalla.
            if (closePressed && allowModalBack && !view.IsCapturingInput)
                view.Back();
            return true;
        }
        if (game.Player == null)
            return true;
        bool pausePressed = (k != null && k.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (pausePressed && !game.Health.IsDead)
        {
            OpenPauseMenu();
            return true;
        }
        if (GameControls.WasPressed(GameAction.Map) && game.District != null)
        {
            OpenPauseMenu();
            view.ShowMap();
            return true;
        }
        return false;
    }

    public void OpenPauseMenu()
    {
        if (InMainMenu || game.Health == null || game.Health.IsDead)
            return;
        PauseSimulation();
        view.ShowPause();
    }

    private void PauseSimulation()
    {
        GamePresentation.CancelHitStop(); // no guardar la velocidad ralentizada de un golpe
        if (!modalOpen)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
        }
        modalOpen = true;
        allowModalBack = true;
        pauseMenuOpen = true;
        Time.timeScale = 0;
        AudioListener.pause = true;
        choices = Array.Empty<Action>();
        game.Player.BlockInputForCurrentFrame();
    }

    public void ReturnToMainMenu()
    {
        PauseSimulation();
        InMainMenu = true;
        view.ShowHome();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && game != null && view != null && !modalOpen && !InMainMenu && game.Health != null && !game.Health.IsDead)
            OpenPauseMenu();
    }

    internal void OpenModal(string title, string description, string[] labels, Action[] callbacks, bool preserveMotion = false, bool allowBack = true)
    {
        allowModalBack = allowBack;
        GamePresentation.CancelHitStop(); // no guardar la velocidad ralentizada de un golpe
        if (!modalOpen)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
        }
        pauseMenuOpen = false;
        AudioListener.pause = true;
        modalOpen = true;
        Time.timeScale = 0f;
        choices = callbacks;
        if (!preserveMotion)
        {
            game.Player.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
        }
        view.ShowModal(title, description, labels, SelectChoice);
    }

    private void SelectChoice(int index)
    {
        if (!modalOpen || index < 0 || index >= choices.Length)
            return;
        choices[index]?.Invoke();
    }

    public void CloseModal()
    {
        if (game.Health != null && game.Health.IsDead)
            return;
        if (modalOpen)
        {
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }
        modalOpen = false;
        pauseMenuOpen = false;
        choices = Array.Empty<Action>();
        if (game.Player != null)
        {
            game.Player.enabled = true;
            game.Player.BlockInputForCurrentFrame();
        }
        if (view != null)
            view.HideModal();
    }

    private void OnDestroy()
    {
        if (modalOpen) { Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause; }
    }
}
