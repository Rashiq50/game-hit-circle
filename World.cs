using System.Numerics;
using Raylib_cs;

/// <summary>
/// The play area in fixed virtual units: the whole castle floor, matching the background image and the Tiled map
/// pixel for pixel. Gameplay code positions everything in this space and never looks at the window size; the camera
/// shows a <see cref="ViewWidth"/> x <see cref="ViewHeight"/> window onto it that follows the player.
/// </summary>
static class World
{
    public const int Tile = 32; // canonical tile size; walls and doors in the map sit on this grid
    public const int Width = 125 * Tile;
    public const int Height = 75 * Tile;
    /// <summary>How much of the world is on screen at once (32 x 20 tiles). Smaller = closer camera, bigger sprites.</summary>
    public const int ViewWidth = 28 * Tile;
    public const int ViewHeight = 16 * Tile;
    const float FollowRate = 6f; // how quickly the camera closes on the player, per second; higher is tighter
    const int SpawnMargin = 50;
    const int SpawnClearance = 60; // spawn box must fit the player (40) and demon (50) hit boxes

    public static readonly Vector2 Center = new(Width / 2f, Height / 2f);
    public static Camera2D Camera = new() { Target = Center, Zoom = 1f };
    static Vector2 followPoint = Center; // where the follow camera is looking right now; eases toward the player

    /// <summary>Eases the camera toward <paramref name="point"/> (the player); frame-rate independent.</summary>
    public static void Follow(Vector2 point, float dt) =>
        followPoint = Vector2.Lerp(followPoint, point, 1f - MathF.Exp(-FollowRate * dt));

    /// <summary>Puts the camera straight onto <paramref name="point"/>, for spawns and restarts so it never pans across the map.</summary>
    public static void SnapTo(Vector2 point) => followPoint = point;

    /// <summary>
    /// With <paramref name="follow"/> the camera shows a view-sized window centred on the follow point, never reaching past
    /// the world's edges (which would show black); otherwise it fits the whole world in the window (menus). While a
    /// <see cref="CameraFocus"/> is running the view zooms in and pans onto the focus point instead.
    /// </summary>
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

    /// <summary>A random spot that leaves a <see cref="SpawnClearance"/> box around it clear of walls.</summary>
    public static Vector2 RandomPoint()
    {
        Vector2 p;
        do
        {
            p = new(Random.Shared.Next(SpawnMargin, Width - SpawnMargin),
                    Random.Shared.Next(SpawnMargin, Height - SpawnMargin));
        } while (CollisionMap.Blocks(new Rectangle(p.X - SpawnClearance / 2f, p.Y - SpawnClearance / 2f, SpawnClearance, SpawnClearance)));
        return p;
    }
}

/// <summary>
/// Cinematic camera zoom: <see cref="Focus"/> eases the camera in onto a point and holds there until <see cref="Release"/>,
/// which eases it back out to the full view. <see cref="World.FitCamera"/> reads <see cref="Amount"/> to blend the two.
/// </summary>
static class CameraFocus
{
    public const float Zoom = 2f; // multiplier over the fit-to-window zoom
    const float EaseTime = 0.35f; // seconds to zoom fully in, and again to zoom fully out

    public static Vector2 Point { get; private set; }
    static bool holding;
    static float level; // 0 = full view, 1 = fully zoomed; moves toward the hold state at 1/EaseTime per second

    /// <summary>0 = full view, 1 = fully zoomed onto <see cref="Point"/>, smoothed so the camera glides.</summary>
    public static float Amount => level * level * (3f - 2f * level);
    /// <summary>True once the zoom-in has finished (or if nothing is being focused).</summary>
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

/// <summary>The actual window, in pixels. Only UI (HUD, menus) should position by this.</summary>
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
