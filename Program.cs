using System.Numerics;
using Raylib_cs;

const int screenWidth = 1000;
const int screenHeight = 600;
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(screenWidth, screenHeight, "Hit them all!");
Raylib.SetTargetFPS(60);
Texture2D background = Raylib.LoadTexture("tile.png");

var game = new Game();

while (!Raylib.WindowShouldClose() && !game.QuitRequested)
{
    float dt = Raylib.GetFrameTime();
    game.Update(dt);
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    Raylib.DrawTexturePro(background,
    new Rectangle(0, 0, background.Width, background.Height),
    new Rectangle(0, 0, Screen.Width, Screen.Height),
    Vector2.Zero, 0, Color.White);
    game.Draw(dt);
    Raylib.EndDrawing();
}


Raylib.UnloadTexture(background);
Raylib.UnloadTexture(Demon.idleSheet);
Raylib.UnloadTexture(Demon.deathSheet);
foreach (var t in Player.heroIdle.Concat(Player.heroWalk).Concat(Player.heroRun).Concat(Player.heroAxe)) Raylib.UnloadTexture(t);
Raylib.CloseWindow();

enum Direction { Down, Up, Left, Right }
enum GameState { Playing, GameOver, Welcome }
enum PlayerState { Idle, Attacking, Running, Walking }
enum DemonState { Idle, Dying }
class Game
{
    const int PlayTime = 5;
    const int PointsPerHit = 10;
    int score;
    double endTime;
    int bonusTime;
    static string highScorePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HitThemAll", "highscore.txt");
    int highScore = loadHighScore();

    static int loadHighScore()
    {
        try
        {
            if (File.Exists(highScorePath) && int.TryParse(File.ReadAllText(highScorePath), out int saved))
                return saved;
        }
        catch (IOException) { }
        return 0;
    }
    GameState state = GameState.Welcome;
    public bool QuitRequested { get; private set; }

    readonly Player player = new();
    readonly Demon demon = new();
    readonly ScorePopup popup = new();

    static bool AnyInputPressed() =>
    Raylib.GetKeyPressed() != 0
    || Raylib.IsMouseButtonPressed(MouseButton.Left)
    || Raylib.IsMouseButtonPressed(MouseButton.Right)
    || Raylib.IsMouseButtonPressed(MouseButton.Middle);
    void StartRound()
    {
        state = GameState.Playing;
        score = 0;
        bonusTime = 0;
        endTime = Raylib.GetTime() + PlayTime;
        player.Reset();
        demon.Respawn(player);
    }

    void OnHit()
    {
        player.playerState = PlayerState.Attacking;
        score += PointsPerHit;
        int rewardTime = (int)endTime - (int)Raylib.GetTime();
        bonusTime = rewardTime <= 5 ? rewardTime : 5;
        demon.demonState = DemonState.Dying;
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

        bool clickedTarget = Raylib.IsMouseButtonPressed(MouseButton.Left) && demon.ContainsPoint(Raylib.GetMousePosition());
        if (!demon.demonState.Equals(DemonState.Dying) && (clickedTarget || demon.Overlaps(player))) OnHit();

        if (demon.IsFullyDead(dt))
        {
            popup.Show(demon.Center);
            demon.Respawn(player);
            endTime = Raylib.GetTime() + PlayTime + bonusTime;
            bonusTime = 0;
        }
    }

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

    public void Draw(float dt)
    {
        switch (state)
        {
            case GameState.Welcome:
                DrawWelcome();
                break;

            case GameState.Playing:
                player.Draw(dt);
                demon.Draw(dt);
                popup.Draw();
                DrawHud();
                break;

            case GameState.GameOver:
                DrawGameOver();
                break;
        }
    }

    static void DrawWelcome()
    {
        const string titleText = "Hit them all!";
        int titleFontSize = 80;
        int titleSize = Raylib.MeasureText(titleText, titleFontSize);
        Raylib.DrawText(titleText, Screen.Width / 2 - titleSize / 2, Screen.Height / 2 - titleFontSize - 20, titleFontSize, Color.Beige);

        const string startText = "Press any key to continue";
        int fontSize = 32;
        int textSize = Raylib.MeasureText(startText, fontSize);
        Raylib.DrawText(startText, Screen.Width / 2 - textSize / 2, Screen.Height / 2 + 20, fontSize, Color.Gray);
    }

