using System.Numerics;
using Raylib_cs;

/// <summary>Owns every texture: loaded once after the window exists, unloaded once before it closes.</summary>
static class Assets
{
    const int HeroFrameSize = 80;
    const int DemonFrameSize = 256;
    // The ultimate's lightning bolt is drawn tall rather than square: one column of sky, one splash on the ground.
    const int UltStrikeFrameWidth = 256;
    const int UltStrikeFrameHeight = 512;

    public static Texture2D Background, Fireball, WelcomBackground, MainMenuBackground;
    public static SpriteStrip DemonIdle, DemonDeath, UltStrike;
    public static Sound SwordSound, ScoreSound, UltSound, SwordBlockSound;

    public static Color OverlayBlack = new Color(0, 0, 0, 200);
    public static Color OverlayBlack2 = new Color(0, 0, 0, 100);
    // one strip per facing direction, indexed by (int)Direction
    public static SpriteStrip[] HeroIdle = [], HeroWalk = [], HeroRun = [], HeroAxe = [];

    public static void Load()
    {
        Background = Raylib.LoadTexture("textures/stages/castle_floor.png");
        WelcomBackground = Raylib.LoadTexture("textures/welcome.png");
        MainMenuBackground = Raylib.LoadTexture("textures/mainmenu.png");
        Fireball = Raylib.LoadTexture("textures/fireball.png");
        DemonIdle = new(Raylib.LoadTexture("textures/Enemy-Melee-Idle-S.png"), DemonFrameSize);
        DemonDeath = new(Raylib.LoadTexture("textures/Enemy-Melee-Death.png"), DemonFrameSize);
        UltStrike = new(Raylib.LoadTexture("textures/hero/ult-attack/ult_strike.png"), UltStrikeFrameWidth, UltStrikeFrameHeight);
        HeroIdle = LoadHeroStrips("idle/idle");
        HeroWalk = LoadHeroStrips("walk/walk");
        HeroRun = LoadHeroStrips("run/run");
        HeroAxe = LoadHeroStrips("axe attack/axe_attack");
        SwordSound = Raylib.LoadSound("sounds/violent-sword-slice-393848.mp3");
        ScoreSound = Raylib.LoadSound("sounds/level-up-523624.mp3");
        UltSound = Raylib.LoadSound("sounds/ult.mp3");
        SwordBlockSound = Raylib.LoadSound("sounds/sword-block.mp3");
    }

    public static void Unload()
    {
        Raylib.UnloadTexture(Background);
        Raylib.UnloadTexture(WelcomBackground);
        Raylib.UnloadTexture(MainMenuBackground);
        Raylib.UnloadTexture(Fireball);
        Raylib.UnloadTexture(DemonIdle.Texture);
        Raylib.UnloadTexture(DemonDeath.Texture);
        Raylib.UnloadTexture(UltStrike.Texture);
        foreach (var strip in HeroIdle.Concat(HeroWalk).Concat(HeroRun).Concat(HeroAxe))
            Raylib.UnloadTexture(strip.Texture);
        Raylib.UnloadSound(SwordSound);
        Raylib.UnloadSound(ScoreSound);
        Raylib.UnloadSound(UltSound);
        Raylib.UnloadSound(SwordBlockSound);
    }

    static SpriteStrip[] LoadHeroStrips(string basePath)
    {
        string[] suffixes = ["down", "up", "left", "right"]; // same order as the Direction enum
        return suffixes
            .Select(d => new SpriteStrip(Raylib.LoadTexture($"textures/hero/{basePath}_{d}.png"), HeroFrameSize))
            .ToArray();
    }
}
