using System.Numerics;
using Raylib_cs;

/// <summary>
/// The play area in fixed virtual units. Gameplay code positions everything in this space and never looks at the
/// window size; the camera scales the world to fit the window (letterboxed) so coordinates in stage data stay valid.
/// </summary>
static class World
{
    public const int Width = 1000;
    public const int Height = 600;
    const int SpawnMargin = 50;
    const int SpawnClearance = 60; // spawn box must fit the player (40) and demon (50) hit boxes

    public static readonly Vector2 Center = new(Width / 2f, Height / 2f);
    public static Camera2D Camera = new() { Target = Center, Zoom = 1f };

    /// <summary>
    /// Largest uniform zoom that keeps the whole world visible, centred in the window. While a <see cref="CameraFocus"/>
    /// is running the view zooms in and pans onto the focus point instead, keeping the edges of the world off-screen.
    /// </summary>
    public static void FitCamera(Vector2 shake)
    {
        float fitZoom = Math.Min(Screen.Width / (float)Width, Screen.Height / (float)Height);
        Camera.Offset = new Vector2(Screen.Width / 2f, Screen.Height / 2f);

        float focus = CameraFocus.Amount;
        Camera.Zoom = fitZoom * float.Lerp(1f, CameraFocus.Zoom, focus);

        // Clamp so the zoomed-in view never reaches past the world's edges (which would show black).
        var halfView = new Vector2(Screen.Width, Screen.Height) / (2f * Camera.Zoom);
        var focusTarget = new Vector2(
            halfView.X >= Width / 2f ? Center.X : Math.Clamp(CameraFocus.Point.X, halfView.X, Width - halfView.X),
            halfView.Y >= Height / 2f ? Center.Y : Math.Clamp(CameraFocus.Point.Y, halfView.Y, Height - halfView.Y));
        Camera.Target = Vector2.Lerp(Center, focusTarget, focus) + shake;
    }

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
