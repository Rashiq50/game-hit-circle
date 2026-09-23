using System.Numerics;
using Raylib_cs;

class ElementalTrail(ElementType elementType)
{
    const float Lifetime = 4f;
    const float Speed = 700f;
    const float AnimFps = 12f;
    const float DrawWidth = 70f;

    Direction direction = Direction.Right;

    float elapsed = Lifetime;
    Vector2 position;

    public bool IsPlaying => elapsed < Lifetime;

    SpriteStrip Strip => elementType switch
    {
        ElementType.Ice => Assets.IceTrail,
        ElementType.Magic or ElementType.Lightning => Assets.MagicTrail,
        _ => Assets.FireTrail,
    };

    float Angle => direction switch
    {
        Direction.Down => 90f,
        Direction.Left => 0f,
        Direction.Up => 270f,
        _ => 0f,
    };

    public void Launch(Vector2 from, Direction dr)
    {
        position = from;
        elapsed = 0;
        direction = dr;
    }

    public void Stop() => elapsed = Lifetime;

    public void Update(float dt)
    {
        if (!IsPlaying) return;
        if (direction is Direction.Right) position.X += Speed * dt;
        else if (direction is Direction.Left) position.X -= Speed * dt;
        else if (direction is Direction.Up) position.Y -= Speed * dt;
        else if (direction is Direction.Down) position.Y += Speed * dt;
        elapsed += dt;
    }

    public void Draw()
    {
        if (!IsPlaying) return;
        var strip = Strip;
        float height = DrawWidth * strip.FrameHeight / strip.FrameSize;
        Rectangle source = new Rectangle(strip.LoopFrame(elapsed, AnimFps) * strip.FrameSize, 0, strip.FrameSize, strip.FrameHeight);
        if (direction is Direction.Left)
        {
            source.Width = -source.Width;
        }
        Raylib.DrawTexturePro(strip.Texture,
            source,
            new Rectangle(position.X, position.Y, DrawWidth, height),
            new Vector2(DrawWidth / 2, height / 2), Angle, Color.White);
    }
}
