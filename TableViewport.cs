using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TimingTableCalculator;

/// <summary>Hosts a table in fit-to-window or manually zoomed scrolling mode.</summary>
public sealed class TableViewport : Grid
{
    private const double MinimumZoom = .30;
    private const double MaximumZoom = 2.50;
    private const double WheelZoomFactor = 1.10;

    private readonly Viewbox fitted = new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
    private readonly ScrollViewer scrolling = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        CanContentScroll = false,
        Visibility = Visibility.Collapsed
    };

    public static readonly DependencyProperty TableProperty = DependencyProperty.Register(
        nameof(Table), typeof(UIElement), typeof(TableViewport),
        new PropertyMetadata(null, (owner, _) => ((TableViewport)owner).UpdateContent()));

    public UIElement? Table
    {
        get => (UIElement?)GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    private readonly CheckBox manualSize;
    private readonly TextBlock zoomText;
    private readonly WrapPanel viewOptions = new();
    private readonly Thumb resizeHandle;
    private double zoom = 1;
    private bool changingMode;

    internal double Zoom => zoom;
    internal bool IsFitToWindow => manualSize.IsChecked != true;

    internal void AddViewOption(CheckBox option)
    {
        option.FontSize = manualSize.FontSize;
        option.FontWeight = manualSize.FontWeight;
        option.Foreground = manualSize.Foreground;
        option.Margin = new Thickness(12, 4, 6, 6);
        option.VerticalAlignment = VerticalAlignment.Center;
        viewOptions.Children.Add(option);
    }

    public TableViewport()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        manualSize = new CheckBox
        {
            Content = "Manual size",
            Foreground = Brushes.White,
            Margin = new Thickness(6, 4, 6, 6),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Enable native-size editing. Ctrl+mouse wheel or the lower-right resize grip sets a custom zoom."
        };
        manualSize.Checked += (_, _) =>
        {
            if (changingMode) return;
            zoom = 1;
            UpdateContent();
        };
        manualSize.Unchecked += (_, _) =>
        {
            if (!changingMode) UpdateContent();
        };
        zoomText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(190, 205, 220)),
            FontSize = 11,
            Margin = new Thickness(2, 4, 6, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Fit to window",
            ToolTip = "Ctrl+mouse wheel over the table to zoom. Normal wheel scrolling remains available."
        };
        viewOptions.Children.Add(manualSize);
        viewOptions.Children.Add(zoomText);
        Children.Add(viewOptions);
        SetRow(fitted, 1);
        SetRow(scrolling, 1);
        Children.Add(fitted);
        Children.Add(scrolling);

        resizeHandle = new Thumb
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 3, 3),
            Cursor = Cursors.SizeNWSE,
            Background = new SolidColorBrush(Color.FromRgb(0, 103, 192)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Opacity = .9,
            ToolTip = "Drag to resize the table"
        };
        resizeHandle.DragStarted += (_, _) => BeginManualResize();
        resizeHandle.DragDelta += (_, e) => ResizeBy(e.HorizontalChange, e.VerticalChange);
        SetRow(resizeHandle, 1);
        Children.Add(resizeHandle);
        PreviewMouseWheel += TableViewport_PreviewMouseWheel;
    }

    private void TableViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || Table is null) return;
        var startingZoom = manualSize.IsChecked == true ? zoom : GetFitZoom();
        SetManualZoom(startingZoom * (e.Delta > 0 ? WheelZoomFactor : 1 / WheelZoomFactor));
        e.Handled = true;
    }

    private void BeginManualResize()
    {
        if (manualSize.IsChecked != true) SetManualZoom(GetFitZoom());
    }

    private void ResizeBy(double horizontalChange, double verticalChange)
    {
        if (Table is null) return;
        var width = Math.Max(1, Table.RenderSize.Width);
        var height = Math.Max(1, Table.RenderSize.Height);
        var horizontalScale = horizontalChange / width;
        var verticalScale = verticalChange / height;
        var change = Math.Abs(horizontalScale) >= Math.Abs(verticalScale) ? horizontalScale : verticalScale;
        SetManualZoom(zoom + change);
    }

    private double GetFitZoom()
    {
        if (Table is null) return 1;
        var width = Math.Max(1, Table.RenderSize.Width);
        var height = Math.Max(1, Table.RenderSize.Height);
        var availableWidth = Math.Max(1, ActualWidth);
        var availableHeight = Math.Max(1, ActualHeight - viewOptions.ActualHeight);
        return Math.Clamp(Math.Min(1, Math.Min(availableWidth / width, availableHeight / height)), MinimumZoom, MaximumZoom);
    }

    internal void SetManualZoom(double value)
    {
        zoom = Math.Clamp(value, MinimumZoom, MaximumZoom);
        changingMode = true;
        manualSize.IsChecked = true;
        changingMode = false;
        UpdateContent();
    }

    private void UpdateContent()
    {
        // Detach before reparenting; the original cells and their input handlers are retained.
        fitted.Child = null;
        scrolling.Content = null;
        if (Table is null) return;
        var manual = manualSize.IsChecked == true;
        fitted.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        scrolling.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        if (manual)
        {
            if (Table is FrameworkElement tableElement) tableElement.LayoutTransform = new ScaleTransform(zoom, zoom);
            scrolling.Content = Table;
            zoomText.Text = $"Zoom {zoom:P0}";
        }
        else
        {
            if (Table is FrameworkElement tableElement) tableElement.LayoutTransform = Transform.Identity;
            fitted.Child = Table;
            zoomText.Text = "Fit to window";
        }
    }
}
