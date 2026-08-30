using System.Windows;

namespace TimingTableCalculator;

internal static class ModelessWindowManager
{
    private static readonly Dictionary<string, WeakReference<Window>> OpenWindows = [];
    private static readonly Dictionary<string, WindowState> RestoreStates = [];

    public static bool ActivateIfOpen(string key)
    {
        if (!OpenWindows.TryGetValue(key, out var reference) || !reference.TryGetTarget(out var window) || !window.IsLoaded) return false;
        RestoreAndActivate(key, window);
        return true;
    }

    public static T ShowOrActivate<T>(string key, Func<T> createWindow) where T : Window
    {
        if (OpenWindows.TryGetValue(key, out var reference) && reference.TryGetTarget(out var existing) && existing is T typed && existing.IsLoaded)
        {
            RestoreAndActivate(key, typed);
            return typed;
        }

        var window = createWindow();
        OpenWindows[key] = new WeakReference<Window>(window);
        RestoreStates[key] = window.WindowState == WindowState.Minimized ? WindowState.Normal : window.WindowState;
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState != WindowState.Minimized) RestoreStates[key] = window.WindowState;
        };
        window.Closed += (_, _) =>
        {
            if (OpenWindows.TryGetValue(key, out var current) && current.TryGetTarget(out var target) && ReferenceEquals(target, window))
            {
                OpenWindows.Remove(key);
                RestoreStates.Remove(key);
            }
        };
        window.Show();
        window.Activate();
        return window;
    }

    private static void RestoreAndActivate(string key, Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = RestoreStates.GetValueOrDefault(key, WindowState.Normal);
        if (!window.IsVisible) window.Show();
        window.Activate();
        window.Focus();
    }
}
