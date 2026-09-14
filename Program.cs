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
float radius = 25;

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

bool isDying = false;
float eraseTime = 0.2f; // in seconds
float dyingElapsed = 0;

bool hasHit(float centerX, float centerY)
{
    if (!isDying)
    {
        return topLeftY <= centerY + radius && topLeftY >= centerY - (radius + sizeY) && topLeftX >= centerX - (radius + sizeX) && topLeftX <= centerX + radius;
    }
    return false;
}

void drawNewCircle()
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
bool isHit = false;

while (!Raylib.WindowShouldClose())
{
    bool boost = false;
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
        score += 10;
        isHit = true;
        isDying = true;
    }
    float t = dyingElapsed / eraseTime;
    if (isHit && dyingElapsed <= eraseTime)
    {
        Raylib.DrawText("Hit !!", 20, currentScreenHeight - 10, 18, Color.Red);
        radius *= 1 - t;
        centerX += 5;
        // centerY -= 4;
        dyingElapsed += dt;
    }
    else
    {
        isDying = false;
        dyingElapsed = 0;
        radius = 25;
    }

    if (isHit && !isDying)
    {
        drawNewCircle();
        isHit = false;
    }
    Raylib.DrawCircleV(new Vector2(centerX, centerY), radius, Raylib.Fade(Color.Beige, 1 - t));
    Raylib.DrawRectangleV(new Vector2(topLeftX, topLeftY), new Vector2(sizeX, sizeY), Color.DarkBlue);
    Raylib.DrawText($"Score: {score}", 20, 20, 18, Color.White);
    Raylib.DrawText($"FPS: {Raylib.GetFPS()}", currentScreenWidth - 100, 20, 24, Color.DarkGray);
    Raylib.EndDrawing();
}

Raylib.CloseWindow();