using System.Numerics;
using Raylib_cs;

/// <summary>Placeholder enemy bodies drawn with primitives, in the spirit of <see cref="HudIcons"/>: every variant gets its own
/// silhouette, colour and idle motion so the player can tell them apart until each has real art. The Grunt keeps the sprite.</summary>
static class EnemyIcons
{
    const float Outline = 2.5f;

    /// <param name="radius">Base body radius; each variant scales itself from it so a tank reads bigger than an imp.</param>
    /// <param name="time">Seconds since spawn; drives the idle motion.</param>
    /// <param name="alpha">Whole-body opacity, 1 = solid; the death fade lowers it.</param>
    public static void Draw(EnemyLook look, Vector2 c, float radius, float time, float alpha)
    {
        switch (look)
        {
            case EnemyLook.Imp: DrawImp(c, radius * 0.75f, time, alpha); break;
            case EnemyLook.Brute: DrawBrute(c, radius * 1.25f, time, alpha); break;
            case EnemyLook.Tank: DrawTank(c, radius * 1.3f, time, alpha); break;
            case EnemyLook.Sniper: DrawSniper(c, radius, time, alpha); break;
            case EnemyLook.Warlord: DrawWarlord(c, radius * 1.5f, time, alpha); break;
            case EnemyLook.Wisp: DrawWisp(c, radius * 0.8f, time, alpha); break;
        }
    }

    /// <summary>Small orange diamond with horns, jittering in place: fragile and quick.</summary>
    static void DrawImp(Vector2 c, float r, float t, float a)
    {
        c.Y += MathF.Sin(t * 9f) * 2f; // nervous twitch
        var body = Raylib.Fade(new Color(235, 110, 40, 255), a);
        var dark = Raylib.Fade(new Color(60, 20, 10, 255), a);
        Raylib.DrawPoly(c, 4, r, 0, body); // rotation 0 puts the corners on the axes, so a 4-gon is a diamond
        Raylib.DrawPolyLinesEx(c, 4, r, 0, Outline, dark);
        // Horns curling out from the top corner.
        Vector2 top = c + new Vector2(0, -r);
        Raylib.DrawLineEx(top + new Vector2(-r * 0.25f, r * 0.2f), top + new Vector2(-r * 0.55f, -r * 0.35f), Outline, dark);
        Raylib.DrawLineEx(top + new Vector2(r * 0.25f, r * 0.2f), top + new Vector2(r * 0.55f, -r * 0.35f), Outline, dark);
        Raylib.DrawCircleV(c + new Vector2(-r * 0.22f, -r * 0.1f), r * 0.1f, dark);
        Raylib.DrawCircleV(c + new Vector2(r * 0.22f, -r * 0.1f), r * 0.1f, dark);
    }

    /// <summary>Wide maroon hexagon with two fists, breathing slowly: melee-only bulk.</summary>
    static void DrawBrute(Vector2 c, float r, float t, float a)
    {
        r *= 1f + 0.04f * MathF.Sin(t * 2f);
        var body = Raylib.Fade(new Color(140, 30, 40, 255), a);
        var dark = Raylib.Fade(new Color(40, 10, 15, 255), a);
        Raylib.DrawPoly(c, 6, r, 0, body); // corners left and right, flat top: reads as shoulders
        Raylib.DrawPolyLinesEx(c, 6, r, 0, Outline, dark);
        // One thick brow instead of eyes.
        Raylib.DrawLineEx(c + new Vector2(-r * 0.4f, -r * 0.2f), c + new Vector2(r * 0.4f, -r * 0.2f), Outline * 1.4f, dark);
        // Fists at the lower corners; the right one pumps up in time with the breathing.
        float fist = r * 0.32f;
        float raise = MathF.Max(0, MathF.Sin(t * 2f)) * r * 0.15f;
        DrawFist(c + new Vector2(-r * 0.75f, r * 0.45f), fist, body, dark);
        DrawFist(c + new Vector2(r * 0.75f, r * 0.45f - raise), fist, body, dark);
    }

    static void DrawFist(Vector2 at, float r, Color fill, Color line)
    {
        Raylib.DrawCircleV(at, r, fill);
        Raylib.DrawRing(at, r - Outline, r, 0, 360, 24, line);
    }

