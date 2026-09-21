/// <summary>One demon as a stage defines it; mirrors <see cref="Demon"/>'s constructor so a stage can spawn it directly.</summary>
record EnemyDef(
    float Health,
    int PointDrop,
    float PowerDrop,
    float RangedDamage,
    float MeleeDamage,
    EnemyAttackTypes AttackType = EnemyAttackTypes.Ranged,
    float ProjectileSpeed = 280f,
    EnemyLook Look = EnemyLook.Sprite)
{
    public Demon Spawn() => new(PowerDrop, PointDrop, RangedDamage, MeleeDamage, Health, AttackType, ProjectileSpeed, Look);
}

/// <summary>The enemy variants stages are built from. Tune a variant here and every stage using it follows.
/// Only the Grunt has real art; the rest borrow a primitive body from <see cref="EnemyIcons"/> so they can be told apart.</summary>
static class Enemies
{
    /// <summary>Baseline ranged demon; the one the game has had so far.</summary>
    public static readonly EnemyDef Grunt = new(Health: 60, PointDrop: 10, PowerDrop: 25, RangedDamage: 12, MeleeDamage: 0);
    /// <summary>Fragile and cheap; fodder to pad early stages.</summary>
    public static readonly EnemyDef Imp = new(Health: 30, PointDrop: 5, PowerDrop: 15, RangedDamage: 8, MeleeDamage: 0, ProjectileSpeed: 320f, Look: EnemyLook.Imp);
    /// <summary>Melee only; harmless at range but hurts on contact.</summary>
    public static readonly EnemyDef Brute = new(Health: 90, PointDrop: 15, PowerDrop: 30, RangedDamage: 0, MeleeDamage: 20, AttackType: EnemyAttackTypes.Melee, Look: EnemyLook.Brute);
    /// <summary>Bullet sponge; slow shots, big payout.</summary>
    public static readonly EnemyDef Tank = new(Health: 180, PointDrop: 30, PowerDrop: 40, RangedDamage: 15, MeleeDamage: 0, ProjectileSpeed: 220f, Look: EnemyLook.Tank);
    /// <summary>Fast, hard-hitting shots on a normal body.</summary>
    public static readonly EnemyDef Sniper = new(Health: 50, PointDrop: 20, PowerDrop: 25, RangedDamage: 25, MeleeDamage: 0, ProjectileSpeed: 420f, Look: EnemyLook.Sniper);
    /// <summary>Shoots and hits back in melee; the stage-closer.</summary>
    public static readonly EnemyDef Warlord = new(Health: 250, PointDrop: 50, PowerDrop: 60, RangedDamage: 20, MeleeDamage: 30, AttackType: EnemyAttackTypes.Both, Look: EnemyLook.Warlord);
    /// <summary>Weak, but killing it fills a big chunk of the ultimate bar.</summary>
    public static readonly EnemyDef Wisp = new(Health: 20, PointDrop: 5, PowerDrop: 60, RangedDamage: 5, MeleeDamage: 0, ProjectileSpeed: 300f, Look: EnemyLook.Wisp);
}

/// <summary>What a stage throws at the player. Add fields here (layout, spawn cadence, ...) rather than special-casing in Game.</summary>
/// <param name="MaxAtOnce">How many demons may be alive at the same time.</param>
/// <param name="Enemies">Every demon the stage spawns, in spawn order; the stage is cleared when all of them are dead.</param>
record StageDef(int MaxAtOnce, EnemyDef[] Enemies)
{
    public int TotalEnemies => Enemies.Length;
}

/// <summary>The stage list; stage numbers are 1-based to match what the player sees.</summary>
static class Stages
{
    /// <summary>Expands (variant, count) groups into the flat spawn list, so a stage reads as a roster rather than a wall of repeats.</summary>
    static EnemyDef[] Roster(params (EnemyDef Kind, int Count)[] groups) =>
        groups.SelectMany(g => Enumerable.Repeat(g.Kind, g.Count)).ToArray();

    static readonly StageDef[] All =
    [
        new(MaxAtOnce: 10, Roster((Enemies.Grunt, 3), (Enemies.Brute, 4), (Enemies.Sniper, 4), (Enemies.Tank, 8), (Enemies.Wisp, 2))),
        new(MaxAtOnce: 2, Roster((Enemies.Grunt, 3))),
        new(MaxAtOnce: 2, Roster((Enemies.Imp, 2), (Enemies.Grunt, 2))),
        new(MaxAtOnce: 3, Roster((Enemies.Imp, 3), (Enemies.Grunt, 2), (Enemies.Brute, 1))),
        new(MaxAtOnce: 3, Roster((Enemies.Imp, 3), (Enemies.Grunt, 3), (Enemies.Brute, 2))),
        new(MaxAtOnce: 3, Roster((Enemies.Grunt, 4), (Enemies.Brute, 2), (Enemies.Sniper, 2), (Enemies.Wisp, 2))),
        new(MaxAtOnce: 4, Roster((Enemies.Imp, 4), (Enemies.Grunt, 4), (Enemies.Sniper, 2), (Enemies.Tank, 2))),
        new(MaxAtOnce: 4, Roster((Enemies.Grunt, 5), (Enemies.Brute, 3), (Enemies.Sniper, 3), (Enemies.Tank, 2), (Enemies.Wisp, 1))),
        new(MaxAtOnce: 4, Roster((Enemies.Imp, 4), (Enemies.Grunt, 4), (Enemies.Brute, 3), (Enemies.Sniper, 3), (Enemies.Tank, 2))),
        new(MaxAtOnce: 5, Roster((Enemies.Grunt, 6), (Enemies.Brute, 4), (Enemies.Sniper, 4), (Enemies.Tank, 3), (Enemies.Wisp, 2), (Enemies.Warlord, 1))),
    ];

    public static int Count => All.Length;
    public static bool IsLast(int stage) => stage >= Count;

    /// <summary>Stages past the end reuse the last one, so a stale save can never index out of range.</summary>
    public static StageDef Get(int stage) => All[Math.Clamp(stage, 1, Count) - 1];
}
