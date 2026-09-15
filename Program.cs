using System.Numerics;
using Raylib_cs;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(1000, 600, "Hit them all!");
Raylib.SetTargetFPS(60);

Assets.Load(); // must come after InitWindow: raylib needs a GL context to upload textures
var game = new Game();

while (!Raylib.WindowShouldClose() && !game.QuitRequested)
{
    game.Update(Raylib.GetFrameTime());

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    game.Draw();
    Raylib.EndDrawing();
}

Assets.Unload();
Raylib.CloseWindow();

enum Direction { Down, Up, Left, Right }
enum GameState { Welcome, Playing, GameOver }
enum PlayerState { Idle, Walking, Running, Attacking }
enum DemonState { Idle, Dying }

/// <summary>A horizontal sprite sheet of square frames, plus the frame arithmetic every animation needs.</summary>
readonly record struct SpriteStrip(Texture2D Texture, int FrameSize)
{
    public int FrameCount => Texture.Width / FrameSize;

    /// <summary>Frame for a looping animation after <paramref name="elapsed"/> seconds.</summary>
    public int LoopFrame(float elapsed, float fps) => (int)(elapsed * fps) % FrameCount;

    /// <summary>Frame for a play-once animation spread over <paramref name="duration"/> seconds; holds the last frame afterwards.</summary>
    public int OneShotFrame(float elapsed, float duration) =>
        Math.Min((int)(elapsed / duration * FrameCount), FrameCount - 1);

    public void Draw(int frame, Vector2 center, float drawSize) =>
        Raylib.DrawTexturePro(Texture,
            new Rectangle(frame * FrameSize, 0, FrameSize, FrameSize),
            new Rectangle(center.X - drawSize / 2, center.Y - drawSize / 2, drawSize, drawSize),
            Vector2.Zero, 0, Color.White);
}

/// <summary>Owns every texture: loaded once after the window exists, unloaded once before it closes.</summary>
static class Assets
{
    const int HeroFrameSize = 80;
    const int DemonFrameSize = 256;

    public static Texture2D Background;
    public static SpriteStrip DemonIdle, DemonDeath;
    // one strip per facing direction, indexed by (int)Direction
    public static SpriteStrip[] HeroIdle = [], HeroWalk = [], HeroRun = [], HeroAxe = [];

    public static void Load()
    {
        Background = Raylib.LoadTexture("tile.png");
        DemonIdle = new(Raylib.LoadTexture("textures/Enemy-Melee-Idle-S.png"), DemonFrameSize);
        DemonDeath = new(Raylib.LoadTexture("textures/Enemy-Melee-Death.png"), DemonFrameSize);
        HeroIdle = LoadHeroStrips("idle/idle");
        HeroWalk = LoadHeroStrips("walk/walk");
        HeroRun = LoadHeroStrips("run/run");
        HeroAxe = LoadHeroStrips("axe attack/axe_attack");
    }

    public static void Unload()
    {
        Raylib.UnloadTexture(Background);
        Raylib.UnloadTexture(DemonIdle.Texture);
        Raylib.UnloadTexture(DemonDeath.Texture);
        foreach (var strip in HeroIdle.Concat(HeroWalk).Concat(HeroRun).Concat(HeroAxe))
            Raylib.UnloadTexture(strip.Texture);
    }

    static SpriteStrip[] LoadHeroStrips(string basePath)
    {
        string[] suffixes = ["down", "up", "left", "right"]; // same order as the Direction enum
        return suffixes
            .Select(d => new SpriteStrip(Raylib.LoadTexture($"textures/hero/{basePath}_{d}.png"), HeroFrameSize))
            .ToArray();
    }
}

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

class Game
{
    const int PlayTime = 5;
    const int MaxBonusTime = 5;
    const int PointsPerHit = 10;

    static readonly string HighScorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HitThemAll", "highscore.txt");

    readonly Player player = new();
    readonly Demon demon = new();
    readonly ScorePopup popup = new();

    GameState state = GameState.Welcome;
    int score;
    int highScore = LoadHighScore();
    int bonusTime;
    double endTime;

