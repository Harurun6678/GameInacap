using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using GyaruRogue.Persistence;
using K = KenneyTheme;

// HUD y menús creados con los packs Kenney incluidos en Resources/KenneyUI.
public sealed class GyaruDemoUI : MonoBehaviour
{
    private enum ScreenPage { Hidden,Home,Pause,Map,Settings,Controls,Save,Load,Confirm,Dialogue }
    private GyaruDemoBootstrap demo;
    private RectTransform hud,overlay,frame,content;
    private CanvasGroup hudOpacity;
    private Image hpFill,hpTrail;
    private Text hpText,roomText,objective,interaction,toast,tokens,timeText,feedback,dashText,buffText;
    private readonly Text[] weaponNames=new Text[2];
    private readonly Image[] weaponIcons=new Image[2],weaponFrames=new Image[2];
    private ScreenPage page;
    private bool homeOrigin;
    private Action confirmBack;
    private GyaruSettingsData draft;
    private float shownHealth=1,trailHealth=1;

    public void Initialize(GyaruDemoBootstrap controller)
    {
        demo=controller;
        gameObject.AddComponent<KenneyUISound>();
        RectTransform root=K.Rect(transform,"Canvas_Interfaz_Kenney",0,0,1600,900);
        Canvas canvas=root.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=120;
        CanvasScaler scaler=root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject events=new GameObject("EventSystem_Interfaz"); events.transform.SetParent(transform,false);
            events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        BuildHUD(root);
        overlay=K.Rect(root,"Capa_Menus",0,0,1600,900); K.Stretch(overlay);
        Image shade=overlay.gameObject.AddComponent<Image>(); shade.color=new Color(.015f,.025f,.05f,.91f); shade.raycastTarget=true;
        K.Image(overlay,"Luz_Ambiente_Kenney",-390,120,1000,1000,new Color(.45f,.18f,.26f,.25f),"Lights/Glow");
        frame=K.Panel(overlay,"Panel_Menu",0,0,1240,716,K.Gold);
        content=K.Rect(frame,"Contenido",0,0,1200,680);
        overlay.gameObject.SetActive(false);
    }
    private void BuildHUD(RectTransform root)
    {
        hud=K.Rect(root,"HUD",0,0,1600,900); K.Stretch(hud); hudOpacity=hud.gameObject.AddComponent<CanvasGroup>();
        RectTransform hp=K.Panel(hud,"Estado_Jugadora",202,-79,340,102,null,new Vector2(0,1));
        K.Image(hp,"Emblema",-132,13,30,30,K.Rose,"Icons/singleplayer").preserveAspect=true;
        K.Label(hp,"Nombre","GYARU",-24,21,168,27,21,K.Paper);
        hpText=K.Label(hp,"Vida","100 / 100",93,21,114,28,18,K.Muted,TextAnchor.MiddleRight);
        K.Image(hp,"Barra_Fondo",0,-16,292,12,new Color(.15f,.19f,.26f));
        hpTrail=K.Image(hp,"Vida_DanoReciente",-146,-16,292,12,K.Gold);
        hpFill=K.Image(hp,"Vida_Actual",-146,-16,292,12,K.Rose);
        hpTrail.rectTransform.pivot=hpFill.rectTransform.pivot=new Vector2(0,.5f);
        K.Image(hud,"Ficha_Icono",50,-155,21,21,K.Gold,"Icons/star",new Vector2(0,1)).preserveAspect=true;
        tokens=K.Label(hud,"Fichas","00",112,-155,88,28,21,K.Gold,TextAnchor.MiddleLeft,new Vector2(0,1));
        timeText=K.Label(hud,"Tiempo","00:00",274,-155,146,28,16,K.Muted,TextAnchor.MiddleRight,new Vector2(0,1));
        roomText=K.Label(hud,"Distrito","DISTRITO INDUSTRIAL",0,-51,630,28,17,K.Gold,TextAnchor.MiddleCenter,new Vector2(.5f,1));
        objective=K.Label(hud,"Objetivo","",0,-82,630,42,20,K.Paper,TextAnchor.MiddleCenter,new Vector2(.5f,1));
        toast=K.Label(hud,"Aviso","",0,-128,700,52,19,K.Mint,TextAnchor.MiddleCenter,new Vector2(.5f,1));
        Button pause=K.Button(hud,"","pause",0,0,54,50,demo.OpenPauseMenu);
        RectTransform pauseRect=(RectTransform)pause.transform; pauseRect.anchorMin=pauseRect.anchorMax=new Vector2(1,1); pauseRect.anchoredPosition=new Vector2(-62,-54);
        Transform pauseIcon=pause.transform.Find("Icono"); if (pauseIcon != null) ((RectTransform)pauseIcon).anchoredPosition=Vector2.zero;
        K.Label(hud,"Atajo_Pausa","ESC",-62,-93,58,22,12,K.Muted,TextAnchor.MiddleCenter,new Vector2(1,1));
        for(int i=0;i<2;i++)
        {
            RectTransform card=K.Panel(hud,"Arma_"+(i+1),136+i*218,89,206,90,null,new Vector2(0,0));
            weaponFrames[i]=card.Find("Marco_Kenney").GetComponent<Image>();
            K.Label(card,"Ranura",(i+1).ToString(),-81,26,24,24,13,K.Muted,TextAnchor.MiddleCenter);
            weaponIcons[i]=K.Image(card,"Arma",-59,-3,53,53,Color.white); weaponIcons[i].preserveAspect=true;
            weaponNames[i]=K.Label(card,"Nombre","",32,2,116,53,17,K.Paper);
        }
        RectTransform dash=K.Panel(hud,"Estado_Dash",520,89,98,90,null,new Vector2(0,0));
        K.Image(dash,"Icono",0,13,28,28,K.Gold,"Icons/arrowRight").preserveAspect=true;
        dashText=K.Label(dash,"Estado","SHIFT",0,-23,86,23,13,K.Gold,TextAnchor.MiddleCenter);
        buffText=K.Label(hud,"Mejora_Temporal","",202,-187,340,26,15,K.Mint,TextAnchor.MiddleLeft,new Vector2(0,1));
        K.Label(hud,"Cambiar_Arma","Q   CAMBIAR ARMA",244,26,422,24,13,K.Muted,TextAnchor.MiddleCenter,new Vector2(0,0));
        interaction=K.Label(hud,"Interaccion","",100,74,650,56,21,K.Gold,TextAnchor.MiddleCenter,new Vector2(.5f,0));
    }
    private void Update() { RefreshNow(); }
    public void RefreshNow()
    {
        if (demo == null || demo.Player == null || demo.Health == null || hud == null) return;
        hud.gameObject.SetActive(!demo.InMainMenu && !demo.IsModalOpen);
        hudOpacity.alpha=GyaruSettings.Current.hudOpacity;
        float target=(float)demo.Health.currentHealth/Mathf.Max(1,demo.Health.maxHealth);
        shownHealth=Mathf.Lerp(shownHealth,target,1-Mathf.Exp(-15*Time.unscaledDeltaTime));
        trailHealth=Mathf.MoveTowards(trailHealth,target,Time.unscaledDeltaTime*.45f);
        if (target > trailHealth) trailHealth=target;
        hpFill.rectTransform.sizeDelta=new Vector2(292*shownHealth,12); hpTrail.rectTransform.sizeDelta=new Vector2(292*trailHealth,12);
        hpFill.color=target <= .3f ? new Color(1,.24f,.28f) : K.Rose;
        hpText.text=demo.Health.currentHealth+" / "+demo.Health.maxHealth;
        dashText.text=demo.Player.CanDash ? "SHIFT" : "CARGANDO";
        dashText.color=demo.Player.CanDash ? K.Gold : K.Muted;
        buffText.text=demo.Player.HasSpeedBuff ? "VELOCIDAD  +  "+Mathf.CeilToInt(demo.Player.SpeedBuffTimeRemaining)+" s" : "";
        tokens.text=demo.Fragments.ToString("00"); timeText.text=Duration(demo.Elapsed);
        roomText.text=demo.CurrentRoom==GyaruDemoBootstrap.DemoRoom.Procedural ? "DISTRITO "+demo.ProceduralRoomNumber.ToString("00")+"  /  INDUSTRIAL" : demo.CurrentRoom==GyaruDemoBootstrap.DemoRoom.Rest ? "REFUGIO  /  SALA DEL CLUB" : "TUTORIAL  /  DISTRITO INDUSTRIAL";
        objective.text=demo.CurrentMission(); interaction.text=demo.CurrentInteractLabel(); toast.text=demo.Toast;
        for(int i=0;i<2;i++)
        {
            WeaponData weapon=i==0 ? demo.Player.WeaponSlot1 : demo.Player.WeaponSlot2;
            weaponNames[i].text=weapon != null ? weapon.weaponName : "Ranura vacía";
            weaponIcons[i].sprite=weapon != null ? weapon.weaponSprite : K.Sprite("Icons/plus");
            weaponIcons[i].color=weapon != null ? Color.white : K.Line;
            weaponFrames[i].color=demo.Player.ActiveWeaponSlot==i ? K.Gold : K.Line;
        }
    }
    private void Begin(ScreenPage next,string title,string subtitle)
    {
        page=next; overlay.gameObject.SetActive(true); hud.gameObject.SetActive(false);
        K.Clear(content); feedback=null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        K.Label(content,"Titulo",title,-254,281,640,58,38,K.Paper);
        K.Label(content,"Subtitulo",subtitle,-82,236,984,32,17,K.Muted);
        K.Image(content,"Separador",0,209,1136,1,K.Line);
        K.Label(content,"Marca","GYARU ROGUE  /  DISTRITO INDUSTRIAL",-280,-321,590,24,12,K.Muted);
        K.Label(content,"Navegacion",next==ScreenPage.Home ? "FLECHAS / MANDO   ·   ENTER / A" : "ESC / B   VOLVER",414,-321,318,24,12,K.Muted,TextAnchor.MiddleRight);
    }
    private static void Focus(Button button)
    { if (EventSystem.current != null && button != null) EventSystem.current.SetSelectedGameObject(button.gameObject); }
    public void ShowPause()
    {
        homeOrigin=false;
        Begin(ScreenPage.Pause,"PAUSA","Respira. El distrito puede esperar.");
        K.Label(content,"Semilla","SEMILLA  "+demo.RunSeed,390,282,360,36,16,K.Gold,TextAnchor.MiddleRight);
        string[] labels={"Reanudar","Mapa del distrito","Configuración","Guardar partida","Cargar partida","Menú principal","Salir del juego"};
        string[] icons={"right","menuGrid","gear","save","open","home","power"};
        Action[] actions={demo.CloseModal,ShowMap,ShowSettings,()=>ShowSlots(true),()=>ShowSlots(false),ConfirmMainMenu,ConfirmQuit};
        for(int i=0;i<labels.Length;i++) { Button b=K.Button(content,labels[i],icons[i],-420,157-i*60,292,49,actions[i],i==0,i==6); if(i==0)Focus(b); }
        RectTransform detail=K.Panel(content,"Resumen_Recorrido",173,-36,744,446);
        K.Label(detail,"Ubicacion",demo.District != null ? demo.District.LocationLabel : demo.CurrentRoom==GyaruDemoBootstrap.DemoRoom.Rest ? "REFUGIO / SALA DEL CLUB" : "ENTRENAMIENTO INDUSTRIAL",0,182,684,35,19,K.Gold);
        RectTransform map=K.Rect(detail,"Mapa_Resumen",0,18,664,246);
        DistrictMapUI.Draw(map,demo.District,640,222,false);
        K.Image(detail,"Separador",0,-132,678,1,K.Line);
        Stat(detail,"SALUD",demo.Health.currentHealth+" / "+demo.Health.maxHealth,-220,-166,K.Rose);
        Stat(detail,"FICHAS",demo.Fragments.ToString("00"),0,-166,K.Gold);
        Stat(detail,"TIEMPO",Duration(demo.Elapsed),220,-166,K.Mint);
    }
    public void ShowHome()
    {
        homeOrigin=true; Begin(ScreenPage.Home,"MENÚ PRINCIPAL","Una nueva noche en el distrito.");
        K.Label(content,"Logo","GYARU\nROGUE",-277,74,550,200,76,K.Paper);
        K.Image(content,"Adorno",-334,-52,362,18,K.Gold,"Frames/Divider");
        K.Label(content,"Descripcion","Explora. Pelea. Encuentra el camino.\nTorres industriales, desvíos y secretos.\nCada semilla abre un nuevo recorrido.",-282,-135,520,112,22,K.Muted);
        Focus(K.Button(content,"Nueva partida","right",347,126,396,61,demo.ResetDemoRun,true));
        K.Button(content,"Cargar partida","open",347,44,396,61,()=>ShowSlots(false));
        K.Button(content,"Configuración","gear",347,-38,396,61,ShowSettings);
        K.Button(content,"Salir del juego","power",347,-120,396,61,ConfirmQuit,false,true);
        K.Label(content,"Inicio","El entrenamiento enseña movimiento,\nsalto doble, dash y combate.",347,-220,396,66,17,K.Muted,TextAnchor.MiddleCenter);
    }
    public void ShowMap()
    {
        Begin(ScreenPage.Map,"MAPA DEL DISTRITO",demo.District == null ? "El mapa se habilita al comenzar el recorrido." : "SEMILLA "+demo.RunSeed+"  /  "+demo.District.VisitedCount+" de "+demo.District.Plan.Rooms.Count+" salas visitadas");
        RectTransform map=K.Rect(content,"Plano_Ampliado",0,-4,1070,365);
        DistrictMapUI.Draw(map,demo.District,1040,345,true);
        K.Label(content,"Leyenda","ROSA  Tú     ·     VERDE  Refugio     ·     ESTRELLA  Cofre     ·     S  Salida     ·     ?  Por explorar",0,-219,1100,35,17,K.Muted,TextAnchor.MiddleCenter);
        Focus(K.Button(content,"Volver","return",-430,-275,260,48,ReturnRoot));
        K.Label(content,"Mapa_Ayuda","Las conexiones muestran los pasos entre salas descubiertas.",171,-275,780,42,16,K.Muted,TextAnchor.MiddleRight);
    }
    public void ShowSettings()
    {
        draft=GyaruSettings.Copy(); DrawSettings();
    }
    private void DrawSettings()
    {
        Begin(ScreenPage.Settings,"CONFIGURACIÓN","Los cambios se conservan al pulsar Aplicar.");
        SliderRow("Volumen general",160,draft.master,v=>{draft.master=v;AudioListener.volume=v;});
        SliderRow("Sonidos de interfaz",101,draft.interfaceVolume,v=>draft.interfaceVolume=v);
        SliderRow("Opacidad del HUD",42,draft.hudOpacity,v=>draft.hudOpacity=v,.5f);
        SettingButton("Pantalla completa",-23,()=>draft.fullscreen ? "Activada" : "Desactivada",()=>draft.fullscreen=!draft.fullscreen);
        SettingButton("Sincronización vertical",-80,()=>draft.vsync ? "Activada" : "Desactivada",()=>draft.vsync=!draft.vsync);
        SettingButton("Minimapa",-137,()=>draft.minimap ? "Visible" : "Oculto",()=>draft.minimap=!draft.minimap);
        SettingButton("Resolución de ventana",-194,()=>draft.width+" × "+draft.height,()=>{
            if(draft.width<1280){draft.width=1280;draft.height=720;}
            else if(draft.width<1600){draft.width=1600;draft.height=900;}
            else if(draft.width<1920){draft.width=1920;draft.height=1080;}
            else{draft.width=1280;draft.height=720;}
        });
        Focus(K.Button(content,"Cancelar","return",-430,-274,260,48,Back));
        K.Button(content,"Aplicar","checkmark",429,-274,260,48,()=>{GyaruSettings.Apply(draft);KenneyUISound.Play("Confirm");ReturnRoot();},true);
        K.Button(content,"Controles","gamepad",0,-274,260,48,ShowControls);
        K.Label(content,"Nota_Pantalla",Application.isEditor ? "Pantalla: los cambios se ven en el juego compilado." : "Pantalla completa: resolución nativa del monitor.",0,-234,900,25,13,K.Muted,TextAnchor.MiddleCenter);
    }
    private void ShowControls()
    {
        Begin(ScreenPage.Controls,"CONTROLES","Teclado, ratón y mando.");
        string[] labels={"Moverse","Saltar / salto doble","Dash","Atacar","Cambiar arma","Interactuar","Bajar de una pasarela","Pausa / volver","Mapa ampliado"};
        string[] keys={"A / D o flechas","Espacio","Shift","J o clic izquierdo","Q  ·  1 / 2","E","S + Espacio","Esc","M"};
        string[] pads={"Stick izquierdo","A / botón inferior","RB / botón superior derecho","X / botón izquierdo","LB / botón superior izquierdo","Y / botón superior","Stick abajo + A","Start / B","Desde Pausa"};
        for(int i=0;i<labels.Length;i++)
        {
            float y=166-i*46;
            K.Label(content,"Accion_"+i,labels[i],-365,y,390,35,20,K.Paper);
            K.Label(content,"Tecla_"+i,keys[i],20,y,350,35,19,K.Gold);
            K.Label(content,"Mando_"+i,pads[i],392,y,350,35,18,K.Muted);
        }
        Focus(K.Button(content,"Volver","return",-430,-275,260,48,DrawSettings));
    }

