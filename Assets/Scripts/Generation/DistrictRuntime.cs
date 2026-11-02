using System;
using System.Collections.Generic;
using UnityEngine;
using GyaruRogue.Generation;
using GyaruRogue.Persistence;

// Construye y mantiene un distrito completo; pasar de sala no destruye lo explorado.
[DefaultExecutionOrder(220)]
public class DistrictRuntime : MonoBehaviour
{
    private sealed class RoomState
    {
        public GameObject Root, Chest;
        public bool Visited, Looted, Healed, Rewarded;
        public int Enemies;
    }
    public DistrictPlan Plan { get; private set; }
    public int CurrentRoomId { get; private set; }
    public int Revision { get; private set; }
    public int VisitedCount { get; private set; }
    public bool ExitReady { get { return states != null && states[Plan.ExitId].Enemies == 0; } }
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
    private GameObject exitGlow;
    private Sprite pixel, chestFrame;
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
    private readonly Dictionary<EnemyHealth, int> enemyOwners = new Dictionary<EnemyHealth, int>();
    private readonly Dictionary<EnemyHealth,int> enemySlots = new Dictionary<EnemyHealth,int>();
    private readonly List<SpriteRenderer> skyLayers = new List<SpriteRenderer>();
    private Transform backdrop;
    private Sprite edge, brick;

    public void Initialize(PlayerController actor, PlayerHealth hp, Camera view, CameraFollow tracking,
        int seed, int stage, int columns, int branches, Action<int, string> reward, Action<string> toast)
    {
        player = actor; health = hp; cameraView = view; follow = tracking; award = reward; notify = toast;
        Plan = DistrictGenerator.Generate(seed, stage, columns, branches);
        states = new RoomState[Plan.Rooms.Count];
        CurrentRoomId = Plan.EntranceId;
        edge = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_02");
        brick = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tile_37");
        if (edge == null || brick == null) throw new InvalidOperationException("Falta Tile_02 o Tile_37 del pack urbano.");
        CreatePixel(); CreateChestFrame(); CreateBackdrop();
        minWorld = new Vector2(float.MaxValue, float.MaxValue);
        maxWorld = new Vector2(float.MinValue, float.MinValue);
        foreach (DistrictRoom room in Plan.Rooms)
        {
            minWorld = Vector2.Min(minWorld, new Vector2(room.WorldX, room.WorldY));
            maxWorld = Vector2.Max(maxWorld, new Vector2(room.WorldX + 24, room.WorldY + 16));
            states[room.Id] = new RoomState(); BuildRoom(room);
        }
        follow.SetWorldBounds(new Rect(minWorld.x, minWorld.y, maxWorld.x - minWorld.x, maxWorld.y - minWorld.y));
        Physics2D.SyncTransforms();
        player.Teleport(Spawn(Plan.EntranceId));
        player.BlockInputForCurrentFrame();
        follow.SnapToTarget();
        CheckCurrentRoom();
        notify("DISTRITO " + Plan.District + "  |  M: MAPA  |  SEMILLA " + Plan.Seed);
        Debug.Log("Distrito " + Plan.District + ": " + Plan.Rooms.Count + " salas, semilla " + Plan.Seed + ", codigo " + Plan.Fingerprint(), this);
    }

    public bool IsVisited(int id) { return states[id].Visited; }
    public bool IsCleared(int id) { return states[id].Enemies == 0; }
    public bool IsLooted(int id) { return states[id].Looted; }
    public bool IsRevealed(int id)
    {
        if (states[id].Visited) return true;
        foreach (int neighbor in Plan.Rooms[id].Neighbors) if (states[neighbor].Visited) return true;
        return false;
    }
    public static string PurposeName(RoomPurpose purpose)
    {
        switch (purpose)
        {
            case RoomPurpose.Entrance: return "ENTRADA";
            case RoomPurpose.Refuge: return "REFUGIO";
            case RoomPurpose.Treasure: return "ALMACEN";
            case RoomPurpose.Exit: return "SALIDA";
            default: return "COMBATE";
        }
    }

