using System.Numerics;
using Raylib_cs;

class Player
{
    const int Size = 40;
    const float Speed = 300f;
    const float BoostMultiplier = 2.5f;
    const float DrawSize = 96f;
    const float AnimFps = 10f;
    const float AttackFps = 16f;
    const int AttackImpactFrame = 3;
    private static readonly bool GodMode = false;
    private bool IsUltimate = false;
    private int UltimateKills = 0;
    private int UltimateKillLimit = 1;
    static readonly float PlayerHealth = 100;
    public float PlayerCurrentHp = PlayerHealth;
    public float PlayerUlti = GodMode ? 100 : 0;
    public float HealthFraction => Math.Clamp(PlayerCurrentHp / PlayerHealth, 0f, 1f);
    public float PowerFraction => Math.Clamp(PlayerUlti / 100, 0f, 1f);

    const int AttackReach = 20;

    /// <summary>The movement box grown by <see cref="AttackReach"/> on the side the player is facing.</summary>
    Rectangle GetAttackingBoundBox() => facing switch
    {
        Direction.Left => new Rectangle(position.X - AttackReach, position.Y, Size + AttackReach, Size),
        Direction.Right => new Rectangle(position.X, position.Y, Size + AttackReach, Size),
        Direction.Up => new Rectangle(position.X, position.Y - AttackReach, Size, Size + AttackReach),
        Direction.Down => new Rectangle(position.X, position.Y, Size, Size + AttackReach),
        _ => new Rectangle(position.X, position.Y, Size, Size),
    };

    Vector2 position = World.RandomPoint() - new Vector2(Size / 2f);
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;
    public Rectangle Bounds => IsAttacking ? GetAttackingBoundBox() : new Rectangle(position.X, position.Y, Size, Size);
    public bool IsAttacking => state == PlayerState.Attacking;
    public bool IsUsingUltimate => IsUltimate;
    /// <summary>True only during the Update in which the swing reaches its impact frame.</summary>
    public bool SwingLanded { get; private set; }

    public Vector2 Center => position + new Vector2(Size / 2f);

    SpriteStrip Strip => (state switch
    {
        PlayerState.Attacking => Assets.HeroAxe,
        PlayerState.Running => Assets.HeroRun,
        PlayerState.Walking => Assets.HeroWalk,
        _ => Assets.HeroIdle,
    })[(int)facing];

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
        position = World.RandomPoint() - new Vector2(Size / 2f);
        state = PlayerState.Idle;
        PlayerCurrentHp = PlayerHealth;
        animElapsed = 0;
        UltimateKills = 0;
    }
    public void Resume(float health, float ulti)
    {
        position = World.RandomPoint() - new Vector2(Size / 2f);
        state = PlayerState.Idle;
        PlayerCurrentHp = health;
        PlayerUlti = ulti;
        animElapsed = 0;
    }

    public void Attack()
    {
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
        Attack();
    }

    public void ReceiveDamage(float damage) => PlayerCurrentHp = !GodMode ? Math.Max(0, PlayerCurrentHp - damage) : 100;
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

        Vector2 move = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.D)) { move.X += step; facing = Direction.Right; }
        if (Raylib.IsKeyDown(KeyboardKey.A)) { move.X -= step; facing = Direction.Left; }
        if (Raylib.IsKeyDown(KeyboardKey.W)) { move.Y -= step; facing = Direction.Up; }
        if (Raylib.IsKeyDown(KeyboardKey.S)) { move.Y += step; facing = Direction.Down; }

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
        var next = Vector2.Clamp(position + delta, Vector2.Zero, new Vector2(World.Width - Size, World.Height - Size));
        var nextBounds = new Rectangle(next.X, next.Y, Size, Size);
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
        Vector2[] candidates = [new Vector2(at.X, at.Y - Size), new Vector2(at.X, at.Y + Size), new Vector2(at.X - Size, at.Y), new Vector2(at.X + Size, at.Y)];
        // List<int> unblockedPoints = new List<int>{};
        // TODO: later play with teleport direction etc
        for (int i = 0; i < candidates.Length; i++)
        {
            if (!CollisionMap.Blocks(new Rectangle(candidates[i].X, candidates[i].Y, Size, Size)))
            {
                Console.WriteLine($"at: {at}  going: {candidates[i]}");
                position = candidates[i];
                break;
            }
        }
    }

    public void Draw() => Strip.Draw(CurrentFrame, Center, DrawSize);
}
