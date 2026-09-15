using System.Numerics;
using System.Text.Json;
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
    World.FitCamera(game.ShakeOffset); // every frame: the window may have been resized
    Raylib.BeginMode2D(World.Camera);
    game.DrawWorld();
    Raylib.EndMode2D();
    game.DrawUi(); // HUD and menus stay in screen space, unaffected by camera pan/zoom
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

    public static Texture2D Background, Fireball;
    public static SpriteStrip DemonIdle, DemonDeath;
    public static Sound SwordSound, ScoreSound, GameOver;

    public static Color OverlayBlack = new Color(0, 0, 0, 150);
    // one strip per facing direction, indexed by (int)Direction
    public static SpriteStrip[] HeroIdle = [], HeroWalk = [], HeroRun = [], HeroAxe = [];

    public static void Load()
    {
        Background = Raylib.LoadTexture("tile.png");
        Fireball = Raylib.LoadTexture("textures/fireball.png");
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
        Raylib.UnloadTexture(Fireball);
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

/// <summary>Everything that survives between launches. Stage/Score are the checkpoint the next run starts from; HighScore is lifetime.</summary>
record SaveData(int Stage = 1, int Score = 0, int HighScore = 0)
{
    public static readonly SaveData Fresh = new();
    public bool HasProgress => Stage > 1 || Score > 0;
}

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

/// <summary>Reads and writes the save file as JSON; any unreadable or missing file counts as a fresh save.</summary>
static class SaveFile
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HitThemAll", "savegame.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options) ?? SaveData.Fresh;
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        return SaveData.Fresh;
    }

    public static void Save(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, Options));
        }
        catch (IOException) { }
    }
}

/// <summary>
/// The play area in fixed virtual units. Gameplay code positions everything in this space and never looks at the
/// window size; the camera scales the world to fit the window (letterboxed) so coordinates in stage data stay valid.
/// </summary>
static class World
{
    public const int Width = 1000;
    public const int Height = 600;
    const int SpawnMargin = 50;

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

    public static Vector2 RandomPoint() => new(
        Random.Shared.Next(SpawnMargin, Width - SpawnMargin),
        Random.Shared.Next(SpawnMargin, Height - SpawnMargin));
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

class Game
{
    const int PointsPerHit = 10;
    private readonly int DAMAGE_BY_PROJECTILE = 12;
    const float ShakeDuration = 0.1f;
    const float ShakeStrength = 6f; // pixels
    float shakeTimeLeft;

    public void Shake() => shakeTimeLeft = ShakeDuration;

    public Vector2 ShakeOffset => shakeTimeLeft > 0
        ? new Vector2(Random.Shared.NextSingle() * 2 - 1, Random.Shared.NextSingle() * 2 - 1) * ShakeStrength
        : Vector2.Zero;

    readonly Player player = new();
    readonly Demon demon = new();
    readonly ScorePopup popup = new();

    GameState state = GameState.Welcome;
    // Checkpoint = state at the start of the current stage; only a stage clear moves it forward.
    SaveData checkpoint = SaveFile.Load();
    int stage;
    int score; // live score for this stage attempt; rolls back to the checkpoint on quit or game over
    int highScore;
    int bonusTime;
    // double endTime;
    double pauseStart;

    public bool QuitRequested { get; private set; }

    // While paused the clock is frozen at the moment the pause began (Resume shifts endTime by the same amount).
    // int SecondsLeft => (int)(endTime - (state == GameState.Paused ? pauseStart : Raylib.GetTime()));

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

    public void DrawWorld()
    {
        DrawBackground();
        if (state is GameState.Playing or GameState.Paused)
        {
            player.Draw();
            demon.Draw();
            popup.Draw();
        }
    }

    public void DrawUi()
    {
        switch (state)
        {
            case GameState.Welcome:
                DrawWelcome();
                break;

            case GameState.Playing:
                DrawHud();
                break;

            case GameState.Paused:
                DrawHud();
                DrawPauseOverlay();
                break;

            case GameState.GameOver:
                DrawGameOver();
                break;
        }
    }

    // Restarts the checkpointed stage from scratch: in-stage progress is never saved.
    void StartRound()
    {
        state = GameState.Playing;
        stage = checkpoint.Stage;
        score = checkpoint.Score;
        highScore = checkpoint.HighScore;
        bonusTime = 0;
        player.Reset();
        demon.ClearProjectiles();
        demon.Respawn(player);
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
    }

    void EndRound()
    {
        state = GameState.GameOver;
        highScore = Math.Max(highScore, score);
        SaveProgress();
    }

    void SaveProgress()
    {
        checkpoint = checkpoint with { HighScore = highScore };
        SaveFile.Save(checkpoint);
    }

    void UpdatePlaying(float dt)
    {
        shakeTimeLeft = Math.Max(0, shakeTimeLeft - dt);
        if (player.PlayerCurrentHp <= 0)
        {
            EndRound();
            return;
        }

        player.Update(dt);
        popup.Update(dt);

        bool clickedDemon = Raylib.IsMouseButtonPressed(MouseButton.Left) && demon.ContainsPoint(World.MousePosition());
        if (demon.IsAlive && !player.IsAttacking && (clickedDemon || demon.Overlaps(player))) StartSwing();
        if (demon.IsAlive && player.SwingLanded) demon.Kill();

        demon.Update(dt, player);
        if (demon.ConsumeProjectileHit(player))
        {
            PlayerHitByPt();
            Shake();
        }

        if (demon.IsDead)
        {
            Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
            Raylib.PlaySound(Assets.ScoreSound);
            popup.Show(demon.Center);
            demon.Respawn(player);
            // endTime = Raylib.GetTime() + PlayTime + bonusTime;
            // bonusTime = 0;
        }

    }