    private void Update()
    {
        if (Plan == null || Time.timeScale <= 0 || health.IsDead) return;
        if (Time.time >= nextRoomCheck)
        {
            nextRoomCheck = Time.time + .1f;
            CheckCurrentRoom();
        }
        if (player.transform.position.y < minWorld.y - 4 || player.transform.position.x < minWorld.x - 4 || player.transform.position.x > maxWorld.x + 4)
        {
            health.TakeDamage(10);
            if (!health.IsDead) TravelTo(refugeId >= 0 ? refugeId : Plan.EntranceId);
        }
    }

    private void CheckCurrentRoom()
    {
        Vector3 position = player.transform.position;
        DistrictRoom current = Plan.AtWorld(position.x, position.y);
        if (current == null) return;
        CurrentRoomId = current.Id;
        if (!states[current.Id].Visited)
        {
            states[current.Id].Visited = true; VisitedCount++; Revision++;
            if (current.Purpose == RoomPurpose.Refuge)
            {
                refugeId = current.Id;
                notify("REFUGIO DESCUBIERTO  |  CURACION Y BALIZA DE REGRESO");
            }
        }
        foreach (DistrictRoom room in Plan.Rooms)
        {
            // Mantiene cargadas las salas cercanas; las lejanas conservan enemigos y cofres.
            bool nearby = Math.Abs(room.Cell.X - current.Cell.X) <= 2 && Math.Abs(room.Cell.Y - current.Cell.Y) <= 2;
            if (states[room.Id].Root.activeSelf != nearby) states[room.Id].Root.SetActive(nearby);
        }
    }

    public string InteractionLabel()
    {
        if (Plan == null) return "";
        DistrictRoom room = Plan.Rooms[CurrentRoomId]; RoomState state = states[room.Id];
        if (room.Purpose == RoomPurpose.Exit && Near(room, 20, 2))
            return ExitReady ? "[ E ] SALIR DEL DISTRITO" : "DERROTA A LOS DOS GUARDIANES DE LA SALIDA";
        if (room.Purpose == RoomPurpose.Treasure && !state.Looted && Near(room, 18, 2))
            return state.Enemies == 0 ? "[ E ] ABRIR COFRE" : "DERROTA AL GUARDIA DEL COFRE";
        if (room.Purpose == RoomPurpose.Refuge && Near(room, 17, 2))
            return state.Healed ? "REFUGIO YA UTILIZADO" : "[ E ] RECUPERAR TODA LA VIDA";
        if ((room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Entrance) && Near(room, 4, 2))
            return room.Id == Plan.EntranceId && refugeId < 0 ? "DESCUBRE EL REFUGIO PARA ACTIVAR LA BALIZA" : "[ E ] VIAJAR ENTRE ENTRADA Y REFUGIO";
        return "";
    }

    // Devuelve true solo al usar la salida; el resto de interacciones ocurren dentro del mapa.
    public bool TryInteract()
    {
        CheckCurrentRoom();
        DistrictRoom room = Plan.Rooms[CurrentRoomId]; RoomState state = states[room.Id];
        if (room.Purpose == RoomPurpose.Exit && Near(room, 20, 2))
        {
            if (!ExitReady) notify("LOS GUARDIANES PROTEGEN ESTA SALIDA");
            return ExitReady;
        }
        if (room.Purpose == RoomPurpose.Treasure && !state.Looted && Near(room, 18, 2))
        {
            if (state.Enemies > 0) { notify("PRIMERO DERROTA AL GUARDIA"); return false; }
            state.Looted = true; Revision++;
            if (state.Chest != null) state.Chest.GetComponent<SpriteRenderer>().color = new Color(.4f, .4f, .5f);
            health.Heal(15); award(2, "COFRE: +2 FICHAS Y HASTA 15 DE VIDA");
        }
        else if (room.Purpose == RoomPurpose.Refuge && !state.Healed && Near(room, 17, 2))
        {
            state.Healed = true; health.Heal(health.maxHealth); notify("VIDA RESTAURADA");
        }
        else if ((room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Entrance) && Near(room, 4, 2) && refugeId >= 0)
            TravelTo(room.Id == Plan.EntranceId ? refugeId : Plan.EntranceId);
        return false;
    }

