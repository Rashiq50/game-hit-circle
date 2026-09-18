using System.Numerics;
using Raylib_cs;

class Game
{
    const float ShakeDuration = 0.1f;
    const float ShakeStrength = 6f; // pixels
    float shakeTimeLeft;
    const int ScreenMargin = 40;
    const float MaxEnemies = 3;
    const float EnemyCooldown = 2.0f;
    public void Shake() => shakeTimeLeft = ShakeDuration;

    public Vector2 ShakeOffset => shakeTimeLeft > 0
        ? new Vector2(Random.Shared.NextSingle() * 2 - 1, Random.Shared.NextSingle() * 2 - 1) * ShakeStrength
        : Vector2.Zero;

    readonly Player player = new();
    // readonly Demon demon = new();
    readonly ScorePopup popup = new();
    readonly List<Demon> demons = [];

    GameState state = GameState.Welcome;
    // Checkpoint = state at the start of the current stage; only a stage clear moves it forward.
    SaveData checkpoint = SaveFile.Load();
    int stage;
    int score; // live score for this stage attempt; rolls back to the checkpoint on quit or game over
    int highScore;

    public bool QuitRequested { get; private set; }
    public bool BlurWorld => state == GameState.Paused; // the pause menu sits over a blurred snapshot of the action
    bool showCollision; // F1 toggles the wall outlines

    public Game() => BuildMenus();

