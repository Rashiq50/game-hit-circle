using System.Numerics;
using Raylib_cs;

/// <summary>An attack in progress, as far as drawing it goes: the melee or the ranged sheet, which way it goes, and how far
/// along it is (0..1 through the windup, 1..2 through the follow-through).</summary>
readonly record struct AttackPose(bool Melee, Direction Toward, float Progress);

/// <summary>Draws the generated enemy art (textures/enemies/, built by tools/enemies/make_enemies.py) and holds each look's
/// size, hit-box and attack metrics. The Grunt keeps its own sprite. The metric tables here mirror LOOKS in the generator,
/// which draws the art to fit them; change both together.</summary>
static class EnemyIcons
{
    /// <summary>Pixels per world unit in the generated art (the generator's OUT). A frame's world size is its pixel size over
    /// this, so the attack sheets, cropped wider than the idle strips to hold their swings, still line up on the body.</summary>
    const float PxPerUnit = 2f;
    /// <summary>Attack frames before this one wind up; this one lands the blow or looses the shot, and the rest follow
    /// through (the generator's ATTACK_HIT).</summary>
    const int AttackHitFrame = 5;

    /// <param name="scale">Draw scale on top of the art's own size: 1 normally, larger while hovered as an ultimate target.</param>
    /// <param name="time">Seconds since spawn; drives the idle loop.</param>
    /// <param name="death">Death progress 0..1, or negative while alive.</param>
    /// <param name="flipX">Mirror the art, which faces right, so the enemy faces left.</param>
    /// <param name="attack">The attack being made, if any; a look without art for it keeps idling.</param>
    public static void Draw(EnemyLook look, Vector2 c, float scale, float time, float death, bool flipX, AttackPose? attack = null)
    {
        var art = Assets.EnemyArt[look];
        // The death strip fades itself out, so the art is always drawn at full opacity.
        if (death >= 0)
            Draw(art.Death, art.Death.OneShotFrame(death, 1f), 0, c, scale, flipX);
        else if (attack is { } a && (a.Melee ? art.Melee : art.Ranged) is { } sheet)
            Draw(sheet, AttackFrame(sheet, a.Progress), AttackRow(a.Toward), c, scale, flipX);
        else
            Draw(art.Idle, art.Idle.LoopFrame(time, IdleFps(look)), 0, c, scale, flipX);
    }

    static void Draw(SpriteStrip strip, int frame, int row, Vector2 c, float scale, bool flipX) =>
        strip.Draw(frame, c, strip.FrameSize / PxPerUnit * scale, Color.White, flipX, row);

    /// <summary>The windup frames spread over the windup and the rest over the follow-through, however long each lasts.</summary>
    static int AttackFrame(SpriteStrip sheet, float progress) => progress < 1f
        ? Math.Clamp((int)(progress * AttackHitFrame), 0, AttackHitFrame - 1)
        : Math.Min(AttackHitFrame + (int)((progress - 1f) * (sheet.FrameCount - AttackHitFrame)), sheet.FrameCount - 1);

    /// <summary>Sheet row for each direction (the generator's ATTACK_ROWS): the side-on row, mirrored for the left like the
    /// rest of the art, then down and up.</summary>
    static int AttackRow(Direction toward) => toward switch
    {
        Direction.Down => 1,
        Direction.Up => 2,
        _ => 0,
    };

    static float IdleFps(EnemyLook look) => look switch
    {
        EnemyLook.Imp => 12f, // twitchy
        EnemyLook.Tank => 6f, // ponderous
        EnemyLook.Wisp => 10f,
        _ => 7f,
    };

    /// <summary>Seconds a look spends winding up a shot (the imp's throw, the sniper's charge) before it leaves. The sniper's
    /// hits hardest from furthest away, so its charge is long enough to see coming. The Grunt's sprite has no windup art,
    /// so it fires the moment it's ready, as it always has.</summary>
    public static float CastWindup(EnemyLook look) => look switch
    {
        EnemyLook.Sprite => 0f,
        EnemyLook.Imp => 0.3f,
        EnemyLook.Wisp => 0.3f,
        EnemyLook.Sniper => 0.6f,
        _ => 0.4f, // Warlord
    };

    /// <summary>Where a look's shot leaves on the release frame, as a multiple of its scaled body radius from the centre,
    /// with the art facing right: the imp's throwing hand, the sniper's crystal, the wisp's mouth (its flame, firing up),
    /// the warlord's sword tip. The Grunt fires from its centre.</summary>
    public static Vector2 Muzzle(EnemyLook look, Direction toward) => (look, AttackRow(toward)) switch
    {
        (EnemyLook.Imp, 0) => new(1.79f, -0.25f),
        (EnemyLook.Imp, 1) => new(1.06f, 0.71f),
        (EnemyLook.Imp, 2) => new(0.98f, -1.34f),
        (EnemyLook.Sniper, 0) => new(2.15f, -0.84f),
        (EnemyLook.Sniper, 1) => new(1.63f, 1.45f),
        (EnemyLook.Sniper, 2) => new(0.59f, -1.9f),
        (EnemyLook.Warlord, 0) => new(1.77f, -0.32f),
        (EnemyLook.Warlord, 1) => new(1.05f, 1.03f),
        (EnemyLook.Warlord, 2) => new(0.83f, -1.73f),
        (EnemyLook.Wisp, 0) => new(1.04f, 0.06f),
        (EnemyLook.Wisp, 1) => new(0.08f, 0.89f),
        (EnemyLook.Wisp, 2) => new(-0.06f, -1.49f),
        _ => Vector2.Zero,
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
