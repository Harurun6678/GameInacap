using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Ejecuta comprobaciones aisladas durante Play Mode, sin paquetes de pruebas adicionales.
public static class GyaruRegressionChecks
{
    [MenuItem("Gyaru Rogue/Comprobar regresiones en Play Mode")]
    public static void Run()
    {
        if (!Application.isPlaying || !GyaruDemoBootstrap.IsDemoActive)
        {
            Debug.LogWarning("Abre TutorialRoom, pulsa Play y ejecuta este menu.");
            return;
        }

        GameObject actor = new GameObject("Regression actor");
        actor.transform.position = new Vector3(1000f, 1000f, 0f);
        WeaponData first = ScriptableObject.CreateInstance<WeaponData>();
        WeaponData second = ScriptableObject.CreateInstance<WeaponData>();
        try
        {
            PlayerController player = actor.AddComponent<PlayerController>();
            player.EquipWeapon(first);
            player.EquipWeapon(first);
            Check(player.WeaponSlot1 == first && player.WeaponSlot2 == null,
                "Equipar la misma arma no debe duplicarla.");
            player.EquipWeapon(second);
            Check(player.WeaponSlot2 == second, "La segunda arma debe usar el segundo slot.");

            FieldInfo cooldown = typeof(PlayerController).GetField("nextAttackTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            cooldown.SetValue(player, Time.time + 10f);
            float before = (float)cooldown.GetValue(player);
            player.SelectWeaponSlot(0);
            player.EquipWeapon(second);
            Check((float)cooldown.GetValue(player) == before,
                "Seleccionar o equipar no debe eliminar la espera del ataque.");

            player.ApplySpeedBuff(5f);
            player.ResetInventory(first);
            Check(player.WeaponSlot2 == null && !player.HasSpeedBuff && player.ActiveWeaponSlot == 0,
                "Reiniciar debe limpiar inventario y mejoras temporales.");
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.one * 20f;
            player.Teleport(new Vector3(1100f, 1000f, 0f));
            Check(body.linearVelocity == Vector2.zero && body.position.x == 1100f,
                "El teletransporte debe detener el movimiento.");

            PlayerHealth health = actor.AddComponent<PlayerHealth>();
            health.RestartSceneOnDeath = false;
            int deaths = 0;
            health.Died += () => deaths++;
            health.TakeDamage(10);
            health.TakeDamage(10);
            Check(health.currentHealth == 90, "La invulnerabilidad debe bloquear el segundo impacto.");
            health.ResetHealth(100);
            health.TakeDamage(100);
            health.TakeDamage(100);
            Check(deaths == 1 && health.IsDead && !player.enabled,
                "La muerte debe notificarse una sola vez y detener al jugador.");
            health.ResetHealth(120);
            health.TakeDamage(10);
            Check(!health.IsDead && health.currentHealth == 110,
                "El reinicio debe limpiar muerte e invulnerabilidad.");
            health.Heal(999);
            Check(health.currentHealth == 120, "La curacion no debe superar la vida maxima.");
            Debug.Log("Gyaru Rogue: 9 comprobaciones de regresion superadas.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
        finally
        {
            UnityEngine.Object.Destroy(actor);
            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
