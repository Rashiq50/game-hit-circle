using System.Numerics;
using Raylib_cs;

/// <summary>A horizontal sprite sheet of square frames, plus the frame arithmetic every animation needs.</summary>
readonly record struct SpriteStrip(Texture2D Texture, int FrameSize)
{
    public int FrameCount => Texture.Width / FrameSize;

    /// <summary>Frame for a looping animation after <paramref name="elapsed"/> seconds.</summary>
    public int LoopFrame(float elapsed, float fps) => (int)(elapsed * fps) % FrameCount;

    /// <summary>Frame for a play-once animation spread over <paramref name="duration"/> seconds; holds the last frame afterwards.</summary>
    public int OneShotFrame(float elapsed, float duration) =>
        Math.Min((int)(elapsed / duration * FrameCount), FrameCount - 1);

    public void Draw(int frame, Vector2 center, float drawSize) =>
        Raylib.DrawTexturePro(Texture,
            new Rectangle(frame * FrameSize, 0, FrameSize, FrameSize),
            new Rectangle(center.X - drawSize / 2, center.Y - drawSize / 2, drawSize, drawSize),
            Vector2.Zero, 0, Color.White);
}
