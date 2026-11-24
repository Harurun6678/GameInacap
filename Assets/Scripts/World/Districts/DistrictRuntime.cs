using System;
using System.Collections.Generic;
using UnityEngine;
using NeonSpite.Generation;
using NeonSpite.Persistence;

// Construye y mantiene un distrito completo; pasar de sala no destruye lo explorado.
[DefaultExecutionOrder(220)]
public class DistrictRuntime : MonoBehaviour
{
    private sealed class RoomState
    {
        public GameObject Root, Chest;
        public HealingDrink Drink;
        public bool Visited, Looted, Healed, Rewarded;
        public int Enemies;
        // Sala inversa (Layout null si la sala nunca se invierte).
        public InverseRoom Layout;
        public bool InverseRevealed, InverseLooted, InverseExpired;
        public float InverseTimeLeft;
        public GameObject Inverse, InverseChest;
        public readonly List<GameObject> NormalOnly = new List<GameObject>();
        // Invertida ahora mismo: se reveló y el tiempo no se acabó.
        public bool InverseActive => InverseRevealed && !InverseExpired;
    }

    // ---------- Salas inversas ----------
    private static readonly Color InverseTint = new Color(.86f,.42f,1f);
    private Color[] skyColors;
    private bool skyInverted;

    public bool IsInverted(int id) { return states[id].InverseActive; }
    // Segundos que le quedan a la sala inversa actual (-1 si no hay cuenta en marcha).
    public float InverseTimeLeft
    {
        get
        {
            if (Plan == null) return -1f;
            RoomState state = states[CurrentRoomId];
            return state.InverseActive && !state.InverseLooted ? Mathf.Max(0f, state.InverseTimeLeft) : -1f;
        }
    }

    // Se acabó el tiempo: la sala vuelve a ser normal, con sus guardias. El cofre se pierde.
    private void ExpireInverse(DistrictRoom room)
    {
        RoomState state = states[room.Id];
        state.InverseExpired = true; state.InverseTimeLeft = 0f; Revision++;
        if (state.Inverse != null) state.Inverse.SetActive(false);
        foreach (GameObject obj in state.NormalOnly) if (obj != null) obj.SetActive(true);
        foreach (float x in room.EnemyX) CreateEnemy(room, x, state.Root.transform);
        // Despejarla vuelve a dar la ficha de siempre.
        if (room.EnemyX.Count > 0) state.Rewarded = false;
        follow.Shake(.25f,.35f);
        if (GamePresentation.Active != null) GamePresentation.Active.SetZone("Se acabó el tiempo", rules, "La sala volvió a la normalidad y sus guardias regresaron.");
        notify("SE ACABÓ EL TIEMPO  ·  LA SALA VOLVIÓ A LA NORMALIDAD");
    }
    public bool IsInverseLooted(int id) { return states[id].InverseLooted; }

    private void BuildInverse(DistrictRoom room, RoomState state, Transform root)
    {
        InverseRoom layout = state.Layout;
        Vector2 origin = new Vector2(room.WorldX, room.WorldY);
        state.Inverse = new GameObject("Sala_Inversa");
        state.Inverse.transform.SetParent(root, false);
        Transform world = state.Inverse.transform;
        Box("Velo_Inverso", world, origin + new Vector2(12,8), new Vector2(24,16), new Color(.30f,.06f,.46f,.34f), -6);
        // Las plataformas se vuelven más exigentes en cada distrito: aguantan menos tiempo.
        float still = Mathf.Max(.55f, .95f - .08f * (Plan.District - 1)), moving = still + .6f;
        for (int i = 0; i < layout.Platforms.Count; i++)
        {
            InversePlatform spec = layout.Platforms[i];
            Vector2 center = origin + new Vector2(spec.X, spec.Top - .125f);
            GameObject obj = new GameObject("Plataforma_Inversa_" + i.ToString("00"));
            obj.transform.SetParent(world, false); obj.transform.position = center; obj.layer = 8;
            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(spec.Width, .25f);
            collider.usedByEffector = true;
            PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 170;
            obj.AddComponent<Rigidbody2D>();
            Color tint = spec.Crumbles ? InverseTint : Color.Lerp(InverseTint, KenneyTheme.Gold, .55f);
            DrawPlatformTiles(obj.transform, center, spec.Width, tint);
            obj.AddComponent<InverseMover>().Configure(spec, origin, player, spec.Motion == InverseMotion.None ? still : moving);
        }
        Vector2 chest = origin + new Vector2(layout.ChestX, layout.ChestTop + BackpackHeight / 2f);
        Sprite glowMask = Resources.Load<Sprite>("KenneyUI/Lights/Glow");
        GameObject glow = null;
        if (glowMask != null)
        {
            glow = Art("Mochila_Brillo", world, glowMask, chest, 6);
            glow.transform.localScale = Vector3.one * 3f / glowMask.bounds.size.x;
            glow.GetComponent<SpriteRenderer>().color = new Color(KenneyTheme.Gold.r, KenneyTheme.Gold.g, KenneyTheme.Gold.b, .22f);
        }
        state.InverseChest = CreateBackpack("Mochila_Inversa", world, chest, null, () => OpenInverseChest(state));
        // El brillo queda fijo (no gira con la mochila) y desaparece con ella.
        if (glow != null) state.InverseChest.GetComponent<BreakableBackpack>().SetCompanion(glow);
        state.Inverse.SetActive(false);
    }

