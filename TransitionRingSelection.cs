using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

internal static class TransitionRingSelection
{
    public static bool TryGetRectangle(IReadOnlyCollection<(int Row, int Col)> selected, out int top, out int bottom, out int left, out int right)
    {
        top = bottom = left = right = 0;
        if (selected.Count == 0) return false;
        top = selected.Min(cell => cell.Row); bottom = selected.Max(cell => cell.Row);
        left = selected.Min(cell => cell.Col); right = selected.Max(cell => cell.Col);
        return selected.Count == (bottom - top + 1) * (right - left + 1);
    }

    public static int MaximumThickness(int top, int bottom, int left, int right) =>
        Math.Max(0, (Math.Min(bottom - top + 1, right - left + 1) - 1) / 2);

    public static HashSet<(int Row, int Col)> Create(int top, int bottom, int left, int right, int thickness)
    {
        var ring = new HashSet<(int Row, int Col)>();
        thickness = Math.Clamp(thickness, 1, MaximumThickness(top, bottom, left, right));
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++)
            if (Math.Min(Math.Min(row - top, bottom - row), Math.Min(col - left, right - col)) < thickness) ring.Add((row, col));
        return ring;
    }
}

internal sealed class TransitionRingWindow : Window
{
    private readonly ComboBox widthBox;
    private Action<int> applyAction = _ => { };

    public TransitionRingWindow(int maximumThickness, int initialThickness, Action<int> apply)
    {
        Title = "Select Transition Ring"; Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = "TRANSITION RING", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "Select only the transition around the feature", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) });
        root.Children.Add(new TextBlock { Text = "The ring is measured inward from the current rectangular selection. The center and the cells outside the rectangle remain unselected anchors.", TextWrapping = TextWrapping.Wrap, LineHeight = 19, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 0, 0, 16) });
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = "RING WIDTH", Width = 105, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, FontWeight = FontWeights.SemiBold });
        widthBox = new ComboBox { Width = 90, Height = 32, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(7, 3, 7, 3) };
        row.Children.Add(widthBox); root.Children.Add(row);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var applyButton = Button("Select ring", true); var closeButton = Button("Close", false); closeButton.Margin = new Thickness(8, 0, 0, 0);
        applyButton.Click += (_, _) => { if (widthBox.SelectedItem is ComboBoxItem { Tag: int width }) applyAction(width); };
        closeButton.Click += (_, _) => Close(); buttons.Children.Add(applyButton); buttons.Children.Add(closeButton); root.Children.Add(buttons); Content = root;
        Configure(maximumThickness, initialThickness, apply);
    }

    public void Configure(int maximumThickness, int initialThickness, Action<int> apply)
    {
        applyAction = apply; widthBox.Items.Clear();
        for (var value = 1; value <= maximumThickness; value++) widthBox.Items.Add(new ComboBoxItem { Content = $"{value} cell{(value == 1 ? "" : "s")}", Tag = value, Foreground = Brushes.Black });
        widthBox.SelectedIndex = Math.Clamp(initialThickness, 1, maximumThickness) - 1;
    }

    private static Button Button(string text, bool primary) => new()
    {
        Content = text, Padding = new Thickness(14, 8, 14, 8), FontWeight = FontWeights.SemiBold,
        Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)),
        Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)),
        BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), BorderThickness = new Thickness(1)
    };
}
