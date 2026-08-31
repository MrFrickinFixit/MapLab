using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class CustomUnitManagerWindow : Window
{
    private readonly TextBox unitBox;
    private readonly ListBox unitList;
    private readonly Action<string> addUnit;
    private readonly Action<string> removeUnit;

    public CustomUnitManagerWindow(IEnumerable<string> units, Action<string> addUnit, Action<string> removeUnit, string axisName = "Y Axis")
    {
        this.addUnit = addUnit; this.removeUnit = removeUnit;
        Title = $"Custom {axisName} Units"; Width = 440; Height = 390; MinWidth = 400; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(243, 243, 243)); FontFamily = new FontFamily("Segoe UI");
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = $"CUSTOM {axisName.ToUpperInvariant()} UNITS", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });
        var addRow = new Grid { Margin = new Thickness(0, 0, 0, 12) }; addRow.ColumnDefinitions.Add(new ColumnDefinition()); addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        unitBox = new TextBox { Padding = new Thickness(9, 7, 9, 7), Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), MaxLength = 24 };
        unitBox.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Add(); e.Handled = true; } }; addRow.Children.Add(unitBox);
        var add = Button("Add", true); add.Margin = new Thickness(8, 0, 0, 0); add.Click += (_, _) => Add(); Grid.SetColumn(add, 1); addRow.Children.Add(add); Grid.SetRow(addRow, 1); root.Children.Add(addRow);
        unitList = new ListBox { Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), Padding = new Thickness(5) };
        foreach (var unit in units) unitList.Items.Add(unit); Grid.SetRow(unitList, 2); root.Children.Add(unitList);
        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) }; actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var remove = Button("Remove selected", false); remove.Click += (_, _) => Remove(); actions.Children.Add(remove);
        var close = Button("Close", true); close.Click += (_, _) => Close(); Grid.SetColumn(close, 1); actions.Children.Add(close); Grid.SetRow(actions, 3); root.Children.Add(actions); Content = root;
    }

    private void Add()
    {
        var unit = unitBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(unit)) { MessageBox.Show("Enter a unit name.", "Custom unit", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (unit.Equals("kPa absolute", StringComparison.OrdinalIgnoreCase) || unit.Equals("PSI gauge", StringComparison.OrdinalIgnoreCase) || unit.Equals("RPM", StringComparison.OrdinalIgnoreCase) || unit.Equals("Unitless", StringComparison.OrdinalIgnoreCase) || unit.Equals("Custom…", StringComparison.OrdinalIgnoreCase) || unitList.Items.Cast<string>().Any(existing => existing.Equals(unit, StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show("That unit is already in the list or uses a reserved name.", "Custom unit", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        unitList.Items.Add(unit); unitList.SelectedItem = unit; unitBox.Clear(); addUnit(unit);
    }

    private void Remove()
    {
        if (unitList.SelectedItem is not string unit) return;
        unitList.Items.Remove(unit); removeUnit(unit);
    }

    private static Button Button(string text, bool primary) => new() { Content = text, MinWidth = 92, Padding = new Thickness(13, 7, 13, 7), Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), FontWeight = FontWeights.SemiBold };
}
