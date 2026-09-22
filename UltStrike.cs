using System.Numerics;
using Raylib_cs;

class UltStrike
{
    const float Duration = 0.75f;
    const float DrawWidth = 160f;
    const float GroundLine = 0.875f;

    float elapsed = Duration;
    Vector2 groundPoint;

    public bool IsPlaying => elapsed < Duration;

    public void Strike(Vector2 at)
    {
        groundPoint = at;
        elapsed = 0;
    }

    public void Stop() => elapsed = Duration;

    public void Update(float dt)
    {
        if (IsPlaying) elapsed += dt;
    }

    public void Draw()
    {
        if (!IsPlaying) return;
        var strip = Assets.UltStrike;
        float height = DrawWidth * strip.FrameHeight / strip.FrameSize;
        var dest = new Rectangle(groundPoint.X - DrawWidth / 2, groundPoint.Y - height * GroundLine, DrawWidth, height);
        strip.Draw(strip.OneShotFrame(elapsed, Duration), dest);
    }
}