    private void TravelTo(int id)
    {
        // Activa primero el destino, para que su suelo exista al mover al jugador.
        states[id].Root.SetActive(true);
        player.Teleport(Spawn(id)); player.BlockInputForCurrentFrame();
        Physics2D.SyncTransforms(); follow.SnapToTarget(); CheckCurrentRoom();
        notify("BALIZA: " + PurposeName(Plan.Rooms[id].Purpose));
    }
    private Vector3 Spawn(int id)
    {
        DistrictRoom room = Plan.Rooms[id]; return new Vector3(room.WorldX + 3f, room.WorldY + 1.85f, 0);
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
        Color tint = room.Purpose == RoomPurpose.Refuge ? new Color(.05f,.14f,.18f,.94f) : new Color(.055f,.07f,.13f,.92f);
        Box("Muro_Fondo", root, new Vector2(room.WorldX + 12, room.WorldY + 8), new Vector2(24,16), tint, -55);
        for (int i = 0; i < 5; i++)
        {
            Box("Ventana_" + i, root, new Vector2(room.WorldX + 3 + i * 4, room.WorldY + 10), new Vector2(1.7f,2.2f), new Color(.34f,.43f,.58f,.25f), -50);
            Box("Marco_" + i, root, new Vector2(room.WorldX + 3 + i * 4, room.WorldY + 10), new Vector2(.08f,2.2f), tint, -49);
        }
        int index = 0;
        foreach (RoomSurface surface in room.Surfaces) BuildSurface(room, surface, root, index++);
        Text("Rotulo_Sala", root, PurposeName(room.Purpose), new Vector2(room.WorldX + 12, room.WorldY + 13.3f), .12f, Color.white);
        if (room.Has(DoorSide.Up)) Text("Senal_Subir", root, "SUBIR", new Vector2(room.WorldX + 7, room.WorldY + 2.5f), .08f, new Color(1,.8f,.4f));
        if (room.Has(DoorSide.Down)) Text("Senal_Bajar", root, "S + ESPACIO", new Vector2(room.WorldX + 12, room.WorldY + 2.6f), .07f, new Color(1,.8f,.4f));
        if (room.Purpose == RoomPurpose.Treasure)
        {
            state.Chest = Art("Cofre", root, chestFrame, new Vector2(room.WorldX + 18, room.WorldY + 1.6f), 8);
            if (chestFrame != null) state.Chest.transform.localScale = Vector3.one * (1.2f / chestFrame.bounds.size.y);
        }
        if (room.Purpose == RoomPurpose.Refuge)
        {
            GameObject fountain = Art("Puesto_Curacion", root, Resources.Load<Sprite>("DemoArt/fountain"), new Vector2(room.WorldX + 17, room.WorldY + 1.85f), 8);
            Fit(fountain, 1.7f);
        }
        if (room.Purpose == RoomPurpose.Entrance || room.Purpose == RoomPurpose.Refuge)
        {
            Box("Baliza", root, new Vector2(room.WorldX + 4, room.WorldY + 1.8f), new Vector2(.5f,1.6f), new Color(.3f,.8f,1), 5);
            Text("Baliza_Rotulo", root, "BALIZA", new Vector2(room.WorldX + 4, room.WorldY + 3), .08f, Color.cyan);
        }
        if (room.Purpose == RoomPurpose.Exit)
        {
            exitGlow = Box("Salida_Estado", root, new Vector2(room.WorldX + 20, room.WorldY + 2.2f), new Vector2(1.6f,2.4f), new Color(.85f,.3f,.35f), 4);
            Box("Salida_Puerta", root, new Vector2(room.WorldX + 20, room.WorldY + 2.2f), new Vector2(1.2f,2.1f), new Color(.04f,.05f,.1f), 5);
        }
        foreach (float x in room.EnemyX) CreateEnemy(room, x, root);
    }

