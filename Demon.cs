using System.Numerics;
using Raylib_cs;

class Demon
{
    const float Radius = 25f;
    const float DrawSize = 110f;
    const float IdleFps = 12f;
    const float DeathDuration = 0.4f;
    const int RespawnAttemptCap = 10;
    const float FireInterval = 2f; // seconds between shots while alive

    public Vector2 Center;
    DemonState state = DemonState.Idle;
    float animElapsed;
    float fireCooldown;
    // Shots already in flight outlive the demon that fired them; they only vanish off-screen or on hit.
    readonly List<EnemyProjectile> projectiles = [];

    public bool IsAlive => state == DemonState.Idle;
    public bool IsDead => state == DemonState.Dying && animElapsed > DeathDuration;
    Rectangle Bounds => BoundsAt(Center);

    static Rectangle BoundsAt(Vector2 center) =>
        new(center.X - Radius, center.Y - Radius, Radius * 2, Radius * 2);

    public bool ContainsPoint(Vector2 p) => Raylib.CheckCollisionPointRec(p, Bounds);
    public bool Overlaps(Player player) => Raylib.CheckCollisionRecs(Bounds, player.Bounds);

    public void Kill()
    {
        state = DemonState.Dying;
        animElapsed = 0;
    }

    public void Respawn(Player player)
    {
        Vector2 candidate = World.RandomPoint();
        for (int attempt = 0; attempt < RespawnAttemptCap && Raylib.CheckCollisionRecs(BoundsAt(candidate), player.Bounds); attempt++)
            candidate = World.RandomPoint();

        Center = candidate;
        state = DemonState.Idle;
        animElapsed = 0;
        fireCooldown = FireInterval;
    }

    public void ClearProjectiles() => projectiles.Clear();

    public void Update(float dt, Player player)
    {
        animElapsed += dt;

        if (IsAlive)
        {
            fireCooldown -= dt;
            if (fireCooldown <= 0)
            {
                projectiles.Add(new EnemyProjectile(Center, player.Center));
                fireCooldown += FireInterval;
            }
        }

        foreach (var p in projectiles) p.Update(dt);
        projectiles.RemoveAll(p => !p.Active);
    }

    /// <summary>True if any projectile hit the player this frame; the projectile is spent so it can only hit once.</summary>
    public bool ConsumeProjectileHit(Player player)
    {
        var hit = projectiles.Find(p => p.Overlaps(player));
        if (hit is null) return false;
        hit.Active = false;
        player.ReceiveDamage(hit.GetDamageNumber());
        return true;
    }

    public void Draw()
    {
        var strip = state == DemonState.Dying ? Assets.DemonDeath : Assets.DemonIdle;
        int frame = state == DemonState.Dying
            ? strip.OneShotFrame(animElapsed, DeathDuration)
            : strip.LoopFrame(animElapsed, IdleFps);
        strip.Draw(frame, Center, DrawSize);
        foreach (var p in projectiles) p.Draw();
    }
}
