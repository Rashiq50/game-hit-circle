using System.Numerics;
using Raylib_cs;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(1000, 600, "Hit them all!");
Raylib.SetTargetFPS(60);

var game = new Game();

while (!Raylib.WindowShouldClose() && !game.QuitRequested)
{
    game.Update(Raylib.GetFrameTime());

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    game.Draw();
    Raylib.EndDrawing();
}

Raylib.CloseWindow();

enum GameState { Welcome, Playing, GameOver }

static class Screen
{
    const int SpawnMargin = 50;

    public static int Width => Raylib.GetScreenWidth();
    public static int Height => Raylib.GetScreenHeight();

    public static Vector2 RandomPoint() => new(
        Random.Shared.Next(SpawnMargin, Width - SpawnMargin),
        Random.Shared.Next(SpawnMargin, Height - SpawnMargin));

    public static void DrawCenteredText(string text, int y, int fontSize, Color color)
    {
        int width = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Width / 2 - width / 2, y, fontSize, color);
    }
}

/// <summary>The player-controlled cube (WASD to move, Left Shift to boost).</summary>
class Player
{
    public const int Size = 40;
    const float Speed = 100f;
    const float BoostMultiplier = 2.5f;

    public Vector2 Position = Screen.RandomPoint();

    public void Update(float dt)
    {
        float speed = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? Speed * BoostMultiplier : Speed;
        float step = speed * dt;

        if (Raylib.IsKeyDown(KeyboardKey.D)) Position.X += step;
        if (Raylib.IsKeyDown(KeyboardKey.A)) Position.X -= step;
        if (Raylib.IsKeyDown(KeyboardKey.W)) Position.Y -= step;
        if (Raylib.IsKeyDown(KeyboardKey.S)) Position.Y += step;

        Position.X = Math.Clamp(Position.X, 0, Screen.Width - Size);
        Position.Y = Math.Clamp(Position.Y, 0, Screen.Height - Size);
    }

    public void Draw() => Raylib.DrawRectangleV(Position, new Vector2(Size, Size), Color.DarkBlue);
}

/// <summary>The circle the player has to hit, either by clicking it or by touching it with the cube.</summary>
class Target
{
    const float FullRadius = 25;
    const float ShrinkTime = 0.2f; // seconds
    const int RespawnAttemptCap = 10;

    public Vector2 Center;
    public float Radius = FullRadius;
    public bool IsShrinking { get; private set; }
    float shrinkElapsed;

    /// <summary>Uses the circle's bounding square, matching the original hit test.</summary>
    public bool ContainsPoint(Vector2 p) =>
        p.X >= Center.X - Radius && p.X <= Center.X + Radius &&
        p.Y >= Center.Y - Radius && p.Y <= Center.Y + Radius;

    public bool Overlaps(Player player) => Overlaps(Center, player);

    bool Overlaps(Vector2 center, Player player) =>
        player.Position.X <= center.X + Radius && player.Position.X + Player.Size >= center.X - Radius &&
        player.Position.Y <= center.Y + Radius && player.Position.Y + Player.Size >= center.Y - Radius;

    /// <summary>Moves the circle to a random spot, trying a few times to avoid spawning on top of the player.</summary>
    public void Respawn(Player player)
    {
        Vector2 candidate = Screen.RandomPoint();
        for (int attempt = 0; attempt <= RespawnAttemptCap && Overlaps(candidate, player); attempt++)
            candidate = Screen.RandomPoint();

        Center = candidate;
        Radius = FullRadius;
        IsShrinking = false;
        shrinkElapsed = 0;
    }

    public void StartShrinking() => IsShrinking = true;

    /// <summary>Advances the shrink animation. Returns true on the frame the animation finishes.</summary>
    public bool UpdateShrink(float dt)
    {
        if (!IsShrinking) return false;

        if (shrinkElapsed <= ShrinkTime)
        {
            Radius *= 1 - shrinkElapsed / ShrinkTime;
            shrinkElapsed += dt;
            return false;
        }

        return true;
    }

    public void Draw() => Raylib.DrawCircleV(Center, Radius, Color.Beige);
}

/// <summary>Floating "+10" text that scales in, holds, fades out and drifts upward.</summary>
class ScorePopup
{
    const float ScaleTime = 0.15f;
    const float HoldTime = 0.5f;
    const float FadeTime = 0.3f;
    const float DriftSpeed = 20f; // pixels per second
    const int FontSize = 24;
    const string Text = "+10";

