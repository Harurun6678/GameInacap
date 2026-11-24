using UnityEngine;

// Anima a la protagonista con las hojas de Resources/PlayerAnim: idle, caminar, correr, saltar,
// atacar (Regla Neón) y cortadores (Twin Cutters).
// Cada hoja es una tira horizontal de 4 celdas iguales; los pies quedan en (PivotX, PivotY) de cada celda.
// Si falta alguna hoja, el jugador conserva su sprite anterior.
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class PlayerAnimator : MonoBehaviour
{
    private const int CellWidth = 152, CellHeight = 115, PivotX = 76, PivotY = 2;
    // 110 px de alto de pie equivalen a 1,8 unidades (el collider mide 1,62).
    private const float StandHeightPx = 110f, StandHeightUnits = 1.8f;
    private const float WalkToRunSpeed = 4.6f;
    // Reparto del ataque: preparación, carga, golpe y remate (fracciones acumuladas).
    private static readonly float[] AttackSplit = { .14f, .32f, .62f, 1f };

    private PlayerController player;
    private Rigidbody2D body;
    private SpriteRenderer drawing;
    private Sprite[] idle, walk, run, jump, attack, cutters;
    private Sprite[] current;
    private float phase, airStart, landUntil;
    private bool wasGrounded = true;

    public bool Ready { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody2D>();
        drawing = GetComponent<SpriteRenderer>();
        if (drawing == null) drawing = GetComponentInChildren<SpriteRenderer>();
        if (player == null || body == null || drawing == null) { enabled = false; return; }

        // Los pies del dibujo coinciden con la base del collider.
        float feetY = -.81f;
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null) feetY = box.offset.y - box.size.y * .5f;
        float ppu = StandHeightPx / StandHeightUnits;
        Vector2 pivot = new Vector2(PivotX / (float)CellWidth, (PivotY - feetY * ppu) / CellHeight);

        idle = Load("idle", pivot, ppu);
        walk = Load("caminar", pivot, ppu);
        run = Load("correr", pivot, ppu);
        jump = Load("saltar", pivot, ppu);
        attack = Load("atacar", pivot, ppu);
        cutters = Load("cortadores", pivot, ppu);
        Ready = idle != null && walk != null && run != null && jump != null;
        if (!Ready)
        {
            Debug.LogWarning("PlayerAnimator: faltan hojas en Resources/PlayerAnim; se mantiene el sprite anterior.");
            enabled = false;
            return;
        }
        drawing.sprite = idle[0];
        drawing.drawMode = SpriteDrawMode.Simple;
        player.SetBodyWeapons(attack != null, cutters != null);
    }

    private static Sprite[] Load(string sheet, Vector2 pivot, float ppu)
    {
        Texture2D texture = Resources.Load<Texture2D>("PlayerAnim/" + sheet);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Point;
        int count = texture.width / CellWidth;
        if (count < 1 || texture.height < CellHeight) return null;
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Sprite.Create(texture, new Rect(i * CellWidth, 0, CellWidth, CellHeight), pivot, ppu, 0, SpriteMeshType.FullRect);
            frames[i].name = sheet + "_" + i;
        }
        return frames;
    }

    private void LateUpdate()
    {
        float now = Time.time;
        bool grounded = player.IsGrounded;
        if (!grounded && wasGrounded) airStart = now;
        if (grounded && !wasGrounded && now - airStart > .14f) landUntil = now + .09f;
        wasGrounded = grounded;

        WeaponData weapon = player.CurrentWeapon;
        float sinceAttack = now - player.LastAttackTime;
        float attackLength = weapon != null ? Mathf.Min(.32f, .95f / Mathf.Max(.1f, weapon.attackRate)) : .3f;
        float vy = body.linearVelocity.y;
        float speed = Mathf.Abs(player.MoveSpeedX);

        Sprite[] strike = weapon == null ? null : weapon.type == WeaponType.Ruler ? attack : weapon.type == WeaponType.Dagger ? cutters : null;
        if (strike != null && sinceAttack < attackLength)
            Show(strike, AttackFrame(sinceAttack / attackLength));
        else if (player.IsDashing)
            Show(run, 0);
        else if (!grounded)
            // Despegue breve, subida y punto alto/caída (piernas recogidas).
            Show(jump, vy > 0f && now - airStart < .07f ? 0 : vy > 2.5f ? 1 : 2);
        else if (now < landUntil)
            Show(jump, Mathf.Min(3, jump.Length - 1));
        else if (speed < .35f)
            Cycle(idle, 5f);
        else if (speed < WalkToRunSpeed)
            Cycle(walk, Mathf.Lerp(6f, 10f, speed / WalkToRunSpeed));
        else
            Cycle(run, Mathf.Clamp(13f * speed / 8f, 10f, 20f));
    }

    private static int AttackFrame(float t)
    {
        for (int i = 0; i < AttackSplit.Length; i++)
            if (t < AttackSplit[i]) return i;
        return AttackSplit.Length - 1;
    }

    private void Show(Sprite[] frames, int index)
    {
        if (current != frames) { current = frames; phase = 0f; }
        drawing.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    private void Cycle(Sprite[] frames, float fps)
    {
        if (current != frames) { current = frames; phase = 0f; }
        else phase = (phase + Time.deltaTime * fps) % frames.Length;
        drawing.sprite = frames[(int)phase % frames.Length];
    }
}