    // La sala se invierte la primera vez que el jugador entra en ella.
    private void RevealInverse(DistrictRoom room, bool silent)
    {
        RoomState state = states[room.Id];
        if (state.Layout == null || state.Inverse == null) return;
        state.InverseRevealed = true; Revision++;
        foreach (GameObject obj in state.NormalOnly) if (obj != null) obj.SetActive(false);
        state.Inverse.SetActive(true);
        // Una sala inversa es solo plataformeo: sus guardias desaparecen y no hay premio por despejarla.
        foreach (KeyValuePair<EnemyHealth,int> pair in new List<KeyValuePair<EnemyHealth,int>>(enemyOwners))
        {
            if (pair.Value != room.Id || pair.Key == null) continue;
            pair.Key.Died -= EnemyDefeated; enemyOwners.Remove(pair.Key); enemySlots.Remove(pair.Key);
            pair.Key.gameObject.SetActive(false); Destroy(pair.Key.gameObject);
        }
        state.Enemies = 0; state.Rewarded = true;
        if (state.InverseLooted && state.InverseChest != null) state.InverseChest.GetComponent<BreakableBackpack>().SetBroken();
        if (silent) return;
        follow.Shake(.25f,.35f);
        if (GamePresentation.Active != null) GamePresentation.Active.SetZone("Sala inversa", rules, "Rompe la mochila antes de que acabe el tiempo. Las plataformas ceden si te quedas quieta.");
        notify("¡LA SALA SE INVIRTIÓ!  ·  " + Mathf.CeilToInt(state.InverseTimeLeft) + " SEGUNDOS PARA ROMPER LA MOCHILA");
    }

    private void OpenInverseChest(RoomState state)
    {
        state.InverseLooted = true; Revision++;
        InverseRoom layout = state.Layout; int amount = layout.RewardAmount;
        WeaponData weapon = player.CurrentWeapon;
        switch (layout.Reward)
        {
            case InverseReward.Healing:
                if (health.currentHealth < health.maxHealth)
                {
                    int heal = Mathf.Max(1, health.maxHealth * amount / 100);
                    health.Heal(heal); notify("MOCHILA INVERSA: CURACIÓN DE " + heal + " DE VIDA"); return;
                }
                break;
            case InverseReward.MaxHealth:
                if (health.maxHealth + amount <= 1000)
                {
                    health.maxHealth += amount; health.Heal(amount); notify("MOCHILA INVERSA: MEJORA  ·  VIDA MÁXIMA +" + amount); return;
                }
                break;
            case InverseReward.Damage:
                if (weapon != null && weapon.damage + amount <= 1000)
                {
                    weapon.damage += amount; notify("MOCHILA INVERSA: MEJORA  ·  " + weapon.weaponName.ToUpperInvariant() + " +" + amount + " DE DAÑO"); return;
                }
                break;
            default:
                award(amount, "MOCHILA INVERSA: +" + amount + " FICHAS"); return;
        }
        // Si el premio no sirve ahora (vida llena, sin arma o al máximo), se cambia por fichas.
        award(2, "MOCHILA INVERSA: +2 FICHAS");
    }

    // Dibuja una plataforma inversa con la misma baldosa que las pasarelas, teñida.
    private void DrawPlatformTiles(Transform parent, Vector2 center, float width, Color tint)
    {
        GameObject fill = Art("Baldosas", parent, edge, center, -5);
        SpriteRenderer renderer = fill.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Tiled;
        float scale = 1f / renderer.sprite.bounds.size.x;
        fill.transform.localScale = new Vector3(scale, scale, 1);
        renderer.size = new Vector2(width / scale, .25f / scale);
        renderer.color = Color.Lerp(new Color(.72f, .79f, .9f), tint, .75f);
    }

    // Mochila destructible (reemplaza a los cofres). Se rompe con BackpackHits golpes.
    private const float BackpackHeight = 1.35f;
    private const int BackpackHits = 3;
    // position: centro de la mochila apoyada en el suelo; el componente la hace flotar y girar.
    private GameObject CreateBackpack(string name, Transform parent, Vector2 position, Func<bool> canBreak, Action onBroken)
    {
        GameObject shadow = Box(name + "_Sombra", parent, position - new Vector2(0, BackpackHeight / 2f - .04f), new Vector2(.95f, .1f), new Color(0, 0, 0, .35f), 7);
        GameObject pack = Art(name, parent, backpack != null ? backpack : chestFrame, position, 9);
        Fit(pack, BackpackHeight);
        BreakableBackpack breakable = pack.AddComponent<BreakableBackpack>();
        breakable.Configure(BackpackHits, canBreak, () => notify("PRIMERO DERROTA AL GUARDIA"), onBroken);
        breakable.SetShadow(shadow.transform);
        return pack;
    }
    public DistrictPlan Plan
    {
        get; private set;
    }
    public int CurrentRoomId
    {
        get; private set;
    }
    public int Revision
    {
        get; private set;
    }
    public int VisitedCount
    {
        get; private set;
    }
    public string LocationLabel
    {
        get
        {
            DistrictRoom room = Plan.Rooms[CurrentRoomId];
            return PurposeName(room.Purpose) + (room.Has(DoorSide.Up) ? " / TORRE" : "") + "  " + (room.Id + 1).ToString("00");
        }
    }

