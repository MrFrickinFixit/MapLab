using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TimingTableCalculator;

internal static class UiInteraction
{
    public static bool IsInsideButton(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ButtonBase) return true;
            current = ParentOf(current);
        }
        return false;
    }

    public static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = ParentOf(current);
        }
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject current)
    {
        if (current is ContentElement content)
            return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;
        if (current is Visual or Visual3D) return VisualTreeHelper.GetParent(current);
        return LogicalTreeHelper.GetParent(current);
    }
}
