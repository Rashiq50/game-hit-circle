using System.Numerics;
using Raylib_cs;

// Asset paths are relative, so run from the exe folder no matter how the game was launched (shortcut, double-click, etc.).
Environment.CurrentDirectory = AppContext.BaseDirectory;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(World.Width, World.Height, "Hit them all!");
// Borderless fullscreen: fills the monitor at its native resolution without a display-mode switch, so alt-tab is instant.
// The world camera fits itself to whatever size the window ends up with. F11 toggles back to a window (see Game.Update).
Raylib.ToggleBorderlessWindowed();
Raylib.InitAudioDevice();

Raylib.SetTargetFPS(60);
Raylib.SetExitKey(KeyboardKey.Null); // Esc is the pause key, not the quit key

Assets.Load(); // must come after InitWindow: raylib needs a GL context to upload textures
BlurEffect.Load();
GrayscaleEffect.Load();
CollisionMap.Load("textures/stages/castle_floor.tmx");
var game = new Game();

while (!Raylib.WindowShouldClose() && !game.QuitRequested)
{
    game.Update(Raylib.GetFrameTime());
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    World.FitCamera(game.ShakeOffset, game.CameraFollowsPlayer); // every frame: the window may have been resized
    if (game.BlurWorld) BlurEffect.Begin();
    Raylib.BeginMode2D(World.Camera);
    game.DrawWorld();
    Raylib.EndMode2D();
    if (game.BlurWorld) BlurEffect.End();
    game.DrawUi(); // HUD and menus stay in screen space, unaffected by camera pan/zoom
    Raylib.EndDrawing();
}

BlurEffect.Unload();
GrayscaleEffect.Unload();
Assets.Unload();
Raylib.CloseAudioDevice();
Raylib.CloseWindow();
