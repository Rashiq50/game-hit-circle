using System.Numerics;
using Raylib_cs;

class Player
{
    const int SizeX = 30;
    const int SizeY = 40;
    const float Speed = 300f;
    const float BoostMultiplier = 2.5f;
    const float DrawSize = 96f;
    const float AnimFps = 10f;
    // Both attacks play the same axe strip; the speed is what makes one feel quick and the other committed.
    const float LightAttackFps = 30f; // fast swing
    const float HeavyAttackFps = 15f; // slow swing
    const float LightDamage = 30;
    const float HeavyDamage = 55;
    const int AttackImpactFrame = 3;
    /// Runtime cheat (F4 in-game): full health and ultimate, and no damage taken.
    public static bool GodMode { get; private set; }
    // 
    private bool IsUltimate = false;
    private int UltimateKills = 0;
    private int UltimateKillLimit = 1;
    static readonly float PlayerHealth = 100;
    public float PlayerCurrentHp = PlayerHealth;
    public float PlayerUlti;
    public float HealthFraction => Math.Clamp(PlayerCurrentHp / PlayerHealth, 0f, 1f);
    public float PowerFraction => Math.Clamp(PlayerUlti / 100, 0f, 1f);

    const int LightAttackReach = 40;
    const int HeavyAttackReach = 60;
    int AttackReach => attackKind == AttackKind.Heavy ? HeavyAttackReach : LightAttackReach;

    /// <summary>Rectangle used for melee collision and debugging; keeps the hitbox aligned with the facing direction.</summary>
    Rectangle GetAttackingBoundBox() => facing switch
    {
        Direction.Left => new Rectangle(position.X - AttackReach, position.Y - (SizeY / 2f), AttackReach, SizeY + SizeY),
        Direction.Right => new Rectangle(position.X + SizeX, position.Y - (SizeY / 2f), AttackReach, SizeY + SizeY),
        Direction.Up => new Rectangle(position.X - (SizeX / 2f), position.Y - AttackReach, SizeX + SizeX, AttackReach),
        Direction.Down => new Rectangle(position.X - (SizeX / 2f), position.Y + SizeY, SizeX + SizeX, AttackReach),
        _ => new Rectangle(position.X, position.Y, SizeX, SizeY),
    };

    /// <summary>Offset from the hit box's top-left to its centre; the box is narrower than tall, so keep the axes apart.</summary>
    static readonly Vector2 HalfSize = new(SizeX / 2f, SizeY / 2f);
    Vector2 position = World.RandomPoint() - HalfSize;
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;
    AttackKind attackKind;
    public Rectangle Bounds => new Rectangle(position.X, position.Y, SizeX, SizeY);
    public Rectangle AttackBounds => IsAttacking ? GetAttackingBoundBox() : new Rectangle(position.X, position.Y, SizeX, SizeY);
    public bool IsAttacking => state == PlayerState.Attacking;
    public bool IsUsingUltimate => IsUltimate;
    public float GetUltimateDamage => 250;
    public float GetMeleeDamage => attackKind == AttackKind.Heavy ? HeavyDamage : LightDamage;
    /// <summary>Which swing is in progress (or was last thrown); Game uses it to pick the damage and, later, stagger.</summary>
    public AttackKind CurrentAttack => attackKind;
    /// <summary>True only during the Update in which the swing reaches its impact frame.</summary>
    public bool SwingLanded { get; private set; }

    public Vector2 Center => position + HalfSize;

    SpriteStrip Strip => (state switch
    {
        PlayerState.Attacking => Assets.HeroAxe,
        PlayerState.Running => Assets.HeroRun,
        PlayerState.Walking => Assets.HeroWalk,
        _ => Assets.HeroIdle,
    })[(int)facing];

    float AttackFps => attackKind == AttackKind.Heavy ? HeavyAttackFps : LightAttackFps;
    float AttackDuration => Strip.FrameCount / AttackFps;

    int CurrentFrame => IsAttacking
        ? Strip.OneShotFrame(animElapsed, AttackDuration)
        : Strip.LoopFrame(animElapsed, AnimFps);

    public void AddUltimateKill()
    {
        UltimateKills += 1;
    }

    public void Reset()
    {
        position = World.RandomPoint() - HalfSize;
        state = PlayerState.Idle;
        PlayerCurrentHp = PlayerHealth;
        animElapsed = 0;
        UltimateKills = 0;
    }
    public void Resume(float health, float ulti)
    {
        position = World.RandomPoint() - HalfSize;
        state = PlayerState.Idle;
        PlayerCurrentHp = health;
        PlayerUlti = ulti;
        animElapsed = 0;
    }

