using System.Numerics;
using System.Xml.Linq;
using Raylib_cs;

static class CollisionMap
{
    static readonly List<Rectangle> walls = [];
    static readonly List<Vector2> enemySpawns = [];

    public static Vector2 PlayerSpawn { get; private set; } = World.Center;
    public static IReadOnlyList<Vector2> EnemySpawns => enemySpawns;

    public static void Load(string tmxPath)
    {
        walls.Clear();
        enemySpawns.Clear();
        PlayerSpawn = World.Center;
        var map = XDocument.Load(tmxPath).Root!;
        var image = map.Element("imagelayer")?.Element("image");
        float srcWidth = (float?)image?.Attribute("width") ?? (int)map.Attribute("width")! * (int)map.Attribute("tilewidth")!;
        float srcHeight = (float?)image?.Attribute("height") ?? (int)map.Attribute("height")! * (int)map.Attribute("tileheight")!;
        float sx = World.Width / srcWidth;
        float sy = World.Height / srcHeight;

        foreach (var obj in map.Elements("objectgroup").Elements("object"))
        {
            float x = (float)obj.Attribute("x")! * sx;
            float y = (float)obj.Attribute("y")! * sy;

            if (obj.Element("point") is not null)
            {
                // Tiled keeps whatever the author typed, so "Player Spawn" and "PlayerSpawn" both count.
                string name = ((string?)obj.Attribute("name") ?? "").Replace(" ", "");
                if (name.Equals("PlayerSpawn", StringComparison.OrdinalIgnoreCase)) PlayerSpawn = new Vector2(x, y);
                else if (name.Equals("EnemySpawn", StringComparison.OrdinalIgnoreCase)) enemySpawns.Add(new Vector2(x, y));
                continue;
            }

            if (obj.Attribute("width") is null || obj.Attribute("height") is null) continue; // polygons/ellipses: not supported
            walls.Add(new Rectangle(x, y, (float)obj.Attribute("width")! * sx, (float)obj.Attribute("height")! * sy));
        }
    }

    public static bool Blocks(Rectangle bounds) => walls.Any(w => Raylib.CheckCollisionRecs(w, bounds));

    /// <summary>True if the straight line from <paramref name="from"/> to <paramref name="to"/> doesn't pass through any wall.</summary>
    public static bool HasLineOfSight(Vector2 from, Vector2 to) => !walls.Any(w => SegmentHitsRect(from, to, w));

    // Slab test: clip the segment's [0, 1] range against the rectangle's x and y extents; anything left over is inside it.
    static bool SegmentHitsRect(Vector2 a, Vector2 b, Rectangle r)
    {
        Vector2 d = b - a;
        float tMin = 0f, tMax = 1f;
        return Clip(a.X, d.X, r.X, r.X + r.Width, ref tMin, ref tMax)
            && Clip(a.Y, d.Y, r.Y, r.Y + r.Height, ref tMin, ref tMax);
    }

    static bool Clip(float start, float delta, float min, float max, ref float tMin, ref float tMax)
    {
        if (delta == 0) return start >= min && start <= max; // parallel to this axis: inside the slab or never
        float t1 = (min - start) / delta, t2 = (max - start) / delta;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }

    /// <summary>Debug view of the solid areas and spawn points, in world space.</summary>
    public static void Draw()
    {
        foreach (var w in walls) Raylib.DrawRectangleLinesEx(w, 2, Color.Red);
        foreach (var p in enemySpawns) Raylib.DrawCircleLinesV(p, 12, Color.Orange);
        Raylib.DrawCircleLinesV(PlayerSpawn, 12, Color.Green);
    }
}
