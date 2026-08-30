using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class ColorCustomizerWindow : Window
{
    private readonly CheckBox enabledBox;
    private readonly Border preview;
    private readonly Button lowButton;
    private readonly Button highButton;
    private readonly Action<ColorCustomizerWindow>? applyAction;

    public bool UseCustomColors => enabledBox.IsChecked == true;
    public Color LowColor { get; private set; }
    public Color HighColor { get; private set; }

    public ColorCustomizerWindow(bool enabled, Color lowColor, Color highColor, Action<ColorCustomizerWindow>? applyAction = null)
    {
        this.applyAction = applyAction;
        Title = "Color Customizer"; Width = 390; Height = 225; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        LowColor = lowColor; HighColor = highColor;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        enabledBox = new CheckBox { Content = "Use Custom Colors", IsChecked = enabled, Foreground = Brushes.White, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        enabledBox.Checked += (_, _) => UpdatePreview(); enabledBox.Unchecked += (_, _) => UpdatePreview(); root.Children.Add(enabledBox);

        preview = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(65, 82, 105)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 10, 0, 8) };
        Grid.SetRow(preview, 1); root.Children.Add(preview);

        var colorButtons = new Grid(); colorButtons.ColumnDefinitions.Add(new ColumnDefinition()); colorButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); colorButtons.ColumnDefinitions.Add(new ColumnDefinition());
        lowButton = MakeColorButton("Set Low Color", () => PickColor(true)); highButton = MakeColorButton("Set High Color", () => PickColor(false));
        colorButtons.Children.Add(lowButton); Grid.SetColumn(highButton, 2); colorButtons.Children.Add(highButton); Grid.SetRow(colorButtons, 2); root.Children.Add(colorButtons);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
        var ok = new Button { Content = "Apply", Width = 90, Height = 32, Margin = new Thickness(0, 16, 10, 0), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold };
        var cancel = new Button { Content = "Close", Width = 90, Height = 32, Margin = new Thickness(0, 16, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(55, 72, 94)) }; cancel.Click += (_, _) => Close();
        ok.Click += (_, _) => { if (applyAction is null) DialogResult = true; else applyAction(this); }; actions.Children.Add(ok); actions.Children.Add(cancel); Grid.SetRow(actions, 3); root.Children.Add(actions);
        Content = root; UpdatePreview();
    }

    private static Button MakeColorButton(string text, Action click)
    {
        var button = new Button { Content = text, Height = 31, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, BorderThickness = new Thickness(2), FontSize = 12 };
        button.Click += (_, _) => click(); return button;
    }

    private void PickColor(bool low)
    {
        var current = low ? LowColor : HighColor;
        var picker = new RgbColorPickerWindow(current) { Owner = this };
        if (picker.ShowDialog() != true) return;
        if (low) LowColor = picker.SelectedColor; else HighColor = picker.SelectedColor; UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (preview is null) return;
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        if (enabledBox.IsChecked == true)
        {
            gradient.GradientStops.Add(new GradientStop(LowColor, 0)); gradient.GradientStops.Add(new GradientStop(HighColor, 1));
        }
        else
        {
            for (var i = 0; i <= 6; i++) gradient.GradientStops.Add(new GradientStop(HslToColor(i * 50, .96, .52), i / 6d));
        }
        preview.Background = gradient; lowButton.BorderBrush = new SolidColorBrush(LowColor); highButton.BorderBrush = new SolidColorBrush(HighColor);
    }

    private static Color HslToColor(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}

internal sealed class RgbColorPickerWindow : Window
{
    private readonly Slider red;
    private readonly Slider green;
    private readonly Slider blue;
    private readonly Border preview;
    public Color SelectedColor => Color.FromRgb((byte)red.Value, (byte)green.Value, (byte)blue.Value);

    public RgbColorPickerWindow(Color initial)
    {
        Title = "Choose Color"; Width = 340; Height = 300; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(18, 26, 38));
        var root = new Grid { Margin = new Thickness(18) };
        for (var i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 3 ? new GridLength(55) : GridLength.Auto });
        red = AddSlider(root, 0, "Red", initial.R); green = AddSlider(root, 1, "Green", initial.G); blue = AddSlider(root, 2, "Blue", initial.B);
        preview = new Border { Height = 38, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 12, 0, 5), BorderBrush = Brushes.White, BorderThickness = new Thickness(1) };
        Grid.SetRow(preview, 3); root.Children.Add(preview);
        red.ValueChanged += (_, _) => UpdatePreview(); green.ValueChanged += (_, _) => UpdatePreview(); blue.ValueChanged += (_, _) => UpdatePreview();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var ok = new Button { Content = "OK", Width = 85, Height = 31, Margin = new Thickness(0, 8, 10, 0), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black };
        var cancel = new Button { Content = "Cancel", Width = 85, Height = 31, Margin = new Thickness(0, 8, 0, 0), IsCancel = true, Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        ok.Click += (_, _) => DialogResult = true; actions.Children.Add(ok); actions.Children.Add(cancel); Grid.SetRow(actions, 4); root.Children.Add(actions);
        Content = root; UpdatePreview();
    }

    private static Slider AddSlider(Grid root, int row, string label, byte value)
    {
        var panel = new Grid { Margin = new Thickness(0, 5, 0, 5) }; panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        var slider = new Slider { Minimum = 0, Maximum = 255, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(8, 0, 8, 0) }; Grid.SetColumn(slider, 1); panel.Children.Add(slider);
        var number = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 204)), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right }; number.Text = ((int)value).ToString(); slider.ValueChanged += (_, _) => number.Text = ((int)slider.Value).ToString(); Grid.SetColumn(number, 2); panel.Children.Add(number);
        Grid.SetRow(panel, row); root.Children.Add(panel); return slider;
    }

    private void UpdatePreview() { if (preview is not null) preview.Background = new SolidColorBrush(SelectedColor); }
}
