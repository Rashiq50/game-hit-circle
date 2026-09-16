using System.Numerics;
using Raylib_cs;

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
    bool showCollision; // F1 toggles the wall outlines

    // While paused the clock is frozen at the moment the pause began (Resume shifts endTime by the same amount).
    // int SecondsLeft => (int)(endTime - (state == GameState.Paused ? pauseStart : Raylib.GetTime()));

    public void Update(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) showCollision = !showCollision;
        switch (state)
        {
            case GameState.Welcome:
                if (AnyInputPressed()) GoToMainMenu();
                break;

            case GameState.MainMenu:
                if (Raylib.IsKeyPressed(KeyboardKey.Q)) QuitRequested = true;
                if (Raylib.IsKeyPressed(KeyboardKey.N)) StartRound();
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
        if (showCollision) CollisionMap.Draw();
    }

    public void DrawUi()
    {
        switch (state)
        {
            case GameState.Welcome:
                DrawWelcome();
                break;

            case GameState.MainMenu:
                DrawMainMenu();
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

    void GoToMainMenu()
    {
        state = GameState.MainMenu;
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

    static void DrawMainMenu()
    {
        // Continue if save file has progress, new game otherwise
        // 3 options: [N] New Game, [C] Continue, [Q] Quit
        const int optionFontSize = 28;
        int optionY = Screen.Height / 2;
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        Screen.DrawCenteredText("[N] New Game", optionY, optionFontSize, Color.Gray);
        // ignore for now
        if (false)
        {
            Screen.DrawCenteredText("[C] Continue", optionY + optionFontSize + 10, optionFontSize, Color.Gray);
        }
        Screen.DrawCenteredText("[Q] Quit", optionY + optionFontSize + 10, optionFontSize, Color.Gray);
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
