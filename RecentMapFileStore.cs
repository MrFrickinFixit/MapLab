using System.IO;
using System.Text.Json;

namespace TimingTableCalculator;

internal static class RecentMapFileStore
{
    internal static string? Read(string preferencePath)
    {
        try
        {
            if (!File.Exists(preferencePath)) return null;
            var state = JsonSerializer.Deserialize<RecentMapFileState>(File.ReadAllText(preferencePath));
            return string.IsNullOrWhiteSpace(state?.MapFilePath) ? null : Path.GetFullPath(state.MapFilePath);
        }
        catch { return null; }
    }

    internal static bool Write(string preferencePath, string mapFilePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferencePath)!);
            var state = new RecentMapFileState { MapFilePath = Path.GetFullPath(mapFilePath) };
            File.WriteAllText(preferencePath, JsonSerializer.Serialize(state));
            return true;
        }
        catch { return false; }
    }

    private sealed class RecentMapFileState
    {
        public string? MapFilePath { get; set; }
    }
}
