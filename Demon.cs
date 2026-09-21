using System.Numerics;
using Raylib_cs;

enum EnemyAttackTypes { Ranged, Melee, Both }

class Demon(float powerDrop, int pointDrop, float rangedDamage, float meleeDamage, float health, EnemyAttackTypes attackType = EnemyAttackTypes.Ranged, float projectileSpeed = 280f, EnemyLook look = EnemyLook.Sprite)
{
    // Debug values
    /// <summary>Global aggro switch (F3 in-game): when false demons never fire at the player.</summary>
    public static bool AggroEnabled = false;
    //
    const float SpriteRadius = 25f; // hit box of the sprite look; the strip has a lot of transparent padding around the body
    const float DrawSize = 110f;
    const float IconRadius = 30f; // base body radius for the primitive-drawn looks; roughly the sprite's visible bulk
    const float TargetMargin = 15f; // how far past the hit box the ultimate pick radius reaches
    const float MinTargetRadius = 45f; // so small looks are still comfortable to click
    const float IdleFps = 12f;
    const float DeathDuration = 0.4f;
    const float FireInterval = 2f; // seconds between shots while alive
    const float HoverScale = 0.2f; // how much the sprite grows when hovered as an ultimate target
    const float HoverEaseTime = 0.12f;
    public Vector2 Center;
    // Half-size of the hit box: matches the solid part of the body, so each look gets a box that fits what it draws.
    readonly Vector2 halfSize = look == EnemyLook.Sprite
        ? new(SpriteRadius, SpriteRadius)
        : EnemyIcons.HitExtent(look) * IconRadius * EnemyIcons.BodyScale(look);
    /// <summary>Distance from the centre to the top of the drawing, decorations included.</summary>
    readonly float visualTop = look == EnemyLook.Sprite ? DrawSize / 2 : EnemyIcons.TopExtent(look) * IconRadius * EnemyIcons.BodyScale(look);
    /// <summary>Mouse pick radius while choosing an ultimate target; roomier than the hit box so it's easy to land on.</summary>
    float TargetRadius => Math.Max(MinTargetRadius, Math.Max(halfSize.X, halfSize.Y) + TargetMargin);
    DemonState state = DemonState.Idle;
    float animElapsed;
    float fireCooldown;
    bool SelectedForUlt = false;
    float hoverLevel; // 0 = not hovered, 1 = fully grown; eased so the scale-up doesn't pop
    // Shots already in flight outlive the demon that fired them; they only vanish off-screen or on hit.
    readonly List<EnemyProjectile> projectiles = [];

    // Attributes
    public float CurrentHp = health - 0;
    public float HealthFraction => Math.Clamp(CurrentHp / health, 0f, 1f);
    public bool IsAlive => state == DemonState.Idle;
    public bool IsDead => state == DemonState.Dying && animElapsed > DeathDuration;
    public Rectangle Bounds => BoundsAt(Center);

    Rectangle BoundsAt(Vector2 center) =>
        new(center.X - halfSize.X, center.Y - halfSize.Y, halfSize.X * 2, halfSize.Y * 2);

    public bool ContainsPoint(Vector2 p) => Raylib.CheckCollisionPointRec(p, Bounds);
    /// <summary>Mouse-over test for ultimate targeting; more forgiving than the hit box since the sprite is much larger.</summary>
    public bool IsUnderCursor(Vector2 p) => Raylib.CheckCollisionPointCircle(p, Center, TargetRadius);
    public bool Overlaps(Player player) => Raylib.CheckCollisionRecs(Bounds, player.AttackBounds);
    public int ScorePoint => pointDrop;
    public float MeleeDamage => meleeDamage;

    public bool IsSelectedForUlt => SelectedForUlt;
    public void SelectForUlt()
    {
        SelectedForUlt = true;
    }
    public void ReleaseFromUlt()
    {
        SelectedForUlt = false;
    }

