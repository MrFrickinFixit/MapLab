using System.IO;
using System.Text.Json;

namespace TimingTableCalculator;

public enum Surface3DInputProfile
{
    DesktopMouse,
    LaptopTouchpad
}

internal static class Surface3DNavigationPreferences
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimingTableCalculator",
        "3d-navigation.json");

    private static Surface3DInputProfile current = Load();

    internal static event Action<Surface3DInputProfile>? Changed;

    internal static Surface3DInputProfile Current
    {
        get => current;
        set
        {
            if (current == value) return;
            current = value;
            Save(value);
            Changed?.Invoke(value);
        }
    }

    private static Surface3DInputProfile Load()
    {
        try
        {
            if (!File.Exists(PreferencePath)) return Surface3DInputProfile.DesktopMouse;
            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(PreferencePath));
            return Enum.TryParse<Surface3DInputProfile>(state?.InputProfile, out var profile)
                ? profile
                : Surface3DInputProfile.DesktopMouse;
        }
        catch { return Surface3DInputProfile.DesktopMouse; }
    }

    private static void Save(Surface3DInputProfile profile)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, JsonSerializer.Serialize(new State { InputProfile = profile.ToString() }));
        }
        catch { }
    }

    private sealed class State
    {
        public string? InputProfile { get; set; }
    }
}
