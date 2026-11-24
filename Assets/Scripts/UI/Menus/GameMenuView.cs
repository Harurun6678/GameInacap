using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using NeonSpite.Persistence;
using NeonSpite.Generation;
using K = KenneyTheme;

// HUD y menús creados con los packs Kenney incluidos en Resources/KenneyUI.
public sealed class GameMenuView : MonoBehaviour
{
    private enum ScreenPage
    {
        Hidden, Home, Pause, Map, Route, Settings, Controls, Save, Load, Confirm, Dialogue
    }
    private NeonSpiteGame demo;
    private RectTransform overlay, frame, content;
    private DemoHud hud;
    private Text feedback;
    private ScreenPage page;
    private bool homeOrigin;
    private Action confirmBack;
    private GameSettingsData draft;

    public void Initialize(NeonSpiteGame controller)
    {
        demo = controller;
        gameObject.AddComponent<KenneyUISound>();
        RectTransform root = K.Rect(transform, "Canvas_Interfaz_Kenney", 0, 0, 1600, 900);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem_Interfaz");
            events.transform.SetParent(transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        hud = new DemoHud(root, demo);
        overlay = K.Rect(root, "Capa_Menus", 0, 0, 1600, 900);
        K.Stretch(overlay);
        Image shade = overlay.gameObject.AddComponent<Image>();
        shade.color = new Color(.015f, .025f, .05f, .91f);
        shade.raycastTarget = true;
        K.Image(overlay, "Luz_Ambiente_Kenney", -390, 120, 1000, 1000, new Color(.45f, .18f, .26f, .25f), "Lights/Glow");
        frame = K.Panel(overlay, "Panel_Menu", 0, 0, 1240, 716, K.Gold);
        content = K.Rect(frame, "Contenido", 0, 0, 1200, 680);
        overlay.gameObject.SetActive(false);
    }
    private void Update()
    {
        UpdateKeyCapture();
        AnimateHome();
        hud?.Refresh();
    }

    public void RefreshNow()
    {
        hud?.Refresh(true);
    }

    private void Begin(ScreenPage next, string title, string subtitle)
    {
        EndCapture(); // cambiar de pantalla cancela una reasignación pendiente
        page = next;
        overlay.gameObject.SetActive(true);
        hud.SetVisible(false);
        K.Clear(content);
        feedback = null;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        K.Label(content, "Titulo", title, -254, 281, 640, 58, 38, K.Paper);
        K.Label(content, "Subtitulo", subtitle, -82, 236, 984, 32, 17, K.Muted);
        K.Image(content, "Separador", 0, 209, 1136, 1, K.Line);
        K.Label(content, "Marca", "NEON SPITE  /  DISTRITO INDUSTRIAL", -280, -321, 590, 24, 12, K.Muted);
        K.Label(content, "Navegacion", next == ScreenPage.Home ? "FLECHAS / MANDO   ·   ENTER / A" : "ESC / B   VOLVER", 414, -321, 318, 24, 12, K.Muted, TextAnchor.MiddleRight);
    }
    private static void Focus(Button button)
    {
        if (EventSystem.current != null && button != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
    public void ShowPause()
    {
        homeOrigin = false;
        Begin(ScreenPage.Pause, "PAUSA", "Respira. El distrito puede esperar.");
        K.Label(content, "Semilla", "SEMILLA  " + demo.RunSeed, 390, 282, 360, 36, 16, K.Gold, TextAnchor.MiddleRight);
        string[] labels = { "Reanudar", "Mapa del distrito", "Distritos y rutas", "Configuración", "Guardar partida", "Cargar partida", "Menú principal", "Salir del juego" };
        string[] icons = { "right", "menuGrid", "barsHorizontal", "gear", "save", "open", "home", "power" };
        Action[] actions = { demo.Menus.CloseModal, ShowMap, ShowRoute, ShowSettings, () => ShowSlots(true), () => ShowSlots(false), ConfirmMainMenu, ConfirmQuit };
        for (int i = 0; i < labels.Length; i++)
        {
            Button b = K.Button(content, labels[i], icons[i], -420, 167 - i * 55, 292, 47, actions[i], i == 0, i == 7);
            if (i == 0)
                Focus(b);
        }
        RectTransform detail = K.Panel(content, "Resumen_Recorrido", 173, -36, 744, 446);
        K.Label(detail, "Ubicacion", demo.District != null ? demo.ZoneName.ToUpperInvariant() + " / " + demo.District.LocationLabel : demo.CurrentRoom == RoomKind.Rest ? "REFUGIO / SALA DEL CLUB" : "MAIN ROOM", 0, 182, 684, 35, 19, K.Gold);
        RectTransform map = K.Rect(detail, "Mapa_Resumen", 0, 18, 664, 246);
        DistrictMapUI.Draw(map, demo.District, 640, 222, false);
        K.Image(detail, "Separador", 0, -132, 678, 1, K.Line);
        Stat(detail, "SALUD", demo.Health.currentHealth + " / " + demo.Health.maxHealth, -220, -166, K.Rose);
        Stat(detail, "FICHAS", demo.Fragments.ToString("00"), 0, -166, K.Gold);
        Stat(detail, "TIEMPO", DemoHud.FormatDuration(demo.Elapsed), 220, -166, K.Mint);
    }
    public void ShowHome()
    {
        homeOrigin=true; Begin(ScreenPage.Home,"MENÚ PRINCIPAL","Una nueva noche en el distrito.");
        // Columna izquierda: logo con un resplandor que late, descripción y resumen del último guardado.
        homeGlow=K.Image(content,"Logo_Resplandor",-300,82,620,330,new Color(K.Rose.r,K.Rose.g,K.Rose.b,.16f),"Lights/Glow");
        K.Label(content,"Logo","NEON\nSPITE",-277,74,550,200,76,K.Paper);
        K.Image(content,"Adorno",-334,-52,362,18,K.Gold,"Frames/Divider");
        K.Label(content,"Descripcion","Explora. Pelea. Encuentra el camino.\nCada semilla abre un nuevo recorrido.",-282,-108,520,62,20,K.Muted);
        RunSaveData latest; int latestSlot=LatestSave(out latest);
        RectTransform last=K.Panel(content,"Ultima_Partida",-282,-208,520,100,latestSlot>0 ? K.Gold : K.Line);
        if (latestSlot>0)
        {
            K.Image(last,"Icono",-222,0,34,34,K.Gold,"Icons/save").preserveAspect=true;
            K.Label(last,"Titulo","ÚLTIMA PARTIDA  ·  RANURA "+latestSlot.ToString("00"),40,29,400,24,13,K.Gold);
            K.Label(last,"Lugar",Location(latest),40,2,400,32,23,K.Paper);
            Text detail=K.Label(last,"Detalle","VIDA "+latest.health+" / "+latest.maxHealth+"  ·  FICHAS "+latest.fragments+"  ·  TIEMPO "+DemoHud.FormatDuration(latest.elapsed)+"  ·  "+SavedDate(latest).ToString("dd/MM HH:mm"),40,-28,400,24,14,K.Muted);
            detail.resizeTextForBestFit=true; detail.resizeTextMinSize=10; detail.resizeTextMaxSize=14;
        }
        else
        {
            K.Image(last,"Icono",-222,0,34,34,K.Muted,"Icons/plus").preserveAspect=true;
            K.Label(last,"Titulo","SIN PARTIDAS GUARDADAS",40,22,400,24,13,K.Muted);
            K.Label(last,"Detalle","El entrenamiento enseña movimiento,\nsalto doble, dash y combate.",40,-13,400,48,16,K.Paper);
        }
        // Columna derecha: con un guardado disponible, «Continuar» pasa a ser la acción principal.
        float y=148; const float step=67,height=57;
        if (latestSlot>0)
        {
            int slot=latestSlot;
            Focus(K.Button(content,"Continuar","right",347,y,396,height,()=>Load(slot),true)); y-=step;
            K.Button(content,"Nueva partida","plus",347,y,396,height,demo.StartNewGame); y-=step;
        }
        else { Focus(K.Button(content,"Nueva partida","right",347,y,396,height,demo.StartNewGame,true)); y-=step; }
        Button load=K.Button(content,"Cargar partida","open",347,y,396,height,()=>ShowSlots(false)); y-=step;
        if (latestSlot<=0 && !AnySlotExists()) { load.interactable=false; load.gameObject.AddComponent<CanvasGroup>().alpha=.4f; }
        K.Button(content,"Configuración","gear",347,y,396,height,ShowSettings); y-=step;
        K.Button(content,"Controles","gamepad",347,y,396,height,()=>{ShowSettings();ShowControls();}); y-=step;
        K.Button(content,"Salir del juego","power",347,y,396,height,ConfirmQuit,false,true);
        Text keys=K.Label(content,"Teclas",GameControls.Label(GameAction.MoveLeft)+"  ·  "+GameControls.Label(GameAction.MoveRight)+"  MOVER     "+GameControls.PrimaryLabel(GameAction.Jump)+"  SALTAR     "+GameControls.PrimaryLabel(GameAction.Dash)+"  DASH     "+GameControls.PrimaryLabel(GameAction.Attack)+"  ATACAR",347,-272,470,24,13,K.Muted,TextAnchor.MiddleCenter);
        keys.resizeTextForBestFit=true; keys.resizeTextMinSize=9; keys.resizeTextMaxSize=13;
    }
    private Image homeGlow;
    // Ranura con el guardado más reciente que se puede leer (0 si no hay ninguno).
    private int LatestSave(out RunSaveData latest)
    {
        latest=null; int best=0; DateTime bestDate=DateTime.MinValue;
        for(int i=1;i<=3;i++)
        {
            RunSaveData data; string message;
            if (!demo.Saves.ReadSlot(i,out data,out message)) continue;
            DateTime date=SavedDate(data);
            if (best==0 || date>bestDate) { best=i; bestDate=date; latest=data; }
        }
        return best;
    }
    private bool AnySlotExists() { for(int i=1;i<=3;i++) if (demo.Saves.SlotExists(i)) return true; return false; }
    private static DateTime SavedDate(RunSaveData data)
    {
        DateTime date;
        return DateTime.TryParse(data.savedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out date) ? date.ToLocalTime() : DateTime.MinValue;
    }
    // El menú principal corre con el juego en pausa: la animación usa tiempo sin escala.
    private void AnimateHome()
    {
        if (page != ScreenPage.Home || homeGlow == null) return;
        float wave=.5f+.5f*Mathf.Sin(Time.unscaledTime*1.6f);
        Color color=homeGlow.color; color.a=Mathf.Lerp(.10f,.24f,wave); homeGlow.color=color;
    }
    public void ShowMap()
    {
        Begin(ScreenPage.Map, "MAPA DEL DISTRITO", demo.District == null ? "El mapa se habilita al comenzar el recorrido." : "SEMILLA " + demo.RunSeed + "  /  " + demo.District.VisitedCount + " de " + demo.District.Plan.Rooms.Count + " salas visitadas");
        RectTransform map = K.Rect(content, "Plano_Ampliado", 0, -4, 1070, 365);
        DistrictMapUI.Draw(map, demo.District, 1040, 345, true);
        K.Label(content, "Leyenda", "ROSA  Tú     ·     VERDE  Refugio     ·     ESTRELLA  Mochila     ·     S  Salida segura     ·     ?  Por explorar", 0, -219, 1100, 35, 17, K.Muted, TextAnchor.MiddleCenter);
        Focus(K.Button(content, "Volver", "return", -430, -275, 260, 48, ReturnRoot));
        K.Label(content, "Mapa_Ayuda", "Las conexiones muestran los pasos entre salas descubiertas.", 171, -275, 780, 42, 16, K.Muted, TextAnchor.MiddleRight);
    }
    public void ShowRoute()
    {
        Begin(ScreenPage.Route, "DISTRITOS Y RUTAS", "SEMILLA " + demo.RunSeed + "   ·   Cada distrito termina en una zona segura.");
        int count = demo.DistrictCount;
        float step = Mathf.Min(330, 1090f / count), width = Mathf.Min(290, step - 20);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * .5f) * step;
            bool completed = i + 1 < demo.ProceduralRoomNumber || (demo.PendingDistrict > 0 && i + 1 == demo.ProceduralRoomNumber);
            bool current = i + 1 == demo.ProceduralRoomNumber && demo.CurrentRoom == RoomKind.Procedural;
            Color accent = completed ? K.Mint : current ? K.Rose : K.Line;
            RectTransform card = K.Panel(content, "Etapa_" + (i + 1), x, 23, width, 268, accent);
            K.Label(card, "Indice", "ETAPA " + (i + 1).ToString("00"), 0, 95, width - 24, 30, 16, accent, TextAnchor.MiddleCenter);
            K.Image(card, "Icono", 0, 44, 32, 32, accent, "Icons/" + (completed ? "checkmark" : current ? "target" : "locked")).preserveAspect = true;
            K.Label(card, "Zona", demo.RouteStageName(i), 0, -23, width - 30, 79, 22, K.Paper, TextAnchor.MiddleCenter);
            K.Label(card, "Estado", completed ? "COMPLETADA" : current ? "EN CURSO" : "POR EXPLORAR", 0, -98, width - 24, 29, 13, K.Muted, TextAnchor.MiddleCenter);
            if (i < count - 1)
                K.Image(content, "Paso_" + i, x + step * .5f, 22, 17, 17, K.Gold, "Icons/arrowRight").preserveAspect = true;
        }
        K.Label(content, "Ritmo", "Completa el distrito → cura tus heridas en la zona segura → usa la puerta.\nLa semilla define las rutas intermedias. El tramo final llega a la Subestación.", 0, -192, 1080, 81, 20, K.Muted, TextAnchor.MiddleCenter);
        Focus(K.Button(content, "Volver", "return", -430, -275, 260, 48, ReturnRoot));
    }

    public void ShowSettings()
    {
        draft = GameSettings.Copy();
        DrawSettings();
    }
    private void DrawSettings()
    {
        Begin(ScreenPage.Settings, "CONFIGURACIÓN", "Los cambios se conservan al pulsar Aplicar.");
        SliderRow("Volumen general", 160, draft.master, v => { draft.master = v; AudioListener.volume = v; });
        SliderRow("Sonidos de interfaz", 101, draft.interfaceVolume, v => draft.interfaceVolume = v);
        SliderRow("Opacidad del HUD", 42, draft.hudOpacity, v => draft.hudOpacity = v, .5f);
        SettingButton("Pantalla completa", -23, () => draft.fullscreen ? "Activada" : "Desactivada", () => draft.fullscreen = !draft.fullscreen);
        SettingButton("Sincronización vertical", -80, () => draft.vsync ? "Activada" : "Desactivada", () => draft.vsync = !draft.vsync);
        SettingButton("Minimapa", -137, () => draft.minimap ? "Visible" : "Oculto", () => draft.minimap = !draft.minimap);
        SettingButton("Resolución de ventana", -194, () => draft.width + " × " + draft.height, () =>
        {
            if (draft.width < 1280)
            {
                draft.width = 1280;
                draft.height = 720;
            }
            else if (draft.width < 1600)
            {
                draft.width = 1600;
                draft.height = 900;
            }
            else if (draft.width < 1920)
            {
                draft.width = 1920;
                draft.height = 1080;
            }
            else
            {
                draft.width = 1280;
                draft.height = 720;
            }
        });
        Focus(K.Button(content, "Cancelar", "return", -430, -274, 260, 48, Back));
        K.Button(content, "Aplicar", "checkmark", 429, -274, 260, 48, () => { GameSettings.Apply(draft); KenneyUISound.Play("Confirm"); ReturnRoot(); }, true);
        K.Button(content, "Controles", "gamepad", 0, -274, 260, 48, () => ShowControls());
        K.Label(content, "Nota_Pantalla", Application.isEditor ? "Pantalla: los cambios se ven en el juego compilado." : "Pantalla completa: resolución nativa del monitor.", 0, -234, 900, 25, 13, K.Muted, TextAnchor.MiddleCenter);
    }
    // ---------- Editor de controles ----------
    private static readonly int ActionCount=Enum.GetValues(typeof(GameAction)).Length;
    private Button[,] bindingButtons;
    private Text controlsNotice;
    private bool capturing, navigationWasEnabled;
    private GameAction captureAction;
    private int captureSlot, captureStartFrame=-1, captureEndFrame=-1;

    // True mientras se espera una tecla nueva (y durante el frame en que termina),
    // para que Esc cancele la captura sin cerrar también la pantalla.
    public bool IsCapturingInput => capturing || Time.frameCount == captureEndFrame;

    private void ShowControls(string notice=null,bool error=false,GameAction? focusAction=null,int focusSlot=0)
    {
        Begin(ScreenPage.Controls,"CONTROLES","Haz clic en una tecla y presiona la nueva. Los cambios se guardan al instante.");
        K.Label(content,"Cabecera_Accion","ACCIÓN",-395,188,310,24,13,K.Muted);
        K.Label(content,"Cabecera_Tecla","TECLA",-118,188,190,24,13,K.Muted,TextAnchor.MiddleCenter);
        K.Label(content,"Cabecera_Alternativa","ALTERNATIVA",88,188,190,24,13,K.Muted,TextAnchor.MiddleCenter);
        K.Label(content,"Cabecera_Mando","MANDO",375,188,330,24,13,K.Muted);
        bindingButtons=new Button[ActionCount,2];
        for(int i=0;i<ActionCount;i++)
        {
            GameAction action=(GameAction)i;
            float y=158-i*31;
            K.Label(content,"Accion_"+i,GameControls.ActionName(action),-395,y,310,28,17,K.Paper);
            for(int slot=0;slot<2;slot++)
            {
                int captured=slot;
                bindingButtons[i,slot]=K.Button(content,GameControls.KeyName(GameControls.Get(action,slot)),null,slot==0 ? -118 : 88,y,190,28,()=>StartCapture(action,captured));
            }
            K.Label(content,"Mando_"+i,GameControls.GamepadName(action),375,y,330,28,15,K.Muted);
        }
        controlsNotice=K.Label(content,"Nota_Controles","",0,-226,1040,25,14,K.Muted,TextAnchor.MiddleCenter);
        SetControlsNotice(notice,error);
        Button back=K.Button(content,"Volver","return",-430,-275,260,48,DrawSettings);
        K.Button(content,"Restablecer","wrench",430,-275,260,48,()=>{GameControls.ResetDefaults();KenneyUISound.Play("Confirm");ShowControls("Se restablecieron las teclas por defecto.");});
        Focus(focusAction.HasValue ? bindingButtons[(int)focusAction.Value,focusSlot] : back);
    }

    private void SetControlsNotice(string notice,bool error)
    {
        if (controlsNotice == null) return;
        controlsNotice.text=string.IsNullOrEmpty(notice) ? "El clic izquierdo también ataca   ·   Esc queda fija para pausa / volver" : notice;
        controlsNotice.color=error ? K.Rose : string.IsNullOrEmpty(notice) ? K.Muted : K.Mint;
    }

    private void StartCapture(GameAction action,int slot)
    {
        if (capturing) RestoreBindingLabel();
        capturing=true; captureAction=action; captureSlot=slot; captureStartFrame=Time.frameCount;
        // Sin esto, las flechas y Enter moverían la selección del menú en vez de asignarse.
        if (EventSystem.current != null) { navigationWasEnabled=EventSystem.current.sendNavigationEvents; EventSystem.current.sendNavigationEvents=false; }
        Text label=bindingButtons[(int)action,slot].GetComponentInChildren<Text>();
        label.text="PULSA UNA TECLA"; label.color=K.Gold;
        SetControlsNotice("«"+GameControls.ActionName(action)+"»: pulsa la nueva tecla   ·   Esc cancela"+(slot==GameControls.Alternate ? "   ·   Retroceso deja la alternativa vacía" : ""),false);
    }

    private void RestoreBindingLabel()
    {
        Button button=bindingButtons?[(int)captureAction,captureSlot];
        if (button == null) return;
        Text label=button.GetComponentInChildren<Text>();
        label.text=GameControls.KeyName(GameControls.Get(captureAction,captureSlot)); label.color=K.Paper;
    }

    private void EndCapture()
    {
        if (!capturing) return;
        capturing=false; captureEndFrame=Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents=navigationWasEnabled;
    }

    private void UpdateKeyCapture()
    {
        if (!capturing) return;
        if (page != ScreenPage.Controls) { EndCapture(); return; }
        // Ignora la tecla que activó el botón (por ejemplo, Enter) en el mismo frame.
        if (Time.frameCount == captureStartFrame) return;
        Keyboard kb=Keyboard.current;
        bool cancel=(kb != null && kb.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        GameAction action=captureAction; int slot=captureSlot;
        if (cancel)
        {
            EndCapture(); KenneyUISound.Play("Back");
            ShowControls(null,false,action,slot); return;
        }
        Key key=GameControls.PressedKeyThisFrame();
        if (key == Key.None) return;
        EndCapture();
        string error=null, notice;
        if (slot == GameControls.Alternate && key == Key.Backspace)
        {
            GameControls.ClearAlternate(action);
            notice="«"+GameControls.ActionName(action)+"» ya no tiene tecla alternativa.";
        }
        else
        {
            error=GameControls.Assign(action,slot,key);
            notice=error ?? "«"+GameControls.ActionName(action)+"» ahora usa "+GameControls.KeyName(key)+".";
        }
        KenneyUISound.Play(error == null ? "Confirm" : "Error");
        ShowControls(notice,error != null,action,slot);
    }

    private void SliderRow(string label, float y, float value, Action<float> changed, float min = 0)
    {
        K.Label(content, "Etiqueta_" + label, label, -316, y, 478, 34, 21, K.Paper);
        RectTransform root = K.Rect(content, "Ajuste_" + label, 217, y, 484, 36);
        // Zona de clic completa, con una pista visible de 8 px.
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        K.Image(root, "Pista", 0, 0, 464, 8, K.Line);
        RectTransform fillArea = K.Rect(root, "Area_Relleno", 0, 0, 464, 8);
        Image fill = K.Image(fillArea, "Relleno", 0, 0, 464, 8, K.Gold);
        RectTransform handleArea = K.Rect(root, "Area_Tirador", 0, 0, 464, 30);
        Image handle = K.Image(handleArea, "Tirador", 0, 0, 16, 26, K.Paper, "Frames/Button");
        handle.raycastTarget = true;
        // El Slider mueve relleno y tirador con sus anclas; el sizeDelta se SUMA a ese tamaño.
        // Relleno: sin tamaño extra, así mide exactamente valor × ancho de la pista.
        // Tirador: 16 px de ancho y 4 px menos que su área (30 - 4 = 26 px de alto).
        fill.rectTransform.sizeDelta = Vector2.zero;
        handle.rectTransform.sizeDelta = new Vector2(16, -4);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = 1;
        slider.SetValueWithoutNotify(value);
        Text number = K.Label(content, "Valor_" + label, Mathf.RoundToInt(value * 100) + "%", 517, y, 80, 32, 18, K.Gold, TextAnchor.MiddleRight);
        slider.onValueChanged.AddListener(v => { number.text = Mathf.RoundToInt(v * 100) + "%"; changed(v); });
    }
    private void SettingButton(string name, float y, Func<string> value, Action change)
    {
        K.Label(content, "Etiqueta_" + name, name, -316, y, 478, 34, 21, K.Paper);
        Button button = null;
        button = K.Button(content, value(), null, 262, y, 588, 43, () => { change(); button.GetComponentInChildren<Text>().text = value(); });
    }
    public void ShowSlots(bool saving, string notice = "")
    {
        Begin(saving ? ScreenPage.Save : ScreenPage.Load, saving ? "GUARDAR PARTIDA" : "CARGAR PARTIDA", saving ? "Elige una ranura. Se conserva una copia del guardado anterior." : "Continúa desde la posición y el estado del último guardado.");
        for (int i = 1; i <= 3; i++)
        {
            int slot = i;
            float x = (i - 2) * 379;
            RectTransform card = K.Panel(content, "Ranura_" + i, x, -15, 354, 374);
            K.Label(card, "Indice", "RANURA  " + i.ToString("00"), 0, 151, 300, 33, 17, K.Gold);
            RunSaveData data;
            string message;
            bool readable = demo.Saves.ReadSlot(i, out data, out message), exists = demo.Saves.SlotExists(i);
            K.Image(card, "Icono", -113, 83, 40, 40, readable ? K.Gold : K.Muted, "Icons/" + (readable ? "save" : exists ? "warning" : "plus")).preserveAspect = true;
            string place = readable ? Location(data) : exists ? "No disponible" : "Ranura vacía";
            K.Label(card, "Lugar", place, 31, 83, 210, 65, 24, K.Paper);
            string detail = readable ? DateTime.Parse(data.savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy  HH:mm") + "\n\nSEMILLA  " + data.seed + "\nVIDA  " + data.health + " / " + data.maxHealth + "    ·    FICHAS  " + data.fragments + "\nTIEMPO  " + DemoHud.FormatDuration(data.elapsed) : exists ? message : "Tu próximo recorrido\npuede comenzar aquí.";
            K.Label(card, "Detalles", detail, 0, -24, 296, 140, 17, K.Muted);
            if (readable && !string.IsNullOrEmpty(message))
                K.Label(card, "Copia", "Se usará la copia de seguridad.", 0, -103, 300, 28, 14, K.Gold);
            Button b = K.Button(card, saving ? "Guardar aquí" : "Cargar partida", saving ? "save" : "open", 0, -143, 302, 51, () =>
            {
                if (saving && exists)
                    Confirm("¿SOBRESCRIBIR RANURA " + slot + "?", "El guardado actual de esta ranura se reemplazará.\nSe conservará su versión anterior como copia.", "Sobrescribir", () => Save(slot), () => ShowSlots(true));
                else if (saving)
                    Save(slot);
                else if (!demo.InMainMenu)
                    Confirm("¿CARGAR ESTA PARTIDA?", "La partida activa se reemplazará.\nLos cambios que no hayas guardado se perderán.", "Cargar partida", () => Load(slot), () => ShowSlots(false));
                else
                    Load(slot);
            }, saving || readable);
            b.interactable = saving || readable;
            if (!b.interactable)
                b.gameObject.AddComponent<CanvasGroup>().alpha = .4f;
        }
        Focus(K.Button(content, "Volver", "return", -430, -275, 260, 48, ReturnRoot));
        feedback = K.Label(content, "Estado", notice, 165, -269, 790, 58, 18, K.Mint, TextAnchor.MiddleRight);
    }
    private void Save(int slot)
    {
        string message;
        bool ok = demo.Saves.SaveSlot(slot, out message);
        KenneyUISound.Play(ok ? "Confirm" : "Error");
        ShowSlots(true, message);
        if (!ok && feedback != null)
            feedback.color = K.Rose;
    }
    private void Load(int slot)
    {
        string message;
        if (demo.Saves.LoadSlot(slot, out message))
        {
            KenneyUISound.Play("Confirm");
            return;
        }
        // Un fallo no cierra el menú ni hace avanzar la simulación.
        if (!demo.IsModalOpen)
            demo.Menus.OpenPauseMenu();
        ShowSlots(false, message);
        feedback.color = K.Rose;
        KenneyUISound.Play("Error");
    }
    private void ConfirmMainMenu()
    {
        Confirm("¿VOLVER AL MENÚ PRINCIPAL?", "El progreso sin guardar se perderá.\nPuedes volver y guardarlo antes de salir.", "Volver al menú", demo.Menus.ReturnToMainMenu, ShowPause);
    }
    private void ConfirmQuit()
    {
        Confirm("¿SALIR DEL JUEGO?", demo.InMainMenu ? "Hasta la próxima noche en el distrito." : "El progreso sin guardar se perderá.\nVuelve a Guardar partida si quieres conservarlo.", "Salir del juego", demo.Menus.QuitGame, ReturnRoot);
    }
    private void Confirm(string title, string description, string accept, Action action, Action cancel)
    {
        Begin(ScreenPage.Confirm, title, "CONFIRMACIÓN");
        confirmBack = cancel;
        K.Image(content, "Aviso", 0, 107, 52, 52, K.Gold, "Icons/warning").preserveAspect = true;
        K.Label(content, "Mensaje", description, 0, 0, 980, 130, 25, K.Paper, TextAnchor.MiddleCenter);
        Focus(K.Button(content, "Cancelar", "return", -210, -171, 354, 59, cancel));
        K.Button(content, accept, "checkmark", 210, -171, 354, 59, action, true);
    }
    public void ShowModal(string title, string description, string[] options, Action<int> selected)
    {
        Begin(ScreenPage.Dialogue, title, "");
        K.Label(content, "Dialogo", description, 0, 113, 1040, 123, 24, K.Paper, TextAnchor.MiddleCenter);
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            string label = options[i];
            if (label.Length > 2 && char.IsDigit(label[0]))
                label = label.Substring(1).TrimStart();
            Button b = K.Button(content, label, i == 0 ? "right" : "return", 0, -15 - i * 83, 730, 65, () => selected(index), i == 0);
            if (i == 0)
                Focus(b);
        }
    }
    public void HideModal()
    {
        page = ScreenPage.Hidden;
        overlay.gameObject.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        RefreshNow();
    }
    private void ReturnRoot()
    {
        if (homeOrigin || demo.InMainMenu)
            ShowHome();
        else
            ShowPause();
    }
    public void Back()
    {
        if (page == ScreenPage.Home || page == ScreenPage.Hidden)
            return;
        KenneyUISound.Play("Back");
        if (page == ScreenPage.Confirm)
        {
            confirmBack?.Invoke();
            return;
        }
        if (page == ScreenPage.Controls)
        {
            DrawSettings();
            return;
        }
        if (page == ScreenPage.Settings)
            AudioListener.volume = GameSettings.Current.master;
        if (page == ScreenPage.Pause || page == ScreenPage.Dialogue)
        {
            if (!demo.Health.IsDead)
                demo.Menus.CloseModal();
            return;
        }
        ReturnRoot();
    }
    private static void Stat(Transform parent, string label, string value, float x, float y, Color color)
    {
        K.Label(parent, "Dato_" + label, label, x, y + 12, 190, 25, 13, K.Muted, TextAnchor.MiddleCenter);
        K.Label(parent, "Valor_" + label, value, x, y - 19, 190, 34, 26, color, TextAnchor.MiddleCenter);
    }
    private static string Location(RunSaveData data)
    {
        return data.room == 0 ? "Main Room" : data.room == 1 ? "Refugio" : BiomeRules.For((DistrictStyle)data.biome).Name + " " + data.district.ToString("00");
    }
}
