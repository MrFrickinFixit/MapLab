using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class OffsetSelectionWindow : Window
{
    private readonly TextBox amountBox;
    private readonly ComboBox unitBox;
    private readonly Action<int, double, bool> applyOffset;
    public double Amount { get; private set; }
    public bool IsPercentage { get; private set; }

    public OffsetSelectionWindow(double amount, bool isPercentage, Action<int, double, bool> applyOffset)
    {
        this.applyOffset = applyOffset; Amount = amount; IsPercentage = isPercentage;
        Title = "Offset Selection"; Width = 410; Height = 300; MinHeight = 300; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(22) }; for (var i = 0; i < 4; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "OFFSET SELECTED CELLS", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 15) });
        var settings = new Grid { Margin = new Thickness(0, 0, 0, 14) }; settings.ColumnDefinitions.Add(new ColumnDefinition()); settings.ColumnDefinitions.Add(new ColumnDefinition());
        var amountPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; amountPanel.Children.Add(Label("OFFSET AMOUNT")); amountBox = Box(amount.ToString("0.###", CultureInfo.InvariantCulture)); amountPanel.Children.Add(amountBox); settings.Children.Add(amountPanel);
        var unitPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; unitPanel.Children.Add(Label("OFFSET UNIT")); unitBox = new ComboBox { Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(8, 5, 8, 5), SelectedIndex = isPercentage ? 1 : 0 }; unitBox.Items.Add(new ComboBoxItem { Content = "Numerical", Foreground = Brushes.Black }); unitBox.Items.Add(new ComboBoxItem { Content = "Percentage of value", Foreground = Brushes.Black }); unitPanel.Children.Add(unitBox); Grid.SetColumn(unitPanel, 1); settings.Children.Add(unitPanel); Grid.SetRow(settings, 1); root.Children.Add(settings);
        var arrows = new Grid { Margin = new Thickness(0, 0, 0, 12) }; arrows.ColumnDefinitions.Add(new ColumnDefinition()); arrows.ColumnDefinitions.Add(new ColumnDefinition());
        var up = ArrowButton("▲  Increase", Color.FromRgb(54, 199, 173), Brushes.Black); var down = ArrowButton("▼  Decrease", Color.FromRgb(67, 89, 118), Brushes.White); up.Click += (_, _) => Apply(1); down.Click += (_, _) => Apply(-1); arrows.Children.Add(up); Grid.SetColumn(down, 1); arrows.Children.Add(down); Grid.SetRow(arrows, 2); root.Children.Add(arrows);
        var bottom = new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(new TextBlock { Text = "Each click updates the active table immediately.", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "Close", Width = 88, Height = 34, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White }; close.Click += (_, _) => Close(); Grid.SetColumn(close, 1); bottom.Children.Add(close); Grid.SetRow(bottom, 3); root.Children.Add(bottom); Content = root;
    }

    private void Apply(int direction)
    {
        if (!double.TryParse(amountBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        { MessageBox.Show("Enter a non-negative numerical offset amount.", "Check offset", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Amount = amount; IsPercentage = unitBox.SelectedIndex == 1; applyOffset(direction, Amount, IsPercentage);
    }

    private static TextBlock Label(string text) => new() { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
    private static TextBox Box(string text) => new() { Text = text, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(8, 6, 8, 6), TextAlignment = TextAlignment.Right };
    private static Button ArrowButton(string text, Color background, Brush foreground) => new() { Content = text, Height = 42, Margin = new Thickness(4), Background = new SolidColorBrush(background), Foreground = foreground, FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0) };
}
