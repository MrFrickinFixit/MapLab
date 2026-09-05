using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TimingTableCalculator;

public sealed class AboutPanel : Grid
{
    private const string SupportUrl = "https://www.paypal.com/paypalme/bdiffenbaugh";
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(0, 103, 192));
    private static readonly SolidColorBrush Text = new(Color.FromRgb(32, 32, 32));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(94, 94, 94));

    public AboutPanel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimingTableCalculator");
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var content = new StackPanel { MaxWidth = 920, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(20, 18, 20, 30) }; root.Content = content; Children.Add(root);

        var identity = new Grid { Margin = new Thickness(0, 0, 0, 24) }; identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) }); identity.ColumnDefinitions.Add(new ColumnDefinition());
        var iconFrame = new Border { Width = 106, Height = 106, Background = new SolidColorBrush(Color.FromRgb(9, 16, 25)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 50, 71)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        try { iconFrame.Child = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/MapLab;component/Assets/MapLabIcon.png", UriKind.Absolute)), Stretch = Stretch.Uniform }; } catch { iconFrame.Child = FallbackIcon(); }
        identity.Children.Add(iconFrame);
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(new TextBlock { Text = "MAP LAB", Foreground = Accent, FontSize = 12, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = "Map Lab", Foreground = Text, FontSize = 34, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) });
        heading.Children.Add(new TextBlock { Text = $"Version {version}  •  64-bit Windows desktop application", Foreground = Muted, FontSize = 13, Margin = new Thickness(0, 5, 0, 0) });
        heading.Children.Add(new TextBlock { Text = "Engine map creation, refinement, visualization, and exchange.", Foreground = Text, FontSize = 15, Margin = new Thickness(0, 9, 0, 0) }); Grid.SetColumn(heading, 1); identity.Children.Add(heading); content.Children.Add(identity);

        content.Children.Add(Card("ABOUT MAP LAB", Paragraph("Map Lab is a Windows-based editor for ignition timing, volumetric-efficiency, and custom numeric maps. It combines spreadsheet-style cell editing with configurable axes, selection-aware smoothing, operating-region setup, heat-map visualization, and an interactive 3D surface viewer.")));
        var features = new UniformGrid { Columns = 2 };
        features.Children.Add(Feature("IGNITION TIMING", "Independent MAP scale, timing regions, boost adjustment, smoothing, and 3D editing."));
        features.Children.Add(Feature("FUELING", "VE editing, VE Setup Wizard, optional lb/hr display, independent MAP scale, exports, and 3D editing."));
        features.Children.Add(Feature("MAP SANDBOX", "Boundary-free custom tables with independent dimensions, axes, custom units, smoothing, history, and exports."));
        features.Children.Add(Feature("DATA EXCHANGE", "Clipboard-compatible tables and axes, CSV and Excel export, heat-map formatting, and autosave recovery."));
        content.Children.Add(Card("CORE CAPABILITIES", features));

        var details = new Grid(); details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); details.ColumnDefinitions.Add(new ColumnDefinition());
        AddDetail(details, 0, "Application version", version); AddDetail(details, 1, "Runtime", RuntimeInformation.FrameworkDescription); AddDetail(details, 2, "Operating system", RuntimeInformation.OSDescription); AddDetail(details, 3, "Process architecture", RuntimeInformation.ProcessArchitecture.ToString()); AddDetail(details, 4, "Autosave location", dataPath); content.Children.Add(Card("SYSTEM & STORAGE", details));

        var copy = Button("Copy system information"); copy.Margin = new Thickness(0, 16, 0, 0); copy.HorizontalAlignment = HorizontalAlignment.Left;
        copy.Click += (_, _) => { var info = $"Map Lab {version}{Environment.NewLine}{RuntimeInformation.FrameworkDescription}{Environment.NewLine}{RuntimeInformation.OSDescription}{Environment.NewLine}Architecture: {RuntimeInformation.ProcessArchitecture}{Environment.NewLine}Autosave: {dataPath}"; try { Clipboard.SetText(info); copy.Content = "Copied"; } catch { copy.Content = "Copy failed"; } }; content.Children.Add(copy);

        var support = new StackPanel();
        support.Children.Add(Paragraph("Map Lab is provided free of charge. If it has been useful to you, you may optionally support its continued development. Contributions are voluntary, are not purchases, do not unlock features or change the license, and do not guarantee support, calibration advice, or future development. They are not tax-deductible charitable donations."));
        var supportButton = Button("Support Map Lab with PayPal");
        supportButton.Margin = new Thickness(0, 14, 0, 0);
        supportButton.HorizontalAlignment = HorizontalAlignment.Left;
        supportButton.ToolTip = "Open the Map Lab PayPal support page in your default browser";
        supportButton.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(SupportUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"Map Lab could not open the support page.\n\n{ex.Message}", "Support Map Lab", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        support.Children.Add(supportButton);
        content.Children.Add(Card("SUPPORT MAP LAB", support));

        content.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 190, 92)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(16), Margin = new Thickness(0, 24, 0, 0), Child = new TextBlock { Text = "Calibration safety: Engine calibration involves inherent risk. Map Lab is a calculation and visualization tool. Verify all values independently and use appropriate safeguards before applying changes to an engine or vehicle.", Foreground = new SolidColorBrush(Color.FromRgb(82, 62, 14)), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, LineHeight = 20 } });
        content.Children.Add(new TextBlock { Text = "Built with .NET and Windows Presentation Foundation.", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) });
    }

    private static Border Card(string title, UIElement body) { var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = title, Foreground = Accent, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 11) }); stack.Children.Add(body); return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12), Child = stack }; }
    private static TextBlock Paragraph(string text) => new() { Text = text, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
    private static Border Feature(string title, string text) { var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) }); stack.Children.Add(new TextBlock { Text = text, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 18 }); return new Border { Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(224, 229, 235)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(14), Margin = new Thickness(0, 0, 10, 10), Child = stack }; }
    private static void AddDetail(Grid grid, int row, string name, string value) { grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var label = new TextBlock { Text = name, Foreground = Muted, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 12, 7) }; var detail = new TextBlock { Text = value, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 7) }; Grid.SetRow(label, row); Grid.SetRow(detail, row); Grid.SetColumn(detail, 1); grid.Children.Add(label); grid.Children.Add(detail); }
    private static Button Button(string text) => new() { Content = text, Padding = new Thickness(14, 8, 14, 8), Background = Accent, Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0, 90, 170)), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold };
    private static UIElement FallbackIcon() { var grid = new UniformGrid { Rows = 4, Columns = 4 }; var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.DodgerBlue, Colors.Blue, Colors.Magenta }; for (var i = 0; i < 16; i++) grid.Children.Add(new Border { Background = new SolidColorBrush(colors[i * (colors.Length - 1) / 15]), BorderBrush = new SolidColorBrush(Color.FromRgb(9, 16, 25)), BorderThickness = new Thickness(1) }); return grid; }
}
