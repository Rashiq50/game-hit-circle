using System.Numerics;
using Raylib_cs;

const int screenWidth = 1000;
const int screenHeight = 600;
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(screenWidth, screenHeight, "Hit them all!");
Raylib.SetTargetFPS(60);
Texture2D background = Raylib.LoadTexture("bg.png");
// circle values
float centerX = 0;
float centerY = 0;
float radius = 25;

// cube values
float topLeftX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
float topLeftY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
int sizeX = 40;
int sizeY = 40;

// game values
int score = 0;
const float boost_multiplier = 2.5f;
const float speed = 100f;
const int circleRetryAttemptCap = 10;
int circleRetryAttempts = 0;
bool isDying = false;
float eraseTime = 0.2f;
float dyingElapsed = 0;
bool hasStarted = false;
bool isPlaying = false;
double endTimer = 0;
const int playTime = 5;
int bonusTime = 0;
bool shouldQuit = false;

// high score, persisted to a file in the user's local app data folder
string highScorePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HitThemAll", "highscore.txt");
int highScore = loadHighScore();

int loadHighScore()
{
    try
    {
        if (File.Exists(highScorePath) && int.TryParse(File.ReadAllText(highScorePath), out int saved))
            return saved;
    }
    catch (IOException) { }
    return 0;
}

void saveHighScore()
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(highScorePath)!);
        File.WriteAllText(highScorePath, highScore.ToString());
    }
    catch (IOException) { }
}

// score popup values
bool popupActive = false;
float popupX = 0;
float popupY = 0;
float popupElapsed = 0;
const float popupScaleTime = 0.15f; // scale up duration
const float popupHoldTime = 0.5f;   // time popup stays at full size before fading
const float popupFadeTime = 0.3f;   // fade out duration
const int popupFontSize = 24;
const string popupText = "+10";


bool hasHit(float centerX, float centerY)
{
    return topLeftY <= centerY + radius && topLeftY >= centerY - (radius + sizeY) && topLeftX >= centerX - (radius + sizeX) && topLeftX <= centerX + radius;
}

bool isClicked(float mouseX, float mouseY)
{
    return mouseX <= centerX + radius && mouseX >= centerX - radius && mouseY >= centerY - radius && mouseY <= centerY + radius;
}

void setNewCircle()
{
    float newCenterX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
    float newCenterY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
    while (hasHit(newCenterX, newCenterY) && circleRetryAttempts <= circleRetryAttemptCap)
    {
        newCenterX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
        newCenterY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
        circleRetryAttempts++;
    }
    centerX = newCenterX;
    centerY = newCenterY;
    circleRetryAttempts = 0;
}

void scoreUp()
{
    score += 10;
    int rewardTime = (int)endTimer - (int)Raylib.GetTime();
    bonusTime = rewardTime <= 5 ? rewardTime : 5;
    isDying = true;
}

void startGame()
{
    score = 0;
    hasStarted = true;
    endTimer = Raylib.GetTime() + playTime;
    isPlaying = true;
    setNewCircle();
}