    bool active;
    Vector2 origin;
    float elapsed;

    public void Show(Vector2 at)
    {
        active = true;
        origin = at;
        elapsed = 0;
    }

    public void Update(float dt)
    {
        if (!active) return;
        elapsed += dt;
        if (elapsed >= ScaleTime + HoldTime + FadeTime) active = false;
    }

    public void Draw()
    {
        if (!active) return;

        float scale = elapsed < ScaleTime ? elapsed / ScaleTime : 1;
        float alpha = elapsed < ScaleTime + HoldTime ? 1 : 1 - (elapsed - ScaleTime - HoldTime) / FadeTime;

        int fontSize = Math.Max(1, (int)(FontSize * scale));
        int width = Raylib.MeasureText(Text, fontSize);
        float drift = elapsed * DriftSpeed;
        Raylib.DrawText(Text,
            (int)(origin.X - width / 2f),
            (int)(origin.Y - fontSize / 2f - drift),
            fontSize,
            Raylib.Fade(Color.Green, alpha));
    }
}

class Game
{
    const int PlayTime = 5; // seconds per round, extended by leftover time on each hit
    const int PointsPerHit = 10;

    readonly Player player = new();
    readonly Target target = new();
    readonly ScorePopup popup = new();

    GameState state = GameState.Welcome;
    int score;
    double endTime;
    int bonusTime;

    public bool QuitRequested { get; private set; }

    public void Update(float dt)
    {
        switch (state)
        {
            case GameState.Welcome:
                if (AnyInputPressed()) StartRound();
                break;

            case GameState.Playing:
                UpdatePlaying(dt);
                break;

            case GameState.GameOver:
                if (Raylib.IsKeyDown(KeyboardKey.R)) StartRound();
                if (Raylib.IsKeyDown(KeyboardKey.Q)) QuitRequested = true;
                break;
        }
    }

    public void Draw()
    {
        switch (state)
        {
            case GameState.Welcome:
                DrawWelcome();
                break;

            case GameState.Playing:
                player.Draw();
                target.Draw();
                popup.Draw();
                DrawHud();
                break;

            case GameState.GameOver:
                DrawGameOver();
                break;
        }
    }

    static bool AnyInputPressed() =>
        Raylib.GetKeyPressed() != 0
        || Raylib.IsMouseButtonPressed(MouseButton.Left)
        || Raylib.IsMouseButtonPressed(MouseButton.Right)
        || Raylib.IsMouseButtonPressed(MouseButton.Middle);

    void StartRound()
    {
        state = GameState.Playing;
        endTime = Raylib.GetTime() + PlayTime;
        target.Respawn(player);
    }

    void UpdatePlaying(float dt)
    {
        if (Raylib.GetTime() > endTime)
        {
            state = GameState.GameOver;
            return;
        }

        player.Update(dt);
        popup.Update(dt);

        bool clickedTarget = Raylib.IsMouseButtonPressed(MouseButton.Left) && target.ContainsPoint(Raylib.GetMousePosition());
        if (!target.IsShrinking && (clickedTarget || target.Overlaps(player))) OnHit();

        if (target.UpdateShrink(dt))
        {
            popup.Show(target.Center);
            target.Respawn(player);
            endTime = Raylib.GetTime() + PlayTime + bonusTime;
            bonusTime = 0;
        }
    }

    void OnHit()
    {
        score += PointsPerHit;
        bonusTime = (int)endTime - (int)Raylib.GetTime();
        target.StartShrinking();
    }

    static void DrawWelcome()
    {
        const int fontSize = 48;
        Screen.DrawCenteredText("Press any key to continue", Screen.Height / 2 - fontSize / 2, fontSize, Color.Gray);
    }

    static void DrawGameOver()
    {
        const int titleSize = 62;
        const int optionSize = 28;

        Screen.DrawCenteredText("Game Over!", Screen.Height / 2 - titleSize / 2, titleSize, Color.Red);

        int optionY = Screen.Height / 2 + titleSize / 2 + 20;
        Screen.DrawCenteredText("[R] Replay", optionY, optionSize, Color.Gray);
        Screen.DrawCenteredText("[Q] Quit", optionY + optionSize + 10, optionSize, Color.Gray);
    }

    void DrawHud()
    {
        int timeLeft = (int)(endTime - Raylib.GetTime());
        Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
        Raylib.DrawText($"Time: {timeLeft:D2}", 20, 40, 16, Color.White);
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
    }
}
