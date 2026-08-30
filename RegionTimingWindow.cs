using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed record RegionTimingProfile(string Region, double LowMap, double LowTiming, double HighMap, double HighTiming);

public sealed class RegionTimingWindow : Window
{
    private readonly Dictionary<string, TextBox[]> fields = [];
    private readonly ComboBox modeBox;
    private readonly Action<RegionTimingWindow>? applyAction;
    private readonly TextBox verticalCellsBox, horizontalCellsBox;
    public RegionTimingProfile[] Profiles { get; private set; } = [];
    public bool BlendValues { get; private set; }
    public int VerticalSmoothCells { get; private set; }
    public int HorizontalSmoothCells { get; private set; }

    public RegionTimingWindow(string mapUnit, IEnumerable<RegionTimingProfile> profiles, bool blendValues, int verticalSmoothCells, int horizontalSmoothCells, Action<RegionTimingWindow>? applyAction = null)
    {
        this.applyAction = applyAction;
        Title = "Timing by Operating Region"; Width = 1100; Height = 500; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(22) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        heading.Children.Add(new TextBlock { Text = $"REGION TIMING PROFILES  •  MAP IN {mapUnit.ToUpperInvariant()}", Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
        var modeLine = new StackPanel { Orientation = Orientation.Horizontal };
        modeLine.Children.Add(new TextBlock { Text = "APPLICATION MODE", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        modeBox = new ComboBox { Width = 260, SelectedIndex = blendValues ? 1 : 0, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(7, 4, 7, 4) };
        modeBox.Items.Add(new ComboBoxItem { Content = "Fill regions (sharp boundaries)", Foreground = Brushes.Black });
        modeBox.Items.Add(new ComboBoxItem { Content = "Fill + smooth region boundaries", Foreground = Brushes.Black });
        modeLine.Children.Add(modeBox); heading.Children.Add(modeLine);
        var widths = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        widths.Children.Add(new TextBlock { Text = "VERTICAL BOUNDARY (COLUMNS PER SIDE)", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        verticalCellsBox = SmallBox(verticalSmoothCells); widths.Children.Add(verticalCellsBox);
        widths.Children.Add(new TextBlock { Text = "HORIZONTAL BOUNDARY (ROWS PER SIDE)", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 8, 0) });
        horizontalCellsBox = SmallBox(horizontalSmoothCells); widths.Children.Add(horizontalCellsBox);
        widths.Children.Add(new TextBlock { Text = "Minimum 3 per side", Foreground = new SolidColorBrush(Color.FromRgb(118, 135, 156)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
        heading.Children.Add(widths); root.Children.Add(heading);
        var table = new Grid(); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(195) }); for (var i = 0; i < 4; i++) table.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 3; i++) table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var source = profiles.ToDictionary(p => p.Region, StringComparer.OrdinalIgnoreCase);
        var regions = new[] { "Idle", "Cruise", "WOT" };
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Idle"] = "Idle",
            ["Cruise"] = "Cruise to Part Throttle",
            ["WOT"] = "Part Throttle to WOT"
        };
        var fieldLabels = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Idle"] = ["LOW MAP", "DEGREES TIMING AT LOW MAP", "HIGH MAP", "DEGREES TIMING AT HIGH MAP"],
            ["Cruise"] = ["CRUISE LOW MAP", "DEGREES TIMING AT CRUISE LOW MAP", "PART THROTTLE MAP", "DEGREES TIMING AT PART THROTTLE"],
            ["WOT"] = ["PART THROTTLE LOW MAP", "DEGREES TIMING PART THROTTLE", "WOT MAP", "DEGREES TIMING AT WOT"]
        };
        for (var row = 0; row < regions.Length; row++)
        {
            var region = regions[row]; var profile = source[region];
            var label = new TextBlock { Text = displayNames[region].ToUpperInvariant(), Foreground = region == "Idle" ? new SolidColorBrush(Color.FromRgb(101, 176, 231)) : region == "Cruise" ? new SolidColorBrush(Color.FromRgb(85, 214, 190)) : new SolidColorBrush(Color.FromRgb(240, 154, 91)), FontWeight = FontWeights.Bold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(label, row); table.Children.Add(label);
            fields[region] =
            [
                AddLabeledBox(table, row, 1, fieldLabels[region][0], profile.LowMap),
                AddLabeledBox(table, row, 2, fieldLabels[region][1], profile.LowTiming),
                AddLabeledBox(table, row, 3, fieldLabels[region][2], profile.HighMap),
                AddLabeledBox(table, row, 4, fieldLabels[region][3], profile.HighTiming)
            ];
        }
        Grid.SetRow(table, 1); root.Children.Add(table);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var apply = new Button { Content = "Apply profiles", Width = 110, Height = 34, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Close", Width = 84, Height = 34, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White }; cancel.Click += (_, _) => Close();
        apply.Click += Apply_Click; actions.Children.Add(apply); actions.Children.Add(cancel); Grid.SetRow(actions, 2); root.Children.Add(actions); Content = root;
    }

    private static TextBox AddLabeledBox(Grid grid, int row, int column, string label, double value)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 12) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 9, FontWeight = FontWeights.Bold, Height = 28, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Bottom });
        var box = new TextBox { Text = value.ToString("0.0", CultureInfo.InvariantCulture), Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(8, 6, 8, 6), TextAlignment = TextAlignment.Right };
        panel.Children.Add(box); Grid.SetRow(panel, row); Grid.SetColumn(panel, column); grid.Children.Add(panel); return box;
    }
    private static TextBox SmallBox(int value) => new() { Text = value.ToString(CultureInfo.InvariantCulture), Width = 48, Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(7, 4, 7, 4), TextAlignment = TextAlignment.Center };

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(verticalCellsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var verticalCells) || verticalCells is < 3 or > 64 ||
            !int.TryParse(horizontalCellsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var horizontalCells) || horizontalCells is < 3 or > 64)
        { MessageBox.Show("Vertical and horizontal smoothing must each be between 3 and 64 cells.", "Check smoothing widths", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var result = new List<RegionTimingProfile>();
        foreach (var (region, boxes) in fields)
        {
            var values = new double[4];
            if (boxes.Where((box, i) => !double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])).Any() || values[0] >= values[2])
            { MessageBox.Show($"Enter valid values for {region}; its high MAP must exceed its low MAP.", "Check region profile", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            result.Add(new RegionTimingProfile(region, values[0], values[1], values[2], values[3]));
        }
        Profiles = result.ToArray(); BlendValues = modeBox.SelectedIndex == 1;
        VerticalSmoothCells = verticalCells; HorizontalSmoothCells = horizontalCells;
        if (applyAction is null) DialogResult = true; else applyAction(this);
    }
}
