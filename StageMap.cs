using Raylib_cs;

/// <summary>
/// The map the current stage is played on: textures/stages/{Name}.png as the floor, and {Name}.tmx for its walls,
/// spawn points and size (loaded into <see cref="CollisionMap"/> and <see cref="World"/>). Only one map is resident;
/// switching unloads the previous background.
/// </summary>
static class StageMap
{
    const string Folder = "textures/stages";

    public static string Name { get; private set; } = "";
    public static Texture2D Background { get; private set; }

    /// <summary>Switches to the named map. Returns false when it's already the loaded one, so nothing moved.</summary>
    public static bool Load(string name)
    {
        if (name == Name) return false;
        Unload();
        CollisionMap.Load($"{Folder}/{name}.tmx");
        Background = Raylib.LoadTexture($"{Folder}/{name}.png");
        Name = name;
        return true;
    }

    public static void Unload()
    {
        if (Name == "") return;
        Raylib.UnloadTexture(Background);
        Name = "";
    }
}
