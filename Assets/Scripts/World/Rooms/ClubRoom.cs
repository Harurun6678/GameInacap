using System;
using UnityEngine;
using NeonSpite.Generation;

public sealed class ClubRoom : MonoBehaviour
{
    private NeonSpiteGame game;
    private HealingDrink drink;
    internal void Initialize(NeonSpiteGame session)
    {
        game = session;
        // El bubble tea de curación es el sprite CURACION_Icono del prefab; vuelve a aparecer en cada visita.
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name == "CURACION_Icono") { drink = HealingDrink.Attach(renderer.gameObject); break; }
    }
    internal string Mission() => game.PendingDistrict > 0 ? "PRÓXIMO: " + BiomeRules.For(game.Progress.PendingStyle).Name.ToUpperInvariant() + "   ·   " + GameControls.PrimaryLabel(GameAction.Interact) + " PARA PREPARARTE" : "DESCANSA, EQUÍPATE Y ENTRA AL RECORRIDO";
    internal void Interact()
    {
        if (game.Near(-4.55f))
            OpenArmory();
        else if (game.Near(0f))
            OpenFountain();
        else if (game.Near(4.55f))
            OpenUpgrade();
        else if (game.Near(9.05f))
            OpenEnterRun();
    }

    internal string InteractionLabel()
    {
        if (game.Near(-4.55f))
            return GameControls.Hint(GameAction.Interact, "EXAMINAR ARMAS");
        if (game.Near(0f))
            return drink != null && drink.Consumed ? "FUENTE UTILIZADA" : GameControls.Hint(GameAction.Interact, "USAR FUENTE DE VIDA");
        if (game.Near(4.55f))
            return GameControls.Hint(GameAction.Interact, "ELEGIR MEJORA");
        if (game.Near(9.05f))
            return GameControls.Hint(GameAction.Interact, "COMENZAR RECORRIDO");
        return "";
    }
    private void OpenArmory()
    {
        game.Menus.OpenModal("ARSENAL", "ELIGE UN ARMA. PUEDES CAMBIAR ENTRE ELLAS CON " + GameControls.PrimaryLabel(GameAction.SwitchWeapon) + ".",
          new string[] { "1   REGLA NEON  |  " + game.Arsenal.Ruler.HitDamage + " DMG", "2   TWIN CUTTERS  |  " + game.Arsenal.Dagger.HitDamage + " DMG RAPIDAS", "3   CERRAR" },
          new Action[] { () => { game.Player.EquipWeapon(game.Arsenal.Ruler); game.Menus.CloseModal(); game.ShowToast("REGLA NEON EQUIPADA"); },
                         () => { game.Player.EquipWeapon(game.Arsenal.Dagger); game.Menus.CloseModal(); game.ShowToast("TWIN CUTTERS EQUIPADOS"); }, game.Menus.CloseModal });
    }

    private void OpenFountain()
    {
        if (drink != null && drink.Consumed)
        {
            game.ShowToast("YA LA USASTE EN ESTA VISITA");
            return;
        }
        if (game.Health.currentHealth >= game.Health.maxHealth)
        {
            game.ShowToast("VIDA COMPLETA");
            return;
        }
        game.Menus.OpenModal("FUENTE DE VIDA", "LA FUENTE RECUPERA TU VIDA GRATIS.",
          new string[] { "1   RECUPERAR VIDA", "2   VOLVER" },
          new Action[] { () => { game.Health.Heal(999); if (drink != null) drink.Consume(); game.Menus.CloseModal(); game.ShowToast("VIDA RESTAURADA"); }, game.Menus.CloseModal });
    }

    private void OpenUpgrade()
    {
        if (game.Progress.UpgradePurchased)
        {
            game.Menus.OpenModal("ALTAR DE ESTRELLAS", "YA USASTE LA MEJORA DE ESTA VISITA AL REFUGIO.", new string[] { "1   VOLVER" }, new Action[] { game.Menus.CloseModal });
            return;
        }
        string message = game.Progress.Fragments > 0 ? "TIENES " + game.Progress.Fragments + " FRAGMENTOS. ELIGE SOLO UNA MEJORA." : "NECESITAS 1 FICHA PARA LA MEJORA.";
        game.Menus.OpenModal("ALTAR DE ESTRELLAS", message,
          new string[] { "1   +20 DE VIDA MAXIMA", "2   +10 DE DANO AL ARMA ACTIVA", "3   VOLVER" },
          new Action[] { () => BuyUpgrade(true), () => BuyUpgrade(false), game.Menus.CloseModal });
    }

    private void BuyUpgrade(bool healthBoost)
    {
        if (game.Progress.Fragments < 1 || game.Progress.UpgradePurchased)
        {
            game.Menus.CloseModal();
            game.ShowToast("FRAGMENTOS INSUFICIENTES");
            return;
        }
        if (!healthBoost && game.Player.CurrentWeapon == null)
        {
            game.Menus.CloseModal();
            game.ShowToast("EQUIPA UN ARMA PRIMERO");
            return;
        }
        game.Progress.Fragments--;
        game.Progress.UpgradePurchased = true;
        if (healthBoost)
        {
            game.Health.maxHealth += 20;
            game.Health.Heal(20);
            game.ShowToast("VIDA MAXIMA +20");
        }
        else if (game.Player.CurrentWeapon != null)
        {
            game.Player.CurrentWeapon.damage += 10;
            game.ShowToast("DANO DE ARMA +10");
        }
        game.Menus.CloseModal();
    }

    private void OpenEnterRun()
    {
        int next = game.PendingDistrict > 0 ? game.PendingDistrict : 1;
        DistrictStyle nextStyle = game.PendingDistrict > 0 ? game.Progress.PendingStyle : game.StartingStyle;
        game.Menus.OpenModal("CONTINUAR RECORRIDO", "DESTINO: " + BiomeRules.For(nextStyle).Name.ToUpperInvariant() + "\n" + BiomeRules.For(nextStyle).Description + "\nSEMILLA: " + game.RunSeed,
          new string[] { "Entrar al distrito " + next, "Volver" },
          new Action[] { () => game.StartDistrict(next, nextStyle), game.Menus.CloseModal });
    }
}