    private RoomState[] states;
    private PlayerController player;
    private PlayerHealth health;
    private Camera cameraView;
    private CameraFollow follow;
    private Action<int, string> award;
    private Action<string> notify;
    private int refugeId = -1;
    private float nextRoomCheck;
    private Vector2 minWorld, maxWorld;
    private const float FountainX = 16f;
    private const float ExitDoorX = 20f;
    private bool finalDistrict;
    private Sprite pixel, chestFrame, backpack;
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
    private readonly Dictionary<EnemyHealth, int> enemyOwners = new Dictionary<EnemyHealth, int>();
    private readonly Dictionary<EnemyHealth, int> enemySlots = new Dictionary<EnemyHealth, int>();
    private readonly List<SpriteRenderer> skyLayers = new List<SpriteRenderer>();
    private Transform backdrop;
    private Sprite edge, brick;
    private BiomeRules rules;
    private Color accent;

    public void Initialize(PlayerController actor, PlayerHealth hp, Camera view, CameraFollow tracking,
        int seed, int stage, int columns, int branches, Action<int, string> reward, Action<string> toast, int version = DistrictPlan.GeneratorVersion, DistrictStyle style = DistrictStyle.Industrial, bool finalDistrict = false)
    {
        player = actor;
        health = hp;
        cameraView = view;
        follow = tracking;
        award = reward;
        notify = toast;
        this.finalDistrict = finalDistrict;
        Plan = DistrictGenerator.Generate(seed, stage, columns, branches, version, style);
        rules = BiomeRules.For(Plan.Style);
        accent = GamePresentation.Hex(rules.Accent);
        states = new RoomState[Plan.Rooms.Count];
        CurrentRoomId = Plan.EntranceId;
        edge = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_02");
        brick = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_37");
        if (edge == null || brick == null)
            throw new InvalidOperationException("Falta Tile_02 o Tile_37 del pack urbano.");
        CreatePixel();
        CreateChestFrame();
        CreateBackdrop();
        minWorld = new Vector2(float.MaxValue, float.MaxValue);
        maxWorld = new Vector2(float.MinValue, float.MinValue);
        foreach (DistrictRoom room in Plan.Rooms)
        {
            minWorld = Vector2.Min(minWorld, new Vector2(room.WorldX, room.WorldY));
            maxWorld = Vector2.Max(maxWorld, new Vector2(room.WorldX + 24, room.WorldY + 16));
            InverseRoom layout = InverseLayout.Build(Plan, room);
            states[room.Id] = new RoomState { Layout = layout, InverseTimeLeft = layout != null ? layout.Duration : 0f };
            BuildRoom(room);
        }
        skyColors = new Color[skyLayers.Count];
        for (int i = 0; i < skyLayers.Count; i++)
            skyColors[i] = skyLayers[i].color;
        follow.SetWorldBounds(new Rect(minWorld.x, minWorld.y, maxWorld.x - minWorld.x, maxWorld.y - minWorld.y));
        Physics2D.SyncTransforms();
        player.Teleport(Spawn(Plan.EntranceId));
        player.BlockInputForCurrentFrame();
        follow.SnapToTarget();
        CheckCurrentRoom();
        follow.SnapToTarget();
        notify("DISTRITO " + Plan.District + "  |  " + GameControls.PrimaryLabel(GameAction.Map) + ": MAPA  |  SEMILLA " + Plan.Seed);
        Debug.Log("Distrito " + Plan.District + ": " + Plan.Rooms.Count + " salas, semilla " + Plan.Seed + ", codigo " + Plan.Fingerprint(), this);
    }

    public bool IsVisited(int id)
    {
        return states[id].Visited;
    }
    public bool IsCleared(int id)
    {
        return states[id].Enemies == 0;
    }
    public bool IsLooted(int id)
    {
        return states[id].Looted;
    }
    public bool IsRevealed(int id)
    {
        if (states[id].Visited)
            return true;
        foreach (int neighbor in Plan.Rooms[id].Neighbors)
            if (states[neighbor].Visited)
                return true;
        return false;
    }
    public static string PurposeName(RoomPurpose purpose)
    {
        switch (purpose)
        {
            case RoomPurpose.Entrance:
                return "ENTRADA";
            case RoomPurpose.Refuge:
                return "REFUGIO";
            case RoomPurpose.Treasure:
                return "ALMACEN";
            case RoomPurpose.Exit:
                return "ZONA SEGURA";
            default:
                return "COMBATE";
        }
    }

    private void Update()
    {
        if (Plan == null || Time.timeScale <= 0 || health.IsDead)
            return;
        if (Time.time >= nextRoomCheck)
        {
            nextRoomCheck = Time.time + .1f;
            CheckCurrentRoom();
        }
        // La cuenta solo corre con el jugador dentro y se detiene al romper la mochila.
        RoomState current = states[CurrentRoomId];
        if (current.InverseActive && !current.InverseLooted)
        {
            current.InverseTimeLeft -= Time.deltaTime;
            if (current.InverseTimeLeft <= 0f)
                ExpireInverse(Plan.Rooms[CurrentRoomId]);
        }
        if (player.transform.position.y < minWorld.y - 4 || player.transform.position.x < minWorld.x - 4 || player.transform.position.x > maxWorld.x + 4)
        {
            health.TakeDamage(10);
            if (!health.IsDead)
                TravelTo(refugeId >= 0 ? refugeId : Plan.EntranceId);
        }
    }

