using System.Numerics;
using Raylib_cs;

class ElementalTrail(ElementType elementType, Vector2 from, Direction dr)
{
    const float Lifetime = 2f;
    const float Speed = 700f;
    const float AnimFps = 12f;
    const float DrawWidth = 70f;

    readonly Direction direction = dr;

    float elapsed = 0;
    bool Active = true;
    Vector2 position = from;
    Vector2 lastFront = from;

    Vector2 Forward => direction switch
    {
        Direction.Right => new(1, 0),
        Direction.Left => new(-1, 0),
        Direction.Up => new(0, -1),
        _ => new(0, 1),
    };

    Vector2 Front => position + Forward * (DrawWidth / 2);

    public bool IsPlaying => elapsed < Lifetime;
    public bool IsActive => Active;

    public Rectangle Bounds => new(position.X, position.Y, DrawWidth, DrawWidth);

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

    public void Stop() => Active = false;

    public void Update(float dt)
    {
        if (!IsPlaying) return;
        position += Forward * Speed * dt;
        elapsed += dt;
        if (elapsed >= Lifetime || !CollisionMap.HasLineOfSight(lastFront, Front))
        {
            Active = false;
        }
        lastFront = Front;
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
