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
    const float IconRadius = 30f; // base body radius for the generated looks (BASE_RADIUS in tools/enemies); roughly the sprite's visible bulk
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
    const float MeleeArc = 120f; // degrees a swing sweeps, centred on where the player stood as the windup began
    const float MeleeInterval = 1.2f; // seconds between swings
    const float AttackRecovery = 0.3f; // follow-through once a blow lands or a shot leaves; the enemy stands its ground through it
    const float SwingFlash = 0.15f; // how long a swing's arc flashes on the ground once the blow lands
    const float ArriveDistance = 4f; // close enough to the last-seen spot to give up the chase
    const float HoverScale = 0.2f; // how much the sprite grows when hovered as an ultimate target
    const float HoverEaseTime = 0.12f;
    const float FacingDeadZone = 12f;
    // After a hit the body holds solid white for a few frames, then snaps back; a flash that starts fading at once reads as mush.
    const float HitFlashHold = 0.07f;
    const float HitFlashFade = 0.08f;
    // Status looks ease in and out rather than popping; out is slower so an expiring status visibly wears off.
    const float StatusFadeIn = 0.1f;
    const float StatusFadeOut = 0.25f;
    const float ShockJitter = 1.5f; // how far the body twitches while shocked; the hit box stays put
    // Move and attack speed a status leaves the enemy with when ApplyStatus isn't given its own (1 = unaffected, 0 = can't
    // act at all). Statuses don't stack: the slowest one applies.
    public const float FrozenSpeed = 0.5f;
    public const float PoisonedSpeed = 0.75f;
    public const float ShockedSpeed = 0.75f;
    public const float StunnedSpeed = 0f;
    // Damage per tick a status deals when ApplyStatus isn't given its own; 0 = no damage over time by default.
    public const float BurningDot = 5f;
    public const float PoisonedDot = 5f;
    const float DotInterval = 1f; // seconds between damage-over-time ticks; the first lands one interval after the status starts
    public Vector2 Center;
    // Half-size of the hit box: matches the solid part of the body, so each look gets a box that fits what it draws.
    readonly Vector2 halfSize = look == EnemyLook.Sprite
        ? new(SpriteRadius, SpriteRadius)
        : EnemyIcons.HitExtent(look) * IconRadius * EnemyIcons.BodyScale(look);
    /// <summary>Distance from the centre to the top of the drawing, decorations included.</summary>
    readonly float visualTop = look == EnemyLook.Sprite ? DrawSize / 2 : EnemyIcons.TopExtent(look) * IconRadius * EnemyIcons.BodyScale(look);
    /// <summary>Rough radius of the drawn body, so the status patterns scale with each look.</summary>
    readonly float bodyRadius = look == EnemyLook.Sprite ? SpriteRadius * 1.4f : IconRadius * EnemyIcons.BodyScale(look);
    /// <summary>Mouse pick radius while choosing an ultimate target; roomier than the hit box so it's easy to land on.</summary>
    float TargetRadius => Math.Max(MinTargetRadius, Math.Max(halfSize.X, halfSize.Y) + TargetMargin);
    EnemyState state = EnemyState.Idle;
    float animElapsed;
    float fireCooldown;
    bool SelectedForUlt = false;
    bool TakingDamageFromTrails = false;
    float hoverLevel; // 0 = not hovered, 1 = fully grown; eased so the scale-up doesn't pop
    float hitFlash; // seconds of hit flash left
    // Indexed by StatusEffect: seconds left, and the eased 0-1 strength the shader draws with.
    readonly float[] statusTime = new float[StatusEffects.Count];
    readonly float[] statusLevel = new float[StatusEffects.Count];
    float statusClock; // world-time seconds that animate the status looks
    Vector2 shockOffset; // body draw offset while shocked
    // Also by StatusEffect: damage per tick while the status lasts (0 = none), and seconds toward its next tick.
    // Each status ticks on its own, so a burning, poisoned enemy takes both.
    readonly float[] statusDot = new float[StatusEffects.Count];
    readonly float[] statusDotTimer = new float[StatusEffects.Count];
    readonly float[] statusSpeed = new float[StatusEffects.Count]; // move/attack speed while the status lasts
    bool facingLeft; // the generated art faces right; mirrored while the player is to the left
    readonly List<EnemyProjectile> projectiles = [];
    float meleeCooldown;
    // The attack in progress, melee or ranged: seconds into it (< 0 when not attacking), and the way it points. A swing
    // fixes its aim as the windup starts, so the telegraph shows where it will land; a shot follows the player until it leaves.
    float attackTime = -1;
    bool meleeAttack;
    bool attackReleased; // the blow has landed or the shot has left; what's left is follow-through
    Vector2 aim = Vector2.UnitX;
    bool strikeLanding; // the swing lands this frame; Game resolves it against the player's parry and body
    Vector2? chaseGoal; // last place the player was seen; kept after losing sight so the enemy checks where they went

    // Attributes
    public float CurrentHp = health - 0;
    public float HealthFraction => Math.Clamp(CurrentHp / health, 0f, 1f);
    public bool IsAlive => state == EnemyState.Idle;
    public bool IsDead => state == EnemyState.Dying && animElapsed > DeathDuration;
    public Rectangle Bounds => BoundsAt(Center);
    /// <summary>How far a swing reaches from the centre: the body's longer half-size plus <see cref="MeleeReach"/>.</summary>
    float MeleeRadius => Math.Max(halfSize.X, halfSize.Y) + MeleeReach;
    bool HasRanged => attackType is EnemyAttackType.Ranged or EnemyAttackType.Both;
    bool HasMelee => attackType is EnemyAttackType.Melee or EnemyAttackType.Both;
    bool Attacking => attackTime >= 0;
    float AttackWindup => meleeAttack ? MeleeWindup : EnemyIcons.CastWindup(look);
    /// <summary>0..1 through the windup, then 1..2 through the follow-through.</summary>
    float AttackProgress => attackTime < AttackWindup
        ? attackTime / AttackWindup
        : 1f + (attackTime - AttackWindup) / AttackRecovery;
    /// <summary>The attack's way to the nearest of the four, which picks the row of the attack sheet the art comes from.</summary>
    Direction AttackDirection => Math.Abs(aim.Y) > Math.Abs(aim.X)
        ? aim.Y > 0 ? Direction.Down : Direction.Up
        : aim.X < 0 ? Direction.Left : Direction.Right;

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
        attackTime = -1;
        strikeLanding = false;
        chaseGoal = null;
        hitFlash = 0;
        ClearStatuses();
    }

    /// <summary>Puts the enemy under a status for at least <paramref name="duration"/> seconds; reapplying refreshes it but
    /// never shortens what's left.</summary>
    /// <param name="dotDamage">Damage the status deals every <see cref="DotInterval"/> seconds while it lasts; null uses the
    /// status's default (<see cref="DefaultDot"/>), 0 means none. Reapplying keeps the harder-hitting of the two.</param>
    /// <param name="speed">Move and attack speed while the status lasts, 1 = unaffected, 0 = can't act (a full freeze or a
    /// stun); null uses the status's default (<see cref="DefaultSpeed"/>). Reapplying keeps the slower of the two.</param>
    public void ApplyStatus(StatusEffect status, float duration, float? dotDamage = null, float? speed = null)
    {
        int i = (int)status;
        float dot = dotDamage ?? DefaultDot(status);
        float slow = Math.Clamp(speed ?? DefaultSpeed(status), 0f, 1f);
        if (statusTime[i] > 0)
        {
            statusDot[i] = Math.Max(statusDot[i], dot);
            statusSpeed[i] = Math.Min(statusSpeed[i], slow);
        }
        else
        {
            statusDot[i] = dot;
            statusSpeed[i] = slow;
            statusDotTimer[i] = 0; // a fresh application waits a full interval before its first tick
        }
        statusTime[i] = Math.Max(statusTime[i], duration);
    }

    public static float DefaultDot(StatusEffect status) => status switch
    {
        StatusEffect.Burning => BurningDot,
        StatusEffect.Poisoned => PoisonedDot,
        _ => 0f,
    };

    public static float DefaultSpeed(StatusEffect status) => status switch
    {
        StatusEffect.Frozen => FrozenSpeed,
        StatusEffect.Poisoned => PoisonedSpeed,
        StatusEffect.Shocked => ShockedSpeed,
        StatusEffect.Stunned => StunnedSpeed,
        _ => 1f,
    };

    public bool HasStatus(StatusEffect status) => statusTime[(int)status] > 0;

    /// <summary>Multiplier on how fast the enemy moves and attacks: the slowest of its active statuses, 0 = can't act.
    /// Scales the clock that movement, attack cooldowns and the melee windup run on; projectiles already in flight keep
    /// full speed.</summary>
    public float ActionSpeed
    {
        get
        {
            float speed = 1f;
            for (int i = 0; i < StatusEffects.Count; i++)
                if (statusTime[i] > 0) speed = Math.Min(speed, statusSpeed[i]);
            return speed;
        }
    }

    /// <summary>Ends every status at once, look included (no fade-out).</summary>
    public void ClearStatuses()
    {
        Array.Clear(statusTime);
        Array.Clear(statusLevel);
        shockOffset = Vector2.Zero;
        Array.Clear(statusDot);
        Array.Clear(statusDotTimer);
        Array.Clear(statusSpeed);
    }

    void UpdateDamageOverTime(float dt, Player player)
    {
        for (int i = 0; i < StatusEffects.Count; i++)
        {
            if (statusTime[i] <= 0 || statusDot[i] <= 0) continue;
            statusDotTimer[i] += dt;
            while (statusDotTimer[i] >= DotInterval && IsAlive)
            {
                statusDotTimer[i] -= DotInterval;
                ReceiveDamage(statusDot[i], player, DamageStyle.DamageOverTime);
            }
        }
    }

    void UpdateStatuses(float dt)
    {
        statusClock += dt;
        for (int i = 0; i < StatusEffects.Count; i++)
        {
            statusTime[i] = Math.Max(0, statusTime[i] - dt);
            statusLevel[i] = statusTime[i] > 0
                ? Math.Min(1f, statusLevel[i] + dt / StatusFadeIn)
                : Math.Max(0f, statusLevel[i] - dt / StatusFadeOut);
        }
        // Rolled here rather than in Draw so the twitch stops while the world is paused or frozen.
        shockOffset = IsAlive && HasStatus(StatusEffect.Shocked)
            ? new Vector2(Random.Shared.NextSingle() * 2 - 1, Random.Shared.NextSingle() * 2 - 1) * ShockJitter
            : Vector2.Zero;
    }

    bool AnyStatusShowing()
    {
        foreach (float level in statusLevel)
            if (level > 0) return true;
        return false;
    }

    /// <param name="style">How the damage number looks; by default a normal hit, or a crit during the ultimate.</param>
    public void ReceiveDamage(float damage, Player player, DamageStyle? style = null)
    {
        FloatingNumbers.Show(new Vector2(Center.X, Center.Y - visualTop * 0.5f), damage,
            style ?? (player.IsUsingUltimate ? DamageStyle.CriticalHit : DamageStyle.EnemyHit));
        CurrentHp = Math.Max(0, CurrentHp - damage);
        hitFlash = HitFlashHold + HitFlashFade;
        if (CurrentHp <= 0 && state != EnemyState.Dying)
        {
            Kill(player);
            Game.DropLoot(Center, pointDrop);
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
        hitFlash = Math.Max(0, hitFlash - dt);
        UpdateStatuses(dt);
        strikeLanding = false;
        float actionSpeed = ActionSpeed;
        bool locked = actionSpeed <= 0; // stunned or frozen solid
        if (locked || !AggroEnabled) attackTime = -1; // interrupts an attack in progress
        // Turn to face the player, with a dead zone so standing right above or below doesn't flicker the art. An attack
        // keeps the facing it was aimed with.
        float dx = player.Center.X - Center.X;
        if (IsAlive && !locked && !Attacking && Math.Abs(dx) > FacingDeadZone) facingLeft = dx < 0;
        if (holdFire) return;
        // After the hold: a tick mid-ultimate could kill the target before the strike lands.
        UpdateDamageOverTime(dt, player);

        // A locked enemy skips acting outright: a zero clock alone would still let a melee enemy start a swing it never finishes.
        bool canAct = IsAlive && AggroEnabled && !locked;
        float actDt = dt * actionSpeed;
        if (canAct)
        {
            if (HasMelee) meleeCooldown -= actDt;
            if (HasRanged) fireCooldown -= actDt;
            // One attack at a time: an enemy with both waits out one before it starts the other.
            if (!Attacking && HasMelee) UpdateMelee(actDt, player, others);
            if (!Attacking && HasRanged) UpdateRanged(player);
            if (Attacking) UpdateAttack(actDt, player);
        }

        foreach (var p in projectiles) p.Update(dt);
        projectiles.RemoveAll(p => !p.Active);
    }

    void UpdateMelee(float dt, Player player, IReadOnlyList<Enemy> others)
    {
        bool seen = CanSee(player, ChaseRange);
        if (seen) chaseGoal = player.Center;

        if (InReach(player.Bounds))
        {
            if (seen && meleeCooldown <= 0) StartAttack(melee: true, player);
            return;
        }
        if (chaseGoal is Vector2 goal && !MoveToward(goal, dt, player, others)) chaseGoal = null;
    }

    void UpdateRanged(Player player)
    {
        if (!CanSee(player, fireRange))
            fireCooldown = Math.Max(fireCooldown, SightReactionDelay);
        // Wind up early enough that the shot leaves right as the cooldown runs out.
        else if (fireCooldown <= EnemyIcons.CastWindup(look))
            StartAttack(melee: false, player);
    }

    void StartAttack(bool melee, Player player)
    {
        meleeAttack = melee;
        attackReleased = false;
        attackTime = 0;
        AimAt(player.Center);
    }

    /// <summary>Runs the windup, lands the blow or looses the shot, then plays out the follow-through.</summary>
    void UpdateAttack(float dt, Player player)
    {
        if (!meleeAttack && !attackReleased)
        {
            // A shot that loses sight of the player is called off, the same as one that never had it.
            if (!CanSee(player, fireRange))
            {
                attackTime = -1;
                fireCooldown = Math.Max(fireCooldown, SightReactionDelay);
                return;
            }
            AimAt(player.Center);
        }
        attackTime += dt;
        if (!attackReleased && attackTime >= AttackWindup)
        {
            attackReleased = true;
            if (meleeAttack)
            {
                strikeLanding = true;
                meleeCooldown = MeleeInterval;
            }
            else Fire(player);
        }
        if (attackTime >= AttackWindup + AttackRecovery) attackTime = -1;
    }

    void Fire(Player player)
    {
        List<Vector2> targets = [];
        if (rangedType.Equals(RangedAttackType.Targeting))
        {
            targets.Add(player.Center);
        }
        else if (rangedType.Equals(RangedAttackType.Directional))
        {
            // Figure out directional fucntionality
        }
        Vector2 from = Muzzle(player);
        foreach (var target in targets)
        {
            projectiles.Add(new EnemyProjectile(from, target, rangedDamage, projectileSpeed));
        }
        fireCooldown = FireInterval;
    }

    /// <summary>Where a shot leaves: the weapon in the art's release frame, mirrored with the facing. Falls back to the centre
    /// when the player is nearer than that (the shot would start behind them) or a wall is in the way.</summary>
    Vector2 Muzzle(Player player)
    {
        Vector2 offset = EnemyIcons.Muzzle(look, AttackDirection) * IconRadius * EnemyIcons.BodyScale(look);
        if (facingLeft) offset.X = -offset.X;
        Vector2 at = Center + offset;
        bool clear = Vector2.DistanceSquared(Center, player.Center) > offset.LengthSquared()
            && CollisionMap.HasLineOfSight(Center, at);
        return clear ? at : Center;
    }

    void AimAt(Vector2 target)
    {
        if (target != Center) aim = Vector2.Normalize(target - Center);
        // A sideways attack is drawn facing the way it goes; up and down keep whichever way the enemy already faced.
        if (AttackDirection is Direction.Left or Direction.Right) facingLeft = aim.X < 0;
    }

    Vector2 NearestPoint(Rectangle box) =>
        Vector2.Clamp(Center, new(box.X, box.Y), new(box.X + box.Width, box.Y + box.Height));

    bool InReach(Rectangle box) => Vector2.Distance(NearestPoint(box), Center) <= MeleeRadius;

    /// <summary>Whether any of <paramref name="box"/> lies in the swing: within <see cref="MeleeRadius"/> of the centre and
    /// inside <see cref="MeleeArc"/> around the aim. Sampled on a grid plus the nearest point, which is plenty for boxes the
    /// size of the player's.</summary>
    bool SwingHits(Rectangle box)
    {
        const int Steps = 4;
        if (InSwing(NearestPoint(box))) return true;
        for (int i = 0; i <= Steps; i++)
            for (int j = 0; j <= Steps; j++)
                if (InSwing(new Vector2(box.X + box.Width * i / Steps, box.Y + box.Height * j / Steps))) return true;
        return false;
    }

    bool InSwing(Vector2 p)
    {
        Vector2 d = p - Center;
        float dist = d.Length();
        if (dist > MeleeRadius) return false;
        return dist < 1f || Vector2.Dot(d / dist, aim) >= MathF.Cos(float.DegreesToRadians(MeleeArc / 2));
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
        if (!strikeLanding || !player.IsParrying || !SwingHits(player.ParryBounds)) return false;
        strikeLanding = false;
        Raylib.PlaySound(Assets.SwordBlockSound);
        return true;
    }

    public bool ConsumeMeleeHit(Player player)
    {
        if (!strikeLanding) return false;
        strikeLanding = false;
        if (!SwingHits(player.Bounds)) return false;
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
        if (Attacking && meleeAttack && IsAlive) DrawSwing();
        // Only the body is shaded, and only while it's hit or under a status: each shader switch flushes raylib's batch.
        bool shaded = hitFlash > 0 || AnyStatusShowing();
        if (shaded)
        {
            float fade = Math.Min(1f, hitFlash / HitFlashFade); // 1 through the hold, then falls to 0
            EnemyBodyEffect.Begin(Center, bodyRadius * scale, statusClock, statusLevel,
                fade * fade, Color.White); // flash squared so the tail drops off fast
        }
        Vector2 bodyAt = Center + shockOffset;
        if (look == EnemyLook.Sprite)
        {
            var strip = state == EnemyState.Dying ? Assets.EnemyDeath : Assets.EnemyIdle;
            int frame = state == EnemyState.Dying
                ? strip.OneShotFrame(animElapsed, DeathDuration)
                : strip.LoopFrame(animElapsed, IdleFps);
            strip.Draw(frame, bodyAt, DrawSize * scale);
        }
        else
        {
            float death = state == EnemyState.Dying ? Math.Clamp(animElapsed / DeathDuration, 0f, 1f) : -1f;
            AttackPose? attack = Attacking && IsAlive ? new(meleeAttack, AttackDirection, AttackProgress) : null;
            EnemyIcons.Draw(look, bodyAt, scale, animElapsed, death, facingLeft, attack);
        }
        if (shaded) EnemyBodyEffect.End();
        if (IsAlive) DrawHealthBar();
        foreach (var p in projectiles) p.Draw();
    }

    /// <summary>The swing's telegraph on the ground: the arc it will sweep, filling out through the windup, then flashing as
    /// the blow lands.</summary>
    void DrawSwing()
    {
        const int Segments = 24;
        float reach = MeleeRadius;
        float mid = float.RadiansToDegrees(MathF.Atan2(aim.Y, aim.X));
        float from = mid - MeleeArc / 2, to = mid + MeleeArc / 2;
        if (!attackReleased)
        {
            float t = Math.Clamp(attackTime / MeleeWindup, 0f, 1f);
            Raylib.DrawCircleSector(Center, reach, from, to, Segments, Raylib.Fade(Color.Red, 0.12f));
            Raylib.DrawCircleSector(Center, reach * t, from, to, Segments, Raylib.Fade(Color.Red, 0.3f));
            Raylib.DrawRing(Center, reach - 2f, reach, from, to, Segments, Raylib.Fade(Color.Red, 0.4f + 0.5f * t));
            return;
        }
        float flash = 1f - Math.Clamp((attackTime - MeleeWindup) / SwingFlash, 0f, 1f);
        if (flash > 0) Raylib.DrawCircleSector(Center, reach, from, to, Segments, Raylib.Fade(Color.White, 0.45f * flash));
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
