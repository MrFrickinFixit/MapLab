using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TimingTableCalculator;

internal static class UiInteraction
{
    public static bool IsInsideButton(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ButtonBase) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