    void StartSwing()
    {
        score += PointsPerHit;
        // bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        Raylib.SetSoundVolume(Assets.SwordSound, 0.3f);
        Raylib.PlaySound(Assets.SwordSound);
        player.Attack();
    }

    void PlayerHitByPt()
    {
        player.ReceiveDamage(DAMAGE_BY_PROJECTILE);
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
            new Rectangle(0, 0, World.Width, World.Height),
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
        // Raylib.DrawText($"Time: {SecondsLeft:D2}", 20, 40, 16, Color.White);
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
        DrawHealthBar();
    }

    void DrawHealthBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        const int margin = 20;
        int x = margin;
        int y = Screen.Height - margin - barHeight;
        int fill = (int)(barWidth * player.HealthFraction);

        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Red);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
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
    const int AttackImpactFrame = 3;
    static float PlayerHealth = 100;
    public float PlayerCurrentHp = PlayerHealth;
    public float HealthFraction => Math.Clamp(PlayerCurrentHp / PlayerHealth, 0f, 1f);

    Vector2 position = World.RandomPoint();
    PlayerState state = PlayerState.Idle;
    Direction facing = Direction.Down;
    float animElapsed;
    public Rectangle Bounds => new(position.X, position.Y, Size, Size);
    public bool IsAttacking => state == PlayerState.Attacking;
    /// <summary>True only during the Update in which the swing reaches its impact frame.</summary>
    public bool SwingLanded { get; private set; }

    public Vector2 Center => position + new Vector2(Size / 2f);

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
        position = World.RandomPoint();
        state = PlayerState.Idle;
        PlayerCurrentHp = PlayerHealth;
        animElapsed = 0;
    }

    public void Attack()
    {
        state = PlayerState.Attacking;
        animElapsed = 0;
    }

    public void ReceiveDamage(float damage) => PlayerCurrentHp = Math.Max(0, PlayerCurrentHp - damage);

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

        position = Vector2.Clamp(position + move, Vector2.Zero, new Vector2(World.Width - Size, World.Height - Size));
    }

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
    const float FireInterval = 2f; // seconds between shots while alive

    public Vector2 Center;
    DemonState state = DemonState.Idle;
    float animElapsed;
    float fireCooldown;
    // Shots already in flight outlive the demon that fired them; they only vanish off-screen or on hit.
    readonly List<EnemyProjectile> projectiles = [];

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
        Vector2 candidate = World.RandomPoint();
        for (int attempt = 0; attempt < RespawnAttemptCap && Raylib.CheckCollisionRecs(BoundsAt(candidate), player.Bounds); attempt++)
            candidate = World.RandomPoint();

        Center = candidate;
        state = DemonState.Idle;
        animElapsed = 0;
        fireCooldown = FireInterval;
    }

    public void ClearProjectiles() => projectiles.Clear();

    public void Update(float dt, Player player)
    {
        animElapsed += dt;

        if (IsAlive)
        {
            fireCooldown -= dt;
            if (fireCooldown <= 0)
            {
                projectiles.Add(new EnemyProjectile(Center, player.Center));
                fireCooldown += FireInterval;
            }
        }

        foreach (var p in projectiles) p.Update(dt);
        projectiles.RemoveAll(p => !p.Active);
    }

    /// <summary>True if any projectile hit the player this frame; the projectile is spent so it can only hit once.</summary>
    public bool ConsumeProjectileHit(Player player)
    {
        var hit = projectiles.Find(p => p.Overlaps(player));
        if (hit is null) return false;
        hit.Active = false;
        return true;
    }

    public void Draw()
    {
        var strip = state == DemonState.Dying ? Assets.DemonDeath : Assets.DemonIdle;
        int frame = state == DemonState.Dying
            ? strip.OneShotFrame(animElapsed, DeathDuration)
            : strip.LoopFrame(animElapsed, IdleFps);
        strip.Draw(frame, Center, DrawSize);
        foreach (var p in projectiles) p.Draw();
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

/// <summary>A single shot fired by a demon: flies in a straight line toward where the player was until it leaves the world or hits.</summary>
class EnemyProjectile(Vector2 from, Vector2 toward)
{
    const float Speed = 400f;
    const float Radius = 15f;
    const float DrawSize = 34f; // the glow extends past the hit box

    Vector2 position = from;
    readonly Vector2 direction = Vector2.Normalize(toward - from);

    public bool Active { get; set; } = true;
    Rectangle Bounds => new(position.X - Radius, position.Y - Radius, Radius * 2, Radius * 2);

    public bool Overlaps(Player player) => Active && Raylib.CheckCollisionRecs(Bounds, player.Bounds);

    public void Draw()
    {
        if (!Active) return;
        var tex = Assets.Fireball;
        Raylib.DrawTexturePro(tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            new Rectangle(position.X - DrawSize / 2, position.Y - DrawSize / 2, DrawSize, DrawSize),
            Vector2.Zero, 0, Color.White);
    }

    public void Update(float dt)
    {
        if (!Active) return;

        position += direction * Speed * dt;

        bool offScreen = position.X < -Radius || position.X > World.Width + Radius
                      || position.Y < -Radius || position.Y > World.Height + Radius;
        if (offScreen) Active = false;
    }
}
