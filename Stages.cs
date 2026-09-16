/// <summary>What a stage throws at the player. Add fields here (demon types, layout, ...) rather than special-casing in Game.</summary>
record StageDef(int DemonCount, string DemonType);

/// <summary>The stage list; stage numbers are 1-based to match what the player sees.</summary>
static class Stages
{
    // Placeholder curve for testing: 3 demons on stage 1 ramping to 20 on stage 10.
    static readonly StageDef[] All =
    [
        new(3, "melee"), new(4, "melee"), new(6, "melee"), new(8, "melee"), new(10, "melee"),
        new(12, "melee"), new(14, "melee"), new(16, "melee"), new(18, "melee"), new(20, "melee"),
    ];

    public static int Count => All.Length;
    public static bool IsLast(int stage) => stage >= Count;

    /// <summary>Stages past the end reuse the last one, so a stale save can never index out of range.</summary>
    public static StageDef Get(int stage) => All[Math.Clamp(stage, 1, Count) - 1];
}