    private void CheckCurrentRoom()
    {
        Vector3 position = player.transform.position;
        DistrictRoom current = Plan.AtWorld(position.x, position.y);
        if (current == null)
            return;
        CurrentRoomId = current.Id;
        follow.SetRoomBounds(new Rect(current.WorldX, current.WorldY - 2, 24, 20));
        RoomState here = states[current.Id];
        if (here.Layout != null && !here.InverseRevealed)
            RevealInverse(current, false);
        // El cielo se tiñe mientras se está en una sala inversa.
        bool tint = here.InverseActive;
        if (tint != skyInverted && skyColors != null)
        {
            skyInverted = tint;
            for (int i = 0; i < skyLayers.Count; i++)
                skyLayers[i].color = tint ? skyColors[i] * new Color(.80f, .45f, 1f, 1f) : skyColors[i];
        }
        if (!states[current.Id].Visited)
        {
            states[current.Id].Visited = true;
            VisitedCount++;
            Revision++;
            if (current.Purpose == RoomPurpose.Refuge)
            {
                refugeId = current.Id;
                notify("REFUGIO DESCUBIERTO  |  CURACION Y BALIZA DE REGRESO");
            }
        }
        // Las salas lejanas conservan sus enemigos y cofres mientras están inactivas.
        float halfWidth = cameraView.orthographicSize * cameraView.aspect + DistrictPlan.RoomWidth;
        float halfHeight = cameraView.orthographicSize + DistrictPlan.RoomHeight;
        Vector2 cameraPosition = cameraView.transform.position;
        foreach (DistrictRoom room in Plan.Rooms)
        {
            bool nearby = (room.WorldX + 24 >= cameraPosition.x - halfWidth && room.WorldX <= cameraPosition.x + halfWidth && room.WorldY + 16 >= cameraPosition.y - halfHeight && room.WorldY <= cameraPosition.y + halfHeight) ||
                (Math.Abs(room.Cell.X - current.Cell.X) <= 1 && Math.Abs(room.Cell.Y - current.Cell.Y) <= 1);
            if (states[room.Id].Root.activeSelf != nearby)
                states[room.Id].Root.SetActive(nearby);
        }
    }

    public string InteractionLabel()
    {
        if (Plan == null)
            return "";
        DistrictRoom room = Plan.Rooms[CurrentRoomId];
        RoomState state = states[room.Id];
        if (state.InverseActive && !state.InverseLooted && Near(room, state.Layout.ChestX, state.Layout.ChestTop + .8f))
            return GameControls.Hint(GameAction.Attack, "ROMPE LA MOCHILA A GOLPES");
        if (room.Purpose == RoomPurpose.Exit && Near(room, ExitDoorX, 2))
            return finalDistrict ? GameControls.Hint(GameAction.Interact, "TERMINAR PARTIDA") : GameControls.Hint(GameAction.Interact, "SIGUIENTE ÁREA");
        if (room.Purpose == RoomPurpose.Exit && Near(room, FountainX, 2))
        {
            if (state.Healed)
                return "FUENTE UTILIZADA";
            if (health.currentHealth >= health.maxHealth)
                return "VIDA COMPLETA";
            return GameControls.Hint(GameAction.Interact, "RESTAURAR TODA LA VIDA");
        }
        if (room.Purpose == RoomPurpose.Treasure && !state.Looted && Near(room, 18, 2))
            return state.Enemies == 0 ? GameControls.Hint(GameAction.Attack, "ROMPE LA MOCHILA A GOLPES") : "DERROTA AL GUARDIA DE LA MOCHILA";
        if (room.Purpose == RoomPurpose.Refuge && Near(room, 17, 2))
            return state.Healed ? "REFUGIO YA UTILIZADO" : health.currentHealth >= health.maxHealth ? "VIDA COMPLETA" : GameControls.Hint(GameAction.Interact, "RECUPERAR TODA LA VIDA");
        if ((room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Entrance) && Near(room, 4, 2))
            return room.Id == Plan.EntranceId && refugeId < 0 ? "DESCUBRE EL REFUGIO PARA ACTIVAR LA BALIZA" : GameControls.Hint(GameAction.Interact, "VIAJAR ENTRE ENTRADA Y REFUGIO");
        return "";
    }

    // Devuelve true solo al usar la salida; el resto de interacciones ocurren dentro del mapa.
    public bool TryInteract()
    {
        CheckCurrentRoom();
        DistrictRoom room = Plan.Rooms[CurrentRoomId];
        RoomState state = states[room.Id];
        if (room.Purpose == RoomPurpose.Exit)
        {
            if (Near(room, ExitDoorX, 2))
                return true;
            if (Near(room, FountainX, 2) && !state.Healed && health.currentHealth < health.maxHealth)
            {
                state.Healed = true;
                health.Heal(health.maxHealth);
                if (state.Drink != null) state.Drink.Consume();
                notify("VIDA RESTAURADA");
            }
            return false;
        }
        // Con la vida llena no se gasta: el bubble tea desaparece al tomarlo.
        if (room.Purpose == RoomPurpose.Refuge && !state.Healed && Near(room, 17, 2) && health.currentHealth < health.maxHealth)
        {
            state.Healed = true;
            health.Heal(health.maxHealth);
            if (state.Drink != null) state.Drink.Consume();
            notify("VIDA RESTAURADA");
        }
        else if ((room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Entrance) && Near(room, 4, 2) && refugeId >= 0)
            TravelTo(room.Id == Plan.EntranceId ? refugeId : Plan.EntranceId);
        return false;
    }

