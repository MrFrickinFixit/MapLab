using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public enum DeltaCompareApplyMode
{
    ApplyPasted,
    SmoothDelta
}

public sealed class DeltaCompareWindow : Window
{
    private readonly TextBox strengthBox, passesBox;
    private readonly Action<DeltaCompareApplyMode, double, int> apply;

    public DeltaCompareWindow(string title, int rows, int columns, double minimumDelta, double maximumDelta, double averageDelta, double averageAbsoluteDelta, Action<DeltaCompareApplyMode, double, int> apply)
    {
        this.apply = apply;
        Title = title; Width = 700; Height = 440; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(18, 26, 38)); FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(22) };
        for (var row = 0; row < 8; row++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "DELTA COMPARE", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });

        var summary = new TextBlock
        {
            Text = $"{columns} x {rows} cells  |  delta {minimumDelta:+0.0;-0.0;0.0} to {maximumDelta:+0.0;-0.0;0.0}  |  average {averageDelta:+0.0;-0.0;0.0}  |  average absolute {averageAbsoluteDelta:0.0}",
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(summary, 1); root.Children.Add(summary);

        root.Children.Add(Note("Current table is what is already in Map Lab. Pasted table is the table or block on the clipboard. Delta is pasted minus current.", 2));

        var settings = new Grid { Margin = new Thickness(0, 12, 0, 14) };
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settings.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        settings.Children.Add(Label("SMOOTH STRENGTH (1-100%)")); strengthBox = Box("65"); Grid.SetColumn(strengthBox, 1); settings.Children.Add(strengthBox);
        var passesLabel = Label("PASSES"); Grid.SetColumn(passesLabel, 3); settings.Children.Add(passesLabel); passesBox = Box("2"); Grid.SetColumn(passesBox, 4); settings.Children.Add(passesBox);
        Grid.SetRow(settings, 3); root.Children.Add(settings);

        root.Children.Add(Option(4, "Use pasted table", "Replace the selected/current cells with the pasted values exactly. No smoothing is applied.", "Use Pasted", DeltaCompareApplyMode.ApplyPasted, false));
        root.Children.Add(Option(5, "Smooth pasted delta", "Apply the pasted values, then run the same basic weighted smoothing across the pasted block. Cells outside the pasted block are unchanged.", "Smooth Delta", DeltaCompareApplyMode.SmoothDelta, true));

        var close = new Button { Content = "Close", Width = 92, Height = 34, Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
        close.Click += (_, _) => Close(); Grid.SetRow(close, 7); root.Children.Add(close);
        Content = root;
    }

    private TextBlock Note(string text, int row)
    {
        var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(note, row); return note;
    }

    private static TextBlock Label(string text) => new() { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private static TextBox Box(string text) => new() { Text = text, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(8, 6, 8, 6), TextAlignment = TextAlignment.Right };

    private Border Option(int row, string heading, string body, string buttonText, DeltaCompareApplyMode mode, bool primary)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = heading, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        copy.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 12 });
        grid.Children.Add(copy);

        var button = Button(buttonText, mode, primary);
        button.Margin = new Thickness(18, 0, 0, 0);
        Grid.SetColumn(button, 1); grid.Children.Add(button);

        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), BorderThickness = new Thickness(1), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 10), Child = grid };
        Grid.SetRow(border, row); return border;
    }

    private Button Button(string text, DeltaCompareApplyMode mode, bool primary)
    {
        var button = new Button { Content = text, Width = 124, Height = 36, Background = new SolidColorBrush(primary ? Color.FromRgb(54, 199, 173) : Color.FromRgb(28, 38, 53)), Foreground = primary ? Brushes.Black : Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
        button.Click += (_, _) => Apply(mode);
        return button;
    }

    private void Apply(DeltaCompareApplyMode mode)
    {
        if (mode == DeltaCompareApplyMode.ApplyPasted)
        {
            apply(mode, 0, 0);
            Close();
            return;
        }
        if (!double.TryParse(strengthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var strength) || strength is < 1 or > 100 ||
            !int.TryParse(passesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var passes) || passes is < 1 or > 20)
        {
            MessageBox.Show("Strength must be 1-100%, and passes must be 1-20.", "Check delta smoothing settings", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        apply(mode, strength / 100, passes);
        Close();
    }
}
