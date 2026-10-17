using System.Text.Json;

namespace GtaRadio;

public sealed class AppConfig
{
    public bool Enabled { get; set; } = true;
    public string? SourceAppId { get; set; }              // null = авто
    public float Volume { get; set; } = 1.0f;
    public float DuckVolume { get; set; } = 0.18f;
    public bool ExitWithGame { get; set; } = true;
    public bool AllowAnyApp { get; set; } = false;
    public int WaitForGameSeconds { get; set; } = 600;
    public string? GameDir { get; set; }
    public string? OutputDevice { get; set; }
    public bool ControlOnly { get; set; } = true;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
    private static string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
