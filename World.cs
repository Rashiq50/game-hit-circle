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

    /// <summary>Largest uniform zoom that keeps the whole world visible, centred in the window.</summary>
    public static void FitCamera(Vector2 shake)
    {
        Camera.Zoom = Math.Min(Screen.Width / (float)Width, Screen.Height / (float)Height);
        Camera.Offset = new Vector2(Screen.Width / 2f, Screen.Height / 2f);
        Camera.Target = Center + shake;
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