    private void TravelTo(int id)
    {
        // Activa primero el destino, para que su suelo exista al mover al jugador.
        states[id].Root.SetActive(true);
        player.Teleport(Spawn(id));
        player.BlockInputForCurrentFrame();
        Physics2D.SyncTransforms();
        follow.SnapToTarget();
        CheckCurrentRoom();
        notify("BALIZA: " + PurposeName(Plan.Rooms[id].Purpose));
    }
    private Vector3 Spawn(int id)
    {
        DistrictRoom room = Plan.Rooms[id];
        return new Vector3(room.WorldX + 3f, room.WorldY + 1.85f, 0);
    }
    private bool Near(DistrictRoom room, float x, float y)
    {
        Vector2 target = new Vector2(room.WorldX + x, room.WorldY + y);
        return Vector2.Distance(player.transform.position, target) < 1.8f;
    }

    private void BuildRoom(DistrictRoom room)
    {
        RoomState state = states[room.Id];
        state.Root = new GameObject("Sala_" + room.Id.ToString("00") + "_" + PurposeName(room.Purpose));
        state.Root.transform.SetParent(transform, false);
        Transform root = state.Root.transform;
        DecorateRoom(room, root);
        int index = 0;
        foreach (RoomSurface surface in room.Surfaces)
        {
            GameObject built = BuildSurface(room, surface, root, index);
            // Las pasarelas propias de la sala (no su armazón) desaparecen al invertirse.
            if (state.Layout != null && index >= room.ShellSurfaces)
                state.NormalOnly.Add(built);
            index++;
        }
        if (state.Layout != null)
            BuildInverse(room, state, root);
        Text("Rotulo_Sala", root, PurposeName(room.Purpose), new Vector2(room.WorldX + 12, room.WorldY + 13.3f), .12f, Color.white);
        if (room.Has(DoorSide.Up))
            Text("Senal_Subir", root, "SUBIR", new Vector2(room.WorldX + 7, room.WorldY + 2.5f), .08f, new Color(1, .8f, .4f));
        if (room.Has(DoorSide.Down))
            Text("Senal_Bajar", root, GameControls.PrimaryLabel(GameAction.MoveDown) + " + " + GameControls.PrimaryLabel(GameAction.Jump), new Vector2(room.WorldX + 12, room.WorldY + 2.6f), .07f, new Color(1, .8f, .4f));
        if (room.Purpose == RoomPurpose.Treasure)
        {
            // La mochila del almacén solo se rompe tras vencer al guardia.
            state.Chest = CreateBackpack("Mochila", root, new Vector2(room.WorldX + 18, room.WorldY + 1f + BackpackHeight / 2f),
                () => state.Enemies == 0, () =>
                {
                    state.Looted = true;
                    Revision++;
                    health.Heal(15);
                    award(2, "MOCHILA: +2 FICHAS Y HASTA 15 DE VIDA");
                });
        }
        if (room.Purpose == RoomPurpose.Refuge)
        {
            GameObject fountain = Art("Puesto_Curacion", root, Resources.Load<Sprite>("DemoArt/fountain"), new Vector2(room.WorldX + 17, room.WorldY + 1.85f), 8);
            Fit(fountain, 1.7f);
            state.Drink = HealingDrink.Attach(fountain);
        }
        if (room.Purpose == RoomPurpose.Entrance || room.Purpose == RoomPurpose.Refuge)
        {
            Box("Baliza", root, new Vector2(room.WorldX + 4, room.WorldY + 1.8f), new Vector2(.5f, 1.6f), new Color(.3f, .8f, 1), 5);
            Text("Baliza_Rotulo", root, "BALIZA", new Vector2(room.WorldX + 4, room.WorldY + 3), .08f, Color.cyan);
        }
        if (room.Purpose == RoomPurpose.Exit)
        {
            Vector2 door = new Vector2(room.WorldX + ExitDoorX, room.WorldY + 2.2f);
            Box("Salida_Estado", root, door, new Vector2(1.6f, 2.4f), KenneyTheme.Mint, 4);
            Box("Salida_Puerta", root, door, new Vector2(1.2f, 2.1f), new Color(.04f, .05f, .1f), 5);
            GameObject fountain = Art("Fuente_ZonaSegura", root, Resources.Load<Sprite>("DemoArt/fountain"),
                new Vector2(room.WorldX + FountainX, room.WorldY + 1.85f), 8);
            Fit(fountain, 1.7f);
            state.Drink = HealingDrink.Attach(fountain);
            Text("Fuente_Rotulo", root, "CURACIÓN", new Vector2(room.WorldX + FountainX, room.WorldY + 3.1f), .08f, KenneyTheme.Mint);
            Text("Salida_Rotulo", root, finalDistrict ? "FIN" : "SIGUIENTE ÁREA", door + new Vector2(0, 1.5f), .08f, KenneyTheme.Mint);
            return; // La salida también es segura al cargar mapas de versiones anteriores.
        }
        foreach (float x in room.EnemyX)
            CreateEnemy(room, x, root);
    }

