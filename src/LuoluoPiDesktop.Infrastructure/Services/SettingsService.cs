using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _filePath;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "LuoluoPiDesktop");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    public void Save()
    {
        var tmp = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(Current, JsonOpts);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }

    public void Reload() => Current = Load();

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
