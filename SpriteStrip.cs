using System.Numerics;
using Raylib_cs;

/// <summary>A horizontal sprite sheet of frames, plus the frame arithmetic every animation needs. A sheet can stack
/// several strips as rows (one per direction, say), all the same length.</summary>
/// <param name="FrameHeight">Only for sheets whose frames aren't square; 0 means "same as <paramref name="FrameSize"/>".</param>
readonly record struct SpriteStrip(Texture2D Texture, int FrameSize, int FrameHeight = 0)
{
    public int FrameCount => Texture.Width / FrameSize;
    int Height => FrameHeight > 0 ? FrameHeight : FrameSize;

    /// <summary>Frame for a looping animation after <paramref name="elapsed"/> seconds.</summary>
    public int LoopFrame(float elapsed, float fps) => (int)(elapsed * fps) % FrameCount;

    /// <summary>Frame for a play-once animation spread over <paramref name="duration"/> seconds; holds the last frame afterwards.</summary>
    public int OneShotFrame(float elapsed, float duration) =>
        Math.Min((int)(elapsed / duration * FrameCount), FrameCount - 1);

    public void Draw(int frame, Vector2 center, float drawSize) =>
        Draw(frame, new Rectangle(center.X - drawSize / 2, center.Y - drawSize / 2, drawSize, drawSize));

    /// <summary>Square draw with a tint (alpha fades it) and an optional horizontal mirror, for art that faces one way.</summary>
    public void Draw(int frame, Vector2 center, float drawSize, Color tint, bool flipX, int row = 0) =>
        Raylib.DrawTexturePro(Texture,
            new Rectangle(frame * FrameSize, row * Height, flipX ? -FrameSize : FrameSize, Height),
            new Rectangle(center.X - drawSize / 2, center.Y - drawSize / 2, drawSize, drawSize), Vector2.Zero, 0, tint);

    /// <summary>Draws a frame into an explicit rectangle, for sheets that aren't square and need their own anchoring.</summary>
    public void Draw(int frame, Rectangle dest) =>
        Raylib.DrawTexturePro(Texture,
            new Rectangle(frame * FrameSize, 0, FrameSize, Height),
            dest, Vector2.Zero, 0, Color.White);
}
