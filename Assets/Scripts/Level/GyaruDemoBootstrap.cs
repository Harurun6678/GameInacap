using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GyaruRogue.Persistence;
using System.IO;

// Construye el tutorial, el descanso y las salas procedurales.
[DefaultExecutionOrder(-200)]
public class GyaruDemoBootstrap : MonoBehaviour
{
    public enum DemoRoom { Tutorial = 0, Rest = 1, Procedural = 2 }

    [SerializeField] private DemoRoom initialRoom = DemoRoom.Tutorial;
    [SerializeField] private bool startInMainMenu = true;
    public static bool IsDemoActive { get; private set; }

    private DemoRoom room;
    private GameObject roomRoot;
    private PlayerController player;
    private PlayerHealth health;
    private Rigidbody2D playerBody;
    private EnemyHealth trainingDummy;
    private GyaruDemoUI ui;
    private Camera gameCamera;
    private CameraFollow cameraFollow;
    private Sprite capsuleSprite;
    private WeaponData ruler;
    private WeaponData dagger;
    private int tutorialStep;
    [Header("Distritos procedurales")]
    [UnityEngine.Serialization.FormerlySerializedAs("combatRoomsPerRun")]
    [Tooltip("Cantidad de mapas completos antes de terminar la partida.")]
    [SerializeField, Range(1, 5)] private int districtsPerRun = 3;
    [Tooltip("Columnas del camino principal. Las torres y los desvios suman mas salas.")]
    [SerializeField, Range(4, 8)] private int mainPathColumns = 5;
    [Tooltip("Desvios que intenta colocar, ademas del circuito de regreso.")]
    [SerializeField, Range(1, 6)] private int optionalBranches = 3;
    private DistrictRuntime districtRuntime;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int fixedSeed = 12345;
    private int activeSeed;
    private int combatRoomNumber;
    private int fragments;
    private bool doorUnlocked;
    private bool upgradePurchased;
    private bool modalOpen;
    private bool pauseMenuOpen;
    private bool previousAudioPause;
    private float previousTimeScale = 1f;
    private Action[] choices = new Action[0];
    private float toastUntil;
    private string toast = "";
    private SaveFileStore saves;
    private float elapsed;
    public bool InMainMenu { get; private set; }
    public DistrictRuntime District => districtRuntime;
    public float Elapsed => elapsed;


    private static readonly Color Navy = new Color(.10f, .17f, .27f, 1f);
    private static readonly Color Rose = new Color(.97f, .38f, .55f, 1f);
    private static readonly Color Gold = new Color(1f, .79f, .43f, 1f);
    private static readonly Color Lilac = new Color(.46f, .71f, .89f, 1f);