    private void BuildSurface(DistrictRoom room, RoomSurface surface, Transform root, int index)
    {
        Vector2 pos = new Vector2(room.WorldX + surface.X + surface.Width / 2, room.WorldY + surface.Bottom + surface.Height / 2);
        GameObject obj = new GameObject((surface.OneWay ? "Pasarela_" : "Bloque_") + index.ToString("00"));
        obj.transform.SetParent(root, false); obj.transform.position = pos; obj.layer = 8;
        BoxCollider2D collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(surface.Width, surface.Height);
        if (surface.OneWay)
        {
            collider.usedByEffector = true;
            PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 170;
        }
        GameObject fill = Art("Baldosas", obj.transform, surface.OneWay ? edge : brick, pos, -5);
        SpriteRenderer renderer = fill.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Tiled;
        float scale = 1f / renderer.sprite.bounds.size.x;
        fill.transform.localScale = new Vector3(scale,scale,1);
        renderer.size = new Vector2(surface.Width / scale, surface.Height / scale);
        if (!surface.OneWay && surface.Height <= 1)
        {
            GameObject rim = Art("Borde", obj.transform, edge, new Vector2(pos.x, room.WorldY + surface.Top - .5f), -4);
            SpriteRenderer sr = rim.GetComponent<SpriteRenderer>(); sr.drawMode = SpriteDrawMode.Tiled;
            float edgeScale = 1f / edge.bounds.size.x; rim.transform.localScale = new Vector3(edgeScale,edgeScale,1);
            sr.size = new Vector2(surface.Width / edgeScale, 1f / edgeScale);
        }
    }

    private void CreateEnemy(DistrictRoom room, float localX, Transform parent)
    {
        bool guardian = room.Purpose == RoomPurpose.Exit;
        GameObject obj = Art(guardian ? "Guardian_Salida" : "Guardia", parent, Resources.Load<Sprite>("DemoArt/dummy"), new Vector2(room.WorldX + localX, room.WorldY + 1.8f), 12);
        Fit(obj, 1.55f);
        // El dibujo tiene escala propia; la fisica mantiene el tamano del personaje.
        SpriteRenderer drawing = obj.GetComponent<SpriteRenderer>();
        GameObject actor = new GameObject(obj.name); actor.transform.SetParent(parent, false); actor.transform.position = obj.transform.position;
        obj.name = "Aspecto"; obj.transform.SetParent(actor.transform, true);
        drawing.color = guardian ? new Color(1,.55f,.55f) : Color.white;
        Rigidbody2D body = actor.AddComponent<Rigidbody2D>(); body.gravityScale = 3.3f; body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        actor.AddComponent<BoxCollider2D>().size = new Vector2(.8f,1.5f);
        EnemyHealth enemy = actor.AddComponent<EnemyHealth>(); enemy.ConfigureHealth((guardian ? 65 : 30) + Plan.District * 5);
        EnemyAI ai = actor.AddComponent<EnemyAI>(); ai.speed = guardian ? 2.8f : 2.2f; ai.damage = guardian ? 13 : 9;
        ai.ConfigureTerritory(new Rect(room.WorldX + 1, room.WorldY + 1, 22, 14));
        enemy.Died += EnemyDefeated; enemyOwners.Add(enemy, room.Id); enemySlots.Add(enemy, room.EnemyX.IndexOf(localX)); states[room.Id].Enemies++;
    }

