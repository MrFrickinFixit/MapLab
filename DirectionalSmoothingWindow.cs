using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class DirectionalSmoothingWindow : Window
{
    private readonly ComboBox direction = new();
    private readonly TextBox strength, passes;
    private readonly Action<DirectionalSmoothingWindow>? applyAction;
    public bool OuterToInner { get; private set; }
    public double Strength { get; private set; }
    public int Passes { get; private set; }

    public DirectionalSmoothingWindow(bool outerToInner, double smoothingStrength = .65, int smoothingPasses = 2, Action<DirectionalSmoothingWindow>? applyAction = null)
    {
        this.applyAction = applyAction;
        Title = "Directional Smoothing"; Width = 430; Height = 350; MinHeight = 350; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = "DIRECTIONAL SELECTION SMOOTHING", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 14) });
        root.Children.Add(Label("DIRECTION"));
        direction.Background = Brushes.White; direction.Foreground = Brushes.Black; direction.Padding = new Thickness(8, 5, 8, 5);
        direction.Items.Add(new ComboBoxItem { Content = "Outer perimeter → inner cells", Foreground = Brushes.Black });
        direction.Items.Add(new ComboBoxItem { Content = "Inner cells → outer perimeter", Foreground = Brushes.Black }); direction.SelectedIndex = outerToInner ? 0 : 1; root.Children.Add(direction);
        var settings = new Grid { Margin = new Thickness(0, 14, 0, 0) }; settings.ColumnDefinitions.Add(new ColumnDefinition()); settings.ColumnDefinitions.Add(new ColumnDefinition());
        strength = AddSetting(settings, "STRENGTH (%)", (smoothingStrength * 100).ToString("0", CultureInfo.InvariantCulture), 0);
        passes = AddSetting(settings, "PASSES", smoothingPasses.ToString(CultureInfo.InvariantCulture), 1); root.Children.Add(settings);
        root.Children.Add(new TextBlock { Text = "The source edge is preserved while smoothing progresses layer by layer toward the destination.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, Margin = new Thickness(0, 12, 0, 0) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 8) };
        var apply = new Button { Content = "Apply", Width = 90, Height = 34, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold };
        apply.Click += Apply; actions.Children.Add(apply); var close = new Button { Content = "Close", Width = 82, Height = 34, Margin = new Thickness(8, 0, 0, 0) }; close.Click += (_, _) => Close(); actions.Children.Add(close); root.Children.Add(actions); Content = root;
    }

    private void Apply(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(strength.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || percent is <= 0 or > 100 || !int.TryParse(passes.Text, out var count) || count is < 1 or > 10)
        { MessageBox.Show("Strength must be 1–100% and passes must be 1–10.", "Check smoothing settings", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        OuterToInner = direction.SelectedIndex == 0; Strength = percent / 100; Passes = count;
        if (applyAction is null) DialogResult = true; else applyAction(this);
    }

    private static TextBlock Label(string text) => new() { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
    private static TextBox AddSetting(Grid grid, string label, string value, int column) { var panel = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 8, 0, column == 0 ? 8 : 0, 0) }; panel.Children.Add(Label(label)); var box = new TextBox { Text = value, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, Padding = new Thickness(8, 6, 8, 6), TextAlignment = TextAlignment.Right }; panel.Children.Add(box); Grid.SetColumn(panel, column); grid.Children.Add(panel); return box; }
}
