using System;
using System.IO;
using UnityEngine;
using NeonSpite.Persistence;

public sealed class RunSaveController : MonoBehaviour
{
    private NeonSpiteGame game;
    private SaveFileStore saves;
    internal void Initialize(NeonSpiteGame session)
    {
        game = session;
        string current = Path.Combine(Application.persistentDataPath, "Partidas");
        string previous = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "..", "GameInacap", "Partidas"));
        saves = new SaveFileStore(current, previous);
    }
    public bool SlotExists(int slot)
    {
        return saves != null && saves.Exists(slot);
    }

    public bool ReadSlot(int slot, out RunSaveData data, out string message)
    {
        data = null;
        message = null;
        string json;
        bool recovered;
        if (!saves.TryRead(slot, out json, out recovered, out message))
            return false;
        try
        {
            data = JsonUtility.FromJson<RunSaveData>(json);
            message = data == null ? "El archivo no contiene una partida." : data.Validate();
            if (message != null)
            {
                data = null;
                return false;
            }
            message = recovered ? "Se recuperará la copia de seguridad anterior." : "";
            return true;
        }
        catch (ArgumentException) { data = null; message = "El archivo no contiene una partida válida."; return false; }
    }

    public bool SaveSlot(int slot, out string message)
    {
        if (game.InMainMenu || game.Health.IsDead)
        {
            message = "No hay una partida activa para guardar.";
            return false;
        }
        RunSaveData data = CaptureRun();
        message = data.Validate();
        if (message != null)
            return false;
        if (!saves.TryWrite(slot, JsonUtility.ToJson(data, true), out message))
            return false;
        message = "Partida guardada en la ranura " + slot + ".";
        game.ShowToast(message);
        return true;
    }

    public bool LoadSlot(int slot, out string message)
    {
        RunSaveData data;
        if (!ReadSlot(slot, out data, out message))
            return false;
        string recovery = message;
        RunSaveData rollback = !game.InMainMenu && !game.Health.IsDead ? CaptureRun() : null;
        try
        {
            ApplyRun(data);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            try
            {
                if (rollback != null)
                    ApplyRun(rollback);
                else
                    game.Menus.ReturnToMainMenu();
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError, this); game.Menus.ReturnToMainMenu(); }
            message = "No se pudo reconstruir la partida. Revisa los assets del proyecto.";
            return false;
        }
        message = string.IsNullOrEmpty(recovery) ? "Partida cargada desde la ranura " + slot + "." : "Cargada la copia de seguridad de la ranura " + slot + ".";
        game.ShowToast(message);
        return true;
    }

    private RunSaveData CaptureRun()
    {
        var player = game.Player;
        Vector3 position = player.transform.position;
        var data = new RunSaveData
        {
            savedUtc = DateTime.UtcNow.ToString("o"),
            room = (int)game.CurrentRoom,
            health = game.Health.currentHealth, maxHealth = game.Health.maxHealth,
            dummyHealth = game.Rooms.Training != null ? game.Rooms.Training.DummyHealth : 0,
            weapon0 = game.Arsenal.IdOf(player.WeaponSlot1), weapon1 = game.Arsenal.IdOf(player.WeaponSlot2),
            activeWeapon = player.ActiveWeaponSlot, rulerDamage = game.Arsenal.Ruler.damage,
            daggerDamage = game.Arsenal.Dagger.damage, speedBuff = player.SpeedBuffTimeRemaining,
            x = position.x, y = position.y
        };
        game.Progress.Capture(data);
        if (game.District != null) game.District.Capture(data);
        return data;
    }

    private void ApplyRun(RunSaveData data)
    {
        game.Health.RestoreHealth(data.maxHealth, data.health);
        game.Menus.LeaveMainMenu();
        game.Menus.CloseModal();
        game.Progress.Restore(data);
        bool resumeNext = data.room == (int)RoomKind.Rest && game.PendingDistrict > 0;
        if (resumeNext) game.StartDistrict(game.PendingDistrict, game.Progress.PendingStyle);
        else game.Rooms.Enter((RoomKind)data.room);
        game.Progress.TutorialStep = data.tutorialStep;
        game.Progress.DoorUnlocked = data.doorUnlocked;
        game.Arsenal.Ruler.damage = data.rulerDamage;
        game.Arsenal.Dagger.damage = data.daggerDamage;
        game.Player.RestoreInventory(game.Arsenal.FromId(data.weapon0), game.Arsenal.FromId(data.weapon1), data.activeWeapon, data.speedBuff);
        if (!resumeNext) game.Player.Teleport(new Vector3(data.x, data.y, 0));
        if (game.Rooms.Training != null) game.Rooms.Training.RestoreDummy(data.dummyHealth);
        if (game.District != null && !resumeNext) game.District.Restore(data);
        Physics2D.SyncTransforms();
        game.Rooms.SnapCamera();
        game.Player.BlockInputForCurrentFrame();
        game.Menus.View.RefreshNow();
    }
}
