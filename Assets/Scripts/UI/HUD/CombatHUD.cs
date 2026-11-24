using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LevelGenerator levelGenerator;

    [Header("Colores")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.07f, 0.12f, 0.94f);
    [SerializeField] private Color healthColor = new Color(0.95f, 0.22f, 0.38f, 1f);
    [SerializeField] private Color activeSlotColor = new Color(1f, 0.36f, 0.65f, 1f);
    [SerializeField] private Color inactiveSlotColor = new Color(0.24f, 0.22f, 0.3f, 1f);

    private Image healthFill;
    private Text healthText;
    private Image[] slotFrames = new Image[2];
    private Image[] slotIcons = new Image[2];
    private Text[] slotNames = new Text[2];
    private Text powerupText;
    private Image powerupFrame;
    private Text roomText;
    private int displayedHealth = -1, displayedMaxHealth = -1;
    private int displayedRoom = -1, displayedRoomCount = -1;
    private Font hudFont;

    private void Awake()
    {
        FindPlayerReferences();
        BuildHud();
    }

    private void Update()
    {
        if (playerHealth == null || playerController == null)
            FindPlayerReferences();
        UpdateHealth();
        UpdateWeaponSlots();
        UpdatePowerup();
    }

    private void FindPlayerReferences()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (levelGenerator == null)
            levelGenerator = FindObjectOfType<LevelGenerator>();
    }

    private void BuildHud()
    {
        hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("NeonSpiteHUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform healthPanel = MakePanel("HealthPanel", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(520f, 44f), panelColor);
        healthFill = MakeImage("HealthFill", healthPanel, new Vector2(0.5f, 0.5f), Vector2.zero, healthColor);
        healthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthFill.rectTransform.offsetMin = new Vector2(4f, 4f);
        healthFill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;
        healthText = MakeText("HealthText", healthPanel, "100 / 100", 20, TextAnchor.MiddleCenter, Color.white);

        roomText = MakeText("RoomText", canvas.transform, "SALA 1 / 3", 18, TextAnchor.MiddleCenter, Color.white);
        SetRect(roomText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(180f, 28f));

        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -70f : 70f;
            RectTransform frameRect = MakeRect("WeaponSlot" + (i + 1), canvas.transform, new Vector2(0.5f, 0f), new Vector2(x, 76f), new Vector2(104f, 94f));
            slotFrames[i] = frameRect.gameObject.AddComponent<Image>();
            slotFrames[i].color = i == 0 ? activeSlotColor : inactiveSlotColor;
            slotIcons[i] = MakeImage("WeaponIcon" + (i + 1), frameRect, new Vector2(0.5f, 0.62f), new Vector2(62f, 52f), Color.white);
            slotIcons[i].preserveAspect = true;
            slotNames[i] = MakeText("WeaponName" + (i + 1), frameRect, "VACIO", 12, TextAnchor.MiddleCenter, Color.white);
            SetRect(slotNames[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 13f), new Vector2(98f, 24f));
            Text keyLabel = MakeText("WeaponKey" + (i + 1), frameRect, GameControls.PrimaryLabel(i == 0 ? GameAction.Weapon1 : GameAction.Weapon2), 12, TextAnchor.UpperRight, Color.white);
            SetRect(keyLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -6f), new Vector2(18f, 18f));
        }

        RectTransform powerRect = MakeRect("PowerupSlot", canvas.transform, new Vector2(0.5f, 0f), new Vector2(205f, 76f), new Vector2(76f, 76f));
        powerupFrame = powerRect.gameObject.AddComponent<Image>();
        powerupFrame.color = inactiveSlotColor;
        powerupText = MakeText("PowerupText", powerRect, "DASH\nLISTO", 13, TextAnchor.MiddleCenter, Color.white);

        Text controls = MakeText("ControlsHint", canvas.transform, GameControls.PrimaryLabel(GameAction.Attack) + " / CLICK: ATACAR    " + GameControls.PrimaryLabel(GameAction.SwitchWeapon) + ": CAMBIAR ARMA    " + GameControls.PrimaryLabel(GameAction.Interact) + ": RECOGER", 13, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
        SetRect(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(460f, 24f));
    }

    private void UpdateHealth()
    {
        if (playerHealth == null || healthFill == null)
            return;
        if (displayedHealth == playerHealth.currentHealth && displayedMaxHealth == playerHealth.maxHealth)
            return;
        displayedHealth = playerHealth.currentHealth;
        displayedMaxHealth = playerHealth.maxHealth;
        float ratio = displayedMaxHealth > 0 ? (float)displayedHealth / displayedMaxHealth : 0f;
        healthFill.fillAmount = Mathf.Clamp01(ratio);
        healthText.text = displayedHealth + " / " + displayedMaxHealth;
    }

    private void UpdateWeaponSlots()
    {
        if (playerController == null)
            return;
        int activeSlot = playerController.ActiveWeaponSlot;
        for (int i = 0; i < slotIcons.Length; i++)
        {
            WeaponData weapon = i == 0 ? playerController.WeaponSlot1 : playerController.WeaponSlot2;
            // Solo se redibuja la ranura cuando cambia su arma o la ranura activa.
            if (slotsDrawn && weapon == shownWeapons[i] && activeSlot == shownActiveSlot)
                continue;
            shownWeapons[i] = weapon;
            slotIcons[i].sprite = weapon != null ? weapon.weaponSprite : null;
            slotIcons[i].enabled = weapon != null && weapon.weaponSprite != null;
            slotNames[i].text = weapon != null ? weapon.weaponName : "VACIO";
            slotFrames[i].color = i == activeSlot ? activeSlotColor : inactiveSlotColor;
        }
        shownActiveSlot = activeSlot;
        slotsDrawn = true;
        if (levelGenerator != null &&
            (displayedRoom != levelGenerator.ActiveRoomIndex || displayedRoomCount != levelGenerator.RoomCount))
        {
            displayedRoom = levelGenerator.ActiveRoomIndex;
            displayedRoomCount = levelGenerator.RoomCount;
            roomText.text = "SALA " + (displayedRoom + 1) + " / " + displayedRoomCount;
        }
    }

    private void UpdatePowerup()
    {
        if (playerController == null || powerupText == null)
            return;
        if (playerController.HasSpeedBuff)
        {
            // Se muestra con un decimal, así que basta con actualizar cuando cambia la décima.
            int tenths = Mathf.RoundToInt(playerController.SpeedBuffTimeRemaining * 10f);
            if (tenths != shownPowerupState)
            {
                shownPowerupState = tenths;
                powerupText.text = "SPEED\n" + playerController.SpeedBuffTimeRemaining.ToString("0.0") + "s";
            }
            powerupFrame.color = activeSlotColor;
        }
        else
        {
            int state = playerController.CanDash ? -1 : -2;
            if (state != shownPowerupState)
            {
                shownPowerupState = state;
                powerupText.text = playerController.CanDash ? "DASH\nLISTO" : "DASH\nRECARGA";
            }
            powerupFrame.color = playerController.CanDash ? activeSlotColor : inactiveSlotColor;
        }
    }

    // Últimos valores dibujados (int.MinValue = todavía no se ha dibujado).
    private int shownActiveSlot = int.MinValue, shownPowerupState = int.MinValue;
    private bool slotsDrawn;
    private readonly WeaponData[] shownWeapons = new WeaponData[2];

    private RectTransform MakeRect(string objectName, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetRect(rect, anchor, anchoredPosition, size);
        return rect;
    }

    private RectTransform MakePanel(string objectName, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rect = MakeRect(objectName, parent, anchor, anchoredPosition, size);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private Image MakeImage(string objectName, Transform parent, Vector2 anchor, Vector2 size, Color color)
    {
        RectTransform rect = MakeRect(objectName, parent, anchor, Vector2.zero, size);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text MakeText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = hudFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private void SetRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
