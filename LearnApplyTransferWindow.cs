using System.Windows;
using System.Windows.Controls;

namespace TimingTableCalculator;

public sealed class LearnApplyTransferWindow : Window
{
    private readonly RadioButton smooth;
    public bool Smooth => smooth.IsChecked == true;

    public LearnApplyTransferWindow(int count, int unmatched)
    {
        Title = "Transfer Learn Offsets to Fueling"; Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = $"Transfer {count} nonzero VE offsets?", FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
        root.Children.Add(new TextBlock { Text = "New VE = current VE x (1 + offset / 100)\nExample: 80 VE with +10% becomes 88 VE.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var only = new RadioButton { Content = "Transfer only", IsChecked = true, GroupName = "TransferMode", Margin = new Thickness(0, 0, 0, 12), ToolTip = "Apply each offset to the underlying VE value. Zero-offset cells stay unchanged, even when Fueling displays lb/hr." };
        smooth = new RadioButton { Content = "Transfer and smooth changed cells", GroupName = "TransferMode", Margin = new Thickness(0, 0, 0, 16), ToolTip = "After transfer, use Smooth to Surroundings on nonzero-offset cells: both directions, reach 2, strength 65%, 2 passes. Other cells stay fixed." };
        root.Children.Add(only); root.Children.Add(smooth);
        if (unmatched > 0) root.Children.Add(new TextBlock { Text = $"{unmatched} retained offsets do not match the current axes and will not transfer.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 10, 0) };
        var apply = new Button { Content = "Transfer", IsDefault = true, MinWidth = 100, Padding = new Thickness(12, 7, 12, 7) };
        apply.Click += (_, _) => DialogResult = true; buttons.Children.Add(cancel); buttons.Children.Add(apply); root.Children.Add(buttons); Content = root;
    }
}
