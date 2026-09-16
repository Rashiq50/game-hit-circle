using System.Numerics;
using Raylib_cs;

/// <summary>Owns every texture: loaded once after the window exists, unloaded once before it closes.</summary>
static class Assets
{
    const int HeroFrameSize = 80;
    const int DemonFrameSize = 256;

    public static Texture2D Background, Fireball, WelcomBackground, MainMenuBackground;
    public static SpriteStrip DemonIdle, DemonDeath;
    public static Sound SwordSound, ScoreSound;

    public static Color OverlayBlack = new Color(0, 0, 0, 200);
    public static Color OverlayBlack2 = new Color(0, 0, 0, 100);
    // one strip per facing direction, indexed by (int)Direction
    public static SpriteStrip[] HeroIdle = [], HeroWalk = [], HeroRun = [], HeroAxe = [];

    public static void Load()
    {
        Background = Raylib.LoadTexture("tile.png");
        WelcomBackground = Raylib.LoadTexture("textures/welcome.png");
        MainMenuBackground = Raylib.LoadTexture("textures/mainmenu.png");
        Fireball = Raylib.LoadTexture("textures/fireball.png");
        DemonIdle = new(Raylib.LoadTexture("textures/Enemy-Melee-Idle-S.png"), DemonFrameSize);
        DemonDeath = new(Raylib.LoadTexture("textures/Enemy-Melee-Death.png"), DemonFrameSize);
        HeroIdle = LoadHeroStrips("idle/idle");
        HeroWalk = LoadHeroStrips("walk/walk");
        HeroRun = LoadHeroStrips("run/run");
        HeroAxe = LoadHeroStrips("axe attack/axe_attack");
        SwordSound = Raylib.LoadSound("sounds/violent-sword-slice-393848.mp3");
        ScoreSound = Raylib.LoadSound("sounds/level-up-523624.mp3");
    }

    public static void Unload()
    {
        Raylib.UnloadTexture(Background);
        Raylib.UnloadTexture(WelcomBackground);
        Raylib.UnloadTexture(MainMenuBackground);
        Raylib.UnloadTexture(Fireball);
        Raylib.UnloadTexture(DemonIdle.Texture);
        Raylib.UnloadTexture(DemonDeath.Texture);
        foreach (var strip in HeroIdle.Concat(HeroWalk).Concat(HeroRun).Concat(HeroAxe))
            Raylib.UnloadTexture(strip.Texture);
        Raylib.UnloadSound(SwordSound);
        Raylib.UnloadSound(ScoreSound);
    }

    static SpriteStrip[] LoadHeroStrips(string basePath)
    {
        string[] suffixes = ["down", "up", "left", "right"]; // same order as the Direction enum
        return suffixes
            .Select(d => new SpriteStrip(Raylib.LoadTexture($"textures/hero/{basePath}_{d}.png"), HeroFrameSize))
            .ToArray();
    }
}
