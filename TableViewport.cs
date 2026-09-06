using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

/// <summary>Fits the entire map in the available space, with a native-size editing mode.</summary>
public sealed class TableViewport : Grid
{
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

    private readonly CheckBox actualSize;
    private readonly WrapPanel viewOptions = new();

    internal void AddViewOption(CheckBox option)
    {
        option.FontSize = actualSize.FontSize;
        option.FontWeight = actualSize.FontWeight;
        option.Foreground = actualSize.Foreground;
        option.Margin = new Thickness(12, 4, 6, 6);
        option.VerticalAlignment = VerticalAlignment.Center;
        viewOptions.Children.Add(option);
    }

    public TableViewport()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        actualSize = new CheckBox
        {
            Content = "Actual size (scroll to edit)",
            Foreground = Brushes.White,
            Margin = new Thickness(6, 4, 6, 6),
            ToolTip = "By default the whole table fits the available space. Enable actual size for larger text and scrollbars."
        };
        actualSize.Checked += (_, _) => UpdateContent();
        actualSize.Unchecked += (_, _) => UpdateContent();
        viewOptions.Children.Add(actualSize);
        Children.Add(viewOptions);
        SetRow(fitted, 1);
        SetRow(scrolling, 1);
        Children.Add(fitted);
        Children.Add(scrolling);
    }

    private void UpdateContent()
    {
        // Detach before reparenting; the original cells and their input handlers are retained.
        fitted.Child = null;
        scrolling.Content = null;
        var nativeSize = actualSize.IsChecked == true;
        fitted.Visibility = nativeSize ? Visibility.Collapsed : Visibility.Visible;
        scrolling.Visibility = nativeSize ? Visibility.Visible : Visibility.Collapsed;
        if (nativeSize) scrolling.Content = Table;
        else fitted.Child = Table;
    }
}
