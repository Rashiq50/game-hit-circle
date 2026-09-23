using System.Numerics;
using Raylib_cs;

class ScorePopup
{
    const float ScaleTime = 0.15f;
    const float HoldTime = 0.5f;
    const float FadeTime = 0.3f;
    const float Duration = ScaleTime + HoldTime + FadeTime;
    const float DriftSpeed = 20f; // pixels per second
    const int FontSize = 12;
    const int IconGap = 2; // between the coin and the number
    float point = 0;

    bool active;
    Vector2 origin;
    float elapsed;

    public void Show(Vector2 at, float pt)
    {
        active = true;
        origin = at;
        elapsed = 0;
        point = pt;
    }

    public void Update(float dt)
    {
        if (!active) return;
        elapsed += dt;
        if (elapsed >= Duration) active = false;
    }

    public void Draw()
    {
        if (!active) return;

        float scale = elapsed < ScaleTime ? elapsed / ScaleTime : 1;
        float alpha = elapsed < ScaleTime + HoldTime ? 1 : 1 - (elapsed - ScaleTime - HoldTime) / FadeTime;
        string text = $"+{point:0.##}";

        int fontSize = Math.Max(1, (int)(FontSize * scale));
        int textWidth = Raylib.MeasureText(text, fontSize);
        // The coin is drawn as tall as the text, so the whole "(coin) +N" group is centred on the origin.
        float iconSize = fontSize * 1.5f;
        float totalWidth = iconSize + IconGap + textWidth;
        float drift = elapsed * DriftSpeed;
        float left = origin.X - totalWidth / 2f;
        float centerY = origin.Y - drift;
        var tint = Raylib.Fade(Color.Gold, alpha);
        HudIcons.DrawCoin(new Rectangle(left, centerY - iconSize / 2f, iconSize, iconSize), tint);
        Raylib.DrawText(text, (int)(left + iconSize + IconGap), (int)(centerY - fontSize / 2f), fontSize, tint);
    }
}
