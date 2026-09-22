using System.Numerics;
using Raylib_cs;

class EnemyProjectile(Vector2 from, Vector2 toward, float damage, float speed)
{
    // const float Speed = 400f;
    const float Radius = 15f;
    const float DrawSize = 34f;

    // const float damage = 12;

    public float GetDamageNumber() => damage;

    Vector2 position = from;
    readonly Vector2 direction = Vector2.Normalize(toward - from);

    public bool Active { get; set; } = true;
    Rectangle Bounds => new(position.X - Radius, position.Y - Radius, Radius * 2, Radius * 2);

    public bool Overlaps(Rectangle bound) => Active && Raylib.CheckCollisionRecs(Bounds, bound);

    public void Draw()
    {
        if (!Active) return;
        var tex = Assets.Fireball;
        Raylib.DrawTexturePro(tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            new Rectangle(position.X - DrawSize / 2, position.Y - DrawSize / 2, DrawSize, DrawSize),
            Vector2.Zero, 0, Color.White);
    }

    public void Update(float dt)
    {
        if (!Active) return;

        position += direction * speed * dt;

        bool offScreen = position.X < -Radius || position.X > World.Width + Radius
                      || position.Y < -Radius || position.Y > World.Height + Radius;
        if (offScreen || CollisionMap.Blocks(Bounds)) Active = false;
    }
}