    static void DrawGameOver()
    {
        const string gameOverText = "Game Over!";
        int fontSize = 62;
        int textSize = Raylib.MeasureText(gameOverText, fontSize);
        Raylib.DrawText("Game Over!", Screen.Width / 2 - textSize / 2, Screen.Height / 2 - fontSize / 2, fontSize, Color.Red);

        const string replayText = "[R] Replay";
        const string quitText = "[Q] Quit";
        int optionFontSize = 28;
        int optionY = Screen.Height / 2 + fontSize / 2 + 20;
        int replaySize = Raylib.MeasureText(replayText, optionFontSize);
        int quitSize = Raylib.MeasureText(quitText, optionFontSize);
        Raylib.DrawText(replayText, Screen.Width / 2 - replaySize / 2, optionY, optionFontSize, Color.Gray);
        Raylib.DrawText(quitText, Screen.Width / 2 - quitSize / 2, optionY + optionFontSize + 10, optionFontSize, Color.Gray);
    }

    void DrawHud()
    {
        Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
        Raylib.DrawText($"High: {highScore}", 160, 20, 18, Color.Gold);
        if (state.Equals(GameState.Playing) && endTime != 0)
        {
            double currentTime = Raylib.GetTime();
            double diff = endTime - currentTime;
            Raylib.DrawText($"Time: {(int)diff:D2}", 20, 40, 16, Color.White);
        }
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Screen.Width - 100, 20, 14, Color.DarkGray);
    }
}

class Screen
{
    public static int Width => Raylib.GetScreenWidth();
    public static int Height => Raylib.GetScreenHeight();
    const int SpawnMargin = 50;

    public static Vector2 RandomPoint() => new(
    Random.Shared.Next(SpawnMargin, Width - SpawnMargin),
    Random.Shared.Next(SpawnMargin, Height - SpawnMargin));
}

class Player
{
    public const int Size = 40;
    const float Speed = 100f;
    const float BoostMultiplier = 2.5f;


    int heroFrame;
    // bool isAttacking = isDying && hasHit(centerX, centerY);
    // bool isAttacking = false;

    public PlayerState playerState = PlayerState.Idle;

    public Vector2 Position = Screen.RandomPoint();
    // hero sprite strips: 80x80 frames, one strip per facing direction (index = Direction)
    const int heroFrameSize = 80;
    const float heroDrawSize = 96f;
    const float heroAnimFps = 10f;
    public static Texture2D[] heroIdle = LoadHeroStrips("idle/idle");
    public static Texture2D[] heroWalk = LoadHeroStrips("walk/walk");
    public static Texture2D[] heroRun = LoadHeroStrips("run/run");
    public static Texture2D[] heroAxe = LoadHeroStrips("axe attack/axe_attack");
    public Direction heroFacing = Direction.Down;
    float heroAnimElapsed = 0;

    static Texture2D[] LoadHeroStrips(string basePath)
    {
        string[] suffixes = { "down", "up", "left", "right" };
        return suffixes.Select(d => Raylib.LoadTexture($"textures/hero/{basePath}_{d}.png")).ToArray();
    }

    public void Reset() => Position = Screen.RandomPoint();

    public void Update(float dt)
    {
        float speed = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? Speed * BoostMultiplier : Speed;
        float step = speed * dt;

        if (Raylib.IsKeyDown(KeyboardKey.D)) { Position.X += step; heroFacing = Direction.Right; playerState = PlayerState.Walking; }
        if (Raylib.IsKeyDown(KeyboardKey.A)) { Position.X -= step; heroFacing = Direction.Left; playerState = PlayerState.Walking; }
        if (Raylib.IsKeyDown(KeyboardKey.W)) { Position.Y -= step; heroFacing = Direction.Up; playerState = PlayerState.Walking; }
        if (Raylib.IsKeyDown(KeyboardKey.S)) { Position.Y += step; heroFacing = Direction.Down; playerState = PlayerState.Walking; }

        Position.X = Math.Clamp(Position.X, 0, Screen.Width - Size);
        Position.Y = Math.Clamp(Position.Y, 0, Screen.Height - Size);
    }

