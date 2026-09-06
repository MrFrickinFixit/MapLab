using System.Windows;
using System.Windows.Controls;

namespace TimingTableCalculator;

internal static class CompactTableHeading
{
    // Keep descriptions beside the title, wrapping only when the window needs it.
    internal static void Align(WrapPanel heading)
    {
        heading.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (FrameworkElement item in heading.Children)
        {
            item.VerticalAlignment = VerticalAlignment.Center;
            item.Margin = new Thickness(16, 2, 0, 2);
            if (item is TextBlock text)
            {
                if (text.FontSize > 20) text.FontSize = 22;
                text.TextWrapping = TextWrapping.Wrap;
                text.TextAlignment = TextAlignment.Right;
            }
        }
    }
}
