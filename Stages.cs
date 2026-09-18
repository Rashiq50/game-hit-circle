/// <summary>What a stage throws at the player. Add fields here (demon types, layout, ...) rather than special-casing in Game.</summary>
record StageDef(int DemonCount);

/// <summary>The stage list; stage numbers are 1-based to match what the player sees.</summary>
static class Stages
{
    // Placeholder curve for testing: 3 demons on stage 1 ramping to 20 on stage 10.
    static readonly StageDef[] All =
    [
        new(3), new(4), new(6), new(8), new(10),
        new(12), new(14), new(16), new(18), new(20),
    ];

    public static int Count => All.Length;
    public static bool IsLast(int stage) => stage >= Count;

    /// <summary>Stages past the end reuse the last one, so a stale save can never index out of range.</summary>
    public static StageDef Get(int stage) => All[Math.Clamp(stage, 1, Count) - 1];
}
