using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public enum BoostRescaleMode
{
    GenerateBoostedRows,
    ProportionalStretch,
    FlatFill
}

public sealed record BoostConversionResult(double MaxBoostPsi, BoostRescaleMode Mode);

public sealed class BoostConversionWindow : Window
{
    private readonly List<MapSensorProfile> customSensors;
    private readonly ComboBox sensorBox;
    private readonly TextBox customPsiBox;
    private readonly TextBox saveAsNameBox;
    private readonly RadioButton generateOption, stretchOption, flatOption;
    private readonly Action<BoostConversionWindow> applyAction;
    private readonly double currentMinMap;
    private readonly double currentMaxMap;
    private readonly bool currentIsPsi;

    public BoostConversionResult? Result { get; private set; }

    public BoostConversionWindow(string title, double currentMinMap, double currentMaxMap, bool currentIsPsi, Action<BoostConversionWindow> applyAction)
    {
        this.applyAction = applyAction; this.currentMinMap = currentMinMap; this.currentMaxMap = currentMaxMap; this.currentIsPsi = currentIsPsi;
        customSensors = MapSensorLibrary.LoadCustomSensors();
        Title = title; Width = 480; Height = 560; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38)); FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(22) };
        for (var i = 0; i < 7; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        root.Children.Add(Heading("CONVERT TO BOOSTED / FORCED INDUCTION", 0));

        var currentMapText = currentIsPsi ? $"{currentMinMap:0.0}–{currentMaxMap:0.0} PSI gauge" : $"{currentMinMap:0}–{currentMaxMap:0} kPa absolute";
        var info = new TextBlock { Text = $"Current MAP range: {currentMapText}", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, Margin = new Thickness(0, 0, 0, 14) };
        Grid.SetRow(info, 1); root.Children.Add(info);

        root.Children.Add(Label("MAP SENSOR", 2));
        sensorBox = new ComboBox { Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 0, 10) };
        foreach (var preset in MapSensorLibrary.Presets) sensorBox.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset, Foreground = Brushes.Black });
        foreach (var custom in customSensors) sensorBox.Items.Add(new ComboBoxItem { Content = custom.Name, Tag = custom, Foreground = Brushes.Black });
        sensorBox.Items.Add(new ComboBoxItem { Content = "Custom…", Tag = null, Foreground = Brushes.Black });
        sensorBox.SelectedIndex = 0;
        Grid.SetRow(sensorBox, 3); root.Children.Add(sensorBox);

        var customPanel = new Grid { Margin = new Thickness(0, 0, 0, 14) }; customPanel.ColumnDefinitions.Add(new ColumnDefinition()); customPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); customPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        customPanel.Children.Add(new TextBlock { Text = "MAXIMUM BOOST (PSI GAUGE)", Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        customPsiBox = new TextBox { Text = "20.0", Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(9, 6, 9, 6), TextAlignment = TextAlignment.Right };
        Grid.SetColumn(customPsiBox, 1); customPanel.Children.Add(customPsiBox);
        var stepDown = new Button { Content = "−0.5", Width = 46, Margin = new Thickness(6, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        stepDown.Click += (_, _) => StepCustomPsi(-.5);
        var stepUp = new Button { Content = "+0.5", Width = 46, Margin = new Thickness(4, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        stepUp.Click += (_, _) => StepCustomPsi(.5);
        var steppers = new StackPanel { Orientation = Orientation.Horizontal }; steppers.Children.Add(stepDown); steppers.Children.Add(stepUp);
        Grid.SetColumn(steppers, 2); customPanel.Children.Add(steppers);
        Grid.SetRow(customPanel, 4); root.Children.Add(customPanel);

        var saveAsPanel = new Grid { Margin = new Thickness(0, 0, 0, 14) }; saveAsPanel.ColumnDefinitions.Add(new ColumnDefinition()); saveAsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        saveAsNameBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(13, 20, 31)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 82)), Padding = new Thickness(9, 6, 9, 6) };
        Panel.SetZIndex(saveAsNameBox, 1);
        var saveAsButton = new Button { Content = "Save sensor as…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 6, 10, 6), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        saveAsButton.Click += SaveCustomSensor_Click;
        saveAsPanel.Children.Add(saveAsNameBox); Grid.SetColumn(saveAsButton, 1); saveAsPanel.Children.Add(saveAsButton);
        Grid.SetRow(saveAsPanel, 5); root.Children.Add(saveAsPanel);

        var modeGroup = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        modeGroup.Children.Add(new TextBlock { Text = "NEW BOOST ROW DATA", Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        generateOption = ModeOption(modeGroup, "Generate boosted values from targets (recommended)", true);
        stretchOption = ModeOption(modeGroup, "Proportional stretch — keep existing values, only widen the MAP axis", false);
        flatOption = ModeOption(modeGroup, "Flat fill — repeat the top existing row for manual editing", false);
        Grid.SetRow(modeGroup, 6); root.Children.Add(modeGroup);

        var bottom = new Grid { Margin = new Thickness(0, 16, 0, 0) }; bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(new TextBlock { Text = "Converts the MAP scale to PSI gauge and extends it up to the sensor's maximum boost.", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var apply = new Button { Content = "Convert", Width = 92, Height = 34, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Margin = new Thickness(8, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Width = 82, Height = 34, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White }; cancel.Click += (_, _) => Close();
        apply.Click += Apply_Click; actions.Children.Add(apply); actions.Children.Add(cancel); Grid.SetColumn(actions, 1); bottom.Children.Add(actions);
        Grid.SetRow(bottom, 7); root.Children.Add(bottom);
        Content = root;

        sensorBox.SelectionChanged += (_, _) => customPsiBox.IsEnabled = sensorBox.SelectedItem is ComboBoxItem { Tag: null };
        customPsiBox.IsEnabled = false;
    }

    private static TextBlock Heading(string text, int row)
    {
        var block = new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontWeight = FontWeights.Bold, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(block, row); return block;
    }

    private static TextBlock Label(string text, int row)
    {
        var block = new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(block, row); return block;
    }

    private static RadioButton ModeOption(StackPanel parent, string text, bool isChecked)
    {
        var radio = new RadioButton { Content = text, GroupName = "BoostRescaleMode", IsChecked = isChecked, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
        parent.Children.Add(radio); return radio;
    }

    private void StepCustomPsi(double delta)
    {
        if (!double.TryParse(customPsiBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var current)) current = 20.0;
        customPsiBox.Text = MapSensorLibrary.RoundToHalfPsi(current + delta).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private bool TryGetSelectedMaxBoostPsi(out double maxBoostPsi)
    {
        if (sensorBox.SelectedItem is ComboBoxItem { Tag: MapSensorProfile profile })
        {
            maxBoostPsi = profile.MaxBoostPsi; return true;
        }
        if (double.TryParse(customPsiBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out maxBoostPsi) && maxBoostPsi > 0)
        {
            maxBoostPsi = MapSensorLibrary.RoundToHalfPsi(maxBoostPsi); return true;
        }
        maxBoostPsi = 0; return false;
    }

    private void SaveCustomSensor_Click(object sender, RoutedEventArgs e)
    {
        var name = saveAsNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("Enter a name for the custom sensor.", "Save sensor", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!TryGetSelectedMaxBoostPsi(out var maxBoostPsi))
        { MessageBox.Show("Enter a valid maximum boost PSI value first.", "Save sensor", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var profile = new MapSensorProfile(name, maxBoostPsi);
        customSensors.RemoveAll(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        customSensors.Add(profile); MapSensorLibrary.SaveCustomSensors(customSensors);
        sensorBox.Items.Insert(sensorBox.Items.Count - 1, new ComboBoxItem { Content = profile.Name, Tag = profile, Foreground = Brushes.Black });
        sensorBox.SelectedIndex = sensorBox.Items.Count - 2; saveAsNameBox.Clear();
        MessageBox.Show($"Saved custom MAP sensor \"{name}\" ({maxBoostPsi:0.0} psi).", "Save sensor", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedMaxBoostPsi(out var maxBoostPsi))
        { MessageBox.Show("Select a MAP sensor or enter a valid custom maximum boost PSI value.", "Check MAP sensor", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var currentMaxPsi = currentIsPsi ? currentMaxMap : (currentMaxMap - 101.325) / 6.894757293168361;
        if (maxBoostPsi <= currentMaxPsi)
        { MessageBox.Show("The MAP sensor's maximum boost must be greater than the table's current maximum MAP.", "Check MAP sensor", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var mode = generateOption.IsChecked == true ? BoostRescaleMode.GenerateBoostedRows : stretchOption.IsChecked == true ? BoostRescaleMode.ProportionalStretch : BoostRescaleMode.FlatFill;
        Result = new BoostConversionResult(maxBoostPsi, mode);
        applyAction(this);
    }
}
