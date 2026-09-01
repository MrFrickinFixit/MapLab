using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

/// <summary>Shows a rendered, indeterminate progress window before a synchronous bulk map update begins.</summary>
public static class WorkingRunner
{
    private static WorkingWindow? current;

    public static void Run(DependencyObject context, Action operation, string message = "Working....")
    {
        if (current is not null)
        {
            operation();
            return;
        }

        var fallbackOwner = context as Window ?? Window.GetWindow(context);
        var activeOwner = Application.Current.Windows.OfType<Window>().LastOrDefault(window => window.IsActive);
        var owner = activeOwner ?? fallbackOwner;
        var progress = new WorkingWindow(message);
        if (owner is not null && owner.IsLoaded) progress.Owner = owner;
        current = progress;

        Exception? failure = null;
        var started = false;
        progress.ContentRendered += (_, _) =>
        {
            if (started) return;
            started = true;
            progress.Dispatcher.BeginInvoke(() =>
            {
                try { operation(); }
                catch (Exception exception) { failure = exception; }
                finally { progress.Complete(); }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        };

        try { progress.ShowDialog(); }
        finally
        {
            if (ReferenceEquals(current, progress)) current = null;
        }
        if (failure is not null)
        {
            if (owner is null) MessageBox.Show(failure.Message, "Map change failed", MessageBoxButton.OK, MessageBoxImage.Error);
            else MessageBox.Show(owner, failure.Message, "Map change failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal sealed class WorkingWindow : Window
{
    public WorkingWindow(string message)
    {
        Title = "Map Lab";
        Width = 360;
        Height = 150;
        MinWidth = 360;
        MinHeight = 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        FontFamily = new FontFamily("Segoe UI");
        Closing += (_, args) => { if (WorkingRunnerIsActive()) args.Cancel = true; };

        var stack = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        stack.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        });
        stack.Children.Add(new ProgressBar
        {
            Height = 8,
            IsIndeterminate = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)),
            Background = new SolidColorBrush(Color.FromRgb(218, 218, 218))
        });
        Content = stack;
    }

    private bool allowClose;
    private bool WorkingRunnerIsActive() => !allowClose;

    public void Complete()
    {
        allowClose = true;
        Close();
    }
}