    private void DecorateRoom(DistrictRoom room, Transform root)
    {
        Color wall = GamePresentation.Hex(rules.Wall, Plan.Style == DistrictStyle.Rooftops ? .86f : .98f);
        Color light = (room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Exit) ? KenneyTheme.Mint : accent;
        Vector2 origin = new Vector2(room.WorldX, room.WorldY);
        Box("Muro_Fondo", root, origin + new Vector2(12, 8), new Vector2(24, 16), wall, -60);
        Box("Zocalo", root, origin + new Vector2(12, 2.1f), new Vector2(24, 2.2f), new Color(.025f, .035f, .06f, .78f), -54);
        for (int i = 0; i < 4; i++)
        {
            float x = 3.5f + i * 5.5f;
            Box("Ventana_" + i, root, origin + new Vector2(x, 9.5f), new Vector2(2.5f, 3.5f), GamePresentation.Hex(rules.Mist, .14f), -50);
            Box("Montante_" + i, root, origin + new Vector2(x, 9.5f), new Vector2(.09f, 3.5f), wall, -49);
            Box("Travesano_" + i, root, origin + new Vector2(x, 9.5f), new Vector2(2.5f, .1f), wall, -49);
            Box("Columna_" + i, root, origin + new Vector2(x + 1.9f, 7.8f), new Vector2(.16f, 12.4f), new Color(.06f, .085f, .13f), -48);
        }
        int[] details = { 49, 50, 53, 54, 57, 58 };
        for (int i = 0; i < 3; i++)
        {
            Sprite tile = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_" + details[(room.Variant + i) % details.Length].ToString("00"));
            if (tile == null)
                continue;
            GameObject panel = Art("Panel_Servicio_" + i, root, tile, origin + new Vector2(4 + i * 7, 5.3f), -43);
            panel.transform.localScale = Vector3.one * (1.4f / tile.bounds.size.x);
            panel.GetComponent<SpriteRenderer>().color = new Color(.29f, .37f, .48f);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 position = origin + new Vector2(i == 0 ? 4 : 20, 12.5f);
            Box("Lampara_Carcasa_" + i, root, position, new Vector2(3, .3f), new Color(.025f, .03f, .045f), -41);
            Box("Lampara_Tubo_" + i, root, position, new Vector2(2.6f, .07f), light, -40);
            Sprite mask = Resources.Load<Sprite>("KenneyUI/Lights/Glow");
            if (mask != null)
            {
                GameObject glow = Art("Luz_Difusa_" + i, root, mask, position - new Vector2(0, .8f), -39);
                glow.transform.localScale = Vector3.one * 5 / mask.bounds.size.x;
                glow.GetComponent<SpriteRenderer>().color = new Color(light.r, light.g, light.b, .10f);
            }
        }
        // Las señales permanecen en la pared, fuera de la silueta del jugador.
        if (room.Has(DoorSide.Right))
            Box("Senal_Derecha", root, origin + new Vector2(22.2f, 3.8f), new Vector2(.5f, .06f), light, -37);
        if (room.Has(DoorSide.Left))
            Box("Senal_Izquierda", root, origin + new Vector2(1.8f, 3.8f), new Vector2(.5f, .06f), light, -37);
        Box("Viga_Superior", root, origin + new Vector2(12, 14.2f), new Vector2(22, .15f), new Color(.07f, .09f, .14f), -45);
    }

    private GameObject BuildSurface(DistrictRoom room, RoomSurface surface, Transform root, int index)
    {
        Vector2 pos = new Vector2(room.WorldX + surface.X + surface.Width / 2, room.WorldY + surface.Bottom + surface.Height / 2);
        GameObject obj = new GameObject((surface.OneWay ? "Pasarela_" : "Bloque_") + index.ToString("00"));
        obj.transform.SetParent(root, false);
        obj.transform.position = pos;
        obj.layer = 8;
        BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(surface.Width, surface.Height);
        if (surface.OneWay)
        {
            collider.usedByEffector = true;
            PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 170;
        }
        GameObject fill = Art("Baldosas", obj.transform, surface.OneWay ? edge : brick, pos, -5);
        SpriteRenderer renderer = fill.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Tiled;
        float scale = 1f / renderer.sprite.bounds.size.x;
        fill.transform.localScale = new Vector3(scale, scale, 1);
        renderer.size = new Vector2(surface.Width / scale, surface.Height / scale);
        renderer.color = new Color(.72f, .79f, .9f);
        if (!surface.OneWay && surface.Height <= 1)
        {
            GameObject rim = Art("Borde", obj.transform, edge, new Vector2(pos.x, room.WorldY + surface.Top - .5f), -4);
            SpriteRenderer sr = rim.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            float edgeScale = 1f / edge.bounds.size.x;
            rim.transform.localScale = new Vector3(edgeScale, edgeScale, 1);
            sr.size = new Vector2(surface.Width / edgeScale, 1f / edgeScale);
            sr.color = Color.Lerp(Color.white, accent, .18f);
        }
        return obj;
    }

