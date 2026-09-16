using System.Numerics;
using System.Xml.Linq;
using Raylib_cs;

/// <summary>
/// Solid areas read from the Tiled map's object layer. The map is authored over the background image, so its
/// rectangles are stretched into world units exactly the way the background is drawn.
/// </summary>
static class CollisionMap
{
    static readonly List<Rectangle> walls = [];

    public static void Load(string tmxPath)
    {
        walls.Clear();
        var map = XDocument.Load(tmxPath).Root!;
        var image = map.Element("imagelayer")?.Element("image");
        float srcWidth = (float?)image?.Attribute("width") ?? (int)map.Attribute("width")! * (int)map.Attribute("tilewidth")!;
        float srcHeight = (float?)image?.Attribute("height") ?? (int)map.Attribute("height")! * (int)map.Attribute("tileheight")!;
        float sx = World.Width / srcWidth;
        float sy = World.Height / srcHeight;

        foreach (var obj in map.Elements("objectgroup").Elements("object"))
        {
            if (obj.Attribute("width") is null || obj.Attribute("height") is null) continue; // points/polygons: not supported
            walls.Add(new Rectangle(
                (float)obj.Attribute("x")! * sx,
                (float)obj.Attribute("y")! * sy,
                (float)obj.Attribute("width")! * sx,
                (float)obj.Attribute("height")! * sy));
        }
    }

    public static bool Blocks(Rectangle bounds) => walls.Any(w => Raylib.CheckCollisionRecs(w, bounds));

    /// <summary>Debug view of the solid areas, in world space.</summary>
    public static void Draw()
    {
        foreach (var w in walls) Raylib.DrawRectangleLinesEx(w, 2, Color.Red);
    }
}
