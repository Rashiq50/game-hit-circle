using System.Numerics;
using Raylib_cs;

class Game
{
    const float ShakeDuration = 0.1f;
    const float ShakeStrength = 6f; // pixels
    float shakeTimeLeft;

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
        bool cinematic = ultimatePhase != UltimatePhase.None; // the demon is frozen while the ultimate plays out
        if (player.PlayerCurrentHp <= 0)
        {
            EndRound();
            return;
        }

        player.Update(dt);
        popup.Update(dt);
        SpawnEnemies();

        var demon = demons.Find(d => d.Overlaps(player));
        Demon? clickedDemon()
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Demon? find = demons.Find(d => d.ContainsPoint(World.MousePosition()));
                return find;
            }
            return null;
            // return false;
        }

        var ClickedDemon = clickedDemon();
        if (player.PlayerUlti == 100 && ClickedDemon != null)
        {
            if (ClickedDemon.IsAlive && !player.IsAttacking && !cinematic)
            {
                ClickedDemon.SelectForUlt();
                StartUltimate(ClickedDemon);
            }
        }

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
            dm.Update(dt, player, holdFire: cinematic);
            if (!cinematic && dm.ConsumeProjectileHit(player))
            {
                Shake();
            }
        }
        demons.RemoveAll(p => p.IsDead);

    }

    void StartSwing(Demon demon)
    {
        score += demon.ScorePoint;
        // bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        player.Attack();
    }

    // The ultimate is a short cinematic: zoom onto the demon, then teleport and swing, then zoom back out.
    enum UltimatePhase { None, ZoomIn, Attack }
    UltimatePhase ultimatePhase;

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
        Raylib.DrawTexturePro(bg,
    new Rectangle(0, 0, bg.Width, bg.Height),
    new Rectangle(0, 0, World.Width, World.Height),
    Vector2.Zero, 0, Color.White);
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
        Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
        Raylib.DrawText($"High: {highScore}", 160, 20, 18, Color.Gold);
        // Raylib.DrawText($"Time: {SecondsLeft:D2}", 20, 40, 16, Color.White);
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
        DrawHealthBar();
        DrawUltimateBar();
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

    void DrawUltimateBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        const int margin = 45;
        int x = 20;
        int y = Screen.Height - margin - barHeight;
        int fill = (int)(barWidth * player.PowerFraction);

        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Yellow);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
    }

}
