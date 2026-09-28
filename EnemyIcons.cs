using System.Numerics;
using Raylib_cs;

/// <summary>Draws the generated enemy art (textures/enemies/, built by tools/enemies/make_enemies.py) and holds each look's
/// size and hit-box metrics. The Grunt keeps its own sprite. The metric tables here mirror LOOKS in the generator, which draws
/// the art to fit them; change both together.</summary>
static class EnemyIcons
{
    /// <summary>World units across one frame, with the body centre in the middle (the generator's CANVAS).</summary>
    const float Canvas = 128f;

    /// <param name="scale">Draw scale on top of the art's own size: 1 normally, larger while hovered as an ultimate target.</param>
    /// <param name="time">Seconds since spawn; drives the idle loop.</param>
    /// <param name="death">Death progress 0..1, or negative while alive.</param>
    /// <param name="flipX">Mirror the art, which faces right, so the enemy faces left.</param>
    public static void Draw(EnemyLook look, Vector2 c, float scale, float time, float death, bool flipX)
    {
        var (idle, dying) = Assets.EnemyArt[look];
        // The death strip fades itself out, so the art is always drawn at full opacity.
        if (death >= 0)
            dying.Draw(dying.OneShotFrame(death, 1f), c, Canvas * scale, Color.White, flipX);
        else
            idle.Draw(idle.LoopFrame(time, IdleFps(look)), c, Canvas * scale, Color.White, flipX);
    }

    static float IdleFps(EnemyLook look) => look switch
    {
        EnemyLook.Imp => 12f, // twitchy
        EnemyLook.Tank => 6f, // ponderous
        EnemyLook.Wisp => 10f,
        _ => 7f,
    };

    /// <summary>How much bigger or smaller each look's body is than the base radius, so a tank reads bigger than an imp.</summary>
    public static float BodyScale(EnemyLook look) => look switch
    {
        EnemyLook.Imp => 0.75f,
        EnemyLook.Brute => 1.25f,
        EnemyLook.Tank => 1.3f,
        EnemyLook.Warlord => 1.5f,
        EnemyLook.Wisp => 0.8f,
        _ => 1f,
    };

    /// <summary>Half-size of the solid part of each look's silhouette, as a multiple of its scaled body radius. Wings, weapons,
    /// horns, crowns and glows are left out so hits land where the body looks solid; a tall sniper gets a tall box.</summary>
    public static Vector2 HitExtent(EnemyLook look) => look switch
    {
        EnemyLook.Imp => new(0.7f, 0.95f), // head and pot belly, not the wings or tail
        EnemyLook.Brute => new(0.95f, 0.9f), // shoulders and fists, not the club
        EnemyLook.Tank => new(0.98f, 0.93f), // shield to fist
        EnemyLook.Sniper => new(0.55f, 1.2f), // hood to hem, not the staff
        EnemyLook.Wisp => new(0.6f, 0.65f), // the core; flame, motes and glow aren't solid
        _ => new(0.92f, 0.92f), // Warlord: pauldrons to greaves, not the sword or crown
    };

    /// <summary>How far above the centre each look's drawing reaches, decorations included, as a multiple of its scaled body
    /// radius; used to float things like the health bar clear of the art.</summary>
    public static float TopExtent(EnemyLook look) => look switch
    {
        EnemyLook.Imp => 1.4f, // horn tips
        EnemyLook.Brute => 1.2f, // raised club
        EnemyLook.Tank => 0.95f,
        EnemyLook.Sniper => 1.65f, // staff crystal
        EnemyLook.Warlord => 1.2f, // crown
        EnemyLook.Wisp => 1.35f, // flame tip
        _ => 1f,
    };
}
