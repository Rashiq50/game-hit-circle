using System.Numerics;
using Raylib_cs;

class Game
{
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
        demon.ClearProjectiles();
        demon.Respawn(player);
    }

    void ContinueRound()
    {
        state = GameState.Playing;
        stage = checkpoint.Stage;
        score = checkpoint.Score;
        highScore = checkpoint.HighScore;
        player.Resume(checkpoint.PlayerHp, checkpoint.PlayerUlti);
        demon.ClearProjectiles();
        demon.Respawn(player);
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
        SaveProgress();
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
        if (player.PlayerCurrentHp <= 0)
        {
            EndRound();
            return;
        }

        player.Update(dt);
        popup.Update(dt);

        if (player.PlayerUlti == 100)
        {
            bool clickedDemon = Raylib.IsMouseButtonPressed(MouseButton.Left) && demon.ContainsPoint(World.MousePosition());
            if (demon.IsAlive && !player.IsAttacking && clickedDemon) StartUltimate(demon);
        }


        if (demon.IsAlive && !player.IsAttacking && demon.Overlaps(player)) StartSwing(demon);
        if (demon.IsAlive && player.SwingLanded)
        {
            demon.Kill(player);
            SaveProgress();

        }

        demon.Update(dt, player);
        if (demon.ConsumeProjectileHit(player))
        {
            Shake();
        }

        if (demon.IsDead)
        {
            Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
            Raylib.PlaySound(Assets.ScoreSound);
            popup.Show(demon.Center, demon.ScorePoint);
            demon.Respawn(player);
            // endTime = Raylib.GetTime() + PlayTime + bonusTime;
            // bonusTime = 0;
        }

    }

    void StartSwing(Demon demon)
    {
        score += demon.ScorePoint;
        // bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
        player.Attack();
    }

    void StartUltimate(Demon demon)
    {
        score += demon.ScorePoint;
        // Raylib.SetSoundVolume(Assets.SwordSound, 0.3f);
        // Raylib.PlaySound(Assets.SwordSound);
        player.Ultimate(demon.Center);
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
                bg = Assets.MainMenuBackground;
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
    }

    void DrawMainMenu()
    {
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Assets.OverlayBlack);
        mainMenu.Draw();
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
