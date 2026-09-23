using System.Numerics;
using Raylib_cs;

class Enemy(float powerDrop, int pointDrop, float rangedDamage, float meleeDamage, float health, EnemyAttackType attackType = EnemyAttackType.Ranged, float projectileSpeed = 280f, EnemyLook look = EnemyLook.Sprite, RangedAttackType rangedType = RangedAttackType.Targeting)
{
    // Debug values
    /// <summary>Global aggro switch (F3 in-game): when false enemies never fire at the player.</summary>
    public static bool AggroEnabled = true;
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
    EnemyState state = EnemyState.Idle;
    float animElapsed;
    float fireCooldown;
    bool SelectedForUlt = false;
    bool TakingDamageFromTrails = false;
    float hoverLevel; // 0 = not hovered, 1 = fully grown; eased so the scale-up doesn't pop
    readonly List<EnemyProjectile> projectiles = [];

    // Attributes
    public float CurrentHp = health - 0;
    public float HealthFraction => Math.Clamp(CurrentHp / health, 0f, 1f);
    public bool IsAlive => state == EnemyState.Idle;
    public bool IsDead => state == EnemyState.Dying && animElapsed > DeathDuration;
    public Rectangle Bounds => BoundsAt(Center);

    Rectangle BoundsAt(Vector2 center) =>
        new(center.X - halfSize.X, center.Y - halfSize.Y, halfSize.X * 2, halfSize.Y * 2);

    public bool ContainsPoint(Vector2 p) => Raylib.CheckCollisionPointRec(p, Bounds);
    /// <summary>Mouse-over test for ultimate targeting; more forgiving than the hit box since the sprite is much larger.</summary>
    public bool IsUnderCursor(Vector2 p) => Raylib.CheckCollisionPointCircle(p, Center, TargetRadius);
    public bool Overlaps(Rectangle target) => Raylib.CheckCollisionRecs(Bounds, target);
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
        state = EnemyState.Dying;
        animElapsed = 0;
        if (!player.IsUsingUltimate) player.ReceivePower(powerDrop);
    }

    /// <param name="others">Enemys already on the field, so this one doesn't land on a spawn point one of them is standing on.</param>
    public void Respawn(Player player, IEnumerable<Enemy> others)
    {
        Center = World.RandomEnemySpawn(others.Where(d => d != this && d.IsAlive).Select(d => d.Bounds).Prepend(player.Bounds));
        state = EnemyState.Idle;
        animElapsed = 0;
        fireCooldown = FireInterval;
    }

    public void ReceiveDamage(float damage, Player player)
    {
        // Above the body, not on it, so the number never sits under the sprite or the health bar.
        FloatingNumbers.Show(new Vector2(Center.X, Center.Y - visualTop * 0.5f), damage,
            player.IsUsingUltimate ? DamageStyle.CriticalHit : DamageStyle.EnemyHit);
        CurrentHp = Math.Max(0, CurrentHp - damage);
        if (CurrentHp <= 0 && state != EnemyState.Dying)
        {
            Kill(player);
            var coin = new CoinDrop(Center, pointDrop);
            Game.AddCoin(coin);
        }
    }

    public void ClearProjectiles() => projectiles.Clear();

    /// <summary>Eases the hover highlight toward <paramref name="hovered"/>; safe to call while the rest of the enemy is frozen.</summary>
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

        if (IsAlive && AggroEnabled && (attackType.Equals(EnemyAttackType.Both) || attackType.Equals(EnemyAttackType.Ranged)))
        {
            fireCooldown -= dt;
            List<Vector2> targets = [];
            if (rangedType.Equals(RangedAttackType.Targeting))
            {
                targets.Add(player.Center);
            }
            else if (rangedType.Equals(RangedAttackType.Directional))
            {
                // Figure out directional fucntionality
            }
            if (fireCooldown <= 0)
            {
                foreach (var target in targets)
                {
                    projectiles.Add(new EnemyProjectile(Center, target, rangedDamage, projectileSpeed));
                }
                fireCooldown += FireInterval;
            }
        }

        foreach (var p in projectiles) p.Update(dt);
        projectiles.RemoveAll(p => !p.Active);
    }

    /// <summary>True if any projectile hit the player this frame; the projectile is spent so it can only hit once.</summary>
    public bool ConsumeProjectileHit(Player player)
    {
        var hit = projectiles.Find(p => p.Overlaps(player.Bounds));
        if (hit is null) return false;
        hit.Active = false;
        player.ReceiveDamage(hit.GetDamageNumber());
        return true;
    }

    public bool BlockProjectile(Player player)
    {
        var hit = projectiles.Find(p => p.Overlaps(player.ParryBounds));
        if (hit is null) return false;
        Raylib.PlaySound(Assets.SwordBlockSound);
        hit.Active = false;
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
        DrawShadow(scale);
        if (look == EnemyLook.Sprite)
        {
            var strip = state == EnemyState.Dying ? Assets.EnemyDeath : Assets.EnemyIdle;
            int frame = state == EnemyState.Dying
                ? strip.OneShotFrame(animElapsed, DeathDuration)
                : strip.LoopFrame(animElapsed, IdleFps);
            strip.Draw(frame, Center, DrawSize * scale);
        }
        else
        {
            // No death strip for the primitive looks: they shrink and fade out over the same window instead.
            float death = state == EnemyState.Dying ? Math.Clamp(animElapsed / DeathDuration, 0f, 1f) : 0f;
            EnemyIcons.Draw(look, Center, IconRadius * scale * (1f - 0.5f * death), animElapsed, 1f - death);
        }
        if (IsAlive) DrawHealthBar();
        foreach (var p in projectiles) p.Draw();
    }

    /// <summary>A soft ground shadow under the feet so the body separates from the background; fades out with the death animation.</summary>
    void DrawShadow(float scale)
    {
        float death = state == EnemyState.Dying ? Math.Clamp(animElapsed / DeathDuration, 0f, 1f) : 0f;
        float rx = halfSize.X * 0.9f * scale * (1f - 0.5f * death);
        float ry = Math.Max(4f, rx * 0.35f);
        int x = (int)Center.X, y = (int)(Center.Y + halfSize.Y * scale - ry * 0.5f);
        float alpha = 1f - death;
        Raylib.DrawEllipse(x, y, rx, ry, Raylib.Fade(Color.Black, 0.18f * alpha));
        Raylib.DrawEllipse(x, y, rx * 0.65f, ry * 0.65f, Raylib.Fade(Color.Black, 0.22f * alpha));
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