    public bool QuitRequested { get; private set; }

    int SecondsLeft => (int)(endTime - Raylib.GetTime());

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
        DrawBackground();
        switch (state)
        {
            case GameState.Welcome:
                DrawWelcome();
                break;

            case GameState.Playing:
                player.Draw();
                demon.Draw();
                popup.Draw();
                DrawHud();
                break;

            case GameState.GameOver:
                DrawGameOver();
                break;
        }
    }

    void StartRound()
    {
        state = GameState.Playing;
        score = 0;
        bonusTime = 0;
        endTime = Raylib.GetTime() + PlayTime;
        player.Reset();
        demon.Respawn(player);
    }

    void EndRound()
    {
        state = GameState.GameOver;
        if (score > highScore)
        {
            highScore = score;
            SaveHighScore(highScore);
        }
    }

    void UpdatePlaying(float dt)
    {
        if (SecondsLeft < 0)
        {
            EndRound();
            return;
        }

        player.Update(dt);
        popup.Update(dt);

        bool clickedDemon = Raylib.IsMouseButtonPressed(MouseButton.Left) && demon.ContainsPoint(Raylib.GetMousePosition());
        if (demon.IsAlive && (clickedDemon || demon.Overlaps(player))) OnHit();

        demon.Update(dt);
        if (demon.IsDead)
        {
            popup.Show(demon.Center);
            demon.Respawn(player);
            endTime = Raylib.GetTime() + PlayTime + bonusTime;
            bonusTime = 0;
        }
    }

    void OnHit()
    {
        score += PointsPerHit;
        bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        player.Attack();
        demon.Kill();
    }

    static bool AnyInputPressed() =>
        Raylib.GetKeyPressed() != 0
        || Raylib.IsMouseButtonPressed(MouseButton.Left)
        || Raylib.IsMouseButtonPressed(MouseButton.Right)
        || Raylib.IsMouseButtonPressed(MouseButton.Middle);

    static void DrawBackground()
    {
        var bg = Assets.Background;
        Raylib.DrawTexturePro(bg,
            new Rectangle(0, 0, bg.Width, bg.Height),
            new Rectangle(0, 0, Screen.Width, Screen.Height),
            Vector2.Zero, 0, Color.White);
    }

    static void DrawWelcome()
    {
        const int titleFontSize = 80;
        Screen.DrawCenteredText("Hit them all!", Screen.Height / 2 - titleFontSize - 20, titleFontSize, Color.Beige);
        Screen.DrawCenteredText("Press any key to continue", Screen.Height / 2 + 20, 32, Color.Gray);
    }

    static void DrawGameOver()
    {
        const int titleFontSize = 62;
        const int optionFontSize = 28;
        int optionY = Screen.Height / 2 + titleFontSize / 2 + 20;

        Screen.DrawCenteredText("Game Over!", Screen.Height / 2 - titleFontSize / 2, titleFontSize, Color.Red);
        Screen.DrawCenteredText("[R] Replay", optionY, optionFontSize, Color.Gray);
        Screen.DrawCenteredText("[Q] Quit", optionY + optionFontSize + 10, optionFontSize, Color.Gray);
    }

    void DrawHud()
    {
        Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
        Raylib.DrawText($"High: {highScore}", 160, 20, 18, Color.Gold);
        Raylib.DrawText($"Time: {SecondsLeft:D2}", 20, 40, 16, Color.White);
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
    }

    static int LoadHighScore()
    {
        try
        {
            if (File.Exists(HighScorePath) && int.TryParse(File.ReadAllText(HighScorePath), out int saved))
                return saved;
        }
        catch (IOException) { }
        return 0;
    }

    static void SaveHighScore(int value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HighScorePath)!);
            File.WriteAllText(HighScorePath, value.ToString());
        }
        catch (IOException) { }
    }
}

class Player
{
    const int Size = 40;
    const float Speed = 100f;
    const float BoostMultiplier = 2.5f;
    const float DrawSize = 96f;
    const float AnimFps = 10f;

    Vector2 position = Screen.RandomPoint();
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;

