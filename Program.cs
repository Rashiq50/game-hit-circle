using System.Numerics;
using Raylib_cs;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(1000, 600, "Hit them all!");
Raylib.InitAudioDevice();
Raylib.SetTargetFPS(60);
Raylib.SetExitKey(KeyboardKey.Null); // Esc is the pause key, not the quit key

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
Raylib.CloseAudioDevice();
Raylib.CloseWindow();

enum Direction { Down, Up, Left, Right }
enum GameState { Welcome, Playing, Paused, GameOver }
enum PlayerState { Idle, Walking, Running, Attacking, Dead }
enum DemonState { Idle, Dying, Attacking }

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
    public static Sound SwordSound, ScoreSound, GameOver;

    public static Color OverlayBlack = new Color(0, 0, 0, 150);
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
        SwordSound = Raylib.LoadSound("sounds/violent-sword-slice-393848.mp3");
        ScoreSound = Raylib.LoadSound("sounds/level-up-523624.mp3");
    }

    public static void Unload()
    {
        Raylib.UnloadTexture(Background);
        Raylib.UnloadTexture(DemonIdle.Texture);
        Raylib.UnloadTexture(DemonDeath.Texture);
        foreach (var strip in HeroIdle.Concat(HeroWalk).Concat(HeroRun).Concat(HeroAxe))
            Raylib.UnloadTexture(strip.Texture);
        Raylib.UnloadSound(SwordSound);
        Raylib.UnloadSound(ScoreSound);
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
    readonly EnemyProjectile projectile = new();

    GameState state = GameState.Welcome;
    int score;
    int highScore = LoadHighScore();
    int bonusTime;
    double endTime;
    double pauseStart;

    public bool QuitRequested { get; private set; }

    // While paused the clock is frozen at the moment the pause began (Resume shifts endTime by the same amount).
    int SecondsLeft => (int)(endTime - (state == GameState.Paused ? pauseStart : Raylib.GetTime()));

    public void Update(float dt)
    {
        switch (state)
        {
            case GameState.Welcome:
                if (AnyInputPressed()) StartRound();
                break;

            case GameState.Playing:
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Pause();
                else UpdatePlaying(dt);
                break;

            case GameState.Paused:
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.R)) Resume();
                if (Raylib.IsKeyPressed(KeyboardKey.Q)) QuitRequested = true;
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
                DrawPlaying();
                break;

            case GameState.Paused:
                DrawPlaying();
                DrawPauseOverlay();
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
        projectile.Show(demon.Center, player.GetCurrentPosition);
    }

    // The round timer is wall-clock based, so the time spent paused is added back on resume.
    void Pause()
    {
        state = GameState.Paused;
        pauseStart = Raylib.GetTime();
    }

    void Resume()
    {
        state = GameState.Playing;
        endTime += Raylib.GetTime() - pauseStart;
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
        projectile.Update(dt);

        bool clickedDemon = Raylib.IsMouseButtonPressed(MouseButton.Left) && demon.ContainsPoint(Raylib.GetMousePosition());
        if (demon.IsAlive && !player.IsAttacking && (clickedDemon || demon.Overlaps(player))) StartSwing();
        if (demon.IsAlive && player.SwingLanded) demon.Kill();

        if (projectile.Overlaps(player))
        {
            Console.Write("Hit !!!");
        }

        demon.Update(dt);
        if (demon.IsDead)
        {
            Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
            Raylib.PlaySound(Assets.ScoreSound);
            popup.Show(demon.Center);
            demon.Respawn(player);
            projectile.Show(demon.Center, player.GetCurrentPosition);
            endTime = Raylib.GetTime() + PlayTime + bonusTime;
            bonusTime = 0;
        }

    }

    // Score is banked when the swing starts; the demon dies when the swing lands (see UpdatePlaying).
    void StartSwing()
    {
        Console.Write("Swing !!!");
        score += PointsPerHit;
        bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        Raylib.SetSoundVolume(Assets.SwordSound, 0.3f);
        Raylib.PlaySound(Assets.SwordSound);
        player.Attack();
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

    void DrawPlaying()
    {
        player.Draw();
        demon.Draw();
        popup.Draw();
        DrawHud();
        projectile.Draw();
    }

    static void DrawPauseOverlay()
    {
        const float coverage = 0.8f;
        const int optionFontSize = 28;
        int w = (int)(Screen.Width * coverage);
        int h = (int)(Screen.Height * coverage);
        int optionY = Screen.Height / 2 - optionFontSize - 5;

        Raylib.DrawRectangle((Screen.Width - w) / 2, (Screen.Height - h) / 2, w, h, Color.Black);
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        Screen.DrawCenteredText("[Esc]/[R] Resume", optionY, optionFontSize, Color.Gray);
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
    const float Speed = 300f;
    const float BoostMultiplier = 2.5f;
    const float DrawSize = 96f;
    const float AnimFps = 10f;
    const float AttackFps = 16f;
    const int AttackImpactFrame = 3; // the slash frame of the axe strip (0-2 wind-up, 4-6 recovery)
    int PlayerHealth = 100;
    public float PlayerCurrentHp = 100;

    Vector2 position = Screen.RandomPoint();
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;

    public Rectangle Bounds => new(position.X, position.Y, Size, Size);
    public bool IsAttacking => state == PlayerState.Attacking;
    /// <summary>True only during the Update in which the swing reaches its impact frame.</summary>
    public bool SwingLanded { get; private set; }

    Vector2 Center => position + new Vector2(Size / 2f);

    public Vector2 GetCurrentPosition => position;

    SpriteStrip Strip => (state switch
    {
        PlayerState.Attacking => Assets.HeroAxe,
        PlayerState.Running => Assets.HeroRun,
        PlayerState.Walking => Assets.HeroWalk,
        _ => Assets.HeroIdle,
    })[(int)facing];

    float AttackDuration => Strip.FrameCount / AttackFps;

    int CurrentFrame => IsAttacking
        ? Strip.OneShotFrame(animElapsed, AttackDuration)
        : Strip.LoopFrame(animElapsed, AnimFps);

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
        SwingLanded = false;
        if (IsAttacking)
        {
            UpdateAttack(dt);
            return;
        }

        animElapsed += dt;
        bool boosting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        float step = (boosting ? Speed * BoostMultiplier : Speed) * dt;

        Vector2 move = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.D)) { move.X += step; facing = Direction.Right; }
        if (Raylib.IsKeyDown(KeyboardKey.A)) { move.X -= step; facing = Direction.Left; }
        if (Raylib.IsKeyDown(KeyboardKey.W)) { move.Y -= step; facing = Direction.Up; }
        if (Raylib.IsKeyDown(KeyboardKey.S)) { move.Y += step; facing = Direction.Down; }

        state = move == Vector2.Zero ? PlayerState.Idle
              : boosting ? PlayerState.Running
              : PlayerState.Walking;

        position = Vector2.Clamp(position + move, Vector2.Zero, new Vector2(Screen.Width - Size, Screen.Height - Size));
    }

    // The swing plays to completion; movement input is ignored until it finishes.
    void UpdateAttack(float dt)
    {
        int frameBefore = CurrentFrame;
        animElapsed += dt;
        SwingLanded = frameBefore < AttackImpactFrame && CurrentFrame >= AttackImpactFrame;
        if (animElapsed >= AttackDuration) state = PlayerState.Idle;
    }

    public void Draw() => Strip.Draw(CurrentFrame, Center, DrawSize);
}

class Demon
{
    const float Radius = 25f;
    const float DrawSize = 110f;
    const float IdleFps = 12f;
    const float DeathDuration = 0.4f;
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

class EnemyProjectile
{
    const float Speed = 400f;
    const float Radius = 15f;

    bool active;
    Vector2 position;
    Vector2 direction;
    Rectangle Bounds => BoundsAt(position);
    static Rectangle BoundsAt(Vector2 center) =>
    new(center.X - Radius, center.Y - Radius, Radius * 2, Radius * 2);
    public bool Overlaps(Player player) => Raylib.CheckCollisionRecs(Bounds, player.Bounds);
    public void Show(Vector2 at, Vector2 to)
    {
        active = true;
        position = at;
        direction = Vector2.Normalize(to - at);
    }

    public void Draw()
    {
        if (!active) return;

        Raylib.DrawCircleV(position, Radius, Color.Gold);
    }

    public void Update(float dt)
    {
        if (!active) return;

        position += direction * Speed * dt;

        bool offScreen = position.X < -Radius || position.X > Screen.Width + Radius
                      || position.Y < -Radius || position.Y > Screen.Height + Radius;
        if (offScreen) active = false;
    }
}
