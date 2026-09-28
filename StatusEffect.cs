/// <summary>Conditions an enemy can be under. Values index the per-enemy status arrays, so keep them 0-based and dense.</summary>
enum StatusEffect { Burning, Frozen, Shocked, Poisoned, Stunned, DamageOverTime }

static class StatusEffects
{
    public const int Count = 6;
}
