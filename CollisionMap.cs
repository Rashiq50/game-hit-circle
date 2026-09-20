using System.Numerics;
using System.Xml.Linq;
using Raylib_cs;

/// <summary>
/// Data read from the Tiled map's object layers: rectangles are solid walls, points named "PlayerSpawn" / "EnemySpawn"
/// are spawn positions. The map is authored over the background image, so everything is stretched into world units
/// exactly the way the background is drawn.
/// </summary>
static class CollisionMap
{
    static readonly List<Rectangle> walls = [];
    static readonly List<Vector2> enemySpawns = [];

    /// <summary>Where the player starts; the world centre if the map has no "PlayerSpawn" point.</summary>
    public static Vector2 PlayerSpawn { get; private set; } = World.Center;
    /// <summary>Every "EnemySpawn" point, in world units; empty if the map has none.</summary>
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

    /// <summary>Debug view of the solid areas and spawn points, in world space.</summary>
    public static void Draw()
    {
        foreach (var w in walls) Raylib.DrawRectangleLinesEx(w, 2, Color.Red);
        foreach (var p in enemySpawns) Raylib.DrawCircleLinesV(p, 12, Color.Orange);
        Raylib.DrawCircleLinesV(PlayerSpawn, 12, Color.Green);
    }
}
