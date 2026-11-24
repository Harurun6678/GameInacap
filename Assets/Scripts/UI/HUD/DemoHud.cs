using UnityEngine;
using UnityEngine.UI;
using K = KenneyTheme;

internal sealed class DemoHud
{
    private readonly NeonSpiteGame demo;
    private readonly RectTransform hud;
    private int lastHealth = -1, lastMaxHealth = -1, lastBuffSeconds = -1;
    private int lastFragments = -1, lastSecond = -1, lastRoomId = -1, lastTutorialStep = -1;
    private bool refreshOnShow = true;
    private CanvasGroup hudOpacity;
    private Image hpFill, hpTrail;
    private Text hpText, roomText, objective, interaction, toast, tokens, timeText, dashText, buffText;
    private readonly Text[] weaponNames = new Text[2], slotKeys = new Text[2];
    private Text switchHint;
    private int lastControlsRevision = int.MinValue, lastActiveSlot = int.MinValue;
    private bool lastCanDash, weaponsDrawn;
    private readonly WeaponData[] lastWeapons = new WeaponData[2];
    // Cuenta regresiva de la sala inversa (solo visible mientras corre).
    private static readonly Color InverseColor = new Color(.86f, .42f, 1f);
    private RectTransform inverseTimer;
    private Text inverseTimeText;
    private bool inverseTimerShown;
    private int shownInverseSeconds = int.MinValue;
    private readonly Image[] weaponIcons = new Image[2], weaponFrames = new Image[2];
    private float shownHealth = 1, trailHealth = 1;

