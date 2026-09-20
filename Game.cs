using System.Numerics;
using Raylib_cs;

class Game
{
    const float ShakeDuration = 0.1f;
    const float ShakeStrength = 6f; // pixels
    float shakeTimeLeft;
    const int ScreenMargin = 40;
    const float EnemyCooldown = 2.0f;
    public void Shake() => shakeTimeLeft = ShakeDuration;

    public Vector2 ShakeOffset => shakeTimeLeft > 0
        ? new Vector2(Random.Shared.NextSingle() * 2 - 1, Random.Shared.NextSingle() * 2 - 1) * ShakeStrength
        : Vector2.Zero;

    readonly Player player = new();
    // readonly Demon demon = new();
    readonly ScorePopup popup = new();
    readonly List<Demon> demons = [];
    // Powers: number keys, listed in the bottom-centre HUD in this order. Slots past the list are drawn empty.
    readonly SlowTimePower slowTime = new();
    Power[] Powers => [slowTime];
    const int PowerSlots = 3;
    /// <summary>Multiplier on the world's clock (demons, shots, popups). The player, camera and HUD always run in real time.</summary>
    float TimeScale => slowTime.IsActive ? SlowTimePower.Scale : 1f;

    GameState state = GameState.Welcome;
    // Checkpoint = state at the start of the current stage; only a stage clear moves it forward.
    SaveData checkpoint = SaveFile.Load();
    int stage;
    int spawned; // demons of the current stage's roster that have entered the field so far
    StageDef CurrentStage => Stages.Get(stage);
    // Stage banner ("Stage 3 complete!", "Stage 4"): the world keeps running underneath, but spawning waits for it.
    const float BannerDuration = 1.8f;
    const float BannerFadeIn = 0.2f;
    const float BannerFadeOut = 0.5f;
    string bannerTitle = "";
    string bannerSubtitle = "";
    float bannerTimeLeft;
    Action? afterBanner; // runs once the banner has faded out, e.g. advancing to the next stage
    bool BannerShowing => bannerTimeLeft > 0;
    int score; // live score for this stage attempt; rolls back to the checkpoint on quit or game over
    int highScore;

    public bool QuitRequested { get; private set; }
    public bool BlurWorld => state == GameState.Paused; // the pause menu sits over a blurred snapshot of the action
    bool showCollision; // F1 toggles the wall outlines
    bool showHitBoxes; // F2 toggles the player/enemy hit boxes
    bool showDebugHelp; // Shift+F1 toggles the debug key list overlay

    public Game() => BuildMenus();

