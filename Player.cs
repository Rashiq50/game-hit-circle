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
    static float PlayerHealth = 100;
    public float PlayerCurrentHp = PlayerHealth;
    public float HealthFraction => Math.Clamp(PlayerCurrentHp / PlayerHealth, 0f, 1f);

    Vector2 position = World.RandomPoint() - new Vector2(Size / 2f);
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;
    public Rectangle Bounds => new(position.X, position.Y, Size, Size);
    public bool IsAttacking => state == PlayerState.Attacking;
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

    public void Reset()
    {
        position = World.RandomPoint() - new Vector2(Size / 2f);
        state = PlayerState.Idle;
        PlayerCurrentHp = PlayerHealth;
        animElapsed = 0;
    }
    public void Resume(float health)
    {
        position = World.RandomPoint() - new Vector2(Size / 2f);
        state = PlayerState.Idle;
        PlayerCurrentHp = health;
        animElapsed = 0;
    }

    public void Attack()
    {
        state = PlayerState.Attacking;
        animElapsed = 0;
    }

    public void ReceiveDamage(float damage) => PlayerCurrentHp = Math.Max(0, PlayerCurrentHp - damage);

    public void Update(float dt)
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
        TryMove(new Vector2(move.X, 0));
        TryMove(new Vector2(0, move.Y));
    }

    void TryMove(Vector2 delta)
    {
        if (delta == Vector2.Zero) return;
        var next = Vector2.Clamp(position + delta, Vector2.Zero, new Vector2(World.Width - Size, World.Height - Size));
        if (!CollisionMap.Blocks(new Rectangle(next.X, next.Y, Size, Size))) position = next;
    }

    void UpdateAttack(float dt)
    {
        int frameBefore = CurrentFrame;
        animElapsed += dt;
        SwingLanded = frameBefore < AttackImpactFrame && CurrentFrame >= AttackImpactFrame;
        if (animElapsed >= AttackDuration) state = PlayerState.Idle;
    }

    public void Draw() => Strip.Draw(CurrentFrame, Center, DrawSize);
}