    private void CreateEnemy(DistrictRoom room, float localX, Transform parent)
    {
        GameObject obj = Art("Guardia", parent, Resources.Load<Sprite>("DemoArt/dummy"), new Vector2(room.WorldX + localX, room.WorldY + 1.8f), 12);
        Fit(obj, 1.55f);
        // El dibujo tiene escala propia; la fisica mantiene el tamano del personaje.
        GameObject actor = new GameObject(obj.name);
        actor.transform.SetParent(parent, false);
        actor.transform.position = obj.transform.position;
        obj.name = "Aspecto";
        obj.transform.SetParent(actor.transform, true);
        Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
        body.gravityScale = 3.3f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        actor.AddComponent<BoxCollider2D>().size = new Vector2(.8f, 1.5f);
        EnemyHealth enemy = actor.AddComponent<EnemyHealth>();
        enemy.ConfigureHealth(30 + Plan.District * 5);
        EnemyAI ai = actor.AddComponent<EnemyAI>();
        ai.speed = 2.2f;
        ai.damage = 12; // antes 9: cada golpe enemigo que conecta duele más
        ai.ConfigureTerritory(new Rect(room.WorldX + 1, room.WorldY + 1, 22, 14));
        actor.AddComponent<ActorFeedback>();
        enemy.Died += EnemyDefeated;
        enemyOwners.Add(enemy, room.Id);
        enemySlots.Add(enemy, room.EnemyX.IndexOf(localX));
        states[room.Id].Enemies++;
    }

    private void EnemyDefeated(EnemyHealth enemy)
    {
        int roomId;
        if (!enemyOwners.TryGetValue(enemy, out roomId))
            return;
        enemy.Died -= EnemyDefeated;
        enemyOwners.Remove(enemy);
        enemySlots.Remove(enemy);
        RoomState state = states[roomId];
        state.Enemies = Mathf.Max(0, state.Enemies - 1);
        Revision++;
        if (state.Enemies == 0 && !state.Rewarded)
        {
            state.Rewarded = true;
            award(1, "SALA DESPEJADA: +1 FICHA");
        }
    }

    public void Capture(RunSaveData save)
    {
        CheckCurrentRoom();
        save.generator = Plan.Version;
        save.biome = (int)Plan.Style;
        save.fingerprint = Plan.Fingerprint();
        save.refuge = refugeId;
        save.rooms = new RoomSave[states.Length];
        for (int i = 0; i < states.Length; i++)
            save.rooms[i] = new RoomSave
            {
                visited = states[i].Visited, looted = states[i].Looted, healed = states[i].Healed, rewarded = states[i].Rewarded,
                inverted = states[i].InverseRevealed, inverseLooted = states[i].InverseLooted,
                inverseExpired = states[i].InverseExpired, inverseTime = states[i].InverseTimeLeft
            };
        List<EnemySave> live = new List<EnemySave>();
        foreach (KeyValuePair<EnemyHealth, int> pair in enemyOwners)
        {
            EnemyHealth enemy = pair.Key;
            if (enemy == null || enemy.CurrentHealth <= 0)
                continue;
            live.Add(new EnemySave { room = pair.Value, slot = enemySlots[enemy], health = enemy.CurrentHealth, x = enemy.transform.position.x, y = enemy.transform.position.y });
        }
        save.enemies = live.ToArray();
    }