    public DemoRoom CurrentRoom => room;
    public int Fragments => fragments;
    public int RunSeed => activeSeed;
    public int ProceduralRoomNumber => combatRoomNumber;
    public PlayerController Player => player;
    public PlayerHealth Health => health;
    public int TutorialStep => tutorialStep;
    public bool DoorUnlocked => doorUnlocked;
    public bool IsModalOpen => modalOpen;
    public bool IsPaused => pauseMenuOpen;
    public bool UpgradePurchased => upgradePurchased;
    public string Toast => Time.unscaledTime < toastUntil ? toast : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsDemoActive = false; }

    private void Awake()
    {
        IsDemoActive = true;
        districtsPerRun = Mathf.Clamp(districtsPerRun, 1, 5);
    }

    private void Start()
    {
        GyaruSettings.Load();
        saves = new SaveFileStore(Path.Combine(Application.persistentDataPath,"Partidas"));
        CreateCamera();
        CreateWeapons();
        CreatePlayer();
        cameraFollow.SetTarget(player.transform);
        ui = new GameObject("UI_EstadoYDialogos").AddComponent<GyaruDemoUI>();
        ui.transform.SetParent(transform,false);
        ui.Initialize(this);
        StartNewRun();
        fragments = initialRoom == DemoRoom.Rest ? 1 : 0; // Un fragmento para probar la escena de descanso.
        if (initialRoom == DemoRoom.Procedural) combatRoomNumber = 1;
        BuildRoom(initialRoom);
        if (startInMainMenu) ReturnToMainMenu();
    }

    private void OnDestroy()
    {
        IsDemoActive = false;
        if (modalOpen) { Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause; }
        if (health != null) health.Died -= OnPlayerDied;
        if (trainingDummy != null) trainingDummy.Died -= OnDummyDefeated;
        if (whitePixel != null)
        {
            Destroy(whitePixel.texture);
            Destroy(whitePixel);
        }
        foreach (Material material in spriteMaterials.Values)
            if (material != null) Destroy(material);
        spriteMaterials.Clear();
        if (capsuleSprite != null)
        {
            Destroy(capsuleSprite.texture);
            Destroy(capsuleSprite);
        }
        if (ruler != null) Destroy(ruler);
        if (dagger != null) Destroy(dagger);
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (modalOpen)
        {
            bool closePressed = (k != null && k.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));
            if (closePressed) ui.Back();
            return;
        }
        if (player == null) return;
        elapsed += Time.deltaTime;
        bool pausePressed = (k != null && k.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (pausePressed && !health.IsDead) { OpenPauseMenu(); return; }
        if (k != null && k.mKey.wasPressedThisFrame && districtRuntime != null) { OpenPauseMenu(); ui.ShowMap(); return; }
        if (room == DemoRoom.Tutorial) UpdateTutorial(k);
        else if (room == DemoRoom.Rest) UpdateRest(k);
        else UpdateProcedural(k);
    }

    private void CreateCamera()
    {
        GameObject c = new GameObject("Camara_Principal");
        c.tag = "MainCamera";
        gameCamera = c.AddComponent<Camera>();
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = Navy;
        c.transform.position = new Vector3(0f, 0f, -10f);
        c.AddComponent<AudioListener>();
        cameraFollow = c.AddComponent<CameraFollow>();
    }

    private void CreateWeapons()
    {
        ruler = ScriptableObject.CreateInstance<WeaponData>();
        ruler.weaponName = "Regla Neon";
        ruler.type = WeaponType.Ruler;
        ruler.damage = 25;
        ruler.attackRange = 1.45f;
        ruler.attackRate = 2.8f;
        ruler.knockbackForce = 3f;
        ruler.weaponSprite = Resources.Load<Sprite>("DemoArt/ruler");

        dagger = ScriptableObject.CreateInstance<WeaponData>();
        dagger.weaponName = "Dagas Jirai";
        dagger.type = WeaponType.Dagger;
        dagger.damage = 18;
        dagger.attackRange = 1.15f;
        dagger.attackRate = 4.3f;
        dagger.knockbackForce = 2f;
        dagger.weaponSprite = Resources.Load<Sprite>("DemoArt/dagger");
    }

    private void CreatePlayer()
    {
        GameObject obj = new GameObject("Jugador_Capsula");
        obj.tag = "Player";
        obj.layer = 0;
        obj.transform.position = new Vector3(-7.5f, -2.42f, 0);
        SpriteRenderer r = obj.AddComponent<SpriteRenderer>();
        r.sprite = CreateCapsuleSprite();
        r.sortingOrder = 20;
        r.sharedMaterial = SpriteMaterial(r.sprite);
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3.3f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(.72f, 1.62f);
        player = obj.AddComponent<PlayerController>();
        health = obj.AddComponent<PlayerHealth>();
        health.RestartSceneOnDeath = false;
        health.Died += OnPlayerDied;
        playerBody = rb;
        // El suelo usa la capa 8, aunque no tenga nombre en el proyecto.
        player.ConfigureGroundLayer(1 << 8);
        player.EquipWeapon(ruler);
    }

    private void BuildRoom(DemoRoom next)
    {
        if (trainingDummy != null) trainingDummy.Died -= OnDummyDefeated;
        if (roomRoot != null)
        {
            // Destroy se completa al final del frame; desactiva las colisiones ahora.
            roomRoot.SetActive(false);
            Destroy(roomRoot);
        }
        districtRuntime = null;
        cameraFollow.ClearWorldBounds();
        room = next;
        roomRoot = new GameObject(next == DemoRoom.Tutorial ? "Tutorial_DistritoIndustrial" : next == DemoRoom.Rest ? "Sala_Descanso" : "Distrito_Procedural");
        CloseModal();
        player.enabled = true;
        playerBody.simulated = true;
        playerBody.linearVelocity = Vector2.zero;
        player.Teleport(new Vector3(next == DemoRoom.Tutorial ? -7.5f : -8.25f, -2.42f, 0));
        if (next == DemoRoom.Tutorial)
        {
            tutorialStep = 0;
            doorUnlocked = false;
            BuildUrbanEnvironment();
            CreateDummy();
            CreateWorldIcon("ENTRENAMIENTO", "DemoArt/dummy", new Vector2(3.15f, -2.38f), 1.75f, 6);
            CreatePortal(new Vector2(9.05f, -2.2f), Rose);
        }
        else if (next == DemoRoom.Rest)
        {
            trainingDummy = null;
            doorUnlocked = true;
            BuildEnvironment("DemoArt/rest_bg", Lilac);
            CreateWorldIcon("ARMAS", "DemoArt/ruler", new Vector2(-4.55f, -2.28f), 1.55f, 6);
            CreateWorldIcon("CURACION", "DemoArt/fountain", new Vector2(.0f, -2.24f), 1.65f, 6);
            CreateWorldIcon("MEJORAS", "DemoArt/sigil", new Vector2(4.55f, -2.24f), 1.65f, 6);
            CreatePortal(new Vector2(9.05f, -2.2f), Gold);
        }
        else
        {
            trainingDummy = null;
            BuildProceduralRoom();
        }
        cameraFollow.SnapToTarget();
        ui.RefreshNow();
    }

    private Sprite CreateCapsuleSprite()
    {
        // Mantiene una silueta de capsula sin depender del sprite interno de Unity.
        Texture2D texture = new Texture2D(36, 82, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < 82; y++)
        for (int x = 0; x < 36; x++)
        {
            float dx = x - 17.5f;
            float dy = y - Mathf.Clamp(y, 18f, 63f);
            texture.SetPixel(x, y, dx * dx + dy * dy <= 18f * 18f ? Color.white : Color.clear);
        }
        texture.Apply();
        capsuleSprite = Sprite.Create(texture, new Rect(0, 0, 36, 82), new Vector2(.5f, .5f), 50f);
        return capsuleSprite;
    }

    private void BuildUrbanEnvironment()
    {
        const float surface = -3.265f;
        string path = "UrbanArt/Pixel Art - City Landscape V2/";
        // Todas las capas del pack miden 512 px y se dibujan a 16 unidades (32 px por unidad),
        // la misma escala de pixel que las baldosas de 1 unidad del suelo.
        CreateCityLayer("Fondo_Cielo", path + "Sky", surface, 16f, -100, 1f);
        CreateSkyTopFill(surface, path + "Sky", 16f);
        CreateCityLayer("Fondo_EdificiosLejanos", path + "Buildings 1", surface, 16f, -90, .85f);
        CreateCityLayer("Fondo_BrumaLejana", path + "Shade 2", surface, 16f, -85, .85f);
        CreateCityLayer("Fondo_EdificiosMedios", path + "Buildings 2", surface, 16f, -80, .6f);
        CreateCityLayer("Fondo_BrumaMedia", path + "Shade 3", surface, 16f, -75, .6f);
        CreateCityLayer("Fondo_EdificiosCercanos", path + "Buildings 3", surface, 16f, -70, .25f);
        BuildUrbanFloor(surface);
        CreateBlock("Limite_Izquierdo", new Vector2(-11, 0), new Vector2(.4f, 30), Color.clear, -8, true);
        CreateBlock("Limite_Derecho", new Vector2(11, 0), new Vector2(.4f, 30), Color.clear, -8, true);
    }

    // El cielo mide ~12.7 unidades de alto. Con doble salto la camara puede subir mas, asi que
    // se prolonga con el color exacto de su fila superior para que nunca asome el fondo de camara.
    private void CreateSkyTopFill(float surface, string skyPath, float width)
    {
        Sprite sky = Resources.Load<Sprite>(skyPath);
        if (sky == null || sky.texture == null) return;
        float skyTop = surface + sky.bounds.size.y * (width / sky.bounds.size.x);
        Color topColor = new Color32(16, 18, 36, 255); // fila superior de Sky.png
        CreateBlock("Fondo_CieloAlto", new Vector2(0f, skyTop + 9.5f), new Vector2(400f, 20f), topColor, -101);
    }

    private void BuildUrbanFloor(float surface)
    {
        // Baldosas de 1 unidad (32 px a 32 px por unidad). 64 columnas = 64 unidades de ancho:
        // cubre los limites jugables (+-11) mas el ancho de camara incluso en pantallas 32:9.
        const int columns = 64;
        const int rows = 8;
        const float tileSize = 1f;
        Transform tiles = new GameObject("Suelo_Baldosas").transform;
        tiles.SetParent(roomRoot.transform, false);
        Sprite edge = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_02");
        // Tile_37 es el ladrillo regular que encaja en horizontal y en vertical con Tile_02.
        // Tile_10 dejaba un patron irregular con huecos.
        Sprite brick = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_37");
        if (edge == null || brick == null)
            Debug.LogError("Faltan Tile_02 o Tile_37 en Resources/UrbanArt/1 Tiles.", this);
        // Un collider continuo evita enganches entre baldosas.
        CreateBlock("Suelo_Colision", new Vector2(0, surface - rows * tileSize * .5f),
            new Vector2(columns * tileSize, rows * tileSize), new Color(.12f,.08f,.18f), -11, true);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            Sprite tile = row == 0 ? edge : brick;
            if (tile == null) continue;
            Vector2 position = new Vector2((column - columns * .5f + .5f) * tileSize,
                surface - (row + .5f) * tileSize);
            GameObject piece = CreateSprite("Baldosa_" + row.ToString("00") + "_" + column.ToString("00"), tile, position, -9);
            piece.transform.localScale = new Vector3(tileSize / tile.bounds.size.x, tileSize / tile.bounds.size.y, 1);
            piece.transform.SetParent(tiles, true);
        }
    }

    private void CreateCityLayer(string title, string path, float bottom, float width, int order, float parallax)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null) { Debug.LogError("Falta el asset: " + path, this); return; }
        float scale = width / sprite.bounds.size.x;
        float height = sprite.bounds.size.y * scale;
        GameObject layer = CreateSprite(title, sprite, new Vector2(0, bottom + height * .5f), order);
        layer.transform.localScale = Vector3.one * scale;
        layer.AddComponent<ParallaxBackground>().Configure(gameCamera, parallax, 0f);
    }

    private void BuildEnvironment(string backgroundPath, Color accent)
    {
        Sprite bg = Resources.Load<Sprite>(backgroundPath);
        if (bg != null)
        {
            var o = CreateSprite("Fondo_Descanso", bg, Vector2.zero, -100);
            o.transform.localScale = new Vector3(22f / bg.bounds.size.x, 12.3f / bg.bounds.size.y, 1f);
        }
        CreateBlock("Suelo_Patio", new Vector2(0f, -4.18f), new Vector2(22f, 1.83f), new Color(.21f, .29f, .37f), -10, true);
        CreateBlock("Borde_Suelo_Dorado", new Vector2(0f, -3.25f), new Vector2(22f, .075f), Gold, -9);
        CreateBlock("Borde_Suelo_Acento", new Vector2(0f, -3.39f), new Vector2(22f, .04f), accent, -9);
        CreateBlock("Muro_Izquierdo", new Vector2(-10.84f, 0f), new Vector2(.45f, 8f), new Color(.23f, .32f, .42f), -8, true);
        CreateBlock("Muro_Derecho", new Vector2(10.84f, 0f), new Vector2(.45f, 8f), new Color(.23f, .32f, .42f), -8, true);
        for (int i = 0; i < 11; i++)
        {
            float x = -10f + i * 2f;
            CreateBlock("Suelo_Junta", new Vector2(x, -3.76f), new Vector2(.045f, .64f), new Color(.44f,.53f,.59f,.65f), -7);
        }
    }

    private void CreateDummy()
    {
        GameObject enemy = new GameObject("Enemigo_MunecoEntrenamiento");
        enemy.transform.SetParent(roomRoot.transform);
        enemy.transform.position = new Vector3(3.15f, -2.45f, 0f);
        enemy.layer = 0;
        BoxCollider2D col = enemy.AddComponent<BoxCollider2D>();
        col.size = new Vector2(.85f, 1.55f);
        trainingDummy = enemy.AddComponent<EnemyHealth>();
        trainingDummy.ConfigureHealth(55);
        trainingDummy.Died += OnDummyDefeated;
        // El muneco de entrenamiento permanece inmovil.
    }

    private void OnDummyDefeated(EnemyHealth who)
    {
        trainingDummy = null;
        if (roomRoot != null) { Transform art = roomRoot.transform.Find("ENTRENAMIENTO_Icono"); if (art != null) Destroy(art.gameObject); }
        tutorialStep = 5;
        doorUnlocked = true;
        fragments += 1;
        ShowToast("+1 FRAGMENTO DE ESTRELLA   |   PORTAL ABIERTO");
    }

    private void UpdateTutorial(Keyboard k)
    {
        if (tutorialStep == 0 && player.transform.position.x > -5.8f)
        {
            tutorialStep = 1; ShowToast("BIEN! AHORA SALTA");
        }
        else if (tutorialStep == 1 && (playerBody.linearVelocity.y > 4f || (k != null && k.spaceKey.wasPressedThisFrame)))
        {
            tutorialStep = 2; ShowToast("SALTO COMPLETADO");
        }
        else if (tutorialStep == 2 && ( (k != null && k.leftShiftKey.wasPressedThisFrame) || !player.CanDash))
        {
            tutorialStep = 3; ShowToast("DASH ACTIVADO");
        }
        else if (tutorialStep == 3 && trainingDummy != null && trainingDummy.CurrentHealth < trainingDummy.maxHealth)
        {
            tutorialStep = 4; ShowToast("DALE UNOS GOLPES MAS");
        }
        if (doorUnlocked && Near(9.05f) && PressedInteract(k))
        {
            ShowToast("SALA DE DESCANSO");
            BuildRoom(DemoRoom.Rest);
        }
    }

    private void UpdateRest(Keyboard k)
    {
        if (!PressedInteract(k)) return;
        if (Near(-4.55f)) OpenArmory();
        else if (Near(0f)) OpenFountain();
        else if (Near(4.55f)) OpenUpgrade();
        else if (Near(9.05f)) OpenEnterRun();
    }

    public string CurrentInteractLabel()
    {
        if (room == DemoRoom.Procedural) return districtRuntime != null ? districtRuntime.InteractionLabel() : "";
        if (room == DemoRoom.Tutorial)
        {
            if (Near(9.05f)) return doorUnlocked ? "[ E ] IR A LA SALA DE DESCANSO" : "PORTAL CERRADO - VENCE AL MUNECO";
            return "";
        }
        if (Near(-4.55f)) return "[ E ] EXAMINAR ARMAS";
        if (Near(0f)) return "[ E ] USAR FUENTE DE VIDA";
        if (Near(4.55f)) return "[ E ] ELEGIR MEJORA";
        if (Near(9.05f)) return "[ E ] COMENZAR RECORRIDO";
        return "";
    }

    public string CurrentMission()
    {
        if (room == DemoRoom.Rest) return "DESCANSA, EQUIPATE Y ENTRA AL RECORRIDO";
        if (room == DemoRoom.Procedural) return districtRuntime != null ? "DISTRITO " + combatRoomNumber + "/" + districtsPerRun + "  |  " + districtRuntime.LocationLabel : "GENERANDO DISTRITO";
        switch(tutorialStep)
        {
            case 0: return "01 / MOVERSE   -   A / D O FLECHAS";
            case 1: return "02 / SALTAR   -   ESPACIO";
            case 2: return "03 / DASH   -   SHIFT IZQUIERDO";
            case 3: return "04 / ATACAR AL MUNECO   -   J O CLICK";
            case 4: return "05 / DERROTA AL MUNECO DE PRACTICA";
            default: return "06 / ENTRA EN EL PORTAL DORADO   -   E";
        }
    }

    private void OpenArmory()
    {
        OpenModal("ARSENAL", "ELIGE UN ARMA. PUEDES CAMBIAR ENTRE ELLAS CON Q.",
          new string[] { "1   REGLA NEON  |  " + ruler.damage + " DMG", "2   DAGAS JIRAI  |  " + dagger.damage + " DMG RAPIDAS", "3   CERRAR" },
          new Action[] { () => { player.EquipWeapon(ruler); CloseModal(); ShowToast("REGLA NEON EQUIPADA"); },
                         () => { player.EquipWeapon(dagger); CloseModal(); ShowToast("DAGAS JIRAI EQUIPADAS"); }, CloseModal });
    }

    private void OpenFountain()
    {
        OpenModal("FUENTE DE VIDA", "LA FUENTE RECUPERA TU VIDA GRATIS.",
          new string[] { "1   RECUPERAR VIDA", "2   VOLVER" },
          new Action[] { () => { health.Heal(999); CloseModal(); ShowToast("VIDA RESTAURADA"); }, CloseModal });
    }

    private void OpenUpgrade()
    {
        if (upgradePurchased)
        {
            OpenModal("ALTAR DE ESTRELLAS", "YA USASTE TU MEJORA EN ESTA DEMO.", new string[] { "1   VOLVER" }, new Action[] { CloseModal });
            return;
        }
        string message = fragments > 0 ? "TIENES " + fragments + " FRAGMENTOS. ELIGE SOLO UNA MEJORA." : "NECESITAS 1 FRAGMENTO DEL TUTORIAL.";
        OpenModal("ALTAR DE ESTRELLAS", message,
          new string[] { "1   +20 DE VIDA MAXIMA", "2   +10 DE DANO AL ARMA ACTIVA", "3   VOLVER" },
          new Action[] { () => BuyUpgrade(true), () => BuyUpgrade(false), CloseModal });
    }

    private void BuyUpgrade(bool healthBoost)
    {
        if (fragments < 1 || upgradePurchased) { CloseModal(); ShowToast("FRAGMENTOS INSUFICIENTES"); return; }
        if (!healthBoost && player.CurrentWeapon == null)
        {
            CloseModal();
            ShowToast("EQUIPA UN ARMA PRIMERO");
            return;
        }
        fragments--;
        upgradePurchased = true;
        if (healthBoost) { health.maxHealth += 20; health.Heal(20); ShowToast("VIDA MAXIMA +20"); }
        else if (player.CurrentWeapon != null) { player.CurrentWeapon.damage += 10; ShowToast("DANO DE ARMA +10"); }
        CloseModal();
    }

    private void OpenEnterRun()
    {
        OpenModal("INICIAR RECORRIDO", "EXPLORA DISTRITOS CON TORRES, DESVIOS Y COFRES.\nSEMILLA: " + activeSeed,
          new string[] { "1   COMENZAR", "2   VOLVER" },
          new Action[] { () => { CloseModal(); combatRoomNumber = 1; BuildRoom(DemoRoom.Procedural); }, CloseModal });
    }


    private void StartNewRun()
    {
        activeSeed = useRandomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : fixedSeed;
        combatRoomNumber = 0;
    }

    private void BuildProceduralRoom()
    {
        combatRoomNumber = Mathf.Max(1,combatRoomNumber);
        districtRuntime = roomRoot.AddComponent<DistrictRuntime>();
        districtRuntime.Initialize(player,health,gameCamera,cameraFollow,activeSeed,combatRoomNumber,
            mainPathColumns,optionalBranches,(amount,message) => { fragments += amount; ShowToast(message); },ShowToast);
        roomRoot.AddComponent<DistrictMapUI>().Initialize(districtRuntime,this);
    }

    private void UpdateProcedural(Keyboard k)
    {
        if (districtRuntime == null || !PressedInteract(k) || !districtRuntime.TryInteract()) return;
        if (combatRoomNumber >= districtsPerRun)
        {
            OpenModal("RECORRIDO COMPLETADO", "TERMINASTE " + districtsPerRun + " DISTRITOS. SEMILLA: " + activeSeed,
                new string[] { "1   NUEVA PARTIDA", "2   VOLVER AL REFUGIO" },
                new Action[] { ResetDemoRun, () => { CloseModal(); StartNewRun(); BuildRoom(DemoRoom.Rest); } });
        }
        else
        {
            OpenModal("DISTRITO COMPLETADO", "PUEDES CONTINUAR O SEGUIR EXPLORANDO ESTE MAPA.",
                new string[] { "1   SIGUIENTE DISTRITO", "2   SEGUIR EXPLORANDO" },
                new Action[] { () => { CloseModal(); combatRoomNumber++; BuildRoom(DemoRoom.Procedural); }, CloseModal });
        }
    }

    public void ResetDemoRun()
    {
        InMainMenu = false;
        elapsed = 0;
        fragments = 0;
        upgradePurchased = false;
        ruler.damage = 25;
        dagger.damage = 18;
        health.ResetHealth(100);
        player.ResetInventory(ruler);
        CloseModal();
        StartNewRun();
        fragments = initialRoom == DemoRoom.Rest ? 1 : 0;
        if (initialRoom == DemoRoom.Procedural) combatRoomNumber = 1;
        BuildRoom(initialRoom);
    }

    private void OnPlayerDied()
    {
        OpenModal("FIN DE LA PARTIDA", "PUEDES VOLVER A INTENTARLO DESDE EL INICIO.",
            new string[] { "1   REINTENTAR" }, new Action[] { ResetDemoRun });
    }

    public void OpenPauseMenu()
    {
        if (InMainMenu || health == null || health.IsDead) return;
        PauseSimulation();
        ui.ShowPause();
    }

    private void PauseSimulation()
    {
        if (!modalOpen) { previousTimeScale=Time.timeScale; previousAudioPause=AudioListener.pause; }
        modalOpen=true; pauseMenuOpen=true; Time.timeScale=0; AudioListener.pause=true;
        choices = new Action[0];
        player.BlockInputForCurrentFrame();
    }

    public void ReturnToMainMenu()
    {
        PauseSimulation(); InMainMenu=true; ui.ShowHome();
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
        if (!focused && ui != null && !modalOpen && !InMainMenu && health != null && !health.IsDead) OpenPauseMenu();
    }

    private void OpenModal(string title, string description, string[] labels, Action[] callbacks, bool preserveMotion = false)
    {
        if (!modalOpen) { previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause; }
        pauseMenuOpen = false;
        AudioListener.pause = true;
        modalOpen = true;
        Time.timeScale = 0f;
        choices = callbacks;
        if (!preserveMotion)
        {
            player.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
        }
        ui.ShowModal(title, description, labels, SelectChoice);
    }

    private void SelectChoice(int index)
    {
        if (!modalOpen || index < 0 || index >= choices.Length) return;
        choices[index]?.Invoke();
    }

    public void CloseModal()
    {
        if (health != null && health.IsDead) return;
        if (modalOpen) { Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause; }
        modalOpen = false;
        pauseMenuOpen = false;
        choices = new Action[0];
        if (player != null) { player.enabled = true; player.BlockInputForCurrentFrame(); }
        if (ui != null) ui.HideModal();
    }

    public bool SlotExists(int slot) { return saves != null && saves.Exists(slot); }

    public bool ReadSlot(int slot, out RunSaveData data, out string message)
    {
        data=null; message=null; string json; bool recovered;
        if (!saves.TryRead(slot,out json,out recovered,out message)) return false;
        try
        {
            data=JsonUtility.FromJson<RunSaveData>(json);
            message=data == null ? "El archivo no contiene una partida." : data.Validate();
            if (message != null) { data=null; return false; }
            message = recovered ? "Se recuperará la copia de seguridad anterior." : "";
            return true;
        }
        catch (ArgumentException) { data=null; message="El archivo no contiene una partida válida."; return false; }
    }

    private RunSaveData CaptureRun()
    {
        RunSaveData data = new RunSaveData {
            savedUtc=DateTime.UtcNow.ToString("o"), seed=activeSeed, district=combatRoomNumber,
            room=(int)room, columns=mainPathColumns, branches=optionalBranches, districts=districtsPerRun,
            health=health.currentHealth, maxHealth=health.maxHealth, fragments=fragments,
            tutorialStep=tutorialStep, dummyHealth=trainingDummy != null ? trainingDummy.CurrentHealth : 0,
            upgraded=upgradePurchased, doorUnlocked=doorUnlocked, weapon0=WeaponId(player.WeaponSlot1),
            weapon1=WeaponId(player.WeaponSlot2), activeWeapon=player.ActiveWeaponSlot,
            rulerDamage=ruler.damage, daggerDamage=dagger.damage, x=player.transform.position.x,
            y=player.transform.position.y, speedBuff=player.SpeedBuffTimeRemaining, elapsed=elapsed
        };
        if (districtRuntime != null) districtRuntime.Capture(data);
        return data;
    }
    private int WeaponId(WeaponData weapon) { return weapon == ruler ? 1 : weapon == dagger ? 2 : 0; }
    private WeaponData WeaponFromId(int id) { return id == 1 ? ruler : id == 2 ? dagger : null; }

    public bool SaveSlot(int slot, out string message)
    {
        if (InMainMenu || health.IsDead) { message="No hay una partida activa para guardar."; return false; }
        RunSaveData data=CaptureRun(); message=data.Validate();
        if (message != null) return false;
        if (!saves.TryWrite(slot,JsonUtility.ToJson(data,true),out message)) return false;
        message="Partida guardada en la ranura " + slot + ".";
        ShowToast(message); return true;
    }

    public bool LoadSlot(int slot, out string message)
    {
        RunSaveData data;
        if (!ReadSlot(slot,out data,out message)) return false;
        string recovery=message;
        RunSaveData rollback = !InMainMenu && !health.IsDead ? CaptureRun() : null;
        try { ApplyRun(data); }
        catch (Exception exception)
        {
            Debug.LogException(exception,this);
            try { if (rollback != null) ApplyRun(rollback); else ReturnToMainMenu(); }
            catch (Exception rollbackError) { Debug.LogException(rollbackError,this); ReturnToMainMenu(); }
            message="No se pudo reconstruir la partida. Revisa los assets del proyecto."; return false;
        }
        message=string.IsNullOrEmpty(recovery) ? "Partida cargada desde la ranura " + slot + "." : "Cargada la copia de seguridad de la ranura " + slot + ".";
        ShowToast(message); return true;
    }

    private void ApplyRun(RunSaveData data)
    {
        health.RestoreHealth(data.maxHealth,data.health);
        InMainMenu=false; CloseModal();
        activeSeed=data.seed; combatRoomNumber=data.district;
        mainPathColumns=data.columns; optionalBranches=data.branches; districtsPerRun=data.districts;
        BuildRoom((DemoRoom)data.room);
        tutorialStep=data.tutorialStep; doorUnlocked=data.doorUnlocked;
        fragments=data.fragments; upgradePurchased=data.upgraded; elapsed=data.elapsed;
        ruler.damage=data.rulerDamage; dagger.damage=data.daggerDamage;
        health.RestoreHealth(data.maxHealth,data.health);
        player.RestoreInventory(WeaponFromId(data.weapon0),WeaponFromId(data.weapon1),data.activeWeapon,data.speedBuff);
        player.Teleport(new Vector3(data.x,data.y,0));
        if (room == DemoRoom.Tutorial && trainingDummy != null)
        {
            if (data.dummyHealth > 0) trainingDummy.RestoreHealth(data.dummyHealth);
            else
            {
                trainingDummy.Died-=OnDummyDefeated; trainingDummy.gameObject.SetActive(false); Destroy(trainingDummy.gameObject); trainingDummy=null;
                Transform art=roomRoot.transform.Find("ENTRENAMIENTO_Icono");
                if (art != null) { art.gameObject.SetActive(false); Destroy(art.gameObject); }
            }
        }
        if (districtRuntime != null) districtRuntime.Restore(data);
        Physics2D.SyncTransforms(); cameraFollow.SnapToTarget(); player.BlockInputForCurrentFrame(); ui.RefreshNow();
    }

    private bool Near(float x) => player != null && Mathf.Abs(player.transform.position.x - x) < 1.5f && Mathf.Abs(player.transform.position.y + 2.42f) < 1.8f;
    private static bool PressedInteract(Keyboard k) => k != null && k.eKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
    private void ShowToast(string message) { toast = message; toastUntil = Time.unscaledTime + 2.2f; }

    private void CreateWorldIcon(string label, string art, Vector2 pos, float size, int order)
    {
        CreateBlock(label + "_Base", new Vector2(pos.x, -3.02f), new Vector2(2.05f, .38f), new Color(.23f, .36f, .48f), 1);
        CreateBlock(label + "_BordeRotulo", new Vector2(pos.x, -2.85f), new Vector2(1.82f, .055f), room == DemoRoom.Tutorial ? Rose : Lilac, 2);
        Sprite spr = Resources.Load<Sprite>(art);
        if (spr != null)
        {
            GameObject o = CreateSprite(label + "_Icono", spr, pos, order);
            float v = size / Mathf.Max(spr.bounds.size.x, spr.bounds.size.y);
            o.transform.localScale = Vector3.one * v;
        }
        GameObject t = new GameObject(label + "_Titulo");
        t.transform.SetParent(roomRoot.transform);
        t.transform.position = new Vector3(pos.x, -1.18f, 0);
        TextMesh mesh = t.AddComponent<TextMesh>();
        mesh.text = label;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = .10f;
        mesh.fontSize = 52;
        mesh.color = new Color(1f, .91f, .78f);
        t.GetComponent<MeshRenderer>().sortingOrder = 35;
    }

    private void CreatePortal(Vector2 pos, Color color)
    {
        CreateBlock("Portal_Aura", pos, new Vector2(1.5f, 2.45f), new Color(color.r, color.g, color.b, .23f), 0);
        CreateBlock("Portal_Interior", pos, new Vector2(1.12f, 2.12f), new Color(.07f,.06f,.14f), 1);
        CreateBlock("Portal_Dintel", pos + new Vector2(0, 1.09f), new Vector2(1.44f,.10f), color, 4);
        CreateBlock("Portal_PilarIzquierdo", pos + new Vector2(-.64f,0), new Vector2(.11f,2.2f), color, 4);
        CreateBlock("Portal_PilarDerecho", pos + new Vector2(.64f,0), new Vector2(.11f,2.2f), color, 4);
    }

    private GameObject CreateSprite(string title, Sprite sprite, Vector2 pos, int sorting)
    {
        GameObject o = new GameObject(title);
        o.transform.SetParent(roomRoot.transform);
        o.transform.position = new Vector3(pos.x, pos.y, 0);
        SpriteRenderer r = o.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = sorting;
        r.sharedMaterial = SpriteMaterial(r.sprite);
        return o;
    }

    private GameObject CreateBlock(string title, Vector2 pos, Vector2 size, Color color, int sorting, bool collider = false)
    {
        GameObject o = new GameObject(title);
        o.transform.SetParent(roomRoot.transform);
        o.transform.position = new Vector3(pos.x, pos.y, 0);
        SpriteRenderer r = o.AddComponent<SpriteRenderer>();
        r.sprite = PixelSprite();
        r.color = color;
        r.sortingOrder = sorting;
        r.sharedMaterial = SpriteMaterial(r.sprite);
        o.transform.localScale = new Vector3(size.x, size.y, 1f);
        if (collider)
        {
            o.layer = 8;
            BoxCollider2D box = o.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }
        return o;
    }

    private static Sprite whitePixel;
    private readonly System.Collections.Generic.Dictionary<Texture, Material> spriteMaterials =
        new System.Collections.Generic.Dictionary<Texture, Material>();
    private static Sprite PixelSprite()
    {
        if (whitePixel != null) return whitePixel;
        Texture2D tx = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tx.SetPixel(0, 0, Color.white);
        tx.Apply();
        whitePixel = Sprite.Create(tx, new Rect(0, 0, 1, 1), new Vector2(.5f,.5f), 1f);
        return whitePixel;
    }
    private Material SpriteMaterial(Sprite sprite)
    {
        if (sprite == null) return null;
        Texture texture = sprite.texture;
        if (spriteMaterials.TryGetValue(texture, out Material material)) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        // Separa materiales por textura y evita depender de la iluminacion 2D.
        material = new Material(shader);
        material.SetTexture("_MainTex", texture);
        spriteMaterials.Add(texture, material);
        return material;
    }
}
