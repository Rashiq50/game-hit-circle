record EnemyDef(
    float Health,
    int PointDrop,
    float PowerDrop,
    float RangedDamage,
    float MeleeDamage,
    EnemyAttackType AttackType = EnemyAttackType.Ranged,
    float ProjectileSpeed = 280f,
    float FireRange = Enemy.DefaultFireRange,
    float MoveSpeed = 0f,
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

/// <summary>The maps in textures/stages/, one .png + .tmx pair each, smallest first.</summary>
static class Maps
{
    public const string ForestGlade = "forest_glade";
    public const string DesertRuins = "desert_ruins";
    public const string SwampBog = "swamp_bog";
    public const string FrozenCavern = "frozen_cavern";
    public const string Graveyard = "graveyard";
    public const string VolcanicCaldera = "volcanic_caldera";
    public const string CastleFloor = "castle_floor";
}

record StageDef(string Map, int MaxAtOnce, EnemyDef[] Enemies, LootChance[] Loot)
{
    public int TotalEnemies => Enemies.Length;
}

static class Stages
{
    static EnemyDef[] Roster(params (EnemyDef Kind, int Count)[] groups) =>
    [
        .. groups.SelectMany(g => Enumerable.Range(0, g.Count).Select(i => (g.Kind, At: (i + 0.5f) / g.Count)))
                 .OrderBy(e => e.At)
                 .Select(e => e.Kind),
    ];

    static LootChance[] Loot(params (DropDef Drop, float Chance)[] entries) =>
        entries.Select(e => new LootChance(e.Drop, e.Chance)).ToArray();

    static readonly StageDef[] All =
    [
        new(Maps.ForestGlade, MaxAtOnce: 4, Roster((Enemies.Brute, 7), (Enemies.Tank, 2), (Enemies.Wisp, 1)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.BigHealth, 0.15f), (Drops.PowerSurge, 0.02f))),
        new(Maps.ForestGlade, MaxAtOnce: 5, Roster((Enemies.Brute, 8), (Enemies.Tank, 4), (Enemies.Imp, 1), (Enemies.Wisp, 1)),
            Loot((Drops.SmallHealth, 0.40f))),
        new(Maps.DesertRuins, MaxAtOnce: 8, Roster((Enemies.Brute, 9), (Enemies.Tank, 5), (Enemies.Imp, 2), (Enemies.Grunt, 1), (Enemies.Wisp, 1)),
            Loot((Drops.SmallHealth, 0.40f))),
        new(Maps.DesertRuins, MaxAtOnce: 10, Roster((Enemies.Brute, 11), (Enemies.Tank, 5), (Enemies.Imp, 3), (Enemies.Grunt, 2), (Enemies.Wisp, 1)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.PowerSurge, 0.02f))),
        new(Maps.SwampBog, MaxAtOnce: 8, Roster((Enemies.Brute, 12), (Enemies.Tank, 6), (Enemies.Imp, 3), (Enemies.Grunt, 3), (Enemies.Wisp, 2)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.PowerSurge, 0.02f))),
        new(Maps.SwampBog, MaxAtOnce: 9, Roster((Enemies.Brute, 12), (Enemies.Tank, 6), (Enemies.Imp, 3), (Enemies.Grunt, 4), (Enemies.Sniper, 1), (Enemies.Wisp, 2)),
            Loot((Drops.SmallHealth, 0.40f), (Drops.BigHealth, 0.03f), (Drops.PowerSurge, 0.02f))),
        new(Maps.FrozenCavern, MaxAtOnce: 14, Roster((Enemies.Brute, 13), (Enemies.Tank, 6), (Enemies.Imp, 4), (Enemies.Grunt, 5), (Enemies.Sniper, 2), (Enemies.Wisp, 2)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.03f), (Drops.PowerSurge, 0.02f))),
        new(Maps.Graveyard, MaxAtOnce: 30, Roster((Enemies.Brute, 21), (Enemies.Tank, 10), (Enemies.Imp, 7), (Enemies.Grunt, 9), (Enemies.Sniper, 5), (Enemies.Wisp, 4)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.04f), (Drops.PowerSurge, 0.03f))),
        new(Maps.VolcanicCaldera, MaxAtOnce: 38, Roster((Enemies.Brute, 24), (Enemies.Tank, 11), (Enemies.Imp, 9), (Enemies.Grunt, 12), (Enemies.Sniper, 8), (Enemies.Wisp, 4)),
            Loot((Drops.SmallHealth, 0.15f), (Drops.BigHealth, 0.05f), (Drops.PowerSurge, 0.03f))),
        new(Maps.CastleFloor, MaxAtOnce: 40, [.. Roster((Enemies.Brute, 28), (Enemies.Tank, 13), (Enemies.Imp, 9), (Enemies.Grunt, 14), (Enemies.Sniper, 11), (Enemies.Wisp, 5)), Enemies.Warlord],
            Loot((Drops.SmallHealth, 0.18f), (Drops.BigHealth, 0.06f), (Drops.PowerSurge, 0.03f))),
    ];

    public static int Count => All.Length;
    public static bool IsLast(int stage) => stage >= Count;

    public static StageDef Get(int stage) => All[Math.Clamp(stage, 1, Count) - 1];
}