    private void EnemyDefeated(EnemyHealth enemy)
    {
        int roomId;
        if (!enemyOwners.TryGetValue(enemy, out roomId)) return;
        enemy.Died -= EnemyDefeated; enemyOwners.Remove(enemy); enemySlots.Remove(enemy);
        RoomState state = states[roomId]; state.Enemies = Mathf.Max(0,state.Enemies - 1); Revision++;
        if (state.Enemies == 0 && !state.Rewarded)
        {
            state.Rewarded = true; award(1,"SALA DESPEJADA: +1 FICHA");
            if (roomId == Plan.ExitId)
            {
                if (exitGlow != null) exitGlow.GetComponent<SpriteRenderer>().color = new Color(.35f,1,.6f);
                notify("GUARDIANES DERROTADOS: SALIDA ABIERTA");
            }
        }
    }

    public void Capture(RunSaveData save)
    {
        CheckCurrentRoom();
        save.fingerprint = Plan.Fingerprint();
        save.refuge = refugeId;
        save.rooms = new RoomSave[states.Length];
        for (int i=0;i<states.Length;i++)
            save.rooms[i] = new RoomSave { visited=states[i].Visited, looted=states[i].Looted, healed=states[i].Healed, rewarded=states[i].Rewarded };
        List<EnemySave> live = new List<EnemySave>();
        foreach (KeyValuePair<EnemyHealth,int> pair in enemyOwners)
        {
            EnemyHealth enemy = pair.Key;
            if (enemy == null || enemy.CurrentHealth <= 0) continue;
            live.Add(new EnemySave { room=pair.Value, slot=enemySlots[enemy], health=enemy.CurrentHealth, x=enemy.transform.position.x, y=enemy.transform.position.y });
        }
        save.enemies = live.ToArray();
    }

