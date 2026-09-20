using System.Numerics;
using Raylib_cs;

/// <summary>Placeholder HUD icons drawn with primitives, so no texture is needed until real art exists.</summary>
static class HudIcons
{
    /// <summary>A clock face with the hands at "half past": the slow-time power.</summary>
    public static void DrawClock(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float r = Math.Min(slot.Width, slot.Height) * 0.32f;
        Raylib.DrawRing(c, r - 2.5f, r, 0, 360, 40, tint);
        Raylib.DrawCircleV(c, 2f, tint);
        // Tick marks at 12, 3, 6 and 9 so it reads as a clock, not a ring.
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            Raylib.DrawLineEx(c + dir * (r - 3f), c + dir * (r - 7f), 2f, tint);
        }
        Raylib.DrawLineEx(c, c + new Vector2(0, r * 0.65f), 2.5f, tint); // minute hand pointing at 6
        Raylib.DrawLineEx(c, c + new Vector2(r * 0.4f, 0), 2.5f, tint); // hour hand pointing at 3
    }

    /// <summary>A mouse with one button filled in: the attack prompts. <paramref name="leftButton"/> false lights the right button.</summary>
    public static void DrawMouse(Rectangle slot, bool leftButton, Color tint, Color highlight)
    {
        float h = Math.Min(slot.Width, slot.Height) * 0.6f;
        float w = h * 0.68f;
        float x = slot.X + (slot.Width - w) / 2;
        float y = slot.Y + (slot.Height - h) / 2;
        var body = new Rectangle(x, y, w, h);
        float buttonHeight = h * 0.4f;

        // The lit button is drawn first so the outline sits on top of it.
        var button = new Rectangle(leftButton ? x : x + w / 2, y, w / 2, buttonHeight);
        Raylib.BeginScissorMode((int)button.X, (int)button.Y, (int)MathF.Ceiling(button.Width), (int)MathF.Ceiling(button.Height));
        Raylib.DrawRectangleRounded(body, 0.6f, 12, highlight);
        Raylib.EndScissorMode();

        Raylib.DrawRectangleRoundedLinesEx(body, 0.6f, 12, 2f, tint);
        Raylib.DrawLineEx(new Vector2(x + w / 2, y), new Vector2(x + w / 2, y + buttonHeight), 2f, tint); // split between the buttons
        Raylib.DrawLineEx(new Vector2(x, y + buttonHeight), new Vector2(x + w, y + buttonHeight), 2f, tint); // bottom of the buttons
    }

    /// <summary>A filled heart with a lighter shine: the health pickup.</summary>
    public static void DrawHeart(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float r = Math.Min(slot.Width, slot.Height) * 0.2f; // radius of each lobe
        // Two lobes that touch at the centre, and a point whose sides run tangent to them so there is no seam.
        Vector2 left = c + new Vector2(-r, -r * 0.55f);
        Vector2 right = c + new Vector2(r, -r * 0.55f);
        Vector2 tip = c + new Vector2(0, r * 1.75f);
        Raylib.DrawCircleV(left, r, tint);
        Raylib.DrawCircleV(right, r, tint);
        Vector2 tl = TangentPoint(left, r, tip, +1);
        Vector2 tr = TangentPoint(right, r, tip, -1);
        Raylib.DrawTriangle(tip, tr, tl, tint);
        Raylib.DrawTriangle(tl, tr, c + new Vector2(0, -r * 0.55f), tint); // fills the wedge between the lobes above the point
        Raylib.DrawCircleV(left + new Vector2(-r * 0.3f, -r * 0.3f), r * 0.25f, Raylib.Fade(Color.White, 0.6f)); // shine
    }

    /// <summary>Where a line from <paramref name="tip"/> touches the circle; <paramref name="side"/> picks which of the two tangents.</summary>
    static Vector2 TangentPoint(Vector2 center, float r, Vector2 tip, float side)
    {
        Vector2 d = tip - center;
        float len = d.Length();
        float ang = MathF.Acos(r / len) * side;
        float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
        return center + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos) / len * r;
    }

    /// <summary>A lightning bolt: the ultimate-point pickup.</summary>
    public static void DrawEnergy(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float h = Math.Min(slot.Width, slot.Height) * 0.7f;
        float w = h * 0.75f;
        // Classic zig-zag: two strokes that overlap at a jagged middle step.
        Vector2 top = c + new Vector2(w * 0.1f, -h * 0.5f);
        Vector2 leftMid = c + new Vector2(-w * 0.35f, h * 0.05f);
        Vector2 stepLeft = c + new Vector2(-w * 0.02f, h * 0.05f);
        Vector2 bottom = c + new Vector2(-w * 0.1f, h * 0.5f);
        Vector2 rightMid = c + new Vector2(w * 0.35f, -h * 0.05f);
        Vector2 stepRight = c + new Vector2(w * 0.02f, -h * 0.05f);
        // Each stroke is a quad, drawn as two counter-clockwise triangles.
        Raylib.DrawTriangle(top, leftMid, stepLeft, tint);
        Raylib.DrawTriangle(top, stepLeft, stepRight, tint);
        Raylib.DrawTriangle(stepRight, stepLeft, bottom, tint);
        Raylib.DrawTriangle(stepRight, bottom, rightMid, tint);
    }

    /// <summary>A shield with a vertical crease: a damage-reduction or invulnerability pickup.</summary>
    public static void DrawShield(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float h = Math.Min(slot.Width, slot.Height) * 0.66f;
        float w = h * 0.8f;
        Vector2 tl = c + new Vector2(-w / 2, -h / 2);
        Vector2 tr = c + new Vector2(w / 2, -h / 2);
        Vector2 ml = c + new Vector2(-w / 2, h * 0.1f);
        Vector2 mr = c + new Vector2(w / 2, h * 0.1f);
        Vector2 tip = c + new Vector2(0, h / 2);
        // Flat top, straight sides, then the sides taper to a point.
        Raylib.DrawTriangle(tl, ml, tr, tint);
        Raylib.DrawTriangle(tr, ml, mr, tint);
        Raylib.DrawTriangle(ml, tip, mr, tint);
        Raylib.DrawLineEx(c + new Vector2(0, -h / 2 + 3f), tip - new Vector2(0, 3f), 2f, Raylib.Fade(Color.Black, 0.35f)); // crease
    }

    /// <summary>A coin with a ring inset: score or currency pickups.</summary>
    public static void DrawCoin(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float r = Math.Min(slot.Width, slot.Height) * 0.32f;
        Raylib.DrawCircleV(c, r, tint);
        Raylib.DrawRing(c, r * 0.6f, r * 0.72f, 0, 360, 32, Raylib.Fade(Color.Black, 0.3f)); // inset ring
        Raylib.DrawCircleV(c + new Vector2(-r * 0.35f, -r * 0.35f), r * 0.18f, Raylib.Fade(Color.White, 0.6f)); // shine
    }

    /// <summary>A skull: danger warnings or a kill counter.</summary>
    public static void DrawSkull(Rectangle slot, Color tint)
    {
        Vector2 c = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
        float r = Math.Min(slot.Width, slot.Height) * 0.28f;
        Vector2 head = c + new Vector2(0, -r * 0.2f);
        Raylib.DrawCircleV(head, r, tint);
        var jaw = new Rectangle(c.X - r * 0.55f, head.Y + r * 0.5f, r * 1.1f, r * 0.75f);
        Raylib.DrawRectangleRounded(jaw, 0.3f, 6, tint);
        // Eyes, nose and two teeth are cut out with a translucent dark so the icon still works over any background.
        var hole = Raylib.Fade(Color.Black, 0.75f);
        Raylib.DrawCircleV(head + new Vector2(-r * 0.38f, 0), r * 0.26f, hole);
        Raylib.DrawCircleV(head + new Vector2(r * 0.38f, 0), r * 0.26f, hole);
        Raylib.DrawTriangle(head + new Vector2(0, r * 0.25f), head + new Vector2(-r * 0.12f, r * 0.55f), head + new Vector2(r * 0.12f, r * 0.55f), hole);
        float teethY = jaw.Y + jaw.Height * 0.45f;
        Raylib.DrawLineEx(new Vector2(c.X - r * 0.18f, teethY), new Vector2(c.X - r * 0.18f, jaw.Y + jaw.Height), 2f, hole);
        Raylib.DrawLineEx(new Vector2(c.X + r * 0.18f, teethY), new Vector2(c.X + r * 0.18f, jaw.Y + jaw.Height), 2f, hole);
    }
}
