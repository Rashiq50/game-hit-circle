/// <summary>One enemy as a stage defines it; mirrors <see cref="Enemy"/>'s constructor so a stage can spawn it directly.</summary>
record EnemyDef(
    float Health,
    int PointDrop,
    float PowerDrop,
    float RangedDamage,
    float MeleeDamage,
    EnemyAttackType AttackType = EnemyAttackType.Ranged,
    float ProjectileSpeed = 280f,
    float FireRange = Enemy.DefaultFireRange,
    float MoveSpeed = 0f, // 0 = holds its position; melee types need this to close the distance
    EnemyLook Look = EnemyLook.Sprite)
{
    public Enemy Spawn() => new(PowerDrop, PointDrop, RangedDamage, MeleeDamage, Health, AttackType, ProjectileSpeed, FireRange, MoveSpeed, Look);
}

static class Enemies
{
    public static readonly EnemyDef Grunt = new(Health: 60, PointDrop: 10, PowerDrop: 25, RangedDamage: 12, MeleeDamage: 0);
    public static readonly EnemyDef Imp = new(Health: 30, PointDrop: 5, PowerDrop: 15, RangedDamage: 8, MeleeDamage: 0, ProjectileSpeed: 220f, FireRange: 300f, Look: EnemyLook.Imp);
    public static readonly EnemyDef Brute = new(Health: 90, PointDrop: 15, PowerDrop: 30, RangedDamage: 0, MeleeDamage: 15, AttackType: EnemyAttackType.Melee, MoveSpeed: 100f, Look: EnemyLook.Brute);
    public static readonly EnemyDef Tank = new(Health: 180, PointDrop: 30, PowerDrop: 40, RangedDamage: 0, MeleeDamage: 5, AttackType: EnemyAttackType.Melee, MoveSpeed: 80f, Look: EnemyLook.Tank);
    public static readonly EnemyDef Sniper = new(Health: 40, PointDrop: 20, PowerDrop: 25, RangedDamage: 25, MeleeDamage: 0, ProjectileSpeed: 420f, FireRange: 800f, Look: EnemyLook.Sniper);
    public static readonly EnemyDef Warlord = new(Health: 250, PointDrop: 50, PowerDrop: 60, RangedDamage: 20, MeleeDamage: 30, AttackType: EnemyAttackType.Both, FireRange: 500f, MoveSpeed: 100f, Look: EnemyLook.Warlord);
    public static readonly EnemyDef Wisp = new(Health: 20, PointDrop: 5, PowerDrop: 60, RangedDamage: 5, MeleeDamage: 0, ProjectileSpeed: 200f, FireRange: 250f, Look: EnemyLook.Wisp);
}

record StageDef(int MaxAtOnce, EnemyDef[] Enemies, LootChance[] Loot)
{
    public int TotalEnemies => Enemies.Length;
}

static class Stages
{
    static EnemyDef[] Roster(params (EnemyDef Kind, int Count)[] groups) =>
        groups.SelectMany(g => Enumerable.Repeat(g.Kind, g.Count)).ToArray();

    static LootChance[] Loot(params (DropDef Drop, float Chance)[] entries) =>
        entries.Select(e => new LootChance(e.Drop, e.Chance)).ToArray();

    static readonly StageDef[] All =
    [
        new(MaxAtOnce: 30, Roster((Enemies.Grunt, 10), (Enemies.Brute, 15), (Enemies.Sniper, 5), (Enemies.Tank, 10), (Enemies.Wisp, 12)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.BigHealth, 0.15f), (Drops.PowerSurge, 0.02f))),
        new(MaxAtOnce: 2, Roster((Enemies.Grunt, 3)),
            Loot((Drops.SmallHealth, 0.40f))),
        new(MaxAtOnce: 2, Roster((Enemies.Imp, 2), (Enemies.Grunt, 2)),
            Loot((Drops.SmallHealth, 0.40f))),
        new(MaxAtOnce: 3, Roster((Enemies.Imp, 3), (Enemies.Grunt, 2), (Enemies.Brute, 1)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.PowerSurge, 0.02f))),
        new(MaxAtOnce: 3, Roster((Enemies.Imp, 3), (Enemies.Grunt, 3), (Enemies.Brute, 2)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.PowerSurge, 0.02f))),
        new(MaxAtOnce: 3, Roster((Enemies.Grunt, 4), (Enemies.Brute, 2), (Enemies.Sniper, 2), (Enemies.Wisp, 2)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.BigHealth, 0.03f), (Drops.PowerSurge, 0.02f))),
        new(MaxAtOnce: 4, Roster((Enemies.Imp, 4), (Enemies.Grunt, 4), (Enemies.Sniper, 2), (Enemies.Tank, 2)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.03f), (Drops.PowerSurge, 0.02f))),
        new(MaxAtOnce: 4, Roster((Enemies.Grunt, 5), (Enemies.Brute, 3), (Enemies.Sniper, 3), (Enemies.Tank, 2), (Enemies.Wisp, 1)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.04f), (Drops.PowerSurge, 0.03f))),
        new(MaxAtOnce: 4, Roster((Enemies.Imp, 4), (Enemies.Grunt, 4), (Enemies.Brute, 3), (Enemies.Sniper, 3), (Enemies.Tank, 2)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.05f), (Drops.PowerSurge, 0.03f))),
        new(MaxAtOnce: 5, Roster((Enemies.Grunt, 6), (Enemies.Brute, 4), (Enemies.Sniper, 4), (Enemies.Tank, 3), (Enemies.Wisp, 2), (Enemies.Warlord, 1)),
            Loot((Drops.SmallHealth, 0.18f), (Drops.BigHealth, 0.06f), (Drops.PowerSurge, 0.03f))),
    ];

    public static int Count => All.Length;
    public static bool IsLast(int stage) => stage >= Count;

    /// <summary>Stages past the end reuse the last one, so a stale save can never index out of range.</summary>
    public static StageDef Get(int stage) => All[Math.Clamp(stage, 1, Count) - 1];
}
