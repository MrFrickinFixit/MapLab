using System.IO;
using System.Text.Json;

namespace TimingTableCalculator;

public sealed record MapSensorProfile(string Name, double MaxBoostPsi, bool IsPreset = false);

public static class MapSensorLibrary
{
    public static readonly MapSensorProfile[] Presets =
    [
        new("2-Bar (~14.5 psi)", 14.5, true),
        new("3-Bar (~29 psi)", 29.0, true),
        new("4-Bar (~43.5 psi)", 43.5, true),
    ];

    private static string SavePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimingTableCalculator", "map-sensors.json");

    public static List<MapSensorProfile> LoadCustomSensors()
    {
        try
        {
            if (!File.Exists(SavePath)) return [];
            var stored = JsonSerializer.Deserialize<List<MapSensorProfile>>(File.ReadAllText(SavePath));
            return stored is null ? [] : stored.Where(sensor => sensor.MaxBoostPsi > 0 && !string.IsNullOrWhiteSpace(sensor.Name)).Select(sensor => sensor with { IsPreset = false }).ToList();
        }
        catch { return []; }
    }

    public static void SaveCustomSensors(IEnumerable<MapSensorProfile> sensors)
    {
        try
        {
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(sensors.ToList()));
        }
        catch { /* best effort persistence */ }
    }

    public static double RoundToHalfPsi(double value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
}
