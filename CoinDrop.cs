using System.Numerics;
using Raylib_cs;

/// <summary>A coin an enemy leaves behind. It hops out on drop, bobs and spins while waiting, and shrinks away on pickup.</summary>
class CoinDrop
{
    const float Size = 30f; // drawn size of the coin
    const float DropDuration = 0.55f; // time from spawn until the coin has settled
    const float DropHeight = 40f; // peak of the hop above the rest position
    const float ScatterDistance = 28f; // how far the coin skids from where the enemy died
    const float PickUpDuration = 0.28f;
    const float PickUpRise = 14f; // how far the coin floats up while it shrinks away
    const float BobHeight = 3f;
    const float BobSpeed = 4f;
    const float SpinSpeed = 3.5f;

    readonly int scorePoint;
    readonly Vector2 halfSize = new(15f, 15f);
    readonly Vector2 restPosition; // where the coin lands and waits
    readonly Vector2 scatter; // ground displacement from the spawn point over the drop

    Vector2 center;
    float age; // seconds since the drop, drives the drop and idle animations
    float pickUpTime; // seconds since pickup started
    bool isPickedup;
    bool isPickingUp;

    public CoinDrop(Vector2 center, int point)
    {
        scorePoint = point;
        this.center = center;
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        scatter = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ScatterDistance * (0.5f + Random.Shared.NextSingle() * 0.5f);
        restPosition = center + scatter;
    }

    public Vector2 Center => center;
    public bool PickedUp => isPickedup;
    public bool PickingdUp => isPickingUp;
    public int ScorePoint => scorePoint;
    /// <summary>The coin can be collected only once it has landed and is not already being consumed.</summary>
    public bool Collectable => !isPickingUp && !isPickedup && age >= DropDuration;

    public bool Overlaps(Player player) => Raylib.CheckCollisionRecs(Bounds, player.Bounds);

    public Rectangle Bounds => BoundsAt(center);

    Rectangle BoundsAt(Vector2 center) =>
        new(center.X - halfSize.X, center.Y - halfSize.Y, halfSize.X * 2, halfSize.Y * 2);

    public void Update(float dt)
    {
        age += dt;
        if (isPickingUp)
        {
            // The coin is consumed where it lies: it never travels, so there is nothing to steer.
            pickUpTime += dt;
            if (pickUpTime >= PickUpDuration) FinishPickUp();
            return;
        }
        // Slide along the ground while airborne, then rest.
        float drop = Math.Clamp(age / DropDuration, 0f, 1f);
        center = Vector2.Lerp(restPosition - scatter, restPosition, 1f - (1f - drop) * (1f - drop));
    }

    public void Draw()
    {
        float alpha = 1f;
        float scale = 1f;
        float lift; // how high the coin is drawn above its ground position
        float widthScale; // squashes the coin horizontally to fake a spin

        if (isPickingUp)
        {
            float t = Math.Clamp(pickUpTime / PickUpDuration, 0f, 1f);
            scale = Math.Max(0f, 1f + 0.35f * MathF.Sin(t * MathF.PI) - t); // brief swell, then shrink to nothing
            alpha = 1f - t * t;
            lift = BobHeight + PickUpRise * t; // drifts up a little as it goes
            widthScale = MathF.Abs(MathF.Cos(age * SpinSpeed * 3f)); // spins fast as it is consumed
        }
        else if (age < DropDuration)
        {
            lift = Hop(age / DropDuration);
            scale = 1f + 0.25f * (1f - age / DropDuration); // slightly oversized as it pops out
            widthScale = MathF.Abs(MathF.Cos(age * SpinSpeed * 2f)); // tumbles in the air
        }
        else
        {
            float idle = age - DropDuration;
            lift = BobHeight * (0.5f + 0.5f * MathF.Sin(idle * BobSpeed));
            widthScale = 0.25f + 0.75f * MathF.Abs(MathF.Cos(idle * SpinSpeed));
        }

        // Ground shadow shrinks and fades as the coin rises.
        float shadowScale = 1f - Math.Clamp(lift / DropHeight, 0f, 0.6f);
        Raylib.DrawEllipse((int)center.X, (int)(center.Y + Size * 0.4f), Size * 0.35f * shadowScale, Size * 0.14f * shadowScale, Raylib.Fade(Color.Black, 0.25f * alpha));

        float w = Size * scale * Math.Max(widthScale, 0.12f);
        float h = Size * scale;
        var slot = new Rectangle(center.X - w / 2, center.Y - lift - h / 2, w, h);
        HudIcons.DrawCoin(slot, Raylib.Fade(Color.Gold, alpha), w / h);
    }

    /// <summary>Height over the drop: one big hop, then a smaller bounce before settling.</summary>
    static float Hop(float t)
    {
        if (t < 0.65f)
        {
            float u = t / 0.65f;
            return DropHeight * 4f * u * (1f - u); // parabola peaking at DropHeight
        }
        float v = (t - 0.65f) / 0.35f;
        return DropHeight * 0.25f * 4f * v * (1f - v);
    }

    public void StartPickUp()
    {
        if (isPickingUp || isPickedup) return;
        isPickingUp = true;
        pickUpTime = 0f;
    }

    public void FinishPickUp()
    {
        isPickingUp = false;
        isPickedup = true;
    }
}
