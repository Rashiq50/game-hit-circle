using System.Numerics;
using Raylib_cs;

const int screenWidth = 1000;
const int screenHeight = 600;
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(screenWidth, screenHeight, "Hit them all!");
Raylib.SetTargetFPS(60);
Color cyan = new Color(0, 255, 255, 255);
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
float eraseTime = 0.2f; // in seconds
float dyingElapsed = 0;
bool hasStarted = false;
bool isPlaying = false;
// bool gameOver = false;
double endTimer = 0;
const int playTime = 5;
int bonusTime = 0;


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
    bonusTime = (int)endTimer - (int)Raylib.GetTime();
    isDying = true;
}

void startGame()
{
    hasStarted = true;
    endTimer = Raylib.GetTime() + playTime;
    isPlaying = true;
    setNewCircle();
}

while (!Raylib.WindowShouldClose())
{
    Vector2 mousePoint = Raylib.GetMousePosition();
    bool boost = false;
    float dt = Raylib.GetFrameTime();
    int currentScreenWidth = Raylib.GetScreenWidth();
    int currentScreenHeight = Raylib.GetScreenHeight();
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);

    // Welcome screen handle & text
    if (!hasStarted && !isPlaying)
    {
        const string startText = "Press any key to continue";
        int fontSize = 48;
        int textSize = Raylib.MeasureText(startText, fontSize);
        Raylib.DrawText(startText, currentScreenWidth / 2 - textSize / 2, currentScreenHeight / 2 - fontSize / 2, fontSize, Color.Gray);
    }
    bool anyKey = Raylib.GetKeyPressed() != 0;
    bool anyMouse = Raylib.IsMouseButtonPressed(MouseButton.Left)
                 || Raylib.IsMouseButtonPressed(MouseButton.Right)
                 || Raylib.IsMouseButtonPressed(MouseButton.Middle);

    if (!hasStarted && (anyKey || anyMouse)) startGame();

    // Game over handle & text
    if (Raylib.GetTime() > endTimer && hasStarted)
    {
        isPlaying = false;
        endTimer = 0;
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
    if (!isPlaying && hasStarted)
    {
        if (Raylib.IsKeyDown(KeyboardKey.R)) startGame();
        if (Raylib.IsKeyDown(KeyboardKey.Q)) Raylib.CloseWindow();
    }

    // Main game
    if (isPlaying)
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isClicked(mousePoint[0], mousePoint[1]) && !isDying) scoreUp();
        }

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift)) boost = true;
        float currentSpeed = boost ? speed * boost_multiplier : speed;

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
                setNewCircle();
                endTimer = Raylib.GetTime() + playTime + bonusTime;
                bonusTime = 0;
            }
        }

        Raylib.DrawRectangleV(new Vector2(topLeftX, topLeftY), new Vector2(sizeX, sizeY), Color.DarkBlue);
        Raylib.DrawCircleV(new Vector2(centerX, centerY), radius, Raylib.Fade(Color.Beige, 1));
        Raylib.DrawText($"Score:", 20, 20, 18, Color.White);
        Raylib.DrawText($" {score}", 90, 20, 18, Color.White);
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

Raylib.CloseWindow();