    // Llamar después de validar la partida y de colocar al jugador.
    public void Restore(RunSaveData save)
    {
        refugeId = save.refuge;
        VisitedCount = 0;
        for (int i = 0; i < states.Length; i++)
        {
            RoomSave saved = save.rooms[i];
            RoomState state = states[i];
            state.Visited = saved.visited;
            state.Looted = saved.looted;
            state.Healed = saved.healed;
            state.Rewarded = saved.rewarded;
            state.Enemies = 0;
            state.InverseLooted = state.Layout != null && saved.inverseLooted;
            if (state.Layout != null)
                // Las partidas guardadas antes de existir la cuenta (tiempo 0) empiezan con la cuenta completa.
                state.InverseTimeLeft = saved.inverseTime > 0f ? Mathf.Min(saved.inverseTime, state.Layout.Duration) : state.Layout.Duration;
            if (state.Visited)
                VisitedCount++;
            if (state.Chest != null && state.Looted)
                state.Chest.GetComponent<BreakableBackpack>().SetBroken();
            if (state.Drink != null && state.Healed)
                state.Drink.SetConsumed();
        }
        Dictionary<int, EnemySave> live = new Dictionary<int, EnemySave>();
        foreach (EnemySave enemy in save.enemies)
            live.Add(enemy.room * 10 + enemy.slot, enemy);
        foreach (KeyValuePair<EnemyHealth, int> pair in new List<KeyValuePair<EnemyHealth, int>>(enemyOwners))
        {
            EnemyHealth enemy = pair.Key;
            EnemySave saved;
            if (!live.TryGetValue(pair.Value * 10 + enemySlots[enemy], out saved))
            {
                // No llama TakeDamage: cargar no concede otra recompensa.
                enemy.Died -= EnemyDefeated;
                enemyOwners.Remove(enemy);
                enemySlots.Remove(enemy);
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
                continue;
            }
            enemy.RestoreHealth(saved.health);
            enemy.transform.position = new Vector3(saved.x, saved.y, 0);
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = new Vector2(saved.x, saved.y);
                body.linearVelocity = Vector2.zero;
            }
            states[pair.Value].Enemies++;
        }
        // Las salas que ya estaban invertidas vuelven así, sin aviso. Las que agotaron su tiempo
        // siguen normales y conservan los guardias guardados.
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Layout == null || !save.rooms[i].inverted || states[i].InverseRevealed)
                continue;
            if (save.rooms[i].inverseExpired)
            {
                states[i].InverseRevealed = states[i].InverseExpired = true;
                states[i].InverseTimeLeft = 0f;
            }
            else
                RevealInverse(Plan.Rooms[i], true);
        }
        CheckCurrentRoom();
        Physics2D.SyncTransforms();
        follow.SnapToTarget();
        Revision++;
    }

    private void CreatePixel()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
    }
    private void CreateChestFrame()
    {
        backpack = Resources.Load<Sprite>("DemoArt/backpack");
        Sprite sheet = Resources.Load<Sprite>("UrbanArt/4 Animated objects/Chest");
        if (sheet == null)
            return;
        chestFrame = Sprite.Create(sheet.texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
        ownedSprites.Add(chestFrame);
    }
    private Material MaterialFor(Sprite sprite)
    {
        if (sprite == null)
            return null;
        Material material;
        if (materials.TryGetValue(sprite.texture, out material))
            return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        material = new Material(shader);
        material.SetTexture("_MainTex", sprite.texture);
        materials.Add(sprite.texture, material);
        return material;
    }
    private GameObject Art(string name, Transform parent, Sprite sprite, Vector2 position, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = MaterialFor(sprite);
        return obj;
    }
    private GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
    {
        GameObject obj = Art(name, parent, pixel, pos, order);
        obj.transform.localScale = new Vector3(size.x, size.y, 1);
        obj.GetComponent<SpriteRenderer>().color = color;
        return obj;
    }
    private static void Fit(GameObject obj, float height)
    {
        Sprite sprite = obj.GetComponent<SpriteRenderer>().sprite;
        if (sprite != null)
            obj.transform.localScale = Vector3.one * height / sprite.bounds.size.y;
    }
    private static void Text(string name, Transform parent, string value, Vector2 position, float size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 40;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        obj.GetComponent<MeshRenderer>().sortingOrder = 25;
    }

    private void CreateBackdrop()
    {
        backdrop = new GameObject("Fondo_Ciudad").transform;
        backdrop.SetParent(transform, false);
        string path = "UrbanArt/Pixel Art - City Landscape V2/";
        foreach (string name in new[] { "Sky", "Buildings 1", "Buildings 2", "Buildings 3" })
        {
            Sprite sprite = Resources.Load<Sprite>(path + name);
            GameObject obj = Art(name, backdrop, sprite, Vector2.zero, -100 + skyLayers.Count * 5);
            obj.GetComponent<SpriteRenderer>().color = skyLayers.Count == 0 ? new Color(.31f, .34f, .45f) : new Color(.49f, .58f, .73f, .8f);
            skyLayers.Add(obj.GetComponent<SpriteRenderer>());
        }
    }
    private void LateUpdate()
    {
        if (cameraView == null || skyLayers.Count == 0)
            return;
        float halfY = cameraView.orthographicSize, halfX = halfY * cameraView.aspect;
        // Escala y tamaño solo cambian si cambia la cámara (zoom o ventana). Cambiar el tamaño
        // de un sprite en modo Tiled regenera su malla, por eso no se hace en cada frame.
        bool resized = halfX != fittedHalfX || halfY != fittedHalfY;
        fittedHalfX = halfX;
        fittedHalfY = halfY;
        Vector3 cameraPosition = cameraView.transform.position;
        SpriteRenderer sky = skyLayers[0];
        if (sky.sprite != null)
        {
            sky.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0);
            if (resized)
            {
                Vector3 skySize = sky.sprite.bounds.size;
                sky.transform.localScale = Vector3.one * Mathf.Max((halfX * 2 + 2) / skySize.x, (halfY * 2 + 2) / skySize.y);
            }
        }
        // Tres tiras repetidas cubren cualquier ancho visible; no hay un limite de dos copias.
        for (int i = 1; i < skyLayers.Count; i++)
        {
            SpriteRenderer renderer = skyLayers[i];
            if (renderer.sprite == null)
                continue;
            float scale = 1f / (32f / renderer.sprite.pixelsPerUnit);
            Vector3 spriteSize = renderer.sprite.bounds.size;
            float width = spriteSize.x * scale;
            float x = cameraPosition.x * (.9f - i * .2f);
            x += Mathf.Round((cameraPosition.x - x) / width) * width;
            if (resized)
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.transform.localScale = new Vector3(scale, scale, 1);
                renderer.size = new Vector2((halfX * 2 + width * 2) / scale, spriteSize.y);
            }
            renderer.transform.position = new Vector3(x, cameraPosition.y - halfY + spriteSize.y * scale / 2, 0);
        }
    }
    private float fittedHalfX = float.NaN, fittedHalfY = float.NaN;
    private void OnDestroy()
    {
        foreach (KeyValuePair<EnemyHealth, int> pair in enemyOwners)
            if (pair.Key != null)
                pair.Key.Died -= EnemyDefeated;
        foreach (Material material in materials.Values)
            if (material != null)
                Destroy(material);
        foreach (Sprite sprite in ownedSprites)
            if (sprite != null)
                Destroy(sprite);
        if (pixel != null)
        {
            Destroy(pixel.texture);
            Destroy(pixel);
        }
    }
}
