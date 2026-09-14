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
float boost_multiplier = 2.5f;
float speed = 100f;

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
    speed = boost ? speed * boost_multiplier : speed;

    if (Raylib.IsKeyDown(KeyboardKey.D)) topLeftX += speed * dt;
    if (topLeftX + sizeX > currentScreenWidth)
    {
        topLeftX = currentScreenWidth - sizeX;
    }
    if (Raylib.IsKeyDown(KeyboardKey.A)) topLeftX -= speed * dt;
    if (topLeftX <= 0)
    {
        topLeftX = 0;
    }
    if (Raylib.IsKeyDown(KeyboardKey.W)) topLeftY -= speed * dt;
    if (topLeftY <= 0)
    {
        topLeftY = 0;
    }
    if (Raylib.IsKeyDown(KeyboardKey.S)) topLeftY += speed * dt;
    if (topLeftY + sizeY > currentScreenHeight)
    {
        topLeftY = currentScreenHeight - sizeY;
    }

    bool hasHit(float centerX, float centerY)
    {
        if (topLeftY <= centerY + radius && topLeftY >= centerY - (radius + sizeY) && topLeftX >= centerX - (radius + sizeX) && topLeftX <= centerX + radius) return true;

        return false;
    }

    if (hasHit(centerX, centerY))
    {
        isHit = true;
        score += 10;
        // draw new circle
        float newCenterX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
        float newCenterY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
        while (hasHit(newCenterX, newCenterY))
        {
            newCenterX = Random.Shared.Next(50, Raylib.GetScreenWidth() - 50);
            newCenterY = Random.Shared.Next(50, Raylib.GetScreenHeight() - 50);
        }
        centerX = newCenterX;
        centerY = newCenterY;
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