    public void Kill(Player player)
    {
        state = DemonState.Dying;
        animElapsed = 0;
        if (!player.IsUsingUltimate)
        {
            player.ReceivePower(powerDrop);
        }
        else
        {
            player.AddUltimateKill();
        }
    }

    /// <param name="others">Demons already on the field, so this one doesn't land on a spawn point one of them is standing on.</param>
    public void Respawn(Player player, IEnumerable<Demon> others)
    {
        Center = World.RandomEnemySpawn(others.Where(d => d != this && d.IsAlive).Select(d => d.Bounds).Prepend(player.Bounds));
        state = DemonState.Idle;
        animElapsed = 0;
        fireCooldown = FireInterval;
    }

    public void ReceiveDamage(float damage) => CurrentHp = Math.Max(0, CurrentHp - damage);

    public void ClearProjectiles() => projectiles.Clear();

    /// <summary>Eases the hover highlight toward <paramref name="hovered"/>; safe to call while the rest of the demon is frozen.</summary>
    public void UpdateHover(bool hovered, float dt)
    {
        float step = dt / HoverEaseTime;
        hoverLevel = hovered ? Math.Min(1f, hoverLevel + step) : Math.Max(0f, hoverLevel - step);
    }

    /// <param name="holdFire">Freezes shooting and any projectiles in flight (animation still plays), e.g. during the player's ultimate.</param>
    public void Update(float dt, Player player, bool holdFire = false)
    {
        animElapsed += dt;
        if (holdFire) return;

        if (IsAlive && AggroEnabled && (attackType.Equals(EnemyAttackTypes.Both) || attackType.Equals(EnemyAttackTypes.Ranged)))
        {
            fireCooldown -= dt;
            if (fireCooldown <= 0)
            {
                projectiles.Add(new EnemyProjectile(Center, player.Center, rangedDamage, projectileSpeed));
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
        float hover = hoverLevel * hoverLevel * (3f - 2f * hoverLevel); // smoothstep
        if (hover > 0)
        {
            // Ring under the sprite so the pick target reads even before the scale-up finishes.
            float ring = TargetRadius + 6f * hover;
            Raylib.DrawRing(Center, ring - 3f, ring, 0, 360, 48, Raylib.Fade(Color.Yellow, 0.8f * hover));
        }
        float scale = 1f + HoverScale * hover;
        if (look == EnemyLook.Sprite)
        {
            var strip = state == DemonState.Dying ? Assets.DemonDeath : Assets.DemonIdle;
            int frame = state == DemonState.Dying
                ? strip.OneShotFrame(animElapsed, DeathDuration)
                : strip.LoopFrame(animElapsed, IdleFps);
            strip.Draw(frame, Center, DrawSize * scale);
        }
        else
        {
            // No death strip for the primitive looks: they shrink and fade out over the same window instead.
            float death = state == DemonState.Dying ? Math.Clamp(animElapsed / DeathDuration, 0f, 1f) : 0f;
            EnemyIcons.Draw(look, Center, IconRadius * scale * (1f - 0.5f * death), animElapsed, 1f - death);
        }
        if (IsAlive) DrawHealthBar();
        foreach (var p in projectiles) p.Draw();
    }

    void DrawHealthBar()
    {
        const float barWidth = 44f;
        const float barHeight = 5f;
        const float gap = 4f; // space between the sprite top and the bar
        float x = Center.X - barWidth / 2;
        float y = Center.Y - visualTop - gap - barHeight;
        var bg = new Rectangle(x, y, barWidth, barHeight);
        var fill = new Rectangle(x, y, barWidth * HealthFraction, barHeight);
        Raylib.DrawRectangleRec(bg, Raylib.Fade(Color.Black, 0.6f));
        Raylib.DrawRectangleRec(fill, HealthFraction > 0.5f ? Color.Lime : HealthFraction > 0.25f ? Color.Orange : Color.Red);
        Raylib.DrawRectangleLinesEx(bg, 1, Raylib.Fade(Color.White, 0.7f));
    }
}
