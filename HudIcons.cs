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
}
