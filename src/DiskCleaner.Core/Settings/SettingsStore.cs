using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiskCleaner.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public CleanerSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new CleanerSettings();
            return JsonSerializer.Deserialize<CleanerSettings>(File.ReadAllText(_filePath), Json) ?? new CleanerSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new CleanerSettings();
        }
    }

    public void Save(CleanerSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Json));
    }
}