    public void Draw(float dt)
    {
        Texture2D heroSheet;
        if (playerState.Equals(PlayerState.Attacking))
        {
            heroSheet = heroAxe[(int)heroFacing];
            int attackFrames = heroSheet.Width / heroFrameSize;
            heroFrame = Math.Min((int)(attackFrames), attackFrames - 1);
        }
        else
        {
            heroSheet = (playerState.Equals(PlayerState.Walking) && Raylib.IsKeyDown(KeyboardKey.LeftShift) ? heroRun
                       : playerState.Equals(PlayerState.Walking) ? heroWalk
                       : heroIdle)[(int)heroFacing];
            heroAnimElapsed += dt;
            heroFrame = (int)(heroAnimElapsed * heroAnimFps) % (heroSheet.Width / heroFrameSize);
        }
        Raylib.DrawTexturePro(heroSheet,
            new Rectangle(heroFrame * heroFrameSize, 0, heroFrameSize, heroFrameSize),
            new Rectangle(Position.X + Size / 2f - heroDrawSize / 2, Position.Y + Size / 2f - heroDrawSize / 2, heroDrawSize, heroDrawSize),
            Vector2.Zero, 0, Color.White);
    }
}

class Demon
{
    // enemy sprite sheets: horizontal strips of 256x256 frames
    public static Texture2D idleSheet = Raylib.LoadTexture("textures/Enemy-Melee-Idle-S.png");
    public static Texture2D deathSheet = Raylib.LoadTexture("textures/Enemy-Melee-Death.png");
    const int frameSize = 256;
    readonly int idleFrameCount = idleSheet.Width / frameSize;
    readonly int deathFrameCount = deathSheet.Width / frameSize;
    const float idleFps = 12f;
    const float spriteDrawSize = 110f; // on-screen size of one frame
    float idleElapsed = 0;
    readonly static int RespawnAttemptCap = 10;
    public DemonState demonState = DemonState.Idle;
    public Vector2 Center;
    public float Radius = 25;
    readonly float eraseTime = 0.6f; // death animation duration
    float dyingElapsed = 0;
    public bool ContainsPoint(Vector2 p) =>
    p.X >= Center.X - Radius && p.X <= Center.X + Radius &&
    p.Y >= Center.Y - Radius && p.Y <= Center.Y + Radius;

    public bool Overlaps(Player player) => Overlaps(Center, player);

    bool Overlaps(Vector2 center, Player player) =>
        player.Position.X <= center.X + Radius && player.Position.X + Player.Size >= center.X - Radius &&
        player.Position.Y <= center.Y + Radius && player.Position.Y + Player.Size >= center.Y - Radius;

    public void Respawn(Player player)
    {
        Vector2 candidate = Screen.RandomPoint();
        for (int attempt = 0; attempt <= RespawnAttemptCap && Overlaps(candidate, player); attempt++)
            candidate = Screen.RandomPoint();

        Center = candidate;
        dyingElapsed = 0;
        demonState = DemonState.Idle;
    }

    public void Draw(float dt)
    {
        Texture2D sheet;
        int frame;
        if (demonState.Equals(DemonState.Dying))
        {
            sheet = deathSheet;
            frame = Math.Min((int)(dyingElapsed / eraseTime * deathFrameCount), deathFrameCount - 1);
        }
        else
        {
            sheet = idleSheet;
            idleElapsed += dt;
            frame = (int)(idleElapsed * idleFps) % idleFrameCount;
        }
        Raylib.DrawTexturePro(sheet,
            new Rectangle(frame * frameSize, 0, frameSize, frameSize),
            new Rectangle(Center.X - spriteDrawSize / 2, Center.Y - spriteDrawSize / 2, spriteDrawSize, spriteDrawSize),
            Vector2.Zero, 0, Color.White);
    }

    public bool IsFullyDead(float dt)
    {
        if (demonState.Equals(DemonState.Dying))
        {
            if (dyingElapsed <= eraseTime)
            {
                dyingElapsed += dt;
            }
            else
            {
                return true;
                // popupActive = true;
                // popupX = centerX;
                // popupY = centerY;
                // popupElapsed = 0;
                // Respawn();
                // endTimer = Raylib.GetTime() + playTime + bonusTime;
                // bonusTime = 0;
            }
        }
        return false;
    }
}

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