    /// <summary>Steel octagon with an inner plate, rivets and a glowing visor: armoured and slow.</summary>
    static void DrawTank(Vector2 c, float r, float t, float a)
    {
        var plate = Raylib.Fade(new Color(95, 115, 140, 255), a);
        var light = Raylib.Fade(new Color(180, 195, 210, 255), a);
        var dark = Raylib.Fade(new Color(30, 40, 55, 255), a);
        const float tilt = 22.5f; // half a side, so the octagon sits on a flat edge
        Raylib.DrawPoly(c, 8, r, tilt, plate);
        Raylib.DrawPolyLinesEx(c, 8, r, tilt, Outline, dark);
        Raylib.DrawPolyLinesEx(c, 8, r * 0.62f, tilt, Outline * 0.8f, light);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * MathF.PI / 2 + MathF.PI / 4;
            Raylib.DrawCircleV(c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r * 0.8f, r * 0.08f, light);
        }
        // Visor slit that glows in time with its slow cannon.
        float glow = 0.5f + 0.5f * MathF.Sin(t * 1.5f);
        var visor = new Rectangle(c.X - r * 0.35f, c.Y - r * 0.08f, r * 0.7f, r * 0.16f);
        Raylib.DrawRectangleRec(visor, Raylib.Fade(new Color(255, 120, 60, 255), a * (0.4f + 0.6f * glow)));
    }

    /// <summary>Tall violet spike with a spinning reticle: fast, precise shots.</summary>
    static void DrawSniper(Vector2 c, float r, float t, float a)
    {
        var body = Raylib.Fade(new Color(120, 60, 200, 255), a);
        var dark = Raylib.Fade(new Color(35, 15, 60, 255), a);
        var lens = Raylib.Fade(new Color(240, 230, 255, 255), a);
        Vector2 top = c + new Vector2(0, -r * 1.35f), bottom = c + new Vector2(0, r * 1.35f);
        Vector2 left = c + new Vector2(-r * 0.5f, 0), right = c + new Vector2(r * 0.5f, 0);
        Tri(top, left, right, body);
        Tri(bottom, right, left, body);
        Raylib.DrawLineEx(top, left, Outline, dark);
        Raylib.DrawLineEx(left, bottom, Outline, dark);
        Raylib.DrawLineEx(bottom, right, Outline, dark);
        Raylib.DrawLineEx(right, top, Outline, dark);
        // Reticle: ring plus ticks that sweep round while it lines up the shot.
        float ring = r * 0.32f;
        Raylib.DrawRing(c, ring - 2f, ring, 0, 360, 32, lens);
        for (int i = 0; i < 4; i++)
        {
            float ang = t * 1.5f + i * MathF.PI / 2;
            var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            Raylib.DrawLineEx(c + dir * (ring + 2f), c + dir * (ring + 7f), 2f, lens);
        }
        Raylib.DrawCircleV(c, 2.5f, lens);
    }

    /// <summary>Crimson orb under a gold crown, with a pulsing aura: the stage-closer.</summary>
    static void DrawWarlord(Vector2 c, float r, float t, float a)
    {
        var goldBase = new Color(255, 200, 60, 255);
        var body = Raylib.Fade(new Color(150, 20, 30, 255), a);
        var dark = Raylib.Fade(new Color(40, 5, 10, 255), a);
        var gold = Raylib.Fade(goldBase, a);
        float pulse = 0.6f + 0.4f * MathF.Sin(t * 3f);
        Raylib.DrawRing(c, r * 1.15f, r * 1.25f, 0, 360, 48, Raylib.Fade(goldBase, a * 0.35f * pulse));
        Raylib.DrawCircleV(c, r, body);
        Raylib.DrawRing(c, r - Outline, r, 0, 360, 48, dark);
        // Crown: a band across the brow with three spikes, the middle one tallest.
        float bandY = c.Y - r * 0.55f;
        float half = r * 0.8f;
        Raylib.DrawRectangleRec(new Rectangle(c.X - half, bandY - r * 0.1f, half * 2, r * 0.2f), gold);
        for (int i = -1; i <= 1; i++)
        {
            float x = c.X + i * half * 0.66f;
            float height = i == 0 ? r * 0.65f : r * 0.45f;
            Tri(new Vector2(x, bandY - height), new Vector2(x - half * 0.3f, bandY), new Vector2(x + half * 0.3f, bandY), gold);
        }
        Raylib.DrawCircleV(c + new Vector2(-r * 0.3f, r * 0.05f), r * 0.11f, gold);
        Raylib.DrawCircleV(c + new Vector2(r * 0.3f, r * 0.05f), r * 0.11f, gold);
    }

    /// <summary>Soft cyan glow with a bright core and orbiting motes, bobbing gently: weak, but worth chasing.</summary>
    static void DrawWisp(Vector2 c, float r, float t, float a)
    {
        c.Y += MathF.Sin(t * 2.5f) * 4f; // floats up and down
        float breathe = 1f + 0.12f * MathF.Sin(t * 5f);
        var glow = new Color(80, 220, 255, 255);
        Raylib.DrawCircleGradient(c, r * 1.5f * breathe, Raylib.Fade(glow, a * 0.5f), Raylib.Fade(glow, 0f));
        Raylib.DrawCircleV(c, r * 0.55f * breathe, Raylib.Fade(glow, a));
        Raylib.DrawCircleV(c, r * 0.3f, Raylib.Fade(Color.White, a));
        for (int i = 0; i < 3; i++)
        {
            float ang = t * 3f + i * MathF.PI * 2 / 3;
            Vector2 p = c + new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r * 0.5f); // flattened orbit for a bit of depth
            Raylib.DrawCircleV(p, r * 0.12f, Raylib.Fade(Color.White, a * 0.8f));
        }
    }

    /// <summary>DrawTriangle culls clockwise triangles; this fixes the winding so callers can list corners in any order.</summary>
    static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cross > 0) (b, c) = (c, b);
        Raylib.DrawTriangle(a, b, c, col);
    }
}
