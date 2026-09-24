using System.Numerics;
using Raylib_cs;

class Enemy(float powerDrop, int pointDrop, float rangedDamage, float meleeDamage, float health, EnemyAttackType attackType = EnemyAttackType.Ranged, float projectileSpeed = 280f, float fireRange = Enemy.DefaultFireRange, float moveSpeed = 0f, EnemyLook look = EnemyLook.Sprite, RangedAttackType rangedType = RangedAttackType.Targeting)
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
    const float SightReactionDelay = 0.5f; // minimum wait before the first shot after the player comes into view
    /// <summary>Centre-to-centre shooting distance when a type doesn't set its own; about half the camera's view width, so shooters are (nearly) on screen.</summary>
    public const float DefaultFireRange = 450f;
    const float ChaseRange = 600f; // how far a melee enemy notices the player; line of sight is still required
    const float MeleeReach = 20f; // how far past its hit box a melee swing lands
    const float MeleeWindup = 0.45f; // telegraph before the swing lands, long enough to step away or parry
    const float MeleeInterval = 1.2f; // seconds between swings
    const float ArriveDistance = 4f; // close enough to the last-seen spot to give up the chase
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
    float meleeCooldown;
    float windup = -1; // < 0 when not swinging, otherwise seconds into the telegraph
    bool strikeLanding; // the swing lands this frame; Game resolves it against the player's parry and body
    Vector2? chaseGoal; // last place the player was seen; kept after losing sight so the enemy checks where they went

    // Attributes
    public float CurrentHp = health - 0;
    public float HealthFraction => Math.Clamp(CurrentHp / health, 0f, 1f);
    public bool IsAlive => state == EnemyState.Idle;
    public bool IsDead => state == EnemyState.Dying && animElapsed > DeathDuration;
    public Rectangle Bounds => BoundsAt(Center);
    Rectangle MeleeBounds => new(Center.X - halfSize.X - MeleeReach, Center.Y - halfSize.Y - MeleeReach,
        (halfSize.X + MeleeReach) * 2, (halfSize.Y + MeleeReach) * 2);
    bool HasRanged => attackType is EnemyAttackType.Ranged or EnemyAttackType.Both;
    bool HasMelee => attackType is EnemyAttackType.Melee or EnemyAttackType.Both;

    Rectangle BoundsAt(Vector2 center) =>
        new(center.X - halfSize.X, center.Y - halfSize.Y, halfSize.X * 2, halfSize.Y * 2);

    public bool ContainsPoint(Vector2 p) => Raylib.CheckCollisionPointRec(p, Bounds);
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

    public void Respawn(Player player, IEnumerable<Enemy> others)
    {
        Center = World.RandomEnemySpawn(others.Where(d => d != this && d.IsAlive).Select(d => d.Bounds).Prepend(player.Bounds));
        state = EnemyState.Idle;
        animElapsed = 0;
        fireCooldown = FireInterval;
        meleeCooldown = 0;
        windup = -1;
        strikeLanding = false;
        chaseGoal = null;
    }

    public void ReceiveDamage(float damage, Player player)
    {
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

    bool CanSee(Player player, float range) =>
        Vector2.DistanceSquared(Center, player.Center) <= range * range
        && CollisionMap.HasLineOfSight(Center, player.Center);

    public void UpdateHover(bool hovered, float dt)
    {
        float step = dt / HoverEaseTime;
        hoverLevel = hovered ? Math.Min(1f, hoverLevel + step) : Math.Max(0f, hoverLevel - step);
    }

    public void Update(float dt, Player player, IReadOnlyList<Enemy> others, bool holdFire = false)
    {
        animElapsed += dt;
        strikeLanding = false;
        if (holdFire) return;

        if (IsAlive && AggroEnabled && HasMelee) UpdateMelee(dt, player, others);

        if (IsAlive && AggroEnabled && HasRanged)
        {
            fireCooldown -= dt;
            if (!CanSee(player, fireRange))
            {
                fireCooldown = Math.Max(fireCooldown, SightReactionDelay);
            }
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

    void UpdateMelee(float dt, Player player, IReadOnlyList<Enemy> others)
    {
        meleeCooldown -= dt;
        if (windup >= 0)
        {
            windup += dt;
            if (windup >= MeleeWindup)
            {
                windup = -1;
                strikeLanding = true;
                meleeCooldown = MeleeInterval;
            }
            return;
        }

        bool seen = CanSee(player, ChaseRange);
        if (seen) chaseGoal = player.Center;

        if (Raylib.CheckCollisionRecs(MeleeBounds, player.Bounds))
        {
            if (seen && meleeCooldown <= 0) windup = 0;
            return;
        }
        if (chaseGoal is Vector2 goal && !MoveToward(goal, dt, player, others)) chaseGoal = null;
    }

    bool MoveToward(Vector2 goal, float dt, Player player, IReadOnlyList<Enemy> others)
    {
        Vector2 d = goal - Center;
        float dist = d.Length();
        if (dist <= ArriveDistance) return false;
        Vector2 step = d / dist * Math.Min(moveSpeed * dt, dist);
        Vector2 before = Center;
        TryMove(new Vector2(step.X, 0), player, others);
        TryMove(new Vector2(0, step.Y), player, others);
        return Center != before;
    }

    void TryMove(Vector2 delta, Player player, IReadOnlyList<Enemy> others)
    {
        if (delta == Vector2.Zero) return;
        var next = BoundsAt(Center + delta);
        if (CollisionMap.Blocks(next) || Raylib.CheckCollisionRecs(next, player.Bounds)) return;
        foreach (var o in others)
            if (o != this && o.IsAlive && o.Overlaps(next) && !o.Overlaps(Bounds)) return; // let already-stacked enemies separate
        Center += delta;
    }

    public bool BlockMelee(Player player)
    {
        if (!strikeLanding || !player.IsParrying || !Raylib.CheckCollisionRecs(MeleeBounds, player.ParryBounds)) return false;
        strikeLanding = false;
        Raylib.PlaySound(Assets.SwordBlockSound);
        return true;
    }

    public bool ConsumeMeleeHit(Player player)
    {
        if (!strikeLanding) return false;
        strikeLanding = false;
        if (!Raylib.CheckCollisionRecs(MeleeBounds, player.Bounds)) return false;
        player.ReceiveDamage(meleeDamage);
        return true;
    }

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
            float ring = TargetRadius + 6f * hover;
            Raylib.DrawRing(Center, ring - 3f, ring, 0, 360, 48, Raylib.Fade(Color.Yellow, 0.8f * hover));
        }
        float scale = 1f + HoverScale * hover;
        DrawShadow(scale);
        if (windup >= 0 && IsAlive) DrawWindup();
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
            float death = state == EnemyState.Dying ? Math.Clamp(animElapsed / DeathDuration, 0f, 1f) : 0f;
            EnemyIcons.Draw(look, Center, IconRadius * scale * (1f - 0.5f * death), animElapsed, 1f - death);
        }
        if (IsAlive) DrawHealthBar();
        foreach (var p in projectiles) p.Draw();
    }

    void DrawWindup()
    {
        float t = Math.Clamp(windup / MeleeWindup, 0f, 1f);
        float reach = Math.Max(halfSize.X, halfSize.Y) + MeleeReach;
        Raylib.DrawCircleV(Center, reach * t, Raylib.Fade(Color.Red, 0.3f));
        Raylib.DrawRing(Center, reach - 2f, reach, 0, 360, 48, Raylib.Fade(Color.Red, 0.4f + 0.5f * t));
    }

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
