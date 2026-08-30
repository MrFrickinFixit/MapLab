using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class BoostRetardWindow : Window
{
    private readonly TextBox rateBox;
    private readonly TextBox lowMapBox;
    private readonly TextBox highMapBox;
    private readonly Action<BoostRetardWindow>? applyAction;
    public double RetardPerPsi { get; private set; }
    public double LowMap { get; private set; }
    public double HighMap { get; private set; }

    public BoostRetardWindow(double rate, double lowMap, double highMap, Action<BoostRetardWindow>? applyAction = null)
    {
        this.applyAction = applyAction;
        Title = "Boost Timing Offset"; Width = 460; Height = 330; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TextBlock { Text = "BOOST TIMING OFFSET", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
        rateBox = AddField(root, 1, "TIMING CHANGE PER PSI (DEGREES)", rate.ToString("0.00", CultureInfo.InvariantCulture));
        lowMapBox = AddField(root, 2, "LOW MAP / OFFSET START (PSI GAUGE)", lowMap.ToString("0.0", CultureInfo.InvariantCulture));
        highMapBox = AddField(root, 3, "HIGH MAP / MAXIMUM OFFSET (PSI GAUGE)", highMap.ToString("0.0", CultureInfo.InvariantCulture));
        var bottom = new Grid { Margin = new Thickness(0, 16, 0, 0) }; bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(new TextBlock { Text = "Negative rate subtracts timing; positive rate adds timing.\nChange is clamped at the high MAP value.", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var apply = new Button { Content = "Apply", Width = 82, Height = 34, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Margin = new Thickness(8, 0, 8, 0) };
        var cancel = new Button { Content = "Close", Width = 82, Height = 34, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White }; cancel.Click += (_, _) => Close();
        apply.Click += Apply_Click; actions.Children.Add(apply); actions.Children.Add(cancel); Grid.SetColumn(actions, 1); bottom.Children.Add(actions); Grid.SetRow(bottom, 4); root.Children.Add(bottom); Content = root;
    }

    private static TextBox AddField(Grid root, int row, string label, string value)
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 10) }; panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = value, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(9, 6, 9, 6), TextAlignment = TextAlignment.Right }; Grid.SetColumn(box, 1); panel.Children.Add(box); Grid.SetRow(panel, row); root.Children.Add(panel); return box;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(rateBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) ||
            !double.TryParse(lowMapBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var low) ||
            !double.TryParse(highMapBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var high) || low >= high)
        {
            MessageBox.Show("Enter a valid signed timing change and a high MAP value greater than the low MAP value.", "Check boost timing settings", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        RetardPerPsi = rate; LowMap = low; HighMap = high;
        if (applyAction is null) DialogResult = true; else applyAction(this);
    }
}
