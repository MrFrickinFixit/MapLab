using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TimingTableCalculator;

/// <summary>An independent, boundary-free editor for arbitrary RPM/MAP tables.</summary>
public sealed class SandboxPanel : Grid
{
    private readonly Grid table = new() { Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock status = new() { Text = "Sandbox ready", Foreground = new SolidColorBrush(Color.FromRgb(169, 201, 192)), FontSize = 12 };
    private readonly TextBox xSize = Box("31", 44), ySize = Box("31", 44);
    private readonly ComboBox unitBox, xUnitBox, leadingPrecisionBox, trailingPrecisionBox;
    private TextBox[,] cells = new TextBox[0, 0];
    private TextBox[] mapEditors = [], rpmEditors = [];
    private double[,] values = new double[0, 0];
    private double[] rpm = [], map = [];
    private string mapUnit = "kPa absolute";
    private string xUnit = "RPM";
    private bool loading, selecting, axisSelecting, axisDragMap, syncingUnit;
    private int axisDragStart;
    private (int Row, int Col)? start, end;
    private readonly HashSet<(int Row, int Col)> pinned = [];
    private readonly HashSet<int> selectedMap = [], selectedRpm = [];
    private readonly Dictionary<TextBox, string> editOriginals = [];
    private readonly Dictionary<TextBox, double> axisEditOriginalValues = [];
    private readonly Stack<SandboxSnapshot> undo = [], redo = [];
    private readonly List<string> customUnits = [], customXUnits = [];
    private AdvancedSmoothingOptions smoothing = new(AdvancedSmoothingAlgorithm.StandardWeighted, .65, 2, false, true, .5);
    private double offsetAmount = 1;
    private bool offsetPercent;
    private int leadingDisplayDigits = 3, trailingDisplayDecimals = 1;
    private bool syncingDisplayPrecision;
    private static string SavePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimingTableCalculator", "sandbox-autosave.json");
    private bool IsPsiUnit => mapUnit.Equals("PSI gauge", StringComparison.OrdinalIgnoreCase);
    private bool IsKpaUnit => mapUnit.Equals("kPa absolute", StringComparison.OrdinalIgnoreCase);
    private double MapIncrement => IsPsiUnit ? .1 : IsKpaUnit ? 1 : .001;
    private string MapFormat => IsPsiUnit ? "0.0" : IsKpaUnit ? "0" : "0.###";
    private string YAxisTitle => mapUnit.Equals("Unitless", StringComparison.OrdinalIgnoreCase) ? "Y AXIS" : $"Y AXIS ({mapUnit})";
    private double XIncrement => xUnit.Equals("RPM", StringComparison.OrdinalIgnoreCase) ? 1 : .001;
    private string XFormat => xUnit.Equals("RPM", StringComparison.OrdinalIgnoreCase) ? "0" : "0.###";
    private static string FormatExactAxisValue(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private string XAxisTitle => xUnit.Equals("Unitless", StringComparison.OrdinalIgnoreCase) ? "X AXIS" : $"X AXIS ({xUnit})";
    private string FormatDisplayValue(double value) => MagnitudeNumberFormatter.Format(value, leadingDisplayDigits, trailingDisplayDecimals);
    private static string FormatEditableValue(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    public SandboxPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());

        unitBox = new ComboBox { Width = 112, Height = 32, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(6, 3, 6, 3), SelectedIndex = 0 };
        RefreshUnitItems();
        unitBox.SelectionChanged += (_, _) => { if (!syncingUnit) UnitSelectionChanged(); };
        xUnitBox = new ComboBox { Width = 112, Height = 32, Background = Brushes.White, Foreground = Brushes.Black, Padding = new Thickness(6, 3, 6, 3), SelectedIndex = 0 };
        RefreshXUnitItems();
        xUnitBox.SelectionChanged += (_, _) => { if (!syncingUnit) XUnitSelectionChanged(); };
        leadingPrecisionBox = PrecisionBox(1, 4, leadingDisplayDigits);
        trailingPrecisionBox = PrecisionBox(0, 3, trailingDisplayDecimals);
        leadingPrecisionBox.SelectionChanged += DisplayPrecisionChanged;
        trailingPrecisionBox.SelectionChanged += DisplayPrecisionChanged;

        var heading = new Grid { Margin = new Thickness(4, 0, 0, 20) };
        heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "MAP LAB", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold });
        title.Children.Add(new TextBlock { Text = "Map Sandbox", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 25, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "Build and reshape custom tables without operating-region boundaries.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        heading.Children.Add(title);
        var badge = new Border { Background = new SolidColorBrush(Color.FromRgb(17, 29, 39)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 64, 53)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 8, 14, 8), VerticalAlignment = VerticalAlignment.Center, Child = status };
        Grid.SetColumn(badge, 1); heading.Children.Add(badge); Children.Add(heading);

        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        var matrix = new StackPanel { Orientation = Orientation.Horizontal };
        matrix.Children.Add(Label("X")); matrix.Children.Add(xSize); matrix.Children.Add(Label("Y")); matrix.Children.Add(ySize); matrix.Children.Add(Button("▦  Resize", Resize, true));
        tools.Children.Add(Group("MATRIX & AXES SETUP", matrix));
        var units = new StackPanel { Orientation = Orientation.Horizontal }; units.Children.Add(unitBox);
        tools.Children.Add(Group("Y AXIS UNITS", units));
        var xUnits = new StackPanel { Orientation = Orientation.Horizontal }; xUnits.Children.Add(xUnitBox);
        tools.Children.Add(Group("X AXIS UNITS", xUnits));
        tools.Children.Add(Group("CELL EDITING", Button("⧉  Copy", (_, _) => Copy()), Button("▣  Paste", (_, _) => Paste()), Button("×  Clear", Clear)));
        tools.Children.Add(Group("SMOOTHING", Button("⌁  Interpolate", Interpolate), Button("⚙  Smooth Selected…", AdvancedSmooth, true), Button("↕  Columns", SmoothColumns), Button("↔  Rows", SmoothRows)));
        tools.Children.Add(Group("NUMBER DISPLAY", Label("LEADING DIGITS"), leadingPrecisionBox, Label("TRAILING DECIMALS"), trailingPrecisionBox));
        tools.Children.Add(Group("VIEW & OUTPUT", Button("▦  3D Map", View3D), Button("⇩  Export CSV", ExportCsv), Button("▤  Export Excel", ExportExcel, true)));
        tools.Children.Add(Group("HISTORY", Button("↶  Undo", (_, _) => Undo()), Button("↷  Redo", (_, _) => Redo())));
        var commandBar = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = tools, Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(commandBar, 1); Children.Add(commandBar);

        var frame = new Border { Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 50, 71)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false, Content = table } };
        Grid.SetRow(frame, 2); Children.Add(frame);
        PreviewKeyDown += SandboxKeyDown;
        table.PreviewMouseLeftButtonUp += (_, _) => { selecting = false; axisSelecting = false; };
        if (!Load()) Initialize(31, 31); else Build();
    }

    private void Initialize(int rows, int cols)
    {
        rpm = BuildAxis(500, 7000, cols, false, 1) ?? Even(500, 7000, cols);
        map = BuildAxis(20, 100, rows, true, 1) ?? Even(100, 20, rows);
        values = new double[rows, cols];
        for (var r = 0; r < rows; r++) for (var c = 0; c < cols; c++) values[r, c] = 10 + 40d * r / Math.Max(1, rows - 1) + 12d * c / Math.Max(1, cols - 1);
        Build(); Save();
    }

    private void Build()
    {
        loading = true; axisEditOriginalValues.Clear(); table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
        cells = new TextBox[map.Length, rpm.Length]; mapEditors = new TextBox[map.Length]; rpmEditors = new TextBox[rpm.Length];
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var c = 0; c < rpm.Length; c++) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var r = 0; r < map.Length; r++) table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) }); table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        var mapTitle = new TextBlock { Text = YAxisTitle, Foreground = Brushes.White, FontWeight = FontWeights.Bold, LayoutTransform = new RotateTransform(-90), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRowSpan(mapTitle, map.Length); table.Children.Add(mapTitle);
        for (var r = 0; r < map.Length; r++)
        {
            AddAxis(map[r], r, 1, true, r);
            for (var c = 0; c < rpm.Length; c++)
            {
                var cell = new TextBox { Tag = (r, c), Text = FormatDisplayValue(values[r, c]), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 57)), BorderThickness = new Thickness(.5), Padding = new Thickness(1) };
                cell.PreviewMouseLeftButtonDown += CellDown; cell.MouseEnter += CellEnter; cell.PreviewMouseRightButtonDown += CellRight;
                cell.GotKeyboardFocus += (_, _) => { var point = ((int Row, int Col))cell.Tag; cell.Text = FormatEditableValue(values[point.Row, point.Col]); editOriginals[cell] = cell.Text; cell.SelectAll(); };
                cell.LostKeyboardFocus += (_, _) => CommitCell(cell); cell.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitCell(cell); Keyboard.ClearFocus(); e.Handled = true; } };
                cell.ContextMenu = CellMenu(); cells[r, c] = cell; Grid.SetRow(cell, r); Grid.SetColumn(cell, c + 2); table.Children.Add(cell);
            }
        }
        for (var c = 0; c < rpm.Length; c++) AddAxis(rpm[c], map.Length, c + 2, false, c);
        var rpmTitle = new TextBlock { Text = XAxisTitle, Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(rpmTitle, map.Length + 1); Grid.SetColumn(rpmTitle, 2); Grid.SetColumnSpan(rpmTitle, rpm.Length); table.Children.Add(rpmTitle);
        loading = false; Refresh(); UpdateSelection();
    }

    private void AddAxis(double value, int row, int column, bool isMap, int index)
    {
        var editor = new TextBox { Tag = (isMap, index), Text = FormatExactAxisValue(value), Foreground = new SolidColorBrush(Color.FromRgb(127, 227, 208)), Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51)), BorderBrush = new SolidColorBrush(Color.FromRgb(38, 58, 76)), BorderThickness = new Thickness(.5), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = isMap ? 11 : 10, FontWeight = FontWeights.Bold, Padding = new Thickness(2) };
        editor.PreviewMouseLeftButtonDown += AxisDown; editor.MouseEnter += AxisEnter;
        editor.GotKeyboardFocus += (_, _) => { ClearCellSelection(); var current = isMap ? map[index] : rpm[index]; axisEditOriginalValues[editor] = current; editor.Text = FormatExactAxisValue(current); editor.SelectAll(); };
        editor.LostKeyboardFocus += (_, _) => CommitAxis(editor); editor.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitAxis(editor); Keyboard.ClearFocus(); e.Handled = true; } };
        var menu = new ContextMenu(); menu.Items.Add(Item("Paste axis values", (_, _) => PasteAxis(isMap, index))); menu.Items.Add(Item("Auto-fill selected axis values", (_, _) => AutoFillAxis(isMap))); editor.ContextMenu = menu;
        if (isMap) mapEditors[index] = editor; else rpmEditors[index] = editor;
        Grid.SetRow(editor, row); Grid.SetColumn(editor, column); table.Children.Add(editor);
    }

    private void CellDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<int, int> p } cell) return;
        ClearAxisSelection(); var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!control && Selected().Count > 1 && IsSelected(p.Item1, p.Item2)) { selecting = false; cell.Focus(); cell.SelectAll(); e.Handled = true; return; }
        if (control) PinRectangle(); else pinned.Clear(); start = end = p; selecting = true; UpdateSelection(); cell.Focus(); e.Handled = true;
    }
    private void CellEnter(object sender, MouseEventArgs e) { if (selecting && e.LeftButton == MouseButtonState.Pressed && sender is TextBox { Tag: ValueTuple<int, int> p }) { end = p; UpdateSelection(); } }
    private void CellRight(object sender, MouseButtonEventArgs e) { if (sender is TextBox { Tag: ValueTuple<int, int> p } && !IsSelected(p.Item1, p.Item2)) { pinned.Clear(); start = end = p; UpdateSelection(); } }
    private ContextMenu CellMenu() { var menu = new ContextMenu(); menu.Items.Add(Item("Copy selected", (_, _) => Copy())); menu.Items.Add(Item("Paste", (_, _) => Paste())); menu.Items.Add(Item("Offset selection…", Offset)); menu.Items.Add(new Separator()); menu.Items.Add(Item("Interpolate selection", Interpolate)); menu.Items.Add(Item("Smooth selected…", AdvancedSmooth)); menu.Items.Add(Item("Smooth rows", SmoothRows)); menu.Items.Add(Item("Smooth columns", SmoothColumns)); menu.Items.Add(new Separator()); menu.Items.Add(Item("Clear selected", Clear)); return menu; }

    private void CommitCell(TextBox cell)
    {
        if (loading || cell.Tag is not ValueTuple<int, int> p || !double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { Refresh(); return; }
        var changed = editOriginals.Remove(cell, out var original) && original != cell.Text; if (!changed) return;
        value = Math.Round(value, 3);
        var selected = Selected(); if (!selected.Contains((p.Item1, p.Item2))) selected = [(p.Item1, p.Item2)];
        PushUndo(); foreach (var point in selected) values[point.Row, point.Col] = value; Refresh(); UpdateSelection(); Save(); status.Text = $"Set {selected.Count} sandbox cells to {FormatEditableValue(value)}";
    }

    private void AxisDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<bool, int> p }) return; ClearCellSelection();
        var selected = p.Item1 ? selectedMap : selectedRpm; var other = p.Item1 ? selectedRpm : selectedMap;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { other.Clear(); if (!selected.Add(p.Item2)) selected.Remove(p.Item2); e.Handled = true; }
        else { selected.Clear(); other.Clear(); selected.Add(p.Item2); axisSelecting = true; axisDragMap = p.Item1; axisDragStart = p.Item2; }
        UpdateAxisVisuals();
    }
    private void AxisEnter(object sender, MouseEventArgs e) { if (!axisSelecting || e.LeftButton != MouseButtonState.Pressed || sender is not TextBox { Tag: ValueTuple<bool, int> p } || p.Item1 != axisDragMap) return; var selected = axisDragMap ? selectedMap : selectedRpm; selected.Clear(); for (var i = Math.Min(axisDragStart, p.Item2); i <= Math.Max(axisDragStart, p.Item2); i++) selected.Add(i); UpdateAxisVisuals(); }

    private void CommitAxis(TextBox editor)
    {
        if (loading || editor.Tag is not ValueTuple<bool, int> p || !double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { RefreshAxisEditors(); return; }
        var currentEditors = p.Item1 ? mapEditors : rpmEditors;
        if (p.Item2 < 0 || p.Item2 >= currentEditors.Length || !ReferenceEquals(editor, currentEditors[p.Item2])) return;
        var axis = p.Item1 ? map : rpm;
        if (axisEditOriginalValues.Remove(editor, out var originalValue) && value.Equals(originalValue) && !axis[p.Item2].Equals(originalValue)) { editor.Text = FormatExactAxisValue(axis[p.Item2]); return; }
        if (value.Equals(axis[p.Item2])) { editor.Text = FormatExactAxisValue(axis[p.Item2]); return; }
        var increment = p.Item1 ? MapIncrement : XIncrement; value = Math.Round(value / increment) * increment;
        double[] updated;
        var minimumIndex = p.Item1 ? axis.Length - 1 : 0; var maximumIndex = p.Item1 ? 0 : axis.Length - 1;
        if (p.Item2 == minimumIndex || p.Item2 == maximumIndex)
        {
            var minimum = p.Item2 == minimumIndex ? value : p.Item1 ? axis[^1] : axis[0]; var maximum = p.Item2 == maximumIndex ? value : p.Item1 ? axis[0] : axis[^1];
            updated = BuildAxis(minimum, maximum, axis.Length, p.Item1, increment) ?? [];
        }
        else
        {
            var valid = p.Item1 ? value < axis[p.Item2 - 1] && value > axis[p.Item2 + 1] : value > axis[p.Item2 - 1] && value < axis[p.Item2 + 1];
            updated = valid ? axis.ToArray() : []; if (valid) updated[p.Item2] = value;
        }
        if (updated.Length == 0) { RefreshAxisEditors(); status.Text = "Axis values must remain ordered and unique"; return; }
        PushUndo(); if (p.Item1) map = updated; else rpm = updated; RefreshAxisEditors(); Save(); status.Text = $"Updated sandbox {(p.Item1 ? "MAP" : "RPM")} axis";
    }

    private void AutoFillAxis(bool isMap)
    {
        var selected = (isMap ? selectedMap : selectedRpm).OrderBy(i => i).ToArray(); if (selected.Length < 2) { Info("Select at least two axis breakpoints."); return; }
        var axis = isMap ? map : rpm; var candidate = axis.ToArray(); var filled = BuildAxis(selected.Min(i => axis[i]), selected.Max(i => axis[i]), selected.Length, isMap, isMap ? MapIncrement : XIncrement);
        if (filled is null) { Info("The selected range is too narrow."); return; }
        for (var i = 0; i < selected.Length; i++) candidate[selected[i]] = filled[i];
        if (!Ordered(candidate, isMap)) { Info("The fill would cross an unselected neighboring value."); return; }
        PushUndo(); if (isMap) map = candidate; else rpm = candidate; RefreshAxisEditors(); Save();
    }

    private void PasteAxis(bool isMap, int focused)
    {
        if (!Clipboard.ContainsText()) return; var tokens = Clipboard.GetText().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens.Any(token => !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))) { Info("The copied axis must contain only finite numbers."); return; }
        var axis = isMap ? map : rpm; var selected = (isMap ? selectedMap : selectedRpm).OrderBy(i => i).ToArray(); var targets = selected.Length > 1 && selected.Length == tokens.Length ? selected : Enumerable.Range(focused, tokens.Length).ToArray();
        if (targets.Any(i => i < 0 || i >= axis.Length)) { Info("The copied axis is too large for the available positions."); return; }
        var candidate = axis.ToArray(); for (var i = 0; i < targets.Length; i++) candidate[targets[i]] = double.Parse(tokens[i], CultureInfo.InvariantCulture);
        if (!Ordered(candidate, isMap)) { Info("Pasted axis values must remain ordered and unique."); return; }
        PushUndo(); if (isMap) map = candidate; else rpm = candidate; RefreshAxisEditors(); Save();
    }

    private void Resize(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(xSize.Text, out var cols) || !int.TryParse(ySize.Text, out var rows) || cols is < 8 or > 64 || rows is < 8 or > 64) { Info("Both matrix sizes must be between 8 and 64."); return; }
        if (rows == map.Length && cols == rpm.Length) return;
        WorkingRunner.Run(this, () => ResizeCore(cols, rows));
    }

    private void ResizeCore(int cols, int rows)
    {
        PushUndo();
        // Clear selections while the visual grid and axes still have matching dimensions.
        ClearCellSelection(); ClearAxisSelection();
        values = Resample(values, rows, cols); rpm = BuildAxis(rpm[0], rpm[^1], cols, false, XIncrement) ?? Even(rpm[0], rpm[^1], cols); map = BuildAxis(map[^1], map[0], rows, true, MapIncrement) ?? Even(map[0], map[^1], rows);
        Build(); Save(); status.Text = $"Sandbox resized to {cols} X × {rows} Y";
    }

    private void Interpolate(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || !SelectionInterpolator.CanApply(t, b, l, r)) { Info("Select at least three cells in a row or column, or at least 3 × 3 cells."); return; } PushUndo(); values = SelectionInterpolator.Apply(values, t, b, l, r); Changed($"Interpolated {r - l + 1} × {b - t + 1} cells"); }
    private void AdvancedSmooth(object? sender, RoutedEventArgs e) { var selected = Selected(); if (selected.Count == 0) { Info("Select cells to smooth."); return; } ModelessWindowManager.ShowOrActivate("Sandbox.AdvancedSmoothing", () => new AdvancedSmoothingWindow(smoothing, dialog => WorkingRunner.Run(this, () => { smoothing = dialog.Options; PushUndo(); values = AdvancedSmoother.Apply(values, selected, smoothing); Changed($"Smoothed {selected.Count} sandbox cells"); })) { Owner = Window.GetWindow(this) }); }
    private void SmoothColumns(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || b - t < 2) { Info("Select at least three rows."); return; } PushUndo(); for (var c = l; c <= r; c++) for (var row = t + 1; row < b; row++) { var x = (map[t] - map[row]) / (map[t] - map[b]); values[row, c] = values[t, c] + (values[b, c] - values[t, c]) * Ease(x); } Changed("Smoothed selected columns"); }
    private void SmoothRows(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || r - l < 2) { Info("Select at least three columns."); return; } PushUndo(); for (var row = t; row <= b; row++) for (var c = l + 1; c < r; c++) { var x = (rpm[c] - rpm[l]) / (rpm[r] - rpm[l]); values[row, c] = values[row, l] + (values[row, r] - values[row, l]) * Ease(x); } Changed("Smoothed selected rows"); }
    private void Clear(object? sender, RoutedEventArgs e) { var selected = Selected(); if (selected.Count == 0) return; PushUndo(); foreach (var p in selected) values[p.Row, p.Col] = 0; Changed($"Cleared {selected.Count} sandbox cells"); }
    private void Offset(object? sender, RoutedEventArgs e) { var selected = Selected(); if (selected.Count == 0) return; ModelessWindowManager.ShowOrActivate("Sandbox.Offset", () => new OffsetSelectionWindow(offsetAmount, offsetPercent, (direction, amount, percent) => { offsetAmount = amount; offsetPercent = percent; PushUndo(); foreach (var p in selected) values[p.Row, p.Col] = percent ? values[p.Row, p.Col] * (1 + direction * amount / 100d) : values[p.Row, p.Col] + direction * amount; Changed($"Offset {selected.Count} sandbox cells"); }) { Owner = Window.GetWindow(this) }); }

    private void View3D(object? sender, RoutedEventArgs e)
    {
        ModelessWindowManager.ShowOrActivate("Sandbox.3D", () =>
        {
            var window = new Surface3DWindow(values, rpm, map, mapUnit, false, Colors.Red, Colors.Magenta, (t, b, l, r) => { start = (t, l); end = (b, r); SmoothBasic(); return (double[,])values.Clone(); }, "3D Map Sandbox", "TABLE VALUE", Handle3D, "Y AXIS", XAxisTitle, "0.########", valueFormatter: FormatDisplayValue) { Owner = Window.GetWindow(this) };
            window.Closed += (_, _) => { ClearCellSelection(); status.Text = "3D sandbox closed  •  selection cleared"; }; return window;
        });
    }
    private void Handle3D(SurfaceSelectionAction action, int top, int bottom, int left, int right, IReadOnlyCollection<(int Row, int Col)> selected, Action<double[,]> refresh)
    {
        if (action == SurfaceSelectionAction.Undo) { Undo(); refresh((double[,])values.Clone()); return; }
        if (action == SurfaceSelectionAction.Redo) { Redo(); refresh((double[,])values.Clone()); return; }
        pinned.Clear(); foreach (var p in selected) pinned.Add(p); start = (top, left); end = (bottom, right); UpdateSelection();
        void Refresh3D() => refresh((double[,])values.Clone());
        switch (action)
        {
            case SurfaceSelectionAction.Copy: Copy(); break;
            case SurfaceSelectionAction.Paste: Paste(); Refresh3D(); break;
            case SurfaceSelectionAction.Offset: Offset(this, new RoutedEventArgs()); break;
            case SurfaceSelectionAction.Interpolate: Interpolate(this, new RoutedEventArgs()); Refresh3D(); break;
            case SurfaceSelectionAction.Smooth: SmoothBasic(); Refresh3D(); break;
            case SurfaceSelectionAction.Advanced:
                var exact = selected.ToArray(); ModelessWindowManager.ShowOrActivate("Sandbox.AdvancedSmoothing", () => new AdvancedSmoothingWindow(smoothing, dialog => WorkingRunner.Run(this, () => { smoothing = dialog.Options; PushUndo(); values = AdvancedSmoother.Apply(values, exact, smoothing); Changed($"Smoothed {exact.Length} sandbox cells"); Refresh3D(); })) { Owner = Window.GetWindow(this) }); break;
            case SurfaceSelectionAction.SmoothRows: SmoothRows(this, new RoutedEventArgs()); Refresh3D(); break;
            case SurfaceSelectionAction.SmoothColumns: SmoothColumns(this, new RoutedEventArgs()); Refresh3D(); break;
            case SurfaceSelectionAction.Clear: Clear(this, new RoutedEventArgs()); Refresh3D(); break;
        }
    }
    private void SmoothBasic() { var selected = Selected(); if (selected.Count == 0) return; PushUndo(); values = AdvancedSmoother.Apply(values, selected, new AdvancedSmoothingOptions(AdvancedSmoothingAlgorithm.StandardWeighted, .65, 2, false, true, .5)); Changed($"Smoothed {selected.Count} sandbox cells"); }

    private void Copy()
    {
        if (!Bounds(out var t, out var b, out var l, out var r)) return; var text = new StringBuilder();
        for (var row = t; row <= b; row++) { for (var c = l; c <= r; c++) { if (c > l) text.Append('\t'); text.Append(values[row, c].ToString("0.###", CultureInfo.InvariantCulture)); } if (row < b) text.AppendLine(); }
        try { Clipboard.SetText(text.ToString()); ClearCellSelection(); status.Text = "Copied sandbox selection  •  selection cleared"; }
        catch { Info("The clipboard is currently unavailable."); }
    }
    private void Paste()
    {
        if (!Clipboard.ContainsText()) return; var lines = Clipboard.GetText().Trim().Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries); if (lines.Length == 0) return;
        var parsed = lines.Select(line => line.Split(['\t', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
        if (parsed.Any(row => row.Length != parsed[0].Length) || parsed.SelectMany(row => row).Any(token => !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))) { Info("Clipboard data must be a rectangular table of numbers."); return; }
        var origin = Bounds(out var t, out _, out var l, out _) ? (Row: t, Col: l) : (Row: 0, Col: 0); if (origin.Row + parsed.Length > map.Length || origin.Col + parsed[0].Length > rpm.Length) { Info("The pasted table does not fit from the selected cell."); return; }
        PushUndo(); for (var r = 0; r < parsed.Length; r++) for (var c = 0; c < parsed[r].Length; c++) values[origin.Row + r, origin.Col + c] = Math.Round(double.Parse(parsed[r][c], CultureInfo.InvariantCulture), 3);
        ClearCellSelection(); Changed($"Pasted {parsed[0].Length} × {parsed.Length} sandbox cells");
    }

    private void ExportCsv(object? sender, RoutedEventArgs e) { var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "map-sandbox.csv" }; if (dialog.ShowDialog() != true) return; var csv = new StringBuilder(); for (var r = 0; r < map.Length; r++) { csv.Append(FormatExactAxisValue(map[r])); for (var c = 0; c < rpm.Length; c++) csv.Append(',').Append(values[r, c].ToString("0.###", CultureInfo.InvariantCulture)); csv.AppendLine(); } csv.Append(XAxisTitle); foreach (var value in rpm) csv.Append(',').Append(FormatExactAxisValue(value)); File.WriteAllText(dialog.FileName, csv.ToString()); status.Text = $"Saved {Path.GetFileName(dialog.FileName)}"; }
    private void ExportExcel(object? sender, RoutedEventArgs e) { var dialog = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "map-sandbox.xlsx" }; if (dialog.ShowDialog() != true) return; ExcelTimingExporter.Export(dialog.FileName, rpm, map, values, mapUnit, Colors.Red, Colors.Lime, Colors.Magenta, false, "Map Sandbox", "Custom Map", XAxisTitle, MagnitudeNumberFormatter.ExcelFormat(leadingDisplayDigits, trailingDisplayDecimals)); status.Text = $"Saved {Path.GetFileName(dialog.FileName)}"; }

    private void RefreshUnitItems()
    {
        syncingUnit = true; unitBox.Items.Clear();
        foreach (var unit in new[] { "kPa absolute", "PSI gauge", "Unitless" }.Concat(customUnits)) unitBox.Items.Add(new ComboBoxItem { Content = unit, Foreground = Brushes.Black });
        unitBox.Items.Add(new ComboBoxItem { Content = "Custom…", Foreground = Brushes.Black });
        var match = unitBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), mapUnit, StringComparison.OrdinalIgnoreCase));
        unitBox.SelectedItem = match ?? unitBox.Items.Cast<ComboBoxItem>().First(item => item.Content?.ToString() == "Unitless"); syncingUnit = false;
    }

    private void UnitSelectionChanged()
    {
        if (unitBox.SelectedItem is not ComboBoxItem item || item.Content is not string selected) return;
        if (selected == "Custom…") { RefreshUnitItems(); OpenCustomUnits(); return; }
        SelectUnit(selected);
    }

    private void SelectUnit(string selected)
    {
        if (map.Length == 0 || selected.Equals(mapUnit, StringComparison.OrdinalIgnoreCase)) return;
        var fromPsi = IsPsiUnit; var fromKpa = IsKpaUnit;
        var toPsi = selected.Equals("PSI gauge", StringComparison.OrdinalIgnoreCase); var toKpa = selected.Equals("kPa absolute", StringComparison.OrdinalIgnoreCase);
        PushUndo();
        // Only the known kPa/PSI pair has a defined conversion. Custom and
        // unitless choices relabel the existing numeric Y axis unchanged.
        if (fromKpa && toPsi) for (var i = 0; i < map.Length; i++) map[i] = Math.Round((map[i] - 101.325) / 6.894757293168361, 1);
        else if (fromPsi && toKpa) for (var i = 0; i < map.Length; i++) map[i] = Math.Round(map[i] * 6.894757293168361 + 101.325);
        mapUnit = selected; RefreshUnitItems(); Build(); Save();
        status.Text = fromKpa && toPsi || fromPsi && toKpa ? $"Sandbox Y axis converted to {mapUnit}" : $"Sandbox Y axis labeled {mapUnit}  •  values unchanged";
    }

    private void OpenCustomUnits()
    {
        ModelessWindowManager.ShowOrActivate("Sandbox.CustomUnits", () => new CustomUnitManagerWindow(customUnits,
            added => { customUnits.Add(added); RefreshUnitItems(); SelectUnit(added); Save(); },
            removed => { customUnits.RemoveAll(unit => unit.Equals(removed, StringComparison.OrdinalIgnoreCase)); if (mapUnit.Equals(removed, StringComparison.OrdinalIgnoreCase)) mapUnit = "Unitless"; RefreshUnitItems(); Build(); Save(); status.Text = $"Removed custom unit {removed}"; }, "Y Axis") { Owner = Window.GetWindow(this) });
    }

    private void RefreshXUnitItems()
    {
        syncingUnit = true; xUnitBox.Items.Clear();
        foreach (var unit in new[] { "RPM", "Unitless" }.Concat(customXUnits)) xUnitBox.Items.Add(new ComboBoxItem { Content = unit, Foreground = Brushes.Black });
        xUnitBox.Items.Add(new ComboBoxItem { Content = "Custom…", Foreground = Brushes.Black });
        var match = xUnitBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), xUnit, StringComparison.OrdinalIgnoreCase));
        xUnitBox.SelectedItem = match ?? xUnitBox.Items.Cast<ComboBoxItem>().First(item => item.Content?.ToString() == "Unitless"); syncingUnit = false;
    }

    private void XUnitSelectionChanged()
    {
        if (xUnitBox.SelectedItem is not ComboBoxItem item || item.Content is not string selected) return;
        if (selected == "Custom…") { RefreshXUnitItems(); OpenCustomXUnits(); return; }
        if (selected.Equals(xUnit, StringComparison.OrdinalIgnoreCase)) return;
        PushUndo(); xUnit = selected; RefreshXUnitItems(); Build(); Save(); status.Text = $"Sandbox X axis labeled {xUnit}  •  values unchanged";
    }

    private void OpenCustomXUnits()
    {
        ModelessWindowManager.ShowOrActivate("Sandbox.CustomXUnits", () => new CustomUnitManagerWindow(customXUnits,
            added => { customXUnits.Add(added); RefreshXUnitItems(); PushUndo(); xUnit = added; RefreshXUnitItems(); Build(); Save(); },
            removed => { customXUnits.RemoveAll(unit => unit.Equals(removed, StringComparison.OrdinalIgnoreCase)); if (xUnit.Equals(removed, StringComparison.OrdinalIgnoreCase)) xUnit = "Unitless"; RefreshXUnitItems(); Build(); Save(); status.Text = $"Removed custom X unit {removed}"; }, "X Axis") { Owner = Window.GetWindow(this) });
    }

    private void PushUndo() { if (loading || values.Length == 0) return; undo.Push(Snapshot()); while (undo.Count > 50) { var keep = undo.Reverse().Skip(1).ToArray(); undo.Clear(); foreach (var item in keep) undo.Push(item); } redo.Clear(); }
    private void Undo() { if (undo.Count == 0) { status.Text = "Nothing to undo"; return; } WorkingRunner.Run(this, () => { redo.Push(Snapshot()); Restore(undo.Pop()); status.Text = "Sandbox change undone"; }); }
    private void Redo() { if (redo.Count == 0) { status.Text = "Nothing to redo"; return; } WorkingRunner.Run(this, () => { undo.Push(Snapshot()); Restore(redo.Pop()); status.Text = "Sandbox change redone"; }); }
    private SandboxSnapshot Snapshot() => new(rpm.ToArray(), map.ToArray(), mapUnit, ToJagged(values), customUnits.ToArray(), xUnit, customXUnits.ToArray(), leadingDisplayDigits, trailingDisplayDecimals);
    private void Restore(SandboxSnapshot snapshot) { loading = true; rpm = snapshot.Rpm.ToArray(); map = snapshot.Map.ToArray(); mapUnit = snapshot.MapUnit; xUnit = snapshot.XUnit ?? "RPM"; values = FromJagged(snapshot.Values); customUnits.Clear(); customUnits.AddRange(snapshot.CustomUnits ?? []); customXUnits.Clear(); customXUnits.AddRange(snapshot.CustomXUnits ?? []); ApplyDisplayPrecision(snapshot.LeadingDisplayDigits, snapshot.TrailingDisplayDecimals); RefreshUnitItems(); RefreshXUnitItems(); xSize.Text = rpm.Length.ToString(); ySize.Text = map.Length.ToString(); loading = false; Build(); Save(); }

    private void Changed(string message) { Refresh(); UpdateSelection(); Save(); status.Text = message; }
    private void DisplayPrecisionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingDisplayPrecision || leadingPrecisionBox.SelectedItem is not ComboBoxItem leading || trailingPrecisionBox.SelectedItem is not ComboBoxItem trailing) return;
        if (!int.TryParse(leading.Content?.ToString(), out var leadingDigits) || !int.TryParse(trailing.Content?.ToString(), out var trailingDecimals)) return;
        leadingDisplayDigits = leadingDigits; trailingDisplayDecimals = trailingDecimals;
        Refresh(); Save(); status.Text = $"Sandbox display set to {leadingDisplayDigits} leading digits / {trailingDisplayDecimals} trailing decimals";
    }
    private void ApplyDisplayPrecision(int leadingDigits, int trailingDecimals)
    {
        leadingDisplayDigits = Math.Clamp(leadingDigits, 1, 4); trailingDisplayDecimals = Math.Clamp(trailingDecimals, 0, 3);
        syncingDisplayPrecision = true; leadingPrecisionBox.SelectedIndex = leadingDisplayDigits - 1; trailingPrecisionBox.SelectedIndex = trailingDisplayDecimals; syncingDisplayPrecision = false;
    }
    private void Refresh() { if (loading || cells.Length == 0) return; loading = true; var min = values.Cast<double>().Min(); var max = values.Cast<double>().Max(); for (var r = 0; r < map.Length; r++) for (var c = 0; c < rpm.Length; c++) { cells[r, c].Text = FormatDisplayValue(values[r, c]); cells[r, c].Background = new SolidColorBrush(Heat((values[r, c] - min) / Math.Max(.001, max - min))); } loading = false; }
    private HashSet<(int Row, int Col)> Selected() { var result = pinned.ToHashSet(); if (Bounds(out var t, out var b, out var l, out var r)) for (var row = t; row <= b; row++) for (var c = l; c <= r; c++) result.Add((row, c)); return result; }
    private bool Bounds(out int top, out int bottom, out int left, out int right) { top = bottom = left = right = 0; if (start is null || end is null) return false; top = Math.Min(start.Value.Row, end.Value.Row); bottom = Math.Max(start.Value.Row, end.Value.Row); left = Math.Min(start.Value.Col, end.Value.Col); right = Math.Max(start.Value.Col, end.Value.Col); return true; }
    private bool IsSelected(int row, int col) => Selected().Contains((row, col));
    private void PinRectangle() { foreach (var p in Selected()) pinned.Add(p); start = end = null; }
    private void UpdateSelection()
    {
        if (cells.Length == 0) return;
        var selected = Selected();
        // Render against the actual visual array. During resize/restore it can
        // briefly differ from the replacement axis dimensions.
        for (var r = 0; r < cells.GetLength(0); r++) for (var c = 0; c < cells.GetLength(1); c++)
        {
            var isSelected = selected.Contains((r, c));
            cells[r, c].BorderBrush = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(29, 42, 57));
            cells[r, c].BorderThickness = new Thickness(isSelected ? 2 : .5);
        }
    }
    private void ClearCellSelection() { start = end = null; pinned.Clear(); selecting = false; UpdateSelection(); }
    private void ClearAxisSelection() { selectedMap.Clear(); selectedRpm.Clear(); axisSelecting = false; UpdateAxisVisuals(); }
    private void UpdateAxisVisuals() { for (var i = 0; i < mapEditors.Length; i++) if (mapEditors[i] is not null) { var on = selectedMap.Contains(i); mapEditors[i].Background = new SolidColorBrush(on ? Color.FromRgb(46, 91, 113) : Color.FromRgb(16, 31, 45)); mapEditors[i].BorderBrush = on ? Brushes.White : new SolidColorBrush(Color.FromRgb(38, 58, 76)); } for (var i = 0; i < rpmEditors.Length; i++) if (rpmEditors[i] is not null) { var on = selectedRpm.Contains(i); rpmEditors[i].Background = new SolidColorBrush(on ? Color.FromRgb(46, 91, 113) : Color.FromRgb(15, 40, 51)); rpmEditors[i].BorderBrush = on ? Brushes.White : new SolidColorBrush(Color.FromRgb(38, 58, 76)); } }
    private void RefreshAxisEditors() { for (var i = 0; i < map.Length; i++) if (mapEditors[i] is not null) mapEditors[i].Text = FormatExactAxisValue(map[i]); for (var i = 0; i < rpm.Length; i++) if (rpmEditors[i] is not null) rpmEditors[i].Text = FormatExactAxisValue(rpm[i]); }

    private void SandboxKeyDown(object sender, KeyEventArgs e) { if (Keyboard.Modifiers != ModifierKeys.Control) return; if (e.Key == Key.A) { pinned.Clear(); start = (0, 0); end = (map.Length - 1, rpm.Length - 1); UpdateSelection(); e.Handled = true; } else if (e.Key == Key.C) { Copy(); e.Handled = true; } else if (e.Key == Key.V) { Paste(); e.Handled = true; } else if (e.Key == Key.Z) { Undo(); e.Handled = true; } else if (e.Key == Key.Y) { Redo(); e.Handled = true; } }
    private void Save() { if (loading || values.Length == 0) return; try { Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!); File.WriteAllText(SavePath, JsonSerializer.Serialize(Snapshot())); } catch { } }
    private bool Load() { try { if (!File.Exists(SavePath)) return false; var state = JsonSerializer.Deserialize<SandboxSnapshot>(File.ReadAllText(SavePath)); if (state is null || state.Rpm.Length is < 8 or > 64 || state.Map.Length is < 8 or > 64 || state.Values.Length != state.Map.Length || state.Values.Any(row => row.Length != state.Rpm.Length)) return false; rpm = state.Rpm; map = state.Map; mapUnit = state.MapUnit; xUnit = state.XUnit ?? "RPM"; values = FromJagged(state.Values); customUnits.Clear(); customUnits.AddRange(state.CustomUnits ?? []); customXUnits.Clear(); customXUnits.AddRange(state.CustomXUnits ?? []); ApplyDisplayPrecision(state.LeadingDisplayDigits, state.TrailingDisplayDecimals); RefreshUnitItems(); RefreshXUnitItems(); xSize.Text = rpm.Length.ToString(); ySize.Text = map.Length.ToString(); return true; } catch { return false; } }

    private static double[]? BuildAxis(double minimum, double maximum, int count, bool descending, double increment) { minimum = Math.Round(minimum / increment) * increment; maximum = Math.Round(maximum / increment) * increment; if (count < 2 || maximum - minimum < increment * (count - 1)) return null; var result = new double[count]; for (var i = 0; i < count; i++) { var ideal = Math.Round((minimum + (maximum - minimum) * i / (count - 1d)) / increment) * increment; var low = i == 0 ? minimum : result[i - 1] + increment; var high = maximum - increment * (count - 1 - i); result[i] = Math.Round(Math.Clamp(ideal, low, high) / increment) * increment; } if (descending) Array.Reverse(result); return result; }
    private static bool Ordered(double[] axis, bool descending) { for (var i = 1; i < axis.Length; i++) if (descending ? axis[i] >= axis[i - 1] : axis[i] <= axis[i - 1]) return false; return true; }
    private static double[] Even(double start, double end, int count) => Enumerable.Range(0, count).Select(i => start + (end - start) * i / (count - 1d)).ToArray();
    private static double[,] Resample(double[,] source, int rows, int cols) { var result = new double[rows, cols]; var oldRows = source.GetLength(0); var oldCols = source.GetLength(1); for (var r = 0; r < rows; r++) for (var c = 0; c < cols; c++) { var sr = r * (oldRows - 1d) / (rows - 1); var sc = c * (oldCols - 1d) / (cols - 1); var r0 = (int)sr; var r1 = Math.Min(oldRows - 1, r0 + 1); var c0 = (int)sc; var c1 = Math.Min(oldCols - 1, c0 + 1); var a = source[r0, c0] + (source[r0, c1] - source[r0, c0]) * (sc - c0); var b = source[r1, c0] + (source[r1, c1] - source[r1, c0]) * (sc - c0); result[r, c] = a + (b - a) * (sr - r0); } return result; }
    private static double[][] ToJagged(double[,] source) { var result = new double[source.GetLength(0)][]; for (var r = 0; r < result.Length; r++) { result[r] = new double[source.GetLength(1)]; for (var c = 0; c < result[r].Length; c++) result[r][c] = source[r, c]; } return result; }
    private static double[,] FromJagged(double[][] source) { var result = new double[source.Length, source[0].Length]; for (var r = 0; r < source.Length; r++) for (var c = 0; c < source[r].Length; c++) result[r, c] = source[r][c]; return result; }
    private static double Ease(double value) { var x = Math.Clamp(value, 0, 1); return x * x * (3 - 2 * x); }
    private static Color Heat(double t) { var h = Math.Clamp(t, 0, 1) * 300; var c = .96 * (1 - Math.Abs(2 * .52 - 1)); var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = .52 - c / 2; var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) }; return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)); }
    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)) };
    private static TextBox Box(string text, double width) => new() { Text = text, Width = width, Padding = new Thickness(6), Margin = new Thickness(0, 0, 6, 0), TextAlignment = TextAlignment.Center, Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)) };
    private static ComboBox PrecisionBox(int minimum, int maximum, int selected)
    {
        var box = new ComboBox { Width = 48, Height = 30, Background = Brushes.White, Foreground = Brushes.Black, Margin = new Thickness(0, 0, 7, 0) };
        for (var value = minimum; value <= maximum; value++) box.Items.Add(new ComboBoxItem { Content = value.ToString(CultureInfo.InvariantCulture), Foreground = Brushes.Black });
        box.SelectedIndex = Math.Clamp(selected - minimum, 0, maximum - minimum); return box;
    }
    private static Border Group(string title, params UIElement[] controls) { var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) }); var row = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var control in controls) row.Children.Add(control); content.Children.Add(row); return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Margin = new Thickness(0, 0, 7, 0), Child = content }; }
    private static Button Button(string text, RoutedEventHandler click, bool primary = false) { var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 7, 0), Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), FontWeight = FontWeights.SemiBold }; button.Click += click; return button; }
    private static MenuItem Item(string header, RoutedEventHandler click) { var item = new MenuItem { Header = header }; item.Click += click; return item; }
    private static void Info(string message) => MessageBox.Show(message, "Map Sandbox", MessageBoxButton.OK, MessageBoxImage.Information);
    private sealed record SandboxSnapshot(double[] Rpm, double[] Map, string MapUnit, double[][] Values, string[]? CustomUnits = null, string? XUnit = "RPM", string[]? CustomXUnits = null, int LeadingDisplayDigits = 3, int TrailingDisplayDecimals = 1);
}
