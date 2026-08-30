using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class AdvancedSmoothingWindow : Window
{
    private readonly ComboBox algorithmBox;
    private readonly TextBox strengthBox, passesBox, influenceBox;
    private readonly CheckBox perimeterBox, overshootBox;
    private readonly Action<AdvancedSmoothingWindow> applyAction;
    public AdvancedSmoothingOptions Options { get; private set; }

    public AdvancedSmoothingWindow(AdvancedSmoothingOptions options, Action<AdvancedSmoothingWindow> applyAction)
    {
        Options = options; this.applyAction = applyAction;
        Title = "Advanced Smoothing"; Width = 610; Height = 455; MinHeight = 455; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(22) }; for (var i = 0; i < 7; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "ADVANCED SELECTION SMOOTHING", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 14) });
        algorithmBox = new ComboBox { Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(8, 5, 8, 5), SelectedIndex = (int)options.Algorithm };
        foreach (var name in new[] { "Shape-preserving interpolation", "Constrained surface smoothing", "Spike removal (median)", "Edge-preserving smoothing", "Weighted center / perimeter", "Standard weighted smoothing" }) algorithmBox.Items.Add(new ComboBoxItem { Content = name, Foreground = Brushes.Black });
        AddField(root, 1, "ALGORITHM", algorithmBox);
        strengthBox = Box((options.Strength * 100).ToString("0", CultureInfo.InvariantCulture)); AddField(root, 2, "STRENGTH (1–100%)", strengthBox);
        passesBox = Box(options.Passes.ToString(CultureInfo.InvariantCulture)); AddField(root, 3, "PASSES (1–20)", passesBox);
        influenceBox = Box((options.CenterInfluence * 100).ToString("0", CultureInfo.InvariantCulture)); AddField(root, 4, "CENTER INFLUENCE (0% = PERIMETER, 100% = CENTER)", influenceBox);
        var checks = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 15) };
        perimeterBox = Check("Preserve selection perimeter", options.PreservePerimeter); overshootBox = Check("Prevent value overshoot", options.PreventOvershoot); checks.Children.Add(perimeterBox); checks.Children.Add(overshootBox); Grid.SetRow(checks, 5); root.Children.Add(checks);
        var bottom = new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(new TextBlock { Text = "Apply stays open and creates a separate Undo step.", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; var apply = Button("Apply", true); var close = Button("Close", false); apply.Click += Apply; close.Click += (_, _) => Close(); actions.Children.Add(apply); actions.Children.Add(close); Grid.SetColumn(actions, 1); bottom.Children.Add(actions); Grid.SetRow(bottom, 6); root.Children.Add(bottom); Content = root;
    }

    private void Apply(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(strengthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var strength) || strength is < 1 or > 100 ||
            !int.TryParse(passesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var passes) || passes is < 1 or > 20 ||
            !double.TryParse(influenceBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var influence) || influence is < 0 or > 100)
        { MessageBox.Show("Strength must be 1–100%, passes 1–20, and center influence 0–100%.", "Check smoothing settings", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Options = new AdvancedSmoothingOptions((AdvancedSmoothingAlgorithm)algorithmBox.SelectedIndex, strength / 100, passes, perimeterBox.IsChecked == true, overshootBox.IsChecked == true, influence / 100);
        applyAction(this);
    }

    private static void AddField(Grid grid, int row, string label, Control control)
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 10) }; panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(control, 1); panel.Children.Add(control); Grid.SetRow(panel, row); grid.Children.Add(panel);
    }
    private static TextBox Box(string text) => new() { Text = text, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(8, 6, 8, 6), TextAlignment = TextAlignment.Right };
    private static CheckBox Check(string text, bool value) => new() { Content = text, IsChecked = value, Foreground = Brushes.White, Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
    private static Button Button(string text, bool primary) => new() { Content = text, Width = 88, Height = 34, Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(primary ? Color.FromRgb(54, 199, 173) : Color.FromRgb(28, 38, 53)), Foreground = primary ? Brushes.Black : Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
}