    public DemoHud(RectTransform root, NeonSpiteGame demo)
    {
        this.demo = demo;
        hud = K.Rect(root, "HUD", 0, 0, 1600, 900);
        K.Stretch(hud);
        hudOpacity = hud.gameObject.AddComponent<CanvasGroup>();
        RectTransform hp = K.Panel(hud, "Estado_Jugadora", 202, -79, 340, 102, null, new Vector2(0, 1));
        K.Image(hp, "Emblema", -132, 13, 30, 30, K.Rose, "Icons/singleplayer").preserveAspect = true;
        K.Label(hp, "Nombre", "GYARU", -24, 21, 168, 27, 21, K.Paper);
        hpText = K.Label(hp, "Vida", "100 / 100", 93, 21, 114, 28, 18, K.Muted, TextAnchor.MiddleRight);
        K.Image(hp, "Barra_Fondo", 0, -16, 292, 12, new Color(.15f, .19f, .26f));
        hpTrail = K.Image(hp, "Vida_DanoReciente", -146, -16, 292, 12, K.Gold);
        hpFill = K.Image(hp, "Vida_Actual", -146, -16, 292, 12, K.Rose);
        hpTrail.rectTransform.pivot = hpFill.rectTransform.pivot = new Vector2(0, .5f);
        K.Image(hud, "Ficha_Icono", 50, -155, 21, 21, K.Gold, "Icons/star", new Vector2(0, 1)).preserveAspect = true;
        tokens = K.Label(hud, "Fichas", "00", 112, -155, 88, 28, 21, K.Gold, TextAnchor.MiddleLeft, new Vector2(0, 1));
        timeText = K.Label(hud, "Tiempo", "00:00", 274, -155, 146, 28, 16, K.Muted, TextAnchor.MiddleRight, new Vector2(0, 1));
        roomText = K.Label(hud, "Distrito", "DISTRITO INDUSTRIAL", 0, -51, 630, 28, 17, K.Gold, TextAnchor.MiddleCenter, new Vector2(.5f, 1));
        objective = K.Label(hud, "Objetivo", "", 0, -82, 630, 42, 20, K.Paper, TextAnchor.MiddleCenter, new Vector2(.5f, 1));
        toast = K.Label(hud, "Aviso", "", 0, -128, 700, 52, 19, K.Mint, TextAnchor.MiddleCenter, new Vector2(.5f, 1));
        Button pause = K.Button(hud, "", "pause", 0, 0, 54, 50, demo.Menus.OpenPauseMenu);
        RectTransform pauseRect = (RectTransform)pause.transform;
        pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1, 1);
        pauseRect.anchoredPosition = new Vector2(-62, -54);
        Transform pauseIcon = pause.transform.Find("Icono");
        if (pauseIcon != null)
            ((RectTransform)pauseIcon).anchoredPosition = Vector2.zero;
        K.Label(hud, "Atajo_Pausa", "ESC", -62, -93, 58, 22, 12, K.Muted, TextAnchor.MiddleCenter, new Vector2(1, 1));
        for (int i = 0; i < 2; i++)
        {
            RectTransform card = K.Panel(hud, "Arma_" + (i + 1), 136 + i * 218, 89, 206, 90, null, new Vector2(0, 0));
            weaponFrames[i] = card.Find("Marco_Kenney").GetComponent<Image>();
            slotKeys[i] = K.Label(card, "Ranura", (i + 1).ToString(), -81, 26, 24, 24, 13, K.Muted, TextAnchor.MiddleCenter);
            weaponIcons[i] = K.Image(card, "Arma", -59, -3, 53, 53, Color.white);
            weaponIcons[i].preserveAspect = true;
            weaponNames[i] = K.Label(card, "Nombre", "", 32, 2, 116, 53, 17, K.Paper);
        }
        RectTransform dash = K.Panel(hud, "Estado_Dash", 520, 89, 98, 90, null, new Vector2(0, 0));
        K.Image(dash, "Icono", 0, 13, 28, 28, K.Gold, "Icons/arrowRight").preserveAspect = true;
        dashText = K.Label(dash, "Estado", "SHIFT", 0, -23, 86, 23, 13, K.Gold, TextAnchor.MiddleCenter);
        buffText = K.Label(hud, "Mejora_Temporal", "", 202, -187, 340, 26, 15, K.Mint, TextAnchor.MiddleLeft, new Vector2(0, 1));
        switchHint = K.Label(hud, "Cambiar_Arma", "Q   CAMBIAR ARMA", 244, 26, 422, 24, 13, K.Muted, TextAnchor.MiddleCenter, new Vector2(0, 0));
        interaction = K.Label(hud, "Interaccion", "", 100, 74, 650, 56, 21, K.Gold, TextAnchor.MiddleCenter, new Vector2(.5f, 0));
        inverseTimer = K.Panel(hud, "Cuenta_Inversa", 0, -186, 250, 66, InverseColor, new Vector2(.5f, 1));
        K.Label(inverseTimer, "Titulo", "SALA INVERSA", 0, 17, 230, 20, 13, InverseColor, TextAnchor.MiddleCenter);
        inverseTimeText = K.Label(inverseTimer, "Tiempo", "0:30", 0, -9, 230, 36, 30, K.Paper, TextAnchor.MiddleCenter);
        inverseTimer.gameObject.SetActive(false);
    }

    private void RefreshInverseTimer()
    {
        float left = demo.CurrentRoom == RoomKind.Procedural && demo.District != null ? demo.District.InverseTimeLeft : -1f;
        bool show = left >= 0f;
        if (show != inverseTimerShown)
        {
            inverseTimerShown = show;
            inverseTimer.gameObject.SetActive(show);
            shownInverseSeconds = int.MinValue;
        }
        if (!show)
            return;
        int seconds = Mathf.CeilToInt(left);
        if (seconds != shownInverseSeconds)
        {
            shownInverseSeconds = seconds;
            inverseTimeText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
        // Los últimos 5 segundos laten en rojo.
        inverseTimeText.color = seconds <= 5 ? Color.Lerp(K.Rose, K.Paper, Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : K.Paper;
    }

    public void SetVisible(bool visible)
    {
        if (hud.gameObject.activeSelf == visible)
            return;
        hud.gameObject.SetActive(visible);
        refreshOnShow = true;
    }

    public void Refresh(bool forceLocation = false)
    {
        if (demo == null || demo.Player == null || demo.Health == null || hud == null)
            return;
        bool visible = !demo.InMainMenu && !demo.IsModalOpen;
        SetVisible(visible);
        float target = (float)demo.Health.currentHealth / Mathf.Max(1, demo.Health.maxHealth);
        shownHealth = Mathf.Lerp(shownHealth, target, 1 - Mathf.Exp(-15 * Time.unscaledDeltaTime));
        trailHealth = Mathf.MoveTowards(trailHealth, target, Time.unscaledDeltaTime * .45f);
        if (target > trailHealth)
            trailHealth = target;
        if (!visible)
            return;
        hudOpacity.alpha = GameSettings.Current.hudOpacity;
        RefreshInverseTimer();
        bool controlsChanged = lastControlsRevision != GameControls.Revision;
        if (controlsChanged || refreshOnShow)
        {
            // Se reasignó alguna tecla: actualiza las indicaciones del HUD.
            lastControlsRevision = GameControls.Revision;
            controlsChanged = true;
            switchHint.text = GameControls.PrimaryLabel(GameAction.SwitchWeapon) + "   CAMBIAR ARMA";
            slotKeys[0].text = GameControls.PrimaryLabel(GameAction.Weapon1);
            slotKeys[1].text = GameControls.PrimaryLabel(GameAction.Weapon2);
        }
        hpFill.rectTransform.sizeDelta = new Vector2(292 * shownHealth, 12);
        hpTrail.rectTransform.sizeDelta = new Vector2(292 * trailHealth, 12);
        hpFill.color = target <= .3f ? new Color(1, .24f, .28f) : K.Rose;
        if (lastHealth != demo.Health.currentHealth || lastMaxHealth != demo.Health.maxHealth)
        {
            lastHealth = demo.Health.currentHealth;
            lastMaxHealth = demo.Health.maxHealth;
            hpText.text = lastHealth + " / " + lastMaxHealth;
        }
        bool canDash = demo.Player.CanDash;
        if (canDash != lastCanDash || controlsChanged)
        {
            lastCanDash = canDash;
            dashText.text = canDash ? GameControls.PrimaryLabel(GameAction.Dash) : "CARGANDO";
            dashText.color = canDash ? K.Gold : K.Muted;
        }
        int buffSeconds = Mathf.CeilToInt(demo.Player.SpeedBuffTimeRemaining);
        if (lastBuffSeconds != buffSeconds)
        {
            lastBuffSeconds = buffSeconds;
            buffText.text = buffSeconds > 0 ? "VELOCIDAD  +  " + buffSeconds + " s" : "";
        }
        if (lastFragments != demo.Fragments)
        {
            lastFragments = demo.Fragments;
            tokens.text = lastFragments.ToString("00");
        }
        int seconds = Mathf.Max(0, (int)demo.Elapsed);
        if (lastSecond != seconds)
        {
            lastSecond = seconds;
            timeText.text = FormatDuration(seconds);
        }
        int roomId = demo.District != null ? demo.District.CurrentRoomId : -1;
        if (forceLocation || refreshOnShow || controlsChanged || roomId != lastRoomId || demo.TutorialStep != lastTutorialStep)
        {
            refreshOnShow = false;
            lastRoomId = roomId;
            lastTutorialStep = demo.TutorialStep;
            roomText.text = demo.CurrentRoom == RoomKind.Procedural
                ? "DISTRITO " + demo.ProceduralRoomNumber.ToString("00") + "  /  " + demo.ZoneName.ToUpperInvariant()
                : demo.CurrentRoom == RoomKind.Rest ? "REFUGIO  /  SALA DEL CLUB" : "MAIN ROOM";
            objective.text = demo.CurrentMission();
        }
        interaction.text = demo.CurrentInteractLabel();
        toast.text = demo.Toast;
        int activeSlot = demo.Player.ActiveWeaponSlot;
        for (int i = 0; i < 2; i++)
        {
            WeaponData weapon = i == 0 ? demo.Player.WeaponSlot1 : demo.Player.WeaponSlot2;
            // Solo se redibuja la ranura cuando cambia su arma o la ranura activa.
            if (weaponsDrawn && !forceLocation && weapon == lastWeapons[i] && activeSlot == lastActiveSlot)
                continue;
            lastWeapons[i] = weapon;
            weaponNames[i].text = weapon != null ? weapon.weaponName : "Ranura vacía";
            weaponIcons[i].sprite = weapon != null ? weapon.weaponSprite : K.Sprite("Icons/plus");
            weaponIcons[i].color = weapon != null ? Color.white : K.Line;
            weaponFrames[i].color = activeSlot == i ? K.Gold : K.Line;
        }
        lastActiveSlot = activeSlot;
        weaponsDrawn = true;
    }

    public static string FormatDuration(float seconds)
    {
        int total = Mathf.Max(0, (int)seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