    private void SliderRow(string label,float y,float value,Action<float> changed,float min=0)
    {
        K.Label(content,"Etiqueta_"+label,label,-316,y,478,34,21,K.Paper);
        RectTransform root=K.Rect(content,"Ajuste_"+label,217,y,484,36);
        // Zona de clic completa, con una pista visible de 8 px.
        Image hit=root.gameObject.AddComponent<Image>(); hit.color=Color.clear;
        K.Image(root,"Pista",0,0,464,8,K.Line);
        RectTransform fillArea=K.Rect(root,"Area_Relleno",0,0,464,8);
        Image fill=K.Image(fillArea,"Relleno",0,0,464,8,K.Gold);
        RectTransform handleArea=K.Rect(root,"Area_Tirador",0,0,464,30);
        Image handle=K.Image(handleArea,"Tirador",0,0,16,26,K.Paper,"Frames/Button"); handle.raycastTarget=true;
        Slider slider=root.gameObject.AddComponent<Slider>(); slider.fillRect=fill.rectTransform; slider.handleRect=handle.rectTransform;
        slider.targetGraphic=handle; slider.direction=Slider.Direction.LeftToRight; slider.minValue=min; slider.maxValue=1; slider.SetValueWithoutNotify(value);
        Text number=K.Label(content,"Valor_"+label,Mathf.RoundToInt(value*100)+"%",517,y,80,32,18,K.Gold,TextAnchor.MiddleRight);
        slider.onValueChanged.AddListener(v=>{number.text=Mathf.RoundToInt(v*100)+"%";changed(v);});
    }
    private void SettingButton(string name,float y,Func<string> value,Action change)
    {
        K.Label(content,"Etiqueta_"+name,name,-316,y,478,34,21,K.Paper);
        Button button=null;
        button=K.Button(content,value(),null,262,y,588,43,()=>{change();button.GetComponentInChildren<Text>().text=value();});
    }
    public void ShowSlots(bool saving,string notice="")
    {
        Begin(saving ? ScreenPage.Save : ScreenPage.Load,saving ? "GUARDAR PARTIDA" : "CARGAR PARTIDA",saving ? "Elige una ranura. Se conserva una copia del guardado anterior." : "Continúa desde la posición y el estado del último guardado.");
        for(int i=1;i<=3;i++)
        {
            int slot=i; float x=(i-2)*379;
            RectTransform card=K.Panel(content,"Ranura_"+i,x,-15,354,374);
            K.Label(card,"Indice","RANURA  "+i.ToString("00"),0,151,300,33,17,K.Gold);
            RunSaveData data; string message;
            bool readable=demo.ReadSlot(i,out data,out message),exists=demo.SlotExists(i);
            K.Image(card,"Icono",-113,83,40,40,readable ? K.Gold : K.Muted,"Icons/"+(readable ? "save" : exists ? "warning" : "plus")).preserveAspect=true;
            string place=readable ? Location(data) : exists ? "No disponible" : "Ranura vacía";
            K.Label(card,"Lugar",place,31,83,210,65,24,K.Paper);
            string detail=readable ? DateTime.Parse(data.savedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy  HH:mm")+"\n\nSEMILLA  "+data.seed+"\nVIDA  "+data.health+" / "+data.maxHealth+"    ·    FICHAS  "+data.fragments+"\nTIEMPO  "+Duration(data.elapsed) : exists ? message : "Tu próximo recorrido\npuede comenzar aquí.";
            K.Label(card,"Detalles",detail,0,-24,296,140,17,K.Muted);
            if (readable && !string.IsNullOrEmpty(message)) K.Label(card,"Copia","Se usará la copia de seguridad.",0,-103,300,28,14,K.Gold);
            Button b=K.Button(card,saving ? "Guardar aquí" : "Cargar partida",saving ? "save" : "open",0,-143,302,51,()=>{
                if (saving && exists) Confirm("¿SOBRESCRIBIR RANURA "+slot+"?","El guardado actual de esta ranura se reemplazará.\nSe conservará su versión anterior como copia.","Sobrescribir",()=>Save(slot),()=>ShowSlots(true));
                else if(saving) Save(slot);
                else if(!demo.InMainMenu) Confirm("¿CARGAR ESTA PARTIDA?","La partida activa se reemplazará.\nLos cambios que no hayas guardado se perderán.","Cargar partida",()=>Load(slot),()=>ShowSlots(false));
                else Load(slot);
            },saving || readable);
            b.interactable=saving || readable;
            if (!b.interactable) b.gameObject.AddComponent<CanvasGroup>().alpha=.4f;
        }
        Focus(K.Button(content,"Volver","return",-430,-275,260,48,ReturnRoot));
        feedback=K.Label(content,"Estado",notice,165,-269,790,58,18,K.Mint,TextAnchor.MiddleRight);
    }
    private void Save(int slot)
    {
        string message; bool ok=demo.SaveSlot(slot,out message); KenneyUISound.Play(ok ? "Confirm" : "Error");
        ShowSlots(true,message); if (!ok && feedback != null) feedback.color=K.Rose;
    }
    private void Load(int slot)
    {
        string message; if (demo.LoadSlot(slot,out message)) { KenneyUISound.Play("Confirm"); return; }
        // Un fallo no cierra el menú ni hace avanzar la simulación.
        if (!demo.IsModalOpen) demo.OpenPauseMenu();
        ShowSlots(false,message); feedback.color=K.Rose; KenneyUISound.Play("Error");
    }
    private void ConfirmMainMenu()
    {
        Confirm("¿VOLVER AL MENÚ PRINCIPAL?","El progreso sin guardar se perderá.\nPuedes volver y guardarlo antes de salir.","Volver al menú",demo.ReturnToMainMenu,ShowPause);
    }
    private void ConfirmQuit()
    {
        Confirm("¿SALIR DEL JUEGO?",demo.InMainMenu ? "Hasta la próxima noche en el distrito." : "El progreso sin guardar se perderá.\nVuelve a Guardar partida si quieres conservarlo.","Salir del juego",demo.QuitGame,ReturnRoot);
    }
    private void Confirm(string title,string description,string accept,Action action,Action cancel)
    {
        Begin(ScreenPage.Confirm,title,"CONFIRMACIÓN"); confirmBack=cancel;
        K.Image(content,"Aviso",0,107,52,52,K.Gold,"Icons/warning").preserveAspect=true;
        K.Label(content,"Mensaje",description,0,0,980,130,25,K.Paper,TextAnchor.MiddleCenter);
        Focus(K.Button(content,"Cancelar","return",-210,-171,354,59,cancel));
        K.Button(content,accept,"checkmark",210,-171,354,59,action,true);
    }
    public void ShowModal(string title,string description,string[] options,Action<int> selected)
    {
        Begin(ScreenPage.Dialogue,title,"");
        K.Label(content,"Dialogo",description,0,113,1040,123,24,K.Paper,TextAnchor.MiddleCenter);
        for(int i=0;i<options.Length;i++)
        {
            int index=i; string label=options[i];
            if (label.Length>2 && char.IsDigit(label[0])) label=label.Substring(1).TrimStart();
            Button b=K.Button(content,label,i==0 ? "right" : "return",0,-15-i*83,730,65,()=>selected(index),i==0); if(i==0)Focus(b);
        }
    }
    public void HideModal()
    {
        page=ScreenPage.Hidden; overlay.gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        RefreshNow();
    }
    private void ReturnRoot() { if(homeOrigin || demo.InMainMenu) ShowHome(); else ShowPause(); }
    public void Back()
    {
        if (page==ScreenPage.Home || page==ScreenPage.Hidden) return;
        KenneyUISound.Play("Back");
        if (page==ScreenPage.Confirm) { confirmBack?.Invoke(); return; }
        if (page==ScreenPage.Controls) { DrawSettings(); return; }
        if (page==ScreenPage.Settings) AudioListener.volume=GyaruSettings.Current.master;
        if (page==ScreenPage.Pause || page==ScreenPage.Dialogue) { if(!demo.Health.IsDead) demo.CloseModal(); return; }
        ReturnRoot();
    }
    private static void Stat(Transform parent,string label,string value,float x,float y,Color color)
    { K.Label(parent,"Dato_"+label,label,x,y+12,190,25,13,K.Muted,TextAnchor.MiddleCenter); K.Label(parent,"Valor_"+label,value,x,y-19,190,34,26,color,TextAnchor.MiddleCenter); }
    private static string Location(RunSaveData data) { return data.room==0 ? "Entrenamiento" : data.room==1 ? "Refugio" : "Distrito "+data.district.ToString("00"); }
    private static string Duration(float seconds) { int total=Mathf.Max(0,(int)seconds); return (total/60).ToString("00")+":"+(total%60).ToString("00"); }
}
