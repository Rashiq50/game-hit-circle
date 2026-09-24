enum DropKind { Coin, Health, Power }
record DropDef(DropKind Kind, float Amount);
//Chance (0..1) that a kill drops
record LootChance(DropDef Drop, float Chance);

static class Drops
{
    public static DropDef Coin(int points) => new(DropKind.Coin, points);
    public static readonly DropDef SmallHealth = new(DropKind.Health, 15);
    public static readonly DropDef BigHealth = new(DropKind.Health, 40);
    public static readonly DropDef PowerSurge = new(DropKind.Power, 50);
}
