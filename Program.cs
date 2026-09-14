using System.Numerics;
using Raylib_cs;

const int screenWidth = 960;
const int screenHeight = 540;
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(screenWidth, screenHeight, "Hit them all!");
Raylib.SetTargetFPS(60);

// circle values
float centerX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
float centerY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
int radius = 25;

// cube values
float topLeftX = 10f;
float topLeftY = 20f;
int sizeX = 40;
int sizeY = 40;

// game values
int score = 0;
const float boost_multiplier = 2.5f;
const float speed = 100f;
const int circleRetryAttemptCap = 10;
int circleRetryAttempts = 0;


bool hasHit(float centerX, float centerY)
{
    if (topLeftY <= centerY + radius && topLeftY >= centerY - (radius + sizeY) && topLeftX >= centerX - (radius + sizeX) && topLeftX <= centerX + radius) return true;

    return false;
}

while (!Raylib.WindowShouldClose())
{
    bool boost = false;
    bool isHit = false;
    float dt = Raylib.GetFrameTime();
    int currentScreenWidth = Raylib.GetScreenWidth();
    int currentScreenHeight = Raylib.GetScreenHeight();
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);

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

    if (hasHit(centerX, centerY))
    {
        isHit = true;
        score += 10;
        // draw new circle
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

    // later work on showing proper hit feedback with proper timing etc. for now ignore
    if (isHit)
    {
        Raylib.DrawText("Hit !!", 20, currentScreenHeight - 10, 18, Color.Red);
    }

    Raylib.DrawCircleV(new Vector2(centerX, centerY), radius, Color.Beige);
    Raylib.DrawRectangleV(new Vector2(topLeftX, topLeftY), new Vector2(sizeX, sizeY), Color.DarkBlue);
    Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
    Raylib.DrawText($"FPS: {Raylib.GetFPS()}", currentScreenWidth - 100, 20, 24, Color.DarkGray);
    Raylib.EndDrawing();
}

Raylib.CloseWindow();