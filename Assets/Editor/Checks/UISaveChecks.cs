using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using NeonSpite.Persistence;

// Prueba de integración optativa. Usa una carpeta temporal y restaura el recorrido.
public static class UISaveChecks
{
    [MenuItem("Neon Spite/Comprobar guardado y UI en Play Mode")]
    public static void Run()
    {
        NeonSpiteGame demo = UnityEngine.Object.FindObjectOfType<NeonSpiteGame>();
        if (!Application.isPlaying || demo == null || demo.InMainMenu || demo.IsModalOpen || demo.Health.IsDead)
        {
            Debug.LogWarning("Inicia una partida y cierra los menús antes de ejecutar la prueba.");
            return;
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo storage = typeof(RunSaveController).GetField("saves", flags);
        MethodInfo capture = typeof(RunSaveController).GetMethod("CaptureRun", flags);
        MethodInfo apply = typeof(RunSaveController).GetMethod("ApplyRun", flags);
        RunSaveData original = (RunSaveData)capture.Invoke(demo.Saves, null);
        if (original.Validate() != null)
        {
            Debug.LogWarning("Coloca al personaje dentro de una sala y repite la prueba.");
            return;
        }
        object originalStore = storage.GetValue(demo.Saves);
        string path = Path.Combine(Application.temporaryCachePath, "GyaruChecks_" + Guid.NewGuid().ToString("N"));
        try
        {
            storage.SetValue(demo.Saves, new SaveFileStore(path));
            demo.Menus.OpenPauseMenu();
            GameMenuView ui = demo.GetComponentInChildren<GameMenuView>();
            ui.ShowSettings();
            ui.Back();
            ui.ShowSlots(true);
            ui.Back();
            ui.ShowSlots(false);
            ui.Back();
            ui.ShowMap();
            ui.Back();
            ui.ShowRoute();
            ui.Back();
            Check(Time.timeScale == 0 && AudioListener.pause, "Navegar no debe reanudar la partida.");
            demo.Menus.CloseModal();
            // Mata un enemigo real para comprobar que cargar no lo revive ni paga otra vez.
            EnemyHealth enemy = demo.District != null ? demo.District.GetComponentInChildren<EnemyHealth>(true) : null;
            if (enemy != null)
                enemy.TakeDamage(int.MaxValue, Vector2.zero);
            demo.Health.RestoreHealth(demo.Health.maxHealth, Mathf.Max(1, demo.Health.maxHealth - 17));
            RunSaveData expected = (RunSaveData)capture.Invoke(demo.Saves, null);
            string error;
            Check(demo.Saves.SaveSlot(1, out error), error);
            demo.Health.Heal(999);
            Check(demo.Saves.LoadSlot(1, out error), error);
            Compare(expected, (RunSaveData)capture.Invoke(demo.Saves, null));
            Check(demo.Saves.LoadSlot(1, out error), error);
            Compare(expected, (RunSaveData)capture.Invoke(demo.Saves, null));
            Check(!demo.IsModalOpen && Time.timeScale > 0 && !AudioListener.pause, "Cargar debe devolver el control.");
            Debug.Log("UI y guardado: navegación, JSON, posición, vida, inventario, mapa y carga repetida comprobados. Partidas del usuario intactas.");
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            storage.SetValue(demo.Saves, originalStore);
            apply.Invoke(demo.Saves, new object[] { original });
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
    private static void Compare(RunSaveData expected, RunSaveData actual)
    {
        expected.savedUtc = actual.savedUtc;
        if (expected.enemies != null)
            Array.Sort(expected.enemies, (a, b) => (a.room * 10 + a.slot).CompareTo(b.room * 10 + b.slot));
        if (actual.enemies != null)
            Array.Sort(actual.enemies, (a, b) => (a.room * 10 + a.slot).CompareTo(b.room * 10 + b.slot));
        Check(JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual), "El estado restaurado no coincide con el guardado.");
    }
    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
