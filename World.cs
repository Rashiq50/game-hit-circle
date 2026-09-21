using System.Numerics;
using Raylib_cs;

static class World
{
    public const int Tile = 32;
    public const int Width = 125 * Tile;
    public const int Height = 75 * Tile;
    public const int ViewWidth = 28 * Tile;
    public const int ViewHeight = 16 * Tile;
    const int EnemySpawnWidth = 62 * Tile;
    const int EnemySpawnHeight = 38 * Tile;
    const float FollowRate = 6f; // how quickly the camera closes on the player, per second; higher is tighter
    const int SpawnMargin = 50;
    const int SpawnClearance = 60;

    public static readonly Vector2 Center = new(Width / 2f, Height / 2f);
    public static Camera2D Camera = new() { Target = Center, Zoom = 1f };
    static Vector2 followPoint = Center; // where the follow camera is looking right now; eases toward the player

    public static void Follow(Vector2 point, float dt) =>
        followPoint = Vector2.Lerp(followPoint, point, 1f - MathF.Exp(-FollowRate * dt));

    public static void SnapTo(Vector2 point) => followPoint = point;

    public static void FitCamera(Vector2 shake, bool follow)
    {
        Camera.Offset = new Vector2(Screen.Width / 2f, Screen.Height / 2f);
        float fitZoom = follow
            ? Math.Min(Screen.Width / (float)ViewWidth, Screen.Height / (float)ViewHeight)
            : Math.Min(Screen.Width / (float)Width, Screen.Height / (float)Height);

        float focus = CameraFocus.Amount;
        Camera.Zoom = fitZoom * float.Lerp(1f, CameraFocus.Zoom, focus);

        var halfView = new Vector2(Screen.Width, Screen.Height) / (2f * Camera.Zoom);
        Vector2 target = Vector2.Lerp(follow ? followPoint : Center, CameraFocus.Point, focus);
        Camera.Target = ClampToWorld(target, halfView) + shake;
    }

    static Vector2 ClampToWorld(Vector2 target, Vector2 halfView) => new(
        halfView.X >= Width / 2f ? Center.X : Math.Clamp(target.X, halfView.X, Width - halfView.X),
        halfView.Y >= Height / 2f ? Center.Y : Math.Clamp(target.Y, halfView.Y, Height - halfView.Y));

    /// <summary>Mouse position in world units, for hit tests against world-space objects.</summary>
    public static Vector2 MousePosition() => Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), Camera);

    public static Vector2 RandomEnemySpawn(Rectangle avoid, int attempts = 10)
    {
        var spawns = CollisionMap.EnemySpawns;
        if (spawns.Count == 0) return RandomPoint();
        Vector2 p = spawns[Random.Shared.Next(spawns.Count)];
        for (int i = 0; i < attempts && Raylib.CheckCollisionRecs(new Rectangle(p.X - SpawnClearance / 2f, p.Y - SpawnClearance / 2f, SpawnClearance, SpawnClearance), avoid); i++)
            p = spawns[Random.Shared.Next(spawns.Count)];
        return p;
    }

    public static Vector2 RandomPoint()
    {
        Vector2 p;
        do
        {
            p = new(Random.Shared.Next(SpawnMargin, EnemySpawnWidth - SpawnMargin),
                    Random.Shared.Next(SpawnMargin, EnemySpawnHeight - SpawnMargin));
        } while (CollisionMap.Blocks(new Rectangle(p.X - SpawnClearance / 2f, p.Y - SpawnClearance / 2f, SpawnClearance, SpawnClearance)));
        return p;
    }
}

static class CameraFocus
{
    public const float Zoom = 2f; // multiplier over the fit-to-window zoom
    const float EaseTime = 0.35f; // seconds to zoom fully in, and again to zoom fully out

    public static Vector2 Point { get; private set; }
    static bool holding;
    static float level;

    public static float Amount => level * level * (3f - 2f * level);
    public static bool IsSettled => level >= 1f || (!holding && level <= 0f);

    public static void Focus(Vector2 point)
    {
        Point = point;
        holding = true;
    }

    public static void Release() => holding = false;

    public static void Update(float dt)
    {
        float step = dt / EaseTime;
        level = holding ? Math.Min(1f, level + step) : Math.Max(0f, level - step);
    }
}

///The actual window, in pixels. Only UI (HUD, menus) should position by this
static class Screen
{
    public static int Width => Raylib.GetScreenWidth();
    public static int Height => Raylib.GetScreenHeight();

    public static void DrawCenteredText(string text, int y, int fontSize, Color color)
    {
        int width = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Width / 2 - width / 2, y, fontSize, color);
    }
}
