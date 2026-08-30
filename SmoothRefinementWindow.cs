using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class SmoothRefinementWindow : Window
{
    private readonly TextBox strengthBox;
    private readonly TextBox passesBox;
    private readonly Action<SmoothRefinementWindow>? applyAction;
    public double Strength { get; private set; }
    public int Passes { get; private set; }

    public SmoothRefinementWindow(double strength, int passes, Action<SmoothRefinementWindow>? applyAction = null)
    {
        this.applyAction = applyAction;
        Title = "Smooth Refinement"; Width = 420; Height = 260; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TextBlock { Text = "SMOOTH REFINEMENT", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 14) });
        strengthBox = AddField(root, 1, "STRENGTH (1–100%)", (strength * 100).ToString("0", CultureInfo.InvariantCulture));
        passesBox = AddField(root, 2, "PASSES (1–20)", passes.ToString(CultureInfo.InvariantCulture));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        var apply = new Button { Content = "Refine", Width = 86, Height = 34, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Margin = new Thickness(0, 14, 8, 0) };
        var cancel = new Button { Content = "Close", Width = 86, Height = 34, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, Margin = new Thickness(0, 14, 0, 0) }; cancel.Click += (_, _) => Close();
        apply.Click += Apply_Click; actions.Children.Add(apply); actions.Children.Add(cancel); Grid.SetRow(actions, 3); root.Children.Add(actions); Content = root;
    }

    private static TextBox AddField(Grid root, int row, string label, string value)
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 12) }; panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = value, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(9, 6, 9, 6), TextAlignment = TextAlignment.Right }; Grid.SetColumn(box, 1); panel.Children.Add(box); Grid.SetRow(panel, row); root.Children.Add(panel); return box;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(strengthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var strength) || strength is < 1 or > 100 ||
            !int.TryParse(passesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var passes) || passes is < 1 or > 20)
        {
            MessageBox.Show("Strength must be 1–100% and passes must be 1–20.", "Check refinement settings", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        Strength = strength / 100; Passes = passes;
        if (applyAction is null) DialogResult = true; else applyAction(this);
    }
}
