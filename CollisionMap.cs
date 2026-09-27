using System.Numerics;
using System.Xml.Linq;
using Raylib_cs;

static class CollisionMap
{
    /// <summary>Objects in an object layer with this name are pits: water, lava, chasms. They stop anything walking,
    /// but shots and line of sight pass over them. Objects in every other layer are full walls.</summary>
    const string PitLayer = "Pits";

    static readonly List<Rectangle> walls = [];
    static readonly List<Rectangle> pits = [];
    static readonly List<Vector2> enemySpawns = [];

    public static Vector2 PlayerSpawn { get; private set; } = World.Center;
    public static IReadOnlyList<Vector2> EnemySpawns => enemySpawns;

    public static void Load(string tmxPath)
    {
        walls.Clear();
        pits.Clear();
        enemySpawns.Clear();
        var map = XDocument.Load(tmxPath).Root!;
        // The map's tile grid sets the world size; object coordinates are authored over the background image, so they
        // scale from its pixels to world units (1:1 unless the art was rendered at a different resolution).
        int mapWidth = (int)map.Attribute("width")! * (int)map.Attribute("tilewidth")!;
        int mapHeight = (int)map.Attribute("height")! * (int)map.Attribute("tileheight")!;
        World.Resize(mapWidth, mapHeight);
        PlayerSpawn = World.Center;
        var image = map.Element("imagelayer")?.Element("image");
        float srcWidth = (float?)image?.Attribute("width") ?? mapWidth;
        float srcHeight = (float?)image?.Attribute("height") ?? mapHeight;
        float sx = World.Width / srcWidth;
        float sy = World.Height / srcHeight;

        foreach (var group in map.Elements("objectgroup"))
        foreach (var obj in group.Elements("object"))
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
            bool isPit = string.Equals((string?)group.Attribute("name"), PitLayer, StringComparison.OrdinalIgnoreCase);
            (isPit ? pits : walls).Add(new Rectangle(x, y, (float)obj.Attribute("width")! * sx, (float)obj.Attribute("height")! * sy));
        }
    }

    /// <summary>True if something walking with these bounds would hit a wall or step into a pit.</summary>
    public static bool Blocks(Rectangle bounds) =>
        walls.Any(w => Raylib.CheckCollisionRecs(w, bounds)) || pits.Any(p => Raylib.CheckCollisionRecs(p, bounds));

    /// <summary>True if a shot with these bounds hits a wall; shots fly over pits.</summary>
    public static bool BlocksShots(Rectangle bounds) => walls.Any(w => Raylib.CheckCollisionRecs(w, bounds));

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

    /// <summary>Debug view of the walls (red), pits (blue) and spawn points, in world space.</summary>
    public static void Draw()
    {
        foreach (var w in walls) Raylib.DrawRectangleLinesEx(w, 2, Color.Red);
        foreach (var p in pits) Raylib.DrawRectangleLinesEx(p, 2, Color.SkyBlue);
        foreach (var p in enemySpawns) Raylib.DrawCircleLinesV(p, 12, Color.Orange);
        Raylib.DrawCircleLinesV(PlayerSpawn, 12, Color.Green);
    }
}