    /// <summary>Ignored mid-swing so a heavy's windup can't be cancelled by mashing.</summary>
    public void Attack(AttackKind kind)
    {
        if (IsAttacking) return;
        StartSwing(kind);
    }

    void StartSwing(AttackKind kind)
    {
        attackKind = kind;
        Raylib.SetSoundVolume(Assets.SwordSound, 0.3f);
        Raylib.PlaySound(Assets.SwordSound);
        state = PlayerState.Attacking;
        animElapsed = 0;
    }

    public void Ultimate(Vector2 at)
    {
        IsUltimate = true;
        TeleportToEntity(at);
        if (!GodMode)
        {
            PlayerUlti = 0;
        }
        StartSwing(AttackKind.Heavy);
    }

    public void ReceiveDamage(float damage) => PlayerCurrentHp = !GodMode ? Math.Max(0, PlayerCurrentHp - damage) : PlayerHealth;

    public void ToggleGodMode()
    {
        GodMode = !GodMode;
        if (GodMode)
        {
            PlayerCurrentHp = PlayerHealth;
            PlayerUlti = 100;
        }
    }
    public void ReceivePower(float amout) => PlayerUlti = !IsUltimate ? Math.Min(100, PlayerUlti + amout) : PlayerUlti;
    public void ReceiveHealth(float amout) => PlayerCurrentHp = Math.Min(100, PlayerUlti + amout);

    /// <param name="obstacles">Solid boxes besides the walls (live enemies); the player slides along them like walls.</param>
    public void Update(float dt, IReadOnlyList<Rectangle> obstacles)
    {
        SwingLanded = false;
        if (IsAttacking)
        {
            UpdateAttack(dt);
            return;
        }

        animElapsed += dt;
        bool boosting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        float step = (boosting ? Speed * BoostMultiplier : Speed) * dt;

        Vector2 dir = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.D)) { dir.X += 1; facing = Direction.Right; }
        if (Raylib.IsKeyDown(KeyboardKey.A)) { dir.X -= 1; facing = Direction.Left; }
        if (Raylib.IsKeyDown(KeyboardKey.W)) { dir.Y -= 1; facing = Direction.Up; }
        if (Raylib.IsKeyDown(KeyboardKey.S)) { dir.Y += 1; facing = Direction.Down; }
        // Normalise so diagonals cover the same distance per second as straight lines.
        Vector2 move = dir == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(dir) * step;

        state = move == Vector2.Zero ? PlayerState.Idle
              : boosting ? PlayerState.Running
              : PlayerState.Walking;

        // Resolve each axis on its own so a wall only stops the component pushing into it and the player slides along it.
        TryMove(new Vector2(move.X, 0), obstacles);
        TryMove(new Vector2(0, move.Y), obstacles);
    }

    void TryMove(Vector2 delta, IReadOnlyList<Rectangle> obstacles)
    {
        if (delta == Vector2.Zero) return;
        var next = Vector2.Clamp(position + delta, Vector2.Zero, new Vector2(World.Width - SizeX, World.Height - SizeY));
        var nextBounds = new Rectangle(next.X, next.Y, SizeX, SizeY);
        if (CollisionMap.Blocks(nextBounds)) return;
        // An obstacle only blocks entry: if we're already inside one (e.g. it spawned on us) we can still walk out.
        foreach (var o in obstacles)
            if (Raylib.CheckCollisionRecs(o, nextBounds) && !Raylib.CheckCollisionRecs(o, Bounds)) return;
        position = next;
    }

    void UpdateAttack(float dt)
    {
        int frameBefore = CurrentFrame;
        animElapsed += dt;
        SwingLanded = frameBefore < AttackImpactFrame && CurrentFrame >= AttackImpactFrame;
        if (SwingLanded && UltimateKills >= UltimateKillLimit)
        {
            IsUltimate = false;
            UltimateKills = 0;
        }
        if (animElapsed >= AttackDuration) state = PlayerState.Idle;
    }

    void TeleportToEntity(Vector2 at)
    {
        Vector2[] candidates = [new Vector2(at.X, at.Y - SizeY), new Vector2(at.X, at.Y + SizeY), new Vector2(at.X - SizeX, at.Y), new Vector2(at.X + SizeX, at.Y)];
        // List<int> unblockedPoints = new List<int>{};
        // TODO: later play with teleport direction etc
        for (int i = 0; i < candidates.Length; i++)
        {
            if (!CollisionMap.Blocks(new Rectangle(candidates[i].X, candidates[i].Y, SizeX, SizeY)))
            {
                Console.WriteLine($"at: {at}  going: {candidates[i]}");
                position = candidates[i];
                break;
            }
        }
    }

    public void Draw()
    {
        Strip.Draw(CurrentFrame, Center, DrawSize);
    }
}
