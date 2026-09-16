using System.Text.Json;

/// <summary>Everything that survives between launches. Stage/Score are the checkpoint the next run starts from; HighScore is lifetime.</summary>
record SaveData(int Stage = 1, int Score = 0, int HighScore = 0, float playerHp = 100)
{
    public static readonly SaveData Fresh = new();
    public bool HasProgress => Stage > 1 || Score > 0;
}

/// <summary>Reads and writes the save file as JSON; any unreadable or missing file counts as a fresh save.</summary>
static class SaveFile
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HitThemAll", "savegame.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options) ?? SaveData.Fresh;
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        return SaveData.Fresh;
    }

    public static void Save(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, Options));
        }
        catch (IOException) { }
    }
}