    public Rectangle Bounds => new(position.X, position.Y, Size, Size);
    Vector2 Center => position + new Vector2(Size / 2f);

    SpriteStrip Strip => (state switch
    {
        PlayerState.Attacking => Assets.HeroAxe,
        PlayerState.Running => Assets.HeroRun,
        PlayerState.Walking => Assets.HeroWalk,
        _ => Assets.HeroIdle,
    })[(int)facing];

    float AttackDuration => Strip.FrameCount / AnimFps;

    public void Reset()
    {
        position = Screen.RandomPoint();
        state = PlayerState.Idle;
        animElapsed = 0;
    }

    public void Attack()
    {
        state = PlayerState.Attacking;
        animElapsed = 0;
    }

    public void Update(float dt)
    {
        animElapsed += dt;
        if (state == PlayerState.Attacking && animElapsed >= AttackDuration)
            state = PlayerState.Idle;

        bool boosting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        float step = (boosting ? Speed * BoostMultiplier : Speed) * dt;

        Vector2 move = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.D)) { move.X += step; facing = Direction.Right; }
        if (Raylib.IsKeyDown(KeyboardKey.A)) { move.X -= step; facing = Direction.Left; }
        if (Raylib.IsKeyDown(KeyboardKey.W)) { move.Y -= step; facing = Direction.Up; }
        if (Raylib.IsKeyDown(KeyboardKey.S)) { move.Y += step; facing = Direction.Down; }

        if (move != Vector2.Zero)
            state = boosting ? PlayerState.Running : PlayerState.Walking;
        else if (state != PlayerState.Attacking)
            state = PlayerState.Idle;

        position = Vector2.Clamp(position + move, Vector2.Zero, new Vector2(Screen.Width - Size, Screen.Height - Size));
    }

    public void Draw()
    {
        var strip = Strip;
        int frame = state == PlayerState.Attacking
            ? strip.OneShotFrame(animElapsed, AttackDuration)
            : strip.LoopFrame(animElapsed, AnimFps);
        strip.Draw(frame, Center, DrawSize);
    }
}

class Demon
{
    const float Radius = 25f;
    const float DrawSize = 110f;
    const float IdleFps = 12f;
    const float DeathDuration = 0.6f;
    const int RespawnAttemptCap = 10;

    public Vector2 Center;
    DemonState state = DemonState.Idle;
    float animElapsed;

    public bool IsAlive => state == DemonState.Idle;
    public bool IsDead => state == DemonState.Dying && animElapsed > DeathDuration;
    Rectangle Bounds => BoundsAt(Center);

    static Rectangle BoundsAt(Vector2 center) =>
        new(center.X - Radius, center.Y - Radius, Radius * 2, Radius * 2);

    public bool ContainsPoint(Vector2 p) => Raylib.CheckCollisionPointRec(p, Bounds);
    public bool Overlaps(Player player) => Raylib.CheckCollisionRecs(Bounds, player.Bounds);

    public void Kill()
    {
        state = DemonState.Dying;
        animElapsed = 0;
    }

    public void Respawn(Player player)
    {
        Vector2 candidate = Screen.RandomPoint();
        for (int attempt = 0; attempt < RespawnAttemptCap && Raylib.CheckCollisionRecs(BoundsAt(candidate), player.Bounds); attempt++)
            candidate = Screen.RandomPoint();

        Center = candidate;
        state = DemonState.Idle;
        animElapsed = 0;
    }

    public void Update(float dt) => animElapsed += dt;

    public void Draw()
    {
        var strip = state == DemonState.Dying ? Assets.DemonDeath : Assets.DemonIdle;
        int frame = state == DemonState.Dying
            ? strip.OneShotFrame(animElapsed, DeathDuration)
            : strip.LoopFrame(animElapsed, IdleFps);
        strip.Draw(frame, Center, DrawSize);
    }
}

class ScorePopup
{
    const float ScaleTime = 0.15f;
    const float HoldTime = 0.5f;
    const float FadeTime = 0.3f;
    const float Duration = ScaleTime + HoldTime + FadeTime;
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
        if (elapsed >= Duration) active = false;
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