    public void Update(float dt)
    {
        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        if (Raylib.IsKeyPressed(KeyboardKey.F1))
        {
            if (shift) showDebugHelp = !showDebugHelp;
            else showCollision = !showCollision;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F2)) showHitBoxes = !showHitBoxes;
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) Demon.AggroEnabled = !Demon.AggroEnabled;
        if (Raylib.IsKeyPressed(KeyboardKey.F4)) player.ToggleGodMode();
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
        if (showHitBoxes && state is GameState.Playing or GameState.Paused) DrawHitBoxes();
    }

    void DrawHitBoxes()
    {
        Raylib.DrawRectangleLinesEx(player.Bounds, 2, Color.Lime);
        foreach (var demon in demons)
            Raylib.DrawRectangleLinesEx(demon.Bounds, 2, demon.IsAlive ? Color.Red : Color.Gray);
    }

    public void DrawUi()
    {
        DrawStateUi();
        if (showDebugHelp) DrawDebugHelp(); // always on top, in every state
    }

    void DrawStateUi()
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

    /// <summary>Panel in the top-right listing every debug key with its current on/off state.</summary>
    void DrawDebugHelp()
    {
        const int fontSize = 18;
        const int lineHeight = 24;
        const int pad = 12;
        const int width = 320;
        (string key, string what, bool? on)[] rows =
        [
            ("Shift+F1", "Debug key list", showDebugHelp),
            ("F1", "Collision map", showCollision),
            ("F2", "Hit boxes", showHitBoxes),
            ("F3", "Enemy aggro", Demon.AggroEnabled),
            ("F4", "Player god mode", Player.GodMode),
            ("F11", "Borderless fullscreen", null),
        ];

        int x = Screen.Width - ScreenMargin - width;
        int y = ScreenMargin;
        int height = pad * 2 + lineHeight * (rows.Length + 1);
        Raylib.DrawRectangle(x, y, width, height, Assets.OverlayBlack);
        Raylib.DrawRectangleLines(x, y, width, height, Color.Gray);

        int ty = y + pad;
        Raylib.DrawText("DEBUG KEYS", x + pad, ty, fontSize, Color.Yellow);
        ty += lineHeight;
        foreach (var (key, what, on) in rows)
        {
            Raylib.DrawText(key, x + pad, ty, fontSize, Color.White);
            Raylib.DrawText(what, x + pad + 90, ty, fontSize, Color.LightGray);
            if (on is bool b)
                Raylib.DrawText(b ? "ON" : "OFF", x + width - pad - 36, ty, fontSize, b ? Color.Lime : Color.Red);
            ty += lineHeight;
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
        ResetPowers();
        CancelUltimateSequence();
        BeginStage();
    }

    void ContinueRound()
    {
        state = GameState.Playing;
        stage = checkpoint.Stage;
        score = checkpoint.Score;
        highScore = checkpoint.HighScore;
        player.Resume(checkpoint.PlayerHp, checkpoint.PlayerUlti);
        ResetPowers();
        CancelUltimateSequence();
        BeginStage();
    }

    void ResetPowers()
    {
        foreach (var power in Powers) power.Reset();
    }

    /// <summary>Wipes the field and restarts the current stage's roster from its first demon.</summary>
    void BeginStage()
    {
        demons.Clear();
        spawned = 0;
        int total = CurrentStage.TotalEnemies;
        ShowBanner($"Stage {stage}", $"{total} demon{(total == 1 ? "" : "s")} incoming");
    }

    void ShowBanner(string title, string subtitle = "", Action? then = null)
    {
        bannerTitle = title;
        bannerSubtitle = subtitle;
        bannerTimeLeft = BannerDuration;
        afterBanner = then;
    }

    void UpdateBanner(float dt)
    {
        if (!BannerShowing) return;
        bannerTimeLeft -= dt;
        if (bannerTimeLeft > 0) return;
        var then = afterBanner;
        afterBanner = null;
        then?.Invoke();
    }

    /// <summary>Feeds the roster onto the field in order, never exceeding the stage's live cap; held while a banner is up.</summary>
    void SpawnEnemies()
    {
        if (BannerShowing) return;
        var def = CurrentStage;
        if (spawned < def.TotalEnemies && demons.Count < def.MaxAtOnce)
        {
            Demon demon = def.Enemies[spawned++].Spawn();
            demons.Add(demon);
            demon.Respawn(player);
        }
    }

    bool StageCleared => spawned >= CurrentStage.TotalEnemies && demons.Count == 0;

    /// <summary>Moves the checkpoint forward; the last stage just replays until there is a proper ending.</summary>
    void AdvanceStage()
    {
        if (!Stages.IsLast(stage)) stage++;
        checkpoint = checkpoint with { Stage = stage };
        SaveProgress();
        BeginStage();
    }

    void GoToMainMenu(bool shouldSave)
    {
        state = GameState.MainMenu;
        if (shouldSave)
        {
            highScore = Math.Max(highScore, score);
            SaveProgress();
        }
        mainMenu.Reset(KeyboardKey.C);
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
        UpdateBanner(dt);
        UpdateUltimateSequence();
        if (player.PlayerCurrentHp <= 0)
        {
            EndRound();
            return;
        }

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

        if (!cinematic)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) player.Attack(AttackKind.Light);
            else if (Raylib.IsMouseButtonPressed(MouseButton.Right)) player.Attack(AttackKind.Heavy);
            foreach (var power in Powers)
                if (Raylib.IsKeyPressed(power.Key)) power.Toggle();
        }
        foreach (var power in Powers) power.Update(dt);
        float worldDt = dt * TimeScale; // everything that is not the player ticks on the (possibly slowed) world clock

        player.Update(dt, demons.Where(d => d.IsAlive).Select(d => d.Bounds).ToList());
        popup.Update(worldDt);
        SpawnEnemies();

        var demon = demons.Find(d => d.Overlaps(player));

        if (demon != null && demon.IsAlive && player.SwingLanded)
        {
            float damage = player.IsUsingUltimate ? player.GetUltimateDamage : player.GetMeleeDamage;
            if (Math.Max(0, demon.CurrentHp - damage) <= 0)
            {
                demon.Kill(player);
                score += demon.ScorePoint;
                SaveProgress();
                Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
                Raylib.PlaySound(Assets.ScoreSound);
                popup.Show(demon.Center, demon.ScorePoint);
            }
            demon.ReceiveDamage(damage);
        }
        // if (demon.IsDead)
        // {
        //     Raylib.SetSoundVolume(Assets.ScoreSound, 0.05f);
        //     Raylib.PlaySound(Assets.ScoreSound);
        //     popup.Show(demon.Center, demon.ScorePoint);
        //     // demon.Respawn(player);
        //     // endTime = Raylib.GetTime() + PlayTime + bonusTime;
        //     // bonusTime = 0;
        // }


        foreach (var dm in demons)
        {
            dm.UpdateHover(false, dt);
            dm.Update(worldDt, player, holdFire: cinematic);
            if (!cinematic && dm.ConsumeProjectileHit(player))
            {
                Shake();
            }
        }
        demons.RemoveAll(p => p.IsDead);
        if (StageCleared && !BannerShowing)
            ShowBanner($"Stage {stage} complete!", then: AdvanceStage);
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

    // void StartSwing(Demon demon)
    // {
    //     score += demon.ScorePoint;
    //     // bonusTime = Math.Min(SecondsLeft, MaxBonusTime);
    // }

    // The ultimate starts with a frozen target pick (F when the meter is full), then plays a short cinematic:
    // zoom onto the demon, then teleport and swing, then zoom back out.
    enum UltimatePhase { None, Targeting, ZoomIn, Attack }
    UltimatePhase ultimatePhase;
    bool IsTargeting => ultimatePhase == UltimatePhase.Targeting;

    void StartUltimate(Demon demon)
    {
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
        switch (ultimatePhase)
        {
            case UltimatePhase.ZoomIn when CameraFocus.IsSettled:
                Demon? target = demons.Find(d => d.IsSelectedForUlt);
                if (target is null) { CancelUltimateSequence(); break; } // target gone (shouldn't happen: the world is frozen)
                player.Ultimate(target.Center);
                ultimatePhase = UltimatePhase.Attack;
                break;
            case UltimatePhase.Attack when !player.IsAttacking:
                CancelUltimateSequence();
                break;
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

    /// <summary>Centered title over a dark band; fades in quickly and out slowly so it never pops.</summary>
    void DrawBanner()
    {
        if (!BannerShowing) return;
        float elapsed = BannerDuration - bannerTimeLeft;
        float alpha = Math.Min(1f, Math.Min(elapsed / BannerFadeIn, bannerTimeLeft / BannerFadeOut));
        const int titleFontSize = 56;
        const int subtitleFontSize = 24;
        const int bandHeight = 130;
        int bandY = Screen.Height / 2 - bandHeight / 2;
        Raylib.DrawRectangle(0, bandY, Screen.Width, bandHeight, Raylib.Fade(Color.Black, 0.55f * alpha));
        Screen.DrawCenteredText(bannerTitle, bandY + 18, titleFontSize, Raylib.Fade(Color.Beige, alpha));
        if (bannerSubtitle.Length > 0)
            Screen.DrawCenteredText(bannerSubtitle, bandY + 18 + titleFontSize + 8, subtitleFontSize, Raylib.Fade(Color.LightGray, alpha));
    }

    void DrawHud()
    {
        DrawBanner();
        const int scoreFontSize = 22;
        HudIcons.DrawCoin(new Rectangle(ScreenMargin - 6, ScreenMargin - 6, scoreFontSize + 12, scoreFontSize + 12), Color.Gold);
        Raylib.DrawText($"{score}", ScreenMargin + scoreFontSize + 6, ScreenMargin, scoreFontSize, Color.White);
        // Raylib.DrawText($"High: {highScore}", ScreenMargin + 140, ScreenMargin, scoreFontSize, Color.Gold);
        int demonsLeft = CurrentStage.TotalEnemies - spawned + demons.Count(d => d.IsAlive);
        Raylib.DrawText($"Stage {stage}/{Stages.Count}   Demons left: {demonsLeft}", ScreenMargin, ScreenMargin + 26, 18, Color.LightGray);
        // Raylib.DrawText($"Time: {SecondsLeft:D2}", 20, 40, 16, Color.White);
        // Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
        DrawHealthBar();
        DrawUltimateBar();
        DrawPowerSlots();
        DrawAttackPrompts();
        DrawUltimateHint();
        if (slowTime.IsActive) DrawSlowTimeTint();
        int cheatY = ScreenMargin + 28;
        if (!Demon.AggroEnabled)
            Raylib.DrawText("Enemy aggro OFF [F3]", ScreenMargin, cheatY, 18, Color.Orange);
        if (Player.GodMode)
            Raylib.DrawText("God mode ON [F4]", ScreenMargin, cheatY + (Demon.AggroEnabled ? 0 : 22), 18, Color.Orange);
    }

    void DrawUltimateHint()
    {
        const int fontSize = 24;
        int y = Screen.Height - ScreenMargin - PowerSlotSize - 12 - fontSize; // bottom centre, just above the power slots
        if (IsTargeting)
            Screen.DrawCenteredText("Click an enemy to unleash your ultimate  -  [F] cancel", y, fontSize, Color.Yellow);
        else if (player.PlayerUlti >= 100 && ultimatePhase == UltimatePhase.None)
            Screen.DrawCenteredText("[F] Ultimate ready", y, fontSize, Color.Yellow);
    }

    const int BarIconSize = 28; // icon slot beside the health and ultimate bars, slightly taller than the bar itself
    const int BarIconGap = 4;

    /// <summary>Icon slot centred on a bar of <paramref name="barHeight"/> at the left screen margin.</summary>
    static Rectangle BarIconSlot(int barY, int barHeight) =>
        new(ScreenMargin, barY + (barHeight - BarIconSize) / 2f, BarIconSize, BarIconSize);

    void DrawHealthBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        int x = ScreenMargin + BarIconSize + BarIconGap;
        int y = Screen.Height - ScreenMargin - barHeight;
        int fill = (int)(barWidth * player.HealthFraction);

        HudIcons.DrawHeart(BarIconSlot(y, barHeight), Color.Red);
        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Red);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
    }

    void DrawUltimateBar()
    {
        const int barWidth = 200;
        const int barHeight = 20;
        const int margin = ScreenMargin + 25;
        int x = ScreenMargin + BarIconSize + BarIconGap;
        int y = Screen.Height - margin - barHeight;
        int fill = (int)(barWidth * player.PowerFraction);

        HudIcons.DrawEnergy(BarIconSlot(y, barHeight), Color.Yellow);
        Raylib.DrawRectangle(x, y, barWidth, barHeight, Color.DarkGray);
        Raylib.DrawRectangle(x, y, fill, barHeight, Color.Yellow);
        Raylib.DrawRectangleLines(x, y, barWidth, barHeight, Color.White);
    }

    const int PowerSlotSize = 54;
    const int PowerSlotGap = 12;
    const int PromptSlotSize = 70;
    const int PromptSlotGap = 14;
    const float SlotRoundness = 0.2f;

    /// <summary>Bottom centre: one square per power slot, with the key in the corner and a fill showing the active time or cooldown left.</summary>
    void DrawPowerSlots()
    {
        const int keyFontSize = 12;
        int totalWidth = PowerSlots * PowerSlotSize + (PowerSlots - 1) * PowerSlotGap;
        int x = Screen.Width / 2 - totalWidth / 2;
        int y = Screen.Height - ScreenMargin - PowerSlotSize;

        for (int i = 0; i < PowerSlots; i++)
        {
            var slot = new Rectangle(x + i * (PowerSlotSize + PowerSlotGap), y, PowerSlotSize, PowerSlotSize);
            Power? power = i < Powers.Length ? Powers[i] : null;
            Raylib.DrawRectangleRounded(slot, SlotRoundness, 6, Raylib.Fade(Color.Black, 0.55f));

            if (power is null)
            {
                Raylib.DrawRectangleRoundedLinesEx(slot, SlotRoundness, 6, 2f, Raylib.Fade(Color.Gray, 0.5f));
                continue;
            }

            // Active: a blue bar drains downward as the effect runs out. Cooling down: a grey bar climbs back up to ready.
            float fill = power.IsActive ? power.ActiveFraction : power.CooldownFraction;
            if (fill > 0)
            {
                float fillHeight = slot.Height * fill;
                Color fillColor = power.IsActive ? Raylib.Fade(Color.SkyBlue, 0.45f) : Raylib.Fade(Color.White, 0.18f);
                Raylib.BeginScissorMode((int)slot.X, (int)(slot.Y + slot.Height - fillHeight), (int)slot.Width, (int)MathF.Ceiling(fillHeight));
                Raylib.DrawRectangleRounded(slot, SlotRoundness, 6, fillColor);
                Raylib.EndScissorMode();
            }

            Color tint = power.IsActive ? Color.SkyBlue : power.IsReady ? Color.White : Color.Gray;
            power.DrawIcon(slot, tint);
            Raylib.DrawRectangleRoundedLinesEx(slot, SlotRoundness, 6, 2f, power.IsActive ? Color.SkyBlue : Color.LightGray);
            Raylib.DrawText(power.KeyLabel, (int)slot.X + 5, (int)slot.Y + 4, keyFontSize, power.IsReady || power.IsActive ? Color.White : Color.Gray);
        }
    }

    /// <summary>Bottom right: the two mouse buttons with the attack each one throws; the slot lights up mid-swing.</summary>
    void DrawAttackPrompts()
    {
        const int labelFontSize = 12;
        int totalWidth = 2 * PromptSlotSize + PromptSlotGap;
        int x = Screen.Width - ScreenMargin - totalWidth;
        int y = Screen.Height - ScreenMargin - PromptSlotSize;
        (string label, bool leftButton, AttackKind kind)[] prompts = [("Light", true, AttackKind.Light), ("Heavy", false, AttackKind.Heavy)];

        for (int i = 0; i < prompts.Length; i++)
        {
            var (label, leftButton, kind) = prompts[i];
            var slot = new Rectangle(x + i * (PromptSlotSize + PromptSlotGap), y, PromptSlotSize, PromptSlotSize);
            bool swinging = player.IsAttacking && player.CurrentAttack == kind;
            Raylib.DrawRectangleRounded(slot, SlotRoundness, 6, Raylib.Fade(Color.Black, 0.55f));
            // The icon sits a little high so the label fits underneath it.
            var iconArea = new Rectangle(slot.X, slot.Y, slot.Width, slot.Height - labelFontSize - 4);
            HudIcons.DrawMouse(iconArea, leftButton, swinging ? Color.Yellow : Color.White, swinging ? Color.Yellow : Color.LightGray);
            Raylib.DrawRectangleRoundedLinesEx(slot, SlotRoundness, 6, 2f, swinging ? Color.Yellow : Color.LightGray);
            int labelWidth = Raylib.MeasureText(label, labelFontSize);
            Raylib.DrawText(label, (int)(slot.X + slot.Width / 2 - labelWidth / 2f), (int)(slot.Y + slot.Height - labelFontSize - 6), labelFontSize, Color.LightGray);
        }
    }

    /// <summary>Faint blue wash over the whole screen so it's obvious the world is running slow.</summary>
    static void DrawSlowTimeTint() =>
        Raylib.DrawRectangle(0, 0, Screen.Width, Screen.Height, Raylib.Fade(Color.SkyBlue, 0.07f));

}
