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
/// Focus mode: F toggles it on/off; while on, each click zooms the camera onto that spot for <see cref="Duration"/>
/// seconds and then eases back out to the full view. Toggling it off cancels any focus in progress.
/// </summary>
static class CameraFocus
{
    public const float Duration = 1.5f;
    public const float Zoom = 2f; // multiplier over the fit-to-window zoom
    const float EaseTime = 0.3f; // seconds spent zooming in at the start and back out at the end

    public static bool Enabled { get; private set; }
    public static Vector2 Point { get; private set; }
    static float timeLeft;

    /// <summary>0 = full view, 1 = fully zoomed onto <see cref="Point"/>; eases in and out at the ends of the focus.</summary>
    public static float Amount
    {
        get
        {
            if (timeLeft <= 0) return 0;
            float elapsed = Duration - timeLeft;
            float t = Math.Clamp(Math.Min(elapsed, timeLeft) / EaseTime, 0f, 1f);
            return t * t * (3f - 2f * t); // smoothstep
        }
    }

    public static void Update(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            Enabled = !Enabled;
            timeLeft = 0;
        }
        if (Enabled && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Point = World.MousePosition();
            timeLeft = Duration;
        }
        timeLeft = Math.Max(0, timeLeft - dt);
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
