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
        var owner = window.Owner;
        var ownerRestoreState = owner?.WindowState is WindowState.Minimized or null ? WindowState.Normal : owner.WindowState;
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
            RestoreOwner(owner, ownerRestoreState);
        };
        window.Show();
        window.Activate();
        return window;
    }

    private static void RestoreAndActivate(string key, Window window)
    {
        if (window.Owner is { } owner)
        {
            var ownerState = owner.WindowState == WindowState.Minimized ? WindowState.Normal : owner.WindowState;
            RestoreOwner(owner, ownerState);
        }
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = RestoreStates.GetValueOrDefault(key, WindowState.Normal);
        if (!window.IsVisible) window.Show();
        window.Activate();
        window.Focus();
    }

    private static void RestoreOwner(Window? owner, WindowState restoreState)
    {
        if (owner is null || Application.Current?.Dispatcher.HasShutdownStarted == true) return;
        owner.Dispatcher.BeginInvoke(() =>
        {
            if (!owner.IsLoaded || !owner.IsVisible) return;
            if (owner.WindowState == WindowState.Minimized)
                owner.WindowState = restoreState == WindowState.Minimized ? WindowState.Normal : restoreState;
            owner.Activate();
            owner.Focus();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
}