    public void Update(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) showCollision = !showCollision;
        if (Raylib.IsKeyPressed(KeyboardKey.F11)) Raylib.ToggleBorderlessWindowed();
        switch (state)
        {
            case GameState.Welcome:
                if (AnyInputPressed()) GoToMainMenu(false);
                break;

            case GameState.MainMenu:
                mainMenu.Update();
                break;

            case GameState.Playing:
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Pause();
                else UpdatePlaying(dt);
                break;

            case GameState.Paused:
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Resume(); // Esc toggles pause; it's not a listed option
                else pauseMenu.Update();
                break;

            case GameState.GameOver:
                gameOverMenu.Update();
                break;
        }
    }

    public void DrawWorld()
    {
        DrawBackground();
        if (state is GameState.Playing or GameState.Paused)
        {
            player.Draw();
            foreach (var demon in demons)
            {
                demon.Draw();
            }
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
                DrawPauseOverlay(); // no HUD: the bars in the bottom-left would sit under the menu
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
        stage = 1;
        score = 0;
        highScore = checkpoint.HighScore;
        player.Reset();
        CancelUltimateSequence();
    }

    void ContinueRound()
    {
        state = GameState.Playing;
        stage = checkpoint.Stage;
        score = checkpoint.Score;
        highScore = checkpoint.HighScore;
        player.Resume(checkpoint.PlayerHp, checkpoint.PlayerUlti);
        CancelUltimateSequence();
    }

    void SpawnEnemies()
    {
        if (demons.Count < MaxEnemies)
        {
            Demon demon = new Demon();
            demons.Add(demon);
            demon.ClearProjectiles();
            demon.Respawn(player);
        }
    }

    void GoToMainMenu(bool shouldSave)
    {
        state = GameState.MainMenu;
        if (shouldSave)
        {
            highScore = Math.Max(highScore, score);
            SaveProgress();
        }
        mainMenu.Reset(KeyboardKey.C); // start on Continue when there's a save, else the first option
    }

    void Pause()
    {
        state = GameState.Paused;
        pauseMenu.Reset();
    }

    void Resume()
    {
        state = GameState.Playing;
    }

    void EndRound()
    {
        state = GameState.GameOver;
        highScore = Math.Max(highScore, score);
        gameOverMenu.Reset();
    }

    void SaveProgress()
    {
        // return;
        checkpoint = checkpoint with { HighScore = highScore, Score = score, PlayerHp = player.PlayerCurrentHp, PlayerUlti = player.PlayerUlti };
        SaveFile.Save(checkpoint);
    }

    void UpdatePlaying(float dt)
    {
        shakeTimeLeft = Math.Max(0, shakeTimeLeft - dt);
        CameraFocus.Update(dt);
        UpdateUltimateSequence();
        if (player.PlayerCurrentHp <= 0)
        {
            EndRound();
            return;
        }

        // F toggles target selection when the meter is full. Everything else waits for a pick (or a cancel).
        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            if (IsTargeting) CancelUltimateSequence();
            else if (player.PlayerUlti >= 100 && !player.IsAttacking && ultimatePhase == UltimatePhase.None)
                ultimatePhase = UltimatePhase.Targeting;
        }
        if (IsTargeting)
        {
            UpdateTargeting(dt);
            return;
        }

        bool cinematic = ultimatePhase != UltimatePhase.None; // the demon is frozen while the ultimate plays out
        player.Update(dt, demons.Where(d => d.IsAlive).Select(d => d.Bounds).ToList());
        popup.Update(dt);
        SpawnEnemies();

        var demon = demons.Find(d => d.Overlaps(player));
        if (demon != null)
        {
            if (demon.IsAlive && !player.IsAttacking)
            {
                StartSwing(demon);
            }
            if (demon.IsAlive && player.SwingLanded)
            {
                demon.Kill(player);
                SaveProgress();
            }
            if (demon.IsDead)
            {
                Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
                Raylib.PlaySound(Assets.ScoreSound);
                popup.Show(demon.Center, demon.ScorePoint);
                // demon.Respawn(player);
                // endTime = Raylib.GetTime() + PlayTime + bonusTime;
                // bonusTime = 0;
            }

        }

        foreach (var dm in demons)
        {
            dm.UpdateHover(false, dt);
            dm.Update(dt, player, holdFire: cinematic);
            if (!cinematic && dm.ConsumeProjectileHit(player))
            {
                Shake();
            }
        }
        demons.RemoveAll(p => p.IsDead);

    }

    /// <summary>The world is frozen; only the hover highlight animates until the player clicks a demon.</summary>
    void UpdateTargeting(float dt)
    {
        Demon? hovered = HoveredTarget();
        foreach (var dm in demons) dm.UpdateHover(dm == hovered, dt);

        if (hovered != null && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            hovered.SelectForUlt();
            StartUltimate(hovered);
        }
    }

    Demon? HoveredTarget()
    {
        Vector2 mouse = World.MousePosition();
        return demons.Find(d => d.IsAlive && d.IsUnderCursor(mouse));
    }

    void StartSwing(Demon demon)
    {
        score += demon.ScorePoint;
        // bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        player.Attack();
    }

    // The ultimate starts with a frozen target pick (F when the meter is full), then plays a short cinematic:
    // zoom onto the demon, then teleport and swing, then zoom back out.
    enum UltimatePhase { None, Targeting, ZoomIn, Attack }
    UltimatePhase ultimatePhase;
    bool IsTargeting => ultimatePhase == UltimatePhase.Targeting;

    void StartUltimate(Demon demon)
    {
        score += demon.ScorePoint;
        // Raylib.SetSoundVolume(Assets.SwordSound, 0.3f);
        // Raylib.PlaySound(Assets.SwordSound);
        ultimatePhase = UltimatePhase.ZoomIn;
        CameraFocus.Focus(demon.Center);
    }

    void CancelUltimateSequence()
    {
        ultimatePhase = UltimatePhase.None;
        CameraFocus.Release();
        foreach (var dm in demons) dm.ReleaseFromUlt();
    }

    void UpdateUltimateSequence()
    {
        Demon? demon = demons.Find(d => d.IsSelectedForUlt);
        if (demon != null)
        {
            switch (ultimatePhase)
            {
                case UltimatePhase.ZoomIn when CameraFocus.IsSettled:
                    player.Ultimate(demon.Center);
                    ultimatePhase = UltimatePhase.Attack;
                    break;
                case UltimatePhase.Attack when !player.IsAttacking:
                    ultimatePhase = UltimatePhase.None;
                    CameraFocus.Release();
                    demon.ReleaseFromUlt();
                    break;
            }
        }
    }

    static bool AnyInputPressed() =>
        Raylib.GetKeyPressed() != 0
        || Raylib.IsMouseButtonPressed(MouseButton.Left)
        || Raylib.IsMouseButtonPressed(MouseButton.Right)
        || Raylib.IsMouseButtonPressed(MouseButton.Middle);

    void DrawBackground()
    {
        var bg = Assets.WelcomBackground;
        switch (state)
        {
            case GameState.Welcome:
                bg = Assets.WelcomBackground;
                break;

            case GameState.MainMenu:
                bg = Assets.WelcomBackground;
                break;

            case GameState.Playing:
                bg = Assets.Background;
                break;

            case GameState.Paused:
                bg = Assets.Background;
                break;

            case GameState.GameOver:
                bg = Assets.Background;
                break;
        }
        if (IsTargeting) GrayscaleEffect.Begin(); // wash the floor out so the coloured targets stand out
        Raylib.DrawTexturePro(bg,
    new Rectangle(0, 0, bg.Width, bg.Height),
    new Rectangle(0, 0, World.Width, World.Height),
    Vector2.Zero, 0, Color.White);
        if (IsTargeting) GrayscaleEffect.End();
    }

    static void DrawWelcome()
    {
        const int titleFontSize = 80;
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        Screen.DrawCenteredText("Hit them all!", Screen.Height / 2 - titleFontSize - 20, titleFontSize, Color.Beige);
        Screen.DrawCenteredText("Press any key to continue", Screen.Height / 2 + 20, 32, Color.Gray);
    }

    bool CanContinue => checkpoint.Score > 0;

    CursorMenu mainMenu = null!; // built in the constructor: the items call back into this instance
    CursorMenu pauseMenu = null!;
    CursorMenu gameOverMenu = null!;

    void BuildMenus()
    {
        mainMenu = new CursorMenu(
            new(KeyboardKey.N, "[N] New Game", StartRound),
            new(KeyboardKey.C, "[C] Continue", ContinueRound, () => CanContinue),
            new(KeyboardKey.Q, "[Q] Quit", () => QuitRequested = true));
        pauseMenu = new CursorMenu(
            new(KeyboardKey.R, "[R] Resume", Resume),
            new(KeyboardKey.M, "[M] Main Menu", () => GoToMainMenu(true)),
            new(KeyboardKey.Q, "[Q] Quit", () => QuitRequested = true));
        gameOverMenu = new CursorMenu(
            new(KeyboardKey.R, "[R] Replay", StartRound),
            new(KeyboardKey.M, "[M] Main Menu", () => GoToMainMenu(false)), // EndRound already saved
            new(KeyboardKey.Q, "[Q] Quit", () => QuitRequested = true));
    }

    void DrawMainMenu()
    {
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack2);
        mainMenu.Draw();
    }

    void DrawGameOver()
    {
        const int titleFontSize = 62;
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        Screen.DrawCenteredText("You Died!", Screen.Height / 2 - titleFontSize / 2, titleFontSize, Color.Red);
        gameOverMenu.Draw();
    }

    void DrawPauseOverlay()
    {
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        pauseMenu.Draw();
    }

    void DrawHud()
    {
        Raylib.DrawText($"Score: {score}", ScreenMargin, ScreenMargin, 22, Color.White);
        Raylib.DrawText($"High: {highScore}", ScreenMargin + 140, ScreenMargin, 22, Color.Gold);
        // Raylib.DrawText($"Time: {SecondsLeft:D2}", 20, 40, 16, Color.White);
        // Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
        DrawHealthBar();
        DrawUltimateBar();
        DrawUltimateHint();
    }

    void DrawUltimateHint()
    {
        const int fontSize = 24;
        int y = Screen.Height - ScreenMargin - fontSize; // bottom centre, level with the health bar
        if (IsTargeting)
            Screen.DrawCenteredText("Click an enemy to unleash your ultimate  -  [F] cancel", y, fontSize, Color.Yellow);
        else if (player.PlayerUlti >= 100 && ultimatePhase == UltimatePhase.None)
            Screen.DrawCenteredText("[F] Ultimate ready", y, fontSize, Color.Yellow);
    }

    void DrawHealthBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        int x = ScreenMargin;
        int y = Screen.Height - ScreenMargin - barHeight;
        int fill = (int)(barWidth * player.HealthFraction);

        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Red);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
    }

    void DrawUltimateBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        const int margin = ScreenMargin + 25;
        int x = ScreenMargin;
        int y = Screen.Height - margin - barHeight;
        int fill = (int)(barWidth * player.PowerFraction);

        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Yellow);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
    }

}
