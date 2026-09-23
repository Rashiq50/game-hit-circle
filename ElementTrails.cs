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
        var dest = new Rectangle(position.X - DrawWidth / 2, position.Y - height / 2, DrawWidth, height);
        strip.Draw(strip.LoopFrame(elapsed, AnimFps), dest);
    }
}