    // Llamar después de validar la partida y de colocar al jugador.
    public void Restore(RunSaveData save)
    {
        refugeId = save.refuge; VisitedCount = 0;
        for (int i=0;i<states.Length;i++)
        {
            RoomSave saved = save.rooms[i]; RoomState state = states[i];
            state.Visited=saved.visited; state.Looted=saved.looted; state.Healed=saved.healed; state.Rewarded=saved.rewarded; state.Enemies=0;
            if (state.Visited) VisitedCount++;
            if (state.Chest != null && state.Looted) state.Chest.GetComponent<SpriteRenderer>().color = new Color(.4f,.4f,.5f);
        }
        Dictionary<int,EnemySave> live = new Dictionary<int,EnemySave>();
        foreach (EnemySave enemy in save.enemies) live.Add(enemy.room*10+enemy.slot,enemy);
        foreach (KeyValuePair<EnemyHealth,int> pair in new List<KeyValuePair<EnemyHealth,int>>(enemyOwners))
        {
            EnemyHealth enemy = pair.Key; EnemySave saved;
            if (!live.TryGetValue(pair.Value*10+enemySlots[enemy],out saved))
            {
                // No llama TakeDamage: cargar no concede otra recompensa.
                enemy.Died -= EnemyDefeated; enemyOwners.Remove(enemy); enemySlots.Remove(enemy);
                enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); continue;
            }
            enemy.RestoreHealth(saved.health);
            enemy.transform.position = new Vector3(saved.x,saved.y,0);
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            if (body != null) { body.position=new Vector2(saved.x,saved.y); body.linearVelocity=Vector2.zero; }
            states[pair.Value].Enemies++;
        }
        if (exitGlow != null && ExitReady) exitGlow.GetComponent<SpriteRenderer>().color = new Color(.35f,1,.6f);
        CheckCurrentRoom(); Physics2D.SyncTransforms(); follow.SnapToTarget(); Revision++;
    }

    private void CreatePixel()
    {
        Texture2D texture = new Texture2D(1,1); texture.SetPixel(0,0,Color.white); texture.Apply();
        pixel = Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
    }
    private void CreateChestFrame()
    {
        Sprite sheet = Resources.Load<Sprite>("UrbanArt/4 Animated objects/Chest");
        if (sheet == null) return;
        chestFrame = Sprite.Create(sheet.texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
        ownedSprites.Add(chestFrame);
    }
    private Material MaterialFor(Sprite sprite)
    {
        if (sprite == null) return null;
        Material material;
        if (materials.TryGetValue(sprite.texture,out material)) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader); material.SetTexture("_MainTex",sprite.texture);
        materials.Add(sprite.texture, material); return material;
    }
    private GameObject Art(string name, Transform parent, Sprite sprite, Vector2 position, int order)
    {
        GameObject obj = new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.position = position;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
        renderer.sharedMaterial = MaterialFor(sprite); return obj;
    }
    private GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
    {
        GameObject obj = Art(name,parent,pixel,pos,order); obj.transform.localScale = new Vector3(size.x,size.y,1);
        obj.GetComponent<SpriteRenderer>().color = color; return obj;
    }
    private static void Fit(GameObject obj, float height)
    {
        Sprite sprite = obj.GetComponent<SpriteRenderer>().sprite;
        if (sprite != null) obj.transform.localScale = Vector3.one * height / sprite.bounds.size.y;
    }
    private static void Text(string name, Transform parent, string value, Vector2 position, float size, Color color)
    {
        GameObject obj = new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.position = position;
        TextMesh text = obj.AddComponent<TextMesh>(); text.text = value; text.fontSize = 40; text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
        obj.GetComponent<MeshRenderer>().sortingOrder = 25;
    }

    private void CreateBackdrop()
    {
        backdrop = new GameObject("Fondo_Ciudad").transform; backdrop.SetParent(transform,false);
        string path = "UrbanArt/Pixel Art - City Landscape V2/";
        foreach (string name in new [] { "Sky", "Buildings 1", "Buildings 2", "Buildings 3" })
        {
            Sprite sprite = Resources.Load<Sprite>(path + name);
            GameObject obj = Art(name,backdrop,sprite,Vector2.zero,-100 + skyLayers.Count * 5);
            skyLayers.Add(obj.GetComponent<SpriteRenderer>());
        }
    }
    private void LateUpdate()
    {
        if (cameraView == null || skyLayers.Count == 0) return;
        float halfY = cameraView.orthographicSize, halfX = halfY * cameraView.aspect;
        SpriteRenderer sky = skyLayers[0];
        if (sky.sprite != null)
        {
            float scale = Mathf.Max((halfX * 2 + 2) / sky.sprite.bounds.size.x,(halfY * 2 + 2) / sky.sprite.bounds.size.y);
            sky.transform.position = new Vector3(cameraView.transform.position.x,cameraView.transform.position.y,0);
            sky.transform.localScale = Vector3.one * scale;
        }
        // Tres tiras repetidas cubren cualquier ancho visible; no hay un limite de dos copias.
        for (int i = 1; i < skyLayers.Count; i++)
        {
            SpriteRenderer renderer = skyLayers[i]; if (renderer.sprite == null) continue;
            renderer.drawMode = SpriteDrawMode.Tiled;
            float scale = 1f / (32f / renderer.sprite.pixelsPerUnit);
            float width = renderer.sprite.bounds.size.x * scale;
            float x = cameraView.transform.position.x * (.9f - i * .2f);
            x += Mathf.Round((cameraView.transform.position.x - x) / width) * width;
            renderer.transform.localScale = new Vector3(scale,scale,1);
            renderer.size = new Vector2((halfX * 2 + width * 2) / scale,renderer.sprite.bounds.size.y);
            renderer.transform.position = new Vector3(x,cameraView.transform.position.y - halfY + renderer.sprite.bounds.size.y * scale / 2,0);
        }
    }
    private void OnDestroy()
    {
        foreach (KeyValuePair<EnemyHealth,int> pair in enemyOwners) if (pair.Key != null) pair.Key.Died -= EnemyDefeated;
        foreach (Material material in materials.Values) if (material != null) Destroy(material);
        foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
        if (pixel != null) { Destroy(pixel.texture); Destroy(pixel); }
    }
}
