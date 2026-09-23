using System.Numerics;
using Raylib_cs;

/// <summary>Who took the hit; picks the colour and weight of the floating number.</summary>
enum DamageStyle { EnemyHit, CriticalHit, PlayerHit, Heal }

/// <summary>
/// One rising damage number. Unlike <see cref="ScorePopup"/> these are pooled in a list, so every hit in a
/// busy frame gets its own number instead of stealing the single popup slot.
/// </summary>
class FloatingNumber
{
    const float PopTime = 0.10f; // scale-in, overshooting a little so the hit reads as a punch
    const float HoldTime = 0.45f;
    const float FadeTime = 0.35f;
    public const float Duration = PopTime + HoldTime + FadeTime;
    const float Overshoot = 1.25f; // peak scale at the end of the pop
    const float RiseSpeed = 90f; // initial upward speed, in world pixels per second
    const float Damping = 2.6f; // the rise eases off instead of running away up the screen
    const int BaseFontSize = 18;
    const int OutlineThickness = 1; // a dark rim so numbers stay readable over light floor tiles

    readonly string text;
    readonly DamageStyle style;
    readonly float sizeScale;
    Vector2 position;
    Vector2 velocity;
    float elapsed;

    public bool IsActive => elapsed < Duration;

    public FloatingNumber(string text, Vector2 at, DamageStyle style, float sizeScale, float drift)
    {
        this.text = text;
        this.style = style;
        this.sizeScale = sizeScale;
        position = at;
        velocity = new Vector2(drift, -RiseSpeed);
    }

    public void Update(float dt)
    {
        elapsed += dt;
        position += velocity * dt;
        velocity -= velocity * Math.Min(1f, Damping * dt); // exponential drag: fast off the entity, drifting at the top
    }

    Color Tint => style switch
    {
        DamageStyle.CriticalHit => Color.Orange,
        DamageStyle.PlayerHit => Color.Red,
        DamageStyle.Heal => Color.Lime,
        _ => Color.White,
    };

    float Scale => elapsed < PopTime
        ? Overshoot * elapsed / PopTime
        : Math.Max(1f, Overshoot - (Overshoot - 1f) * Math.Min(1f, (elapsed - PopTime) / PopTime));

    float Alpha => elapsed < PopTime + HoldTime ? 1f : 1f - (elapsed - PopTime - HoldTime) / FadeTime;

    public void Draw()
    {
        if (!IsActive) return;
        int fontSize = Math.Max(1, (int)(BaseFontSize * sizeScale * Scale));
        float alpha = Math.Clamp(Alpha, 0f, 1f);
        int width = Raylib.MeasureText(text, fontSize);
        int x = (int)(position.X - width / 2f);
        int y = (int)(position.Y - fontSize / 2f);

        var outline = Raylib.Fade(Color.Black, 0.7f * alpha);
        for (int dx = -OutlineThickness; dx <= OutlineThickness; dx++)
            for (int dy = -OutlineThickness; dy <= OutlineThickness; dy++)
                if (dx != 0 || dy != 0)
                    Raylib.DrawText(text, x + dx, y + dy, fontSize, outline);
        Raylib.DrawText(text, x, y, fontSize, Raylib.Fade(Tint, alpha));
    }
}

/// <summary>Every floating number on the field. World-space, so they pan and zoom with the camera.</summary>
static class FloatingNumbers
{
    const int MaxOnScreen = 48; // a hard cap so a big multi-hit frame can't flood the field
    const float SpreadX = 16f; // random horizontal jitter, so numbers stacked on one entity stay readable
    const float DriftSpeed = 26f; // sideways speed given to that jitter
    /// <summary>Damage that counts as a "full size" number; bigger hits grow a little past it.</summary>
    const float ReferenceDamage = 60f;

    static readonly List<FloatingNumber> numbers = [];

    public static void Show(Vector2 at, float amount, DamageStyle style)
    {
        if (amount <= 0) return;
        float jitter = (Random.Shared.NextSingle() * 2 - 1) * SpreadX;
        float sizeScale = style == DamageStyle.CriticalHit
            ? 1.6f
            : 1f + 0.5f * Math.Clamp(amount / ReferenceDamage, 0f, 1f);
        string text = style == DamageStyle.Heal ? $"+{amount:0.##}" : $"{amount:0.##}";
        numbers.Add(new FloatingNumber(text, at + new Vector2(jitter, 0), style, sizeScale, jitter / SpreadX * DriftSpeed));
        if (numbers.Count > MaxOnScreen) numbers.RemoveRange(0, numbers.Count - MaxOnScreen);
    }

    public static void Update(float dt)
    {
        foreach (var n in numbers) n.Update(dt);
        numbers.RemoveAll(n => !n.IsActive);
    }

    public static void Draw()
    {
        foreach (var n in numbers) n.Draw();
    }

    /// <summary>Wipes the field, e.g. when a stage restarts.</summary>
    public static void Clear() => numbers.Clear();
}
