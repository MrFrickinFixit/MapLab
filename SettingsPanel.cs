using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class SettingsPanel : Grid
{
    private readonly TextBlock status = new() { Text = "No settings file imported or exported this session.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 12 };

    public SettingsPanel(Action exportSettings, Action importSettings)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        var heading = new StackPanel { Margin = new Thickness(4, 0, 0, 20) };
        heading.Children.Add(new TextBlock { Text = "MAP LAB", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = "Settings", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 27, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) });
        heading.Children.Add(new TextBlock { Text = "Back up or move the complete Map Lab workspace.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 13, Margin = new Thickness(0, 5, 0, 0) }); Children.Add(heading);

        var content = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(new TextBlock { Text = "IMPORT & EXPORT", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        content.Children.Add(new TextBlock { Text = "A .map file contains the Timing, Fueling, Learn Apply, and Sandbox tables together with their axes, units, boundaries, colors, number display, smoothing, and VE setup settings.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 13, LineHeight = 20, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(MakeButton("⇩  Export .map", (_, _) => exportSettings(), true));
        buttons.Children.Add(MakeButton("⇧  Import .map", (_, _) => importSettings(), false)); content.Children.Add(buttons);
        content.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(225, 225, 225)), Margin = new Thickness(0, 18, 0, 14) });
        content.Children.Add(status);
        content.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 190, 92)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(12), Margin = new Thickness(0, 18, 0, 0), Child = new TextBlock { Text = "Import replaces all current workspaces, including Learn Apply offsets. Map Lab validates the complete file and asks for confirmation first.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(82, 62, 14)) } });
        var card = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(22), VerticalAlignment = VerticalAlignment.Top, Child = content }; Grid.SetRow(card, 1); Children.Add(card);
    }

    public void SetStatus(string text) => status.Text = text;
    private static Button MakeButton(string text, RoutedEventHandler click, bool primary)
    {
        var button = new Button { Content = text, Padding = new Thickness(15, 9, 15, 9), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold };
        button.Click += click; return button;
    }
}