while (!Raylib.WindowShouldClose() && !shouldQuit)
{
    Vector2 mousePoint = Raylib.GetMousePosition();
    float dt = Raylib.GetFrameTime();
    int currentScreenWidth = Raylib.GetScreenWidth();
    int currentScreenHeight = Raylib.GetScreenHeight();
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    // stretch the background image to fill the (resizable) window
    Raylib.DrawTexturePro(background,
        new Rectangle(0, 0, background.Width, background.Height),
        new Rectangle(0, 0, currentScreenWidth, currentScreenHeight),
        Vector2.Zero, 0, Color.White);

    // Welcome screen handle & text
    if (!hasStarted && !isPlaying)
    {
        const string titleText = "Hit them all!";
        int titleFontSize = 80;
        int titleSize = Raylib.MeasureText(titleText, titleFontSize);
        Raylib.DrawText(titleText, currentScreenWidth / 2 - titleSize / 2, currentScreenHeight / 2 - titleFontSize - 20, titleFontSize, Color.Beige);

        const string startText = "Press any key to continue";
        int fontSize = 32;
        int textSize = Raylib.MeasureText(startText, fontSize);
        Raylib.DrawText(startText, currentScreenWidth / 2 - textSize / 2, currentScreenHeight / 2 + 20, fontSize, Color.Gray);
    }
    bool anyKey = Raylib.GetKeyPressed() != 0;
    bool anyMouse = Raylib.IsMouseButtonPressed(MouseButton.Left)
                 || Raylib.IsMouseButtonPressed(MouseButton.Right)
                 || Raylib.IsMouseButtonPressed(MouseButton.Middle);

    if (!hasStarted && (anyKey || anyMouse)) startGame();

    // Game over handle & text
    if (isPlaying && Raylib.GetTime() > endTimer)
    {
        isPlaying = false;
        endTimer = 0;
        if (score > highScore)
        {
            highScore = score;
            saveHighScore();
        }
    }
    if (hasStarted && !isPlaying)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.R)) startGame();
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) shouldQuit = true;
    }
    if (hasStarted && !isPlaying)
    {
        const string gameOverText = "Game Over!";
        int fontSize = 62;
        int textSize = Raylib.MeasureText(gameOverText, fontSize);
        Raylib.DrawText("Game Over!", currentScreenWidth / 2 - textSize / 2, currentScreenHeight / 2 - fontSize / 2, fontSize, Color.Red);

        const string replayText = "[R] Replay";
        const string quitText = "[Q] Quit";
        int optionFontSize = 28;
        int optionY = currentScreenHeight / 2 + fontSize / 2 + 20;
        int replaySize = Raylib.MeasureText(replayText, optionFontSize);
        int quitSize = Raylib.MeasureText(quitText, optionFontSize);
        Raylib.DrawText(replayText, currentScreenWidth / 2 - replaySize / 2, optionY, optionFontSize, Color.Gray);
        Raylib.DrawText(quitText, currentScreenWidth / 2 - quitSize / 2, optionY + optionFontSize + 10, optionFontSize, Color.Gray);
    }

    // Main game
    if (isPlaying)
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isClicked(mousePoint[0], mousePoint[1]) && !isDying) scoreUp();
        }

        float currentSpeed = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? speed * boost_multiplier : speed;

        if (Raylib.IsKeyDown(KeyboardKey.D)) topLeftX += currentSpeed * dt;
        if (topLeftX + sizeX > currentScreenWidth)
        {
            topLeftX = currentScreenWidth - sizeX;
        }
        if (Raylib.IsKeyDown(KeyboardKey.A)) topLeftX -= currentSpeed * dt;
        if (topLeftX <= 0)
        {
            topLeftX = 0;
        }
        if (Raylib.IsKeyDown(KeyboardKey.W)) topLeftY -= currentSpeed * dt;
        if (topLeftY <= 0)
        {
            topLeftY = 0;
        }
        if (Raylib.IsKeyDown(KeyboardKey.S)) topLeftY += currentSpeed * dt;
        if (topLeftY + sizeY > currentScreenHeight)
        {
            topLeftY = currentScreenHeight - sizeY;
        }

        if (hasHit(centerX, centerY) && !isDying) scoreUp();

        if (isDying)
        {
            if (dyingElapsed <= eraseTime)
            {
                float t = dyingElapsed / eraseTime;
                radius *= 1 - t;
                dyingElapsed += dt;
            }
            else
            {
                isDying = false;
                dyingElapsed = 0;
                radius = 25;
                popupActive = true;
                popupX = centerX;
                popupY = centerY;
                popupElapsed = 0;
                setNewCircle();
                endTimer = Raylib.GetTime() + playTime + bonusTime;
                bonusTime = 0;
            }
        }

        Raylib.DrawRectangleV(new Vector2(topLeftX, topLeftY), new Vector2(sizeX, sizeY), Color.DarkBlue);
        Raylib.DrawCircleV(new Vector2(centerX, centerY), radius, Raylib.Fade(Color.Beige, 1));

        // Floating "+10" popup with animation
        if (popupActive)
        {
            float scale;
            float alpha;
            if (popupElapsed < popupScaleTime)
            {
                scale = popupElapsed / popupScaleTime;
                alpha = 1;
            }
            else if (popupElapsed < popupScaleTime + popupHoldTime)
            {
                scale = 1;
                alpha = 1;
            }
            else if (popupElapsed < popupScaleTime + popupHoldTime + popupFadeTime)
            {
                scale = 1;
                alpha = 1 - (popupElapsed - popupScaleTime - popupHoldTime) / popupFadeTime;
            }
            else
            {
                scale = 0;
                alpha = 0;
                popupActive = false;
            }

            if (popupActive)
            {
                int fontSize = Math.Max(1, (int)(popupFontSize * scale));
                int textWidth = Raylib.MeasureText(popupText, fontSize);
                float drift = popupElapsed * 20; // float upward slowly
                Raylib.DrawText(popupText, (int)(popupX - textWidth / 2f), (int)(popupY - fontSize / 2f - drift), fontSize, Raylib.Fade(Color.Green, alpha));
                popupElapsed += dt;
            }
        }
        Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
        Raylib.DrawText($"High: {highScore}", 160, 20, 18, Color.Gold);
        if (hasStarted && endTimer != 0)
        {
            double currentTime = Raylib.GetTime();
            double diff = endTimer - currentTime;
            Raylib.DrawText($"Time: {(int)diff:D2}", 20, 40, 16, Color.White);
        }
        Raylib.DrawText($"FPS: {Raylib.GetFPS()}", currentScreenWidth - 100, 20, 14, Color.DarkGray);
    }

    Raylib.EndDrawing();
}

Raylib.UnloadTexture(background);
Raylib.CloseWindow();