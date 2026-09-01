using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace TimingTableCalculator;

public sealed class FuelingPanel : Grid
{
    private readonly Grid table = new() { Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)) };
    private readonly TextBlock status = new() { Foreground = new SolidColorBrush(Color.FromRgb(118, 135, 156)), FontSize = 11 };
    private readonly TextBox matrixXBox, matrixYBox;
    private readonly ComboBox leadingPrecisionBox, trailingPrecisionBox;
    private readonly Action<int, int> resizeMatrix;
    private readonly Action<bool, int[]> autoFillAxis;
    private readonly Action<bool, int?, int[]> pasteAxis;
    private readonly Action<int, int> setRegionBoundaries;
    private readonly Func<bool, int, double, double[]?> editAxis;
    private ComboBox mapUnitBox = null!;
    private CheckBox conversionViewBox = null!;
    private TextBlock fuelTableTitle = null!;
    private Button boundaryButton = null!;
    private bool settingBoundaries, boundaryPickFromWizard, syncingMapUnit, syncingConversion, syncingDisplayPrecision, showFuelFlow;
    private VeSetupWizard? veSetupWizard;
    private readonly HashSet<int> selectedMapAxis = [], selectedRpmAxis = [];
    private TextBox[] mapAxisCells = [], rpmAxisCells = [];
    private bool axisSelecting, axisDragIsMap;
    private int axisDragStart;
    private TextBox[,] cells = new TextBox[0, 0];
    private double[,] ve = new double[0, 0];
    private double[] rpm = [], map = [];
    private string mapUnit = "kPa absolute";
    private string MapFormat => mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0";
    private string FormatMap(double value) => value.ToString(MapFormat, CultureInfo.InvariantCulture);
    private static string FormatExactAxisValue(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string FormatEditableVe(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
    private static double RoundEditableVe(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
    private int leadingDisplayDigits = 3, trailingDisplayDecimals = 1;
    private string FormatVeDisplayValue(double value)
    {
        var magnitude = Math.Abs(value);
        var leadingDigits = magnitude < 1 ? 1 : (int)Math.Floor(Math.Log10(magnitude)) + 1;
        var format = trailingDisplayDecimals > 0 && leadingDigits < leadingDisplayDigits
            ? "0." + new string('0', trailingDisplayDecimals)
            : "0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
    private string VeExcelNumberFormat()
    {
        if (trailingDisplayDecimals == 0 || leadingDisplayDigits <= 1) return "0";
        var threshold = Math.Pow(10, leadingDisplayDigits - 1).ToString("0", CultureInfo.InvariantCulture);
        return $"[>={threshold}]0;0.{new string('0', trailingDisplayDecimals)}";
    }
    private double idleBoundaryRpm, wotBoundaryMap;
    private int idleBoundaryCol, wotBoundaryRow;
    private (int Row, int Col)? start, end;
    private readonly HashSet<(int Row, int Col)> pinnedFuelSelection = [];
    private bool selecting, loading;
    private bool directionalOuterToInner = true;
    private double directionalStrength = .65;
    private int directionalPasses = 2;
    private double refinementStrength = .45;
    private int refinementPasses = 4;
    private AdvancedSmoothingOptions advancedSmoothingOptions = new(AdvancedSmoothingAlgorithm.StandardWeighted, .65, 2, false, true, .5);
    private VeSetupSettings veSetupSettings = new();
    private double selectionOffsetAmount = 1;
    private bool selectionOffsetIsPercentage;
    private readonly Stack<double[,]> undoHistory = [];
    private readonly Stack<double[,]> redoHistory = [];
    private readonly Dictionary<TextBox, string> editOriginals = [];
    private readonly Dictionary<TextBox, double> axisEditOriginalValues = [];
    private static string SavePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimingTableCalculator", "fueling-autosave.json");

    public FuelingPanel(Action<int, int> resizeMatrix, Action<bool, int[]> autoFillAxis, Action<bool, int?, int[]> pasteAxis, Action<int, int> setRegionBoundaries, Func<bool, int, double, double[]?> editAxis)
    {
        this.resizeMatrix = resizeMatrix; this.autoFillAxis = autoFillAxis; this.pasteAxis = pasteAxis; this.setRegionBoundaries = setRegionBoundaries; this.editAxis = editAxis;
        matrixXBox = MatrixSizeBox("31"); matrixYBox = MatrixSizeBox("31");
        leadingPrecisionBox = PrecisionBox(1, 4, leadingDisplayDigits); trailingPrecisionBox = PrecisionBox(0, 3, trailingDisplayDecimals);
        leadingPrecisionBox.SelectionChanged += (_, _) => ApplyDisplayPrecision(); trailingPrecisionBox.SelectionChanged += (_, _) => ApplyDisplayPrecision();
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        mapUnitBox = CreateMapUnitBox();
        var heading = new Grid { Margin = new Thickness(4, 0, 0, 20) }; heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel(); title.Children.Add(new TextBlock { Text = "FUELING LAB", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold });
        fuelTableTitle = new TextBlock { Text = "Fuel Table — VE (%)", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 25, FontWeight = FontWeights.SemiBold }; title.Children.Add(fuelTableTitle); heading.Children.Add(title);
        var mapUnits = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 24, 0) };
        mapUnits.Children.Add(new TextBlock { Text = "MAP UNITS", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }); mapUnits.Children.Add(mapUnitBox);
        conversionViewBox = new CheckBox { Content = "View as lb/hr", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)) };
        conversionViewBox.Checked += (_, _) => { if (!syncingConversion) SetFuelFlowView(true); }; conversionViewBox.Unchecked += (_, _) => { if (!syncingConversion) SetFuelFlowView(false); }; mapUnits.Children.Add(conversionViewBox);
        Grid.SetColumn(mapUnits, 1); heading.Children.Add(mapUnits);
        status.Text = "Fuel table ready"; status.Foreground = new SolidColorBrush(Color.FromRgb(169, 201, 192)); status.FontSize = 12; status.VerticalAlignment = VerticalAlignment.Center;
        var statusBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(17, 29, 39)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 64, 53)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 8, 14, 8), VerticalAlignment = VerticalAlignment.Center, Child = status };
        Grid.SetColumn(statusBadge, 3); heading.Children.Add(statusBadge); Children.Add(heading);

        var frame = new Border { Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 50, 71)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false, Content = table } };
        Grid.SetRow(frame, 2); Children.Add(frame);
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(ControlGroup("FUEL SETUP TOOLS", Button("◒  VE Setup", OpenVeSetup, true), Button("⇧  Convert to Boosted", ConvertToBoosted_Click, false)));
        tools.Children.Add(MatrixAxisGroup());
        tools.Children.Add(ControlGroup("CELL EDITING", Button("⧉  Copy", (_, _) => CopySelection()), Button("▣  Paste", (_, _) => PasteSelection()), Button("△  Delta", DeltaCompare)));
        tools.Children.Add(ControlGroup("SMOOTHING", Button("⌁  Interpolate", InterpolateSelection), Button("⚙  Smooth Selected…", AdvancedSmooth, true), Button("↕  Columns", SmoothColumns), Button("↔  Rows", SmoothRows)));
        tools.Children.Add(DisplayPrecisionGroup());
        tools.Children.Add(ControlGroup("VIEW & OUTPUT", Button("▦  3D Map", View3D), Button("⇩  Export CSV", ExportCsv), Button("▤  Export Excel", ExportExcel, true)));
        tools.Children.Add(ControlGroup("HISTORY", Button("↶  Undo", (_, _) => Undo()), Button("↷  Redo", (_, _) => Redo())));
        var commandBar = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = tools, Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(commandBar, 1); Children.Add(commandBar);
        PreviewKeyDown += FuelingPanel_PreviewKeyDown;
        table.PreviewMouseLeftButtonUp += (_, _) => { selecting = false; axisSelecting = false; };
    }

    public void UpdateAxes(double[] newRpm, double[] newMap, string newMapUnit, double idleRpm, double wotMap)
    {
        if (newRpm.Length == 0 || newMap.Length == 0) return;
        matrixXBox.Text = newRpm.Length.ToString(CultureInfo.InvariantCulture); matrixYBox.Text = newMap.Length.ToString(CultureInfo.InvariantCulture);
        var incomingBoundaryCol = Closest(newRpm, idleRpm);
        var incomingBoundaryRow = Closest(newMap, wotMap);
        if (ve.Length == 0)
        {
            rpm = newRpm.ToArray(); map = newMap.ToArray(); mapUnit = newMapUnit;
            if (!Load()) GenerateValues();
            idleBoundaryCol = Math.Clamp(incomingBoundaryCol, 0, rpm.Length - 1);
            wotBoundaryRow = Math.Clamp(incomingBoundaryRow, 0, map.Length - 1);
            idleBoundaryRpm = rpm[idleBoundaryCol]; wotBoundaryMap = map[wotBoundaryRow];
            SyncMapUnitControl(); Build(); return;
        }
        if (ve.GetLength(0) != newMap.Length || ve.GetLength(1) != newRpm.Length)
        {
            ve = Resample(ve, newMap.Length, newRpm.Length);
            undoHistory.Clear(); redoHistory.Clear();
        }
        rpm = newRpm.ToArray();
        if (map.Length != newMap.Length)
        {
            var resized = BuildMapAxis(map[^1], map[0], newMap.Length);
            map = resized ?? EvenMapAxis(map[0], map[^1], newMap.Length);
        }
        idleBoundaryCol = Math.Clamp(incomingBoundaryCol, 0, rpm.Length - 1);
        wotBoundaryRow = Math.Clamp(incomingBoundaryRow, 0, map.Length - 1);
        idleBoundaryRpm = rpm[idleBoundaryCol]; wotBoundaryMap = map[wotBoundaryRow];
        SyncMapUnitControl(); Build(); Save();
    }

    private void SyncMapUnitControl()
    {
        syncingMapUnit = true;
        mapUnitBox.SelectedIndex = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        syncingMapUnit = false;
    }

    private Border MatrixAxisGroup()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock { Text = "X", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); panel.Children.Add(matrixXBox);
        panel.Children.Add(new TextBlock { Text = "Y", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); panel.Children.Add(matrixYBox);
        panel.Children.Add(Button("▦  Resize", ResizeRequested, true));
        boundaryButton = Button("⌖  Set boundaries", ToggleBoundarySetting, true); panel.Children.Add(boundaryButton);
        panel.Children.Add(new TextBlock { Text = "Hover map • click to lock", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        return ControlGroup("MATRIX & AXES SETUP", panel);
    }

    private ComboBox CreateMapUnitBox()
    {
        var box = new ComboBox { Width = 112, Height = 32, Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), Padding = new Thickness(6, 3, 6, 3), SelectedIndex = 0 };
        box.Items.Add(new ComboBoxItem { Content = "kPa absolute", Foreground = Brushes.Black }); box.Items.Add(new ComboBoxItem { Content = "PSI gauge", Foreground = Brushes.Black }); box.SelectedIndex = 0;
        box.SelectionChanged += (_, _) => { if (!syncingMapUnit && box.SelectedIndex >= 0) ChangeFuelMapUnit(box.SelectedIndex); };
        return box;
    }

    private double FuelMapIncrement => mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? .1 : 1;
    private double RoundFuelMap(double value) => Math.Round(value / FuelMapIncrement) * FuelMapIncrement;

    private void ChangeFuelMapUnit(int unitIndex)
    {
        if (unitIndex is not (0 or 1) || map.Length == 0) return;
        var fromPsi = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase);
        var toPsi = unitIndex == 1;
        if (fromPsi == toPsi) return;
        for (var index = 0; index < map.Length; index++) map[index] = RoundForUnit(ConvertMapUnit(map[index], fromPsi, toPsi), toPsi);
        veSetupSettings.IdleMap = ConvertMapUnit(veSetupSettings.IdleMap, fromPsi, toPsi);
        veSetupSettings.IdleHighMap = ConvertMapUnit(veSetupSettings.IdleHighMap, fromPsi, toPsi);
        veSetupSettings.MaximumMap = ConvertMapUnit(veSetupSettings.MaximumMap, fromPsi, toPsi);
        mapUnit = toPsi ? "PSI gauge" : "kPa absolute";
        wotBoundaryRow = Math.Clamp(wotBoundaryRow, 0, map.Length - 1); wotBoundaryMap = map[wotBoundaryRow];
        SyncMapUnitControl(); Build(); Save(); veSetupWizard?.UpdateMapAxisAndUnit(map, mapUnit, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
        status.Text = $"Fuel MAP scale converted to {mapUnit}  •  timing MAP scale unchanged";
    }

    private static double ConvertMapUnit(double value, bool fromPsi, bool toPsi)
    {
        if (fromPsi == toPsi) return value;
        return toPsi ? (value - 101.325) / 6.894757293168361 : value * 6.894757293168361 + 101.325;
    }

    private static double RoundForUnit(double value, bool psi) => psi ? Math.Round(value, 1) : Math.Round(value);

    private double[]? BuildMapAxis(double minimum, double maximum, int count)
    {
        var increment = FuelMapIncrement;
        minimum = RoundFuelMap(minimum); maximum = RoundFuelMap(maximum);
        if (count < 2 || maximum - minimum + .0000001 < increment * (count - 1)) return null;
        var ascending = new double[count];
        for (var position = 0; position < count; position++)
        {
            var ideal = RoundFuelMap(minimum + (maximum - minimum) * position / (count - 1d));
            var lower = position == 0 ? minimum : ascending[position - 1] + increment;
            var upper = maximum - increment * (count - 1 - position);
            ascending[position] = RoundFuelMap(Math.Clamp(ideal, lower, upper));
        }
        Array.Reverse(ascending); return ascending;
    }

    private double[] EvenMapAxis(double maximum, double minimum, int count) =>
        Enumerable.Range(0, count).Select(index => RoundFuelMap(maximum + (minimum - maximum) * index / Math.Max(1d, count - 1d))).ToArray();

    private double[]? RescaleFuelMapAxis(double minimum, double maximum)
    {
        var updated = BuildMapAxis(minimum, maximum, map.Length); if (updated is null) return null;
        var previousBoundary = wotBoundaryMap;
        map = updated; wotBoundaryRow = Closest(map, Math.Clamp(previousBoundary, map[^1], map[0])); wotBoundaryMap = map[wotBoundaryRow];
        RefreshFuelMapAxisEditors(); ApplyBoundaries(); Save();
        veSetupWizard?.UpdateBoundaryMapValues(map, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
        return map.ToArray();
    }

    private double[]? EditFuelMapAxisValue(int index, double value)
    {
        if (index < 0 || index >= map.Length || !double.IsFinite(value)) return null;
        value = RoundFuelMap(value);
        double[] updated;
        if (index == 0 || index == map.Length - 1)
        {
            var minimum = index == map.Length - 1 ? value : map[^1];
            var maximum = index == 0 ? value : map[0];
            updated = BuildMapAxis(minimum, maximum, map.Length) ?? [];
            if (updated.Length == 0) return null;
        }
        else
        {
            if (value >= map[index - 1] || value <= map[index + 1]) return null;
            updated = map.ToArray(); updated[index] = value;
        }
        map = updated; wotBoundaryMap = map[Math.Clamp(wotBoundaryRow, 0, map.Length - 1)];
        RefreshFuelMapAxisEditors(); ApplyBoundaries(); Save();
        veSetupWizard?.UpdateBoundaryMapValues(map, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
        return map.ToArray();
    }

    private void AutoFillFuelMapAxis(int[] selected)
    {
        var minimum = selected.Min(index => map[index]); var maximum = selected.Max(index => map[index]);
        var values = BuildMapAxis(minimum, maximum, selected.Length);
        if (values is null) { Info("The selected MAP range is too narrow for the number of fuel breakpoints."); return; }
        var candidate = map.ToArray(); for (var position = 0; position < selected.Length; position++) candidate[selected[position]] = values[position];
        if (Enumerable.Range(1, candidate.Length - 1).Any(index => candidate[index] >= candidate[index - 1]))
        { Info("That fill would cross an unselected neighboring fuel MAP value."); return; }
        map = candidate; wotBoundaryMap = map[Math.Clamp(wotBoundaryRow, 0, map.Length - 1)]; RefreshFuelMapAxisEditors(); ApplyBoundaries(); Save();
        veSetupWizard?.UpdateBoundaryMapValues(map, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
    }

    private void PasteFuelMapAxis(int focusedIndex, int[] selected)
    {
        if (!Clipboard.ContainsText()) { Info("The clipboard does not contain MAP values."); return; }
        var tokens = Clipboard.GetText().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var pasted = new double[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
            if (!double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out pasted[index]) || !double.IsFinite(pasted[index]))
            { Info("The copied MAP column must contain only numeric values."); return; }
        int[] targets;
        if (selected.Length > 1 && selected.Length == pasted.Length) targets = selected;
        else
        {
            var first = selected.Length > 0 ? selected[0] : focusedIndex;
            if (first < 0 || first + pasted.Length > map.Length) { Info("The copied MAP column is too large for the remaining fuel MAP positions."); return; }
            targets = Enumerable.Range(first, pasted.Length).ToArray();
        }
        var candidate = map.ToArray(); for (var index = 0; index < targets.Length; index++) candidate[targets[index]] = pasted[index];
        if (Enumerable.Range(1, candidate.Length - 1).Any(index => candidate[index] >= candidate[index - 1]))
        { Info("Pasted fuel MAP values must decrease from top to bottom."); return; }
        map = candidate; wotBoundaryMap = map[Math.Clamp(wotBoundaryRow, 0, map.Length - 1)];
        selectedMapAxis.Clear(); foreach (var target in targets) selectedMapAxis.Add(target);
        RefreshFuelMapAxisEditors(); ApplyBoundaries(); Save(); status.Text = $"Pasted {targets.Length} fuel MAP breakpoints as entered  •  no auto-scaling";
        veSetupWizard?.UpdateBoundaryMapValues(map, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
    }

    private void RefreshFuelMapAxisEditors()
    {
        for (var index = 0; index < map.Length && index < mapAxisCells.Length; index++)
            if (mapAxisCells[index] is not null) mapAxisCells[index].Text = FormatExactAxisValue(map[index]);
        UpdateAxisSelectionVisuals();
    }

    private void ToggleBoundarySetting(object? sender, RoutedEventArgs e)
    {
        settingBoundaries = !settingBoundaries; boundaryButton.Content = settingBoundaries ? "×  Cancel boundaries" : "⌖  Set boundaries";
        if (settingBoundaries)
        {
            start = end = null;
            selecting = false;
        }
        Cursor = settingBoundaries ? Cursors.Cross : Cursors.Arrow;
        if (!settingBoundaries) ApplyBoundaries();
        status.Text = settingBoundaries ? "Hover over the Fuel map and click a cell to lock both region boundaries" : "Boundary setting cancelled";
        if (!settingBoundaries && boundaryPickFromWizard) { boundaryPickFromWizard = false; veSetupWizard?.CancelBoundaryPick(); }
    }

    private void BeginWizardBoundarySetting()
    {
        boundaryPickFromWizard = true; settingBoundaries = true; start = end = null; selecting = false;
        boundaryButton.Content = "×  Cancel boundaries"; Cursor = Cursors.Cross; status.Text = "Hover over the Fuel map and click a cell to lock both region boundaries";
        Window.GetWindow(this)?.Activate(); Focus();
    }

    private void ResizeRequested(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(matrixXBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns) || columns is < 8 or > 64 ||
            !int.TryParse(matrixYBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows) || rows is < 8 or > 64)
        { Info("Both X and Y matrix sizes must be between 8 and 64."); return; }
        resizeMatrix(columns, rows);
    }

    private void OpenVeSetup(object? sender, RoutedEventArgs e)
    {
        VeSelection? selected = Bounds(out var top, out var bottom, out var left, out var right) ? new VeSelection(top, bottom, left, right) : null;
        var boundary = new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow);
        veSetupWizard = ModelessWindowManager.ShowOrActivate("Fuel.VeSetup", () => new VeSetupWizard(ve, rpm, map, mapUnit, selected, boundary, veSetupSettings, BeginWizardBoundarySetting, RescaleWizardMapAxis, SetBoostedMapUnitFromWizard, (updated, settings) => WorkingRunner.Run(this, () => ApplyVeSetup(updated, settings))) { Owner = Window.GetWindow(this) });
        veSetupWizard.UpdateBoundaryMapValues(map, boundary);
    }

    private double[]? RescaleWizardMapAxis(double minimum, double maximum)
    {
        var updated = RescaleFuelMapAxis(minimum, maximum); if (updated is null) return null;
        status.Text = $"MAP axis rescaled to {FormatMap(map[^1])}–{FormatMap(map[0])} {mapUnit}  •  boundary moved to nearest breakpoint"; return map.ToArray();
    }

    private void SetBoostedMapUnitFromWizard(bool boosted) => ChangeFuelMapUnit(boosted ? 1 : 0);

    private void ConvertToBoosted_Click(object? sender, RoutedEventArgs e)
    {
        if (map.Length == 0) return;
        var confirm = MessageBox.Show(Window.GetWindow(this), "Converting to a boosted table keeps the matrix size the same and redistributes the MAP scale to span the new boosted range. This cannot be reversed with Undo, and the undo/redo history will be cleared.\n\nContinue with the conversion?", "Convert to boosted", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;
        var fromPsi = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase);
        ModelessWindowManager.ShowOrActivate("Fuel.BoostConvert", () => new BoostConversionWindow("Convert Fuel Table to Boosted", map[^1], map[0], fromPsi, dialog => ApplyBoostConversion(dialog)) { Owner = Window.GetWindow(this) });
    }

    private void ApplyBoostConversion(BoostConversionWindow dialog)
    {
        if (dialog.Result is not { } result) return;
        var fromPsi = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase);
        var currentMinPsi = fromPsi ? map[^1] : ConvertMapUnit(map[^1], false, true);
        var currentMaxPsi = fromPsi ? map[0] : ConvertMapUnit(map[0], false, true);
        var existingRows = map.Length;
        var newMinPsi = currentMinPsi;
        var newMaxPsi = result.MaxBoostPsi;

        undoHistory.Clear(); redoHistory.Clear();

        var newRowCount = existingRows;
        var newMap = new double[newRowCount];
        for (var i = 0; i < newRowCount; i++)
        {
            var proportion = i / (double)(newRowCount - 1);
            newMap[i] = Math.Round(newMaxPsi - proportion * (newMaxPsi - newMinPsi), 1);
        }

        var newVe = new double[newRowCount, rpm.Length];
        var wotMap = currentMaxPsi;
        var regionMap = newMinPsi + (wotMap - newMinPsi) * .5;
        for (var row = 0; row < newRowCount; row++)
        {
            for (var col = 0; col < rpm.Length; col++)
            {
                var mapValue = newMap[row];
                if (mapValue >= wotMap)
                {
                    newVe[row, col] = result.Mode switch
                    {
                        BoostRescaleMode.GenerateBoostedRows => Math.Round(VeSetupWizard.GenerateBoostedVe(mapValue, rpm[col], wotMap, regionMap, veSetupSettings), 1),
                        BoostRescaleMode.FlatFill => ve[0, col],
                        _ => ve[0, col]
                    };
                }
                else
                {
                    var closestIdx = 0;
                    var closestDist = double.MaxValue;
                    for (var oldIdx = 0; oldIdx < map.Length; oldIdx++)
                    {
                        var oldMapPsi = fromPsi ? map[oldIdx] : ConvertMapUnit(map[oldIdx], false, true);
                        var dist = Math.Abs(oldMapPsi - mapValue);
                        if (dist < closestDist) { closestDist = dist; closestIdx = oldIdx; }
                    }
                    newVe[row, col] = ve[closestIdx, col];
                }
            }
        }

        map = newMap; ve = newVe; mapUnit = "PSI gauge";
        wotBoundaryRow = 0;
        for (var i = 0; i < map.Length; i++)
            if (map[i] >= currentMaxPsi) { wotBoundaryRow = i; break; }
        wotBoundaryRow = Math.Clamp(wotBoundaryRow, 0, map.Length - 1);
        wotBoundaryMap = map[wotBoundaryRow];
        idleBoundaryCol = Math.Clamp(idleBoundaryCol, 0, rpm.Length - 1);
        veSetupSettings.Boosted = true; veSetupSettings.MaximumMap = map[0]; veSetupSettings.MapSensorBar = Math.Clamp(veSetupSettings.MapSensorBar, 1, 3);

        SyncMapUnitControl(); Build(); Save(); RefreshFuelMapAxisEditors();
        veSetupWizard?.UpdateMapAxisAndUnit(map, mapUnit, new VeRegionBoundary(idleBoundaryCol, wotBoundaryRow));
        status.Text = $"Fuel table converted to boosted  •  MAP now {FormatMap(map[^1])}–{FormatMap(map[0])} PSI gauge";
        dialog.Close();
    }

    private void ApplyVeSetup(double[,] updated, VeSetupSettings appliedSettings)
    {
        if (updated.GetLength(0) != ve.GetLength(0) || updated.GetLength(1) != ve.GetLength(1)) return;
        PushUndo(); ve = (double[,])updated.Clone(); veSetupSettings = appliedSettings; Save(); RefreshAll(); UpdateSelection();
        status.Text = $"VE setup applied  •  {ve.Cast<double>().Min():0.0}–{ve.Cast<double>().Max():0.0}%";
    }

    private void SetFuelFlowView(bool enabled)
    {
        showFuelFlow = enabled; fuelTableTitle.Text = enabled ? "Fuel Table — Estimated Fuel Flow (lb/hr)" : "Fuel Table — VE (%)";
        RefreshAll(); ApplyBoundaries(); Save(); status.Text = enabled ? "Viewing estimated total fuel flow  •  VE values retained" : "Viewing editable volumetric efficiency values";
    }

    private double[,] DisplayValues() => showFuelFlow ? VeSetupWizard.ConvertToFuelFlow(ve, rpm, map, mapUnit, veSetupSettings) : (double[,])ve.Clone();

    private void GenerateValues()
    {
        ve = new double[map.Length, rpm.Length];
        for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++)
        {
            var load = (map[row] - map[^1]) / Math.Max(.1, map[0] - map[^1]); var speed = col / (double)Math.Max(1, rpm.Length - 1);
            ve[row, col] = 42 + load * 62 + Math.Sin(speed * Math.PI) * 12;
        }
    }

    private void Build()
    {
        loading = true; axisEditOriginalValues.Clear(); cells = new TextBox[map.Length, rpm.Length]; mapAxisCells = new TextBox[map.Length]; rpmAxisCells = new TextBox[rpm.Length];
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); axisSelecting = false;
        table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var col = 0; col < rpm.Length; col++) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var row = 0; row < map.Length; row++) table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) }); table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        var mapTitle = new TextBlock { Text = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? "MAP (PSIG)" : "MAP (kPa)", Foreground = Brushes.White, FontWeight = FontWeights.Bold, LayoutTransform = new RotateTransform(-90), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRowSpan(mapTitle, map.Length); table.Children.Add(mapTitle);
        for (var row = 0; row < map.Length; row++)
        {
            AddAxisEditor(map[row], row, 1, true, row);
            for (var col = 0; col < rpm.Length; col++)
            {
                var cell = new TextBox { Tag = (row, col), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 57)), BorderThickness = new Thickness(.5), Padding = new Thickness(1) };
                cell.PreviewMouseLeftButtonDown += CellDown; cell.MouseEnter += CellEnter; cell.PreviewMouseRightButtonDown += CellRightClick; cell.ContextMenu = CreateContextMenu();
                cell.GotKeyboardFocus += (_, _) =>
                {
                    if (!showFuelFlow)
                    {
                        var point = (ValueTuple<int, int>)cell.Tag;
                        var editable = FormatEditableVe(ve[point.Item1, point.Item2]);
                        editOriginals[cell] = editable;
                        cell.Text = editable;
                    }
                    cell.SelectAll();
                };
                cell.LostFocus += CellEdited; cell.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Keyboard.ClearFocus(); e.Handled = true; } }; cells[row, col] = cell;
                Grid.SetRow(cell, row); Grid.SetColumn(cell, col + 2); table.Children.Add(cell);
            }
        }
        for (var col = 0; col < rpm.Length; col++) AddAxisEditor(rpm[col], map.Length, col + 2, false, col);
        var rpmTitle = new TextBlock { Text = "Engine RPM", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(rpmTitle, map.Length + 1); Grid.SetColumn(rpmTitle, 2); Grid.SetColumnSpan(rpmTitle, rpm.Length); table.Children.Add(rpmTitle);
        loading = false; RefreshAll(); ApplyBoundaries();
    }

    private void CellDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<int, int> p } cell) return;
        ClearAxisSelection();
        if (Keyboard.FocusedElement is TextBox { Tag: ValueTuple<int, int> } focusedCell && !ReferenceEquals(focusedCell, cell))
            CellEdited(focusedCell, new RoutedEventArgs());
        if (settingBoundaries)
        {
            settingBoundaries = false; boundaryButton.Content = "⌖  Set boundaries"; Cursor = Cursors.Arrow;
            idleBoundaryCol = p.Item2; wotBoundaryRow = p.Item1;
            idleBoundaryRpm = rpm[idleBoundaryCol]; wotBoundaryMap = map[wotBoundaryRow]; ApplyBoundaries(); Save();
            setRegionBoundaries(p.Item1, p.Item2); status.Text = $"Region boundaries locked at {rpm[p.Item2]:0} RPM and {FormatMap(map[p.Item1])} {mapUnit}";
            if (boundaryPickFromWizard) { boundaryPickFromWizard = false; veSetupWizard?.CompleteBoundaryPick(map, new VeRegionBoundary(p.Item2, p.Item1)); }
            e.Handled = true; return;
        }
        var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (!control && SelectedFuelCells().Take(2).Count() > 1 && IsFuelCellSelected(p.Item1, p.Item2))
        {
            selecting = false; cell.Focus(); cell.SelectAll(); e.Handled = true; return;
        }
        if (control) PinActiveFuelSelection(); else pinnedFuelSelection.Clear();
        start = end = p; selecting = true; UpdateSelection(); cell.Focus(); e.Handled = true;
    }
    private void CellEnter(object sender, MouseEventArgs e) { if (sender is not TextBox { Tag: ValueTuple<int, int> p }) return; if (settingBoundaries) { idleBoundaryCol = p.Item2; wotBoundaryRow = p.Item1; RenderBoundaries(); status.Text = $"Preview: Idle regions through {rpm[p.Item2]:0} RPM • High-MAP regions from {FormatMap(map[p.Item1])} {mapUnit}"; return; } if (!selecting || e.LeftButton != MouseButtonState.Pressed) return; end = p; UpdateSelection(); }
    private void CellRightClick(object sender, MouseButtonEventArgs e) { if (sender is not TextBox { Tag: ValueTuple<int, int> p }) return; if (!IsFuelCellSelected(p.Item1, p.Item2)) { pinnedFuelSelection.Clear(); start = end = p; selecting = false; UpdateSelection(); } }
    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu(); menu.Items.Add(Item("Copy selected", (_, _) => CopySelection())); menu.Items.Add(Item("Paste", (_, _) => PasteSelection())); menu.Items.Add(Item("Offset selection…", OffsetSelection)); menu.Items.Add(new Separator());
        menu.Items.Add(Item("Interpolate selection", InterpolateSelection)); menu.Items.Add(Item("Smooth selected…", AdvancedSmooth)); menu.Items.Add(Item("Smooth rows", SmoothRows)); menu.Items.Add(Item("Smooth columns", SmoothColumns));
        menu.Items.Add(new Separator()); menu.Items.Add(Item("Clear selected", ClearSelected)); return menu;
    }
    private static MenuItem Item(string header, RoutedEventHandler click) { var item = new MenuItem { Header = header }; item.Click += click; return item; }
    private void ClearSelected(object? sender, RoutedEventArgs e) { var selected = SelectedFuelCells(); if (selected.Count == 0) return; PushUndo(); foreach (var cell in selected) ve[cell.Row, cell.Col] = 0; Save(); RefreshAll(); UpdateSelection(); status.Text = $"Cleared {selected.Count} selected fuel cells"; }
    private void OffsetSelection(object? sender, RoutedEventArgs e)
    {
        if (showFuelFlow) { Info("Offset works on VE percentages. Clear 'View as lb/hr' before applying an offset."); return; }
        if (ModelessWindowManager.ActivateIfOpen("Fuel.Offset")) return;
        if (!Bounds(out var top, out var bottom, out var left, out var right)) return;
        ModelessWindowManager.ShowOrActivate("Fuel.Offset", () => new OffsetSelectionWindow(selectionOffsetAmount, selectionOffsetIsPercentage, (direction, amount, percentage) => ApplyOffset(top, bottom, left, right, direction, amount, percentage)) { Owner = Window.GetWindow(this) });
    }
    private void ApplyOffset(int top, int bottom, int left, int right, int direction, double amount, bool percentage)
    {
        selectionOffsetAmount = amount; selectionOffsetIsPercentage = percentage; PushUndo(); var selected = SelectedFuelCells();
        if (selected.Count == 0) for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) selected.Add((row, col));
        foreach (var cell in selected) ve[cell.Row, cell.Col] = percentage ? ve[cell.Row, cell.Col] * (1 + direction * amount / 100) : ve[cell.Row, cell.Col] + direction * amount;
        Save(); RefreshAll(); UpdateSelection(); status.Text = $"{selected.Count} fuel cells {(direction > 0 ? "increased" : "decreased")} by {amount:0.###}{(percentage ? "%" : "")}";
    }
    private void CellEdited(object sender, RoutedEventArgs e)
    {
        if (showFuelFlow) { RefreshAll(); return; }
        if (loading || sender is not TextBox { Tag: ValueTuple<int, int> p } cell || !double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) { RefreshAll(); return; }
        var changed = editOriginals.Remove(cell, out var original) && !string.Equals(original, cell.Text, StringComparison.Ordinal);
        if (!changed) { RefreshAll(); UpdateSelection(); return; }
        value = RoundEditableVe(value);
        PushUndo();
        if (IsFuelCellSelected(p.Item1, p.Item2))
        {
            var selected = SelectedFuelCells(); foreach (var selectedCell in selected) ve[selectedCell.Row, selectedCell.Col] = value;
            status.Text = $"Set {selected.Count} selected fuel cells to {FormatEditableVe(value)}";
        }
        else ve[p.Item1, p.Item2] = value;
        Save(); RefreshAll(); UpdateSelection();
    }

    private void FuelingPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && settingBoundaries) { settingBoundaries = false; boundaryButton.Content = "⌖  Set boundaries"; Cursor = Cursors.Arrow; ApplyBoundaries(); status.Text = "Boundary setting cancelled"; if (boundaryPickFromWizard) { boundaryPickFromWizard = false; veSetupWizard?.CancelBoundaryPick(); } e.Handled = true; return; }
        if (Keyboard.FocusedElement is TextBox { Tag: ValueTuple<bool, int> axisTag })
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V) { PasteSelectedAxis(axisTag.Item1, axisTag.Item2); e.Handled = true; }
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.A) { pinnedFuelSelection.Clear(); start = (0, 0); end = (ve.GetLength(0) - 1, ve.GetLength(1) - 1); UpdateSelection(); e.Handled = true; }
        else if (e.Key == Key.C) { CopySelection(); e.Handled = true; }
        else if (e.Key == Key.V) { PasteSelection(); e.Handled = true; }
        else if (e.Key == Key.Z) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y) { Redo(); e.Handled = true; }
    }

    private void CopySelection()
    {
        if (!Bounds(out var top, out var bottom, out var left, out var right)) { Info("Select one or more fuel cells first."); return; }
        var text = new StringBuilder();
        var copiedValues = showFuelFlow ? DisplayValues() : ve;
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++) { if (col > left) text.Append('\t'); text.Append(copiedValues[row, col].ToString("0.###", CultureInfo.InvariantCulture)); }
            if (row < bottom) text.AppendLine();
        }
        try { Clipboard.SetText(text.ToString()); ClearFuelSelection(); status.Text = $"Copied {right - left + 1} × {bottom - top + 1} fuel cells  •  selection cleared"; }
        catch { Info("The clipboard is currently unavailable."); }
    }

    private void PasteSelection()
    {
        if (showFuelFlow) { Info("Pasting is available in VE% view. Clear 'View as lb/hr' before pasting fuel-table values."); return; }
        if (!Bounds(out var top, out var bottom, out var left, out var right)) { Info("Select the first destination fuel cell or destination area."); return; }
        string text; try { if (!Clipboard.ContainsText()) return; text = Clipboard.GetText().Trim(); } catch { Info("The clipboard is currently unavailable."); return; }
        var rows = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(line.Contains('\t') ? '\t' : ',', StringSplitOptions.TrimEntries)).ToArray();
        if (rows.Length == 0) return;
        PushUndo();
        if (rows.Length == 1 && rows[0].Length == 1)
        {
            if (!double.TryParse(rows[0][0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { Info("Clipboard cells must contain numeric values."); return; }
            value = RoundEditableVe(value);
            for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) ve[row, col] = value;
        }
        else
        {
            for (var sourceRow = 0; sourceRow < rows.Length && top + sourceRow < map.Length; sourceRow++)
            for (var sourceCol = 0; sourceCol < rows[sourceRow].Length && left + sourceCol < rpm.Length; sourceCol++)
            {
                if (!double.TryParse(rows[sourceRow][sourceCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { Info("Clipboard cells must contain numeric values."); return; }
                var row = top + sourceRow; var col = left + sourceCol; ve[row, col] = RoundEditableVe(value);
            }
            end = (Math.Min(map.Length - 1, top + rows.Length - 1), Math.Min(rpm.Length - 1, left + rows.Max(row => row.Length) - 1));
        }
        Save(); RefreshAll(); ClearFuelSelection(); status.Text = "Fuel values pasted from clipboard  •  selection cleared";
    }

    private void DeltaCompare(object? sender, RoutedEventArgs e)
    {
        if (showFuelFlow) { Info("Delta compare works on VE percentages. Clear 'View as lb/hr' before comparing fuel-table values."); return; }
        if (!TryReadClipboardTable(out var pasted)) return;
        if (!TryCreateDeltaTarget(pasted, out var target, out var top, out var bottom, out var left, out var right)) return;

        var deltas = new List<double>();
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) deltas.Add(target[row, col] - ve[row, col]);
        var window = new DeltaCompareWindow(
            "Fuel Delta Compare",
            bottom - top + 1,
            right - left + 1,
            deltas.Min(),
            deltas.Max(),
            deltas.Average(),
            deltas.Average(Math.Abs),
            (mode, strength, passes) => ApplyDeltaCompare(target, top, bottom, left, right, mode, strength, passes))
        { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private bool TryReadClipboardTable(out double[][] values)
    {
        values = [];
        string text; try { if (!Clipboard.ContainsText()) { Info("Copy a numeric table to the clipboard first."); return false; } text = Clipboard.GetText().Trim(); } catch { Info("The clipboard is currently unavailable."); return false; }
        var rows = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(line.Contains('\t') ? '\t' : ',', StringSplitOptions.TrimEntries)).ToArray();
        if (rows.Length == 0) { Info("Copy a numeric table to the clipboard first."); return false; }
        values = new double[rows.Length][];
        for (var row = 0; row < rows.Length; row++)
        {
            values[row] = new double[rows[row].Length];
            for (var col = 0; col < rows[row].Length; col++)
                if (!double.TryParse(rows[row][col], NumberStyles.Float, CultureInfo.InvariantCulture, out values[row][col]) || !double.IsFinite(values[row][col]))
                { Info("Clipboard cells must contain numeric values."); return false; }
        }
        return true;
    }

    private bool TryCreateDeltaTarget(double[][] pasted, out double[,] target, out int top, out int bottom, out int left, out int right)
    {
        target = (double[,])ve.Clone();
        if (Bounds(out top, out bottom, out left, out right))
        {
            if (pasted.Length == 1 && pasted[0].Length == 1)
            {
                for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) target[row, col] = pasted[0][0];
                return true;
            }
            if (top + pasted.Length > map.Length || left + pasted.Max(row => row.Length) > rpm.Length)
            { Info("The pasted table is too large for the selected starting fuel cell."); return false; }
            bottom = top + pasted.Length - 1; right = left + pasted.Max(row => row.Length) - 1;
            for (var sourceRow = 0; sourceRow < pasted.Length; sourceRow++)
            for (var sourceCol = 0; sourceCol < pasted[sourceRow].Length; sourceCol++)
                target[top + sourceRow, left + sourceCol] = pasted[sourceRow][sourceCol];
            return true;
        }

        if (pasted.Length != map.Length || pasted.Any(row => row.Length != rpm.Length))
        { Info("Select a destination fuel cell, or copy a full-size table that matches the Fueling matrix."); return false; }
        top = 0; bottom = map.Length - 1; left = 0; right = rpm.Length - 1;
        for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++) target[row, col] = pasted[row][col];
        return true;
    }

    private void ApplyDeltaCompare(double[,] target, int top, int bottom, int left, int right, DeltaCompareApplyMode mode, double strength, int passes)
    {
        PushUndo();
        var next = mode switch
        {
            DeltaCompareApplyMode.SmoothDelta => SmoothDeltaBlock(target, top, bottom, left, right, strength, passes),
            _ => (double[,])target.Clone()
        };
        for (var row = 0; row < ve.GetLength(0); row++) for (var col = 0; col < ve.GetLength(1); col++) ve[row, col] = Math.Round(next[row, col], 1);
        Save(); RefreshAll(); ClearFuelSelection();
        status.Text = mode switch
        {
            DeltaCompareApplyMode.SmoothDelta => $"Smoothed pasted delta across {right - left + 1} x {bottom - top + 1} fuel cells",
            _ => $"Applied pasted target to {right - left + 1} x {bottom - top + 1} fuel cells"
        };
    }

    private double[,] SmoothDeltaBlock(double[,] target, int top, int bottom, int left, int right, double strength, int passes)
    {
        var result = (double[,])target.Clone();
        for (var pass = 0; pass < passes; pass++)
        {
            var next = (double[,])result.Clone();
            for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++)
            {
                double sum = 0, weight = 0;
                for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++)
                {
                    var rr = row + dr; var cc = col + dc;
                    if (rr < top || rr > bottom || cc < left || cc > right) continue;
                    var w = (dr == 0 ? 2 : 1) * (dc == 0 ? 2 : 1);
                    sum += result[rr, cc] * w; weight += w;
                }
                next[row, col] = result[row, col] + (sum / Math.Max(.0001, weight) - result[row, col]) * strength;
            }
            result = next;
        }
        return result;
    }

    private void ClearFuelSelection()
    {
        pinnedFuelSelection.Clear(); start = end = null; selecting = false; ApplyBoundaries();
    }

    private void SmoothSelection(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || b - t < 2 || r - l < 2) { Info("Select at least 3 × 3 fuel cells."); return; } Smooth(t, b, l, r, 2, .65); }
    private void InterpolateSelection(object? sender, RoutedEventArgs e)
    {
        if (showFuelFlow) { Info("Interpolation works on VE percentages. Clear 'View as lb/hr' before interpolating."); return; }
        if (!Bounds(out var top, out var bottom, out var left, out var right) || !SelectionInterpolator.CanApply(top, bottom, left, right)) { Info("Select at least three cells in a row or column, or a selection at least 3 × 3."); return; }
        PushUndo(); ve = SelectionInterpolator.Apply(ve, top, bottom, left, right); Save(); RefreshAll(); UpdateSelection(); status.Text = $"Interpolated {right - left + 1} × {bottom - top + 1} fuel cells from the selected perimeter";
    }
    private void Refine(object? sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Fuel.Refinement")) return;
        if (!Bounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2 || right - left < 2) { Info("Select at least 3 × 3 fuel cells."); return; }
        ModelessWindowManager.ShowOrActivate("Fuel.Refinement", () => new SmoothRefinementWindow(refinementStrength, refinementPasses, applied => WorkingRunner.Run(this, () => ApplyRefinement(applied, top, bottom, left, right))) { Owner = Window.GetWindow(this) });
    }
    private void ApplyRefinement(SmoothRefinementWindow dialog, int top, int bottom, int left, int right)
    {
        refinementStrength = dialog.Strength; refinementPasses = dialog.Passes;
        Smooth(top, bottom, left, right, refinementPasses, refinementStrength); Save();
        status.Text = $"Fuel selection refined  •  {refinementStrength * 100:0}% × {refinementPasses} passes";
    }
    private void AdvancedSmooth(object? sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Fuel.AdvancedSmoothing")) return;
        var selected = SelectedFuelCells(); if (selected.Count == 0) { Info("Select one or more fuel cells first."); return; }
        ModelessWindowManager.ShowOrActivate("Fuel.AdvancedSmoothing", () => new AdvancedSmoothingWindow(advancedSmoothingOptions, dialog => WorkingRunner.Run(this, () => ApplyAdvancedSmoothing(dialog, selected))) { Owner = Window.GetWindow(this) });
    }
    private void ApplyAdvancedSmoothing(AdvancedSmoothingWindow dialog, IReadOnlyCollection<(int Row, int Col)> selected)
    {
        advancedSmoothingOptions = dialog.Options; PushUndo(); ve = AdvancedSmoother.Apply(ve, selected, advancedSmoothingOptions);
        Save(); RefreshAll(); UpdateSelection(); status.Text = $"Smoothed {selected.Count} selected fuel cells  •  {advancedSmoothingOptions.Algorithm}  •  {advancedSmoothingOptions.Passes} passes";
    }
    private void DirectionalSmooth(object? sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Fuel.DirectionalSmoothing")) return;
        if (!Bounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2 || right - left < 2) { Info("Select at least 3 × 3 fuel cells."); return; }
        ModelessWindowManager.ShowOrActivate("Fuel.DirectionalSmoothing", () => new DirectionalSmoothingWindow(directionalOuterToInner, directionalStrength, directionalPasses, applied => ApplyDirectional(applied, top, bottom, left, right)) { Owner = Window.GetWindow(this) });
    }

    private void ApplyDirectional(DirectionalSmoothingWindow dialog, int top, int bottom, int left, int right)
    {
        directionalOuterToInner = dialog.OuterToInner; directionalStrength = dialog.Strength; directionalPasses = dialog.Passes;
        PushUndo();
        ve = DirectionalSmoother.Apply(ve, top, bottom, left, right, dialog.OuterToInner, dialog.Strength, dialog.Passes);
        Save(); RefreshAll(); UpdateSelection(); status.Text = dialog.OuterToInner ? "Smoothed fuel selection from outer perimeter inward" : "Smoothed fuel selection from inner core outward";
    }
    private void Smooth(int top, int bottom, int left, int right, int passes, double strength)
    {
        PushUndo();
        var work = (double[,])ve.Clone();
        for (var pass = 0; pass < passes; pass++) { var next = (double[,])work.Clone(); for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) { double sum = 0, weight = 0; for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++) { var rr = row + dr; var cc = col + dc; if (rr < top || rr > bottom || cc < left || cc > right) continue; var w = (dr == 0 ? 2 : 1) * (dc == 0 ? 2 : 1); sum += work[rr, cc] * w; weight += w; } next[row, col] = work[row, col] + (sum / weight - work[row, col]) * strength; } work = next; }
        ve = work; Save(); RefreshAll(); UpdateSelection(); status.Text = $"Smoothed {right - left + 1} × {bottom - top + 1} fuel cells";
    }

    private void SmoothColumns(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || b - t < 2) { Info("Select at least 3 rows."); return; } PushUndo(); for (var col = l; col <= r; col++) for (var row = t + 1; row < b; row++) { var x = (map[t] - map[row]) / (map[t] - map[b]); x = Ease(x); ve[row, col] = ve[t, col] + (ve[b, col] - ve[t, col]) * x; } Save(); RefreshAll(); UpdateSelection(); }
    private void SmoothRows(object? sender, RoutedEventArgs e) { if (!Bounds(out var t, out var b, out var l, out var r) || r - l < 2) { Info("Select at least 3 columns."); return; } PushUndo(); for (var row = t; row <= b; row++) for (var col = l + 1; col < r; col++) { var x = (rpm[col] - rpm[l]) / (rpm[r] - rpm[l]); x = Ease(x); ve[row, col] = ve[row, l] + (ve[row, r] - ve[row, l]) * x; } Save(); RefreshAll(); UpdateSelection(); }
    private void View3D(object? sender, RoutedEventArgs e)
    {
        ModelessWindowManager.ShowOrActivate("Fuel.3D", () =>
        {
            var displayed = DisplayValues();
            var window = new Surface3DWindow(displayed, rpm, map, mapUnit, false, Colors.Red, Colors.Magenta,
                showFuelFlow ? (_, _, _, _) => displayed : (t, b, l, r) => { start = (t, l); end = (b, r); Smooth(t, b, l, r, 2, .65); return (double[,])ve.Clone(); },
                showFuelFlow ? "3D Fuel Flow Map" : "3D Volumetric Efficiency Map", showFuelFlow ? "FUEL FLOW (lb/hr)" : "VOLUMETRIC EFFICIENCY (%)", showFuelFlow ? null : Handle3DSelectionAction, rpmFormat: "0.########", valueFormat: showFuelFlow ? "0.0" : "0", valueFormatter: showFuelFlow ? null : FormatVeDisplayValue) { Owner = Window.GetWindow(this) };
            window.Closed += (_, _) =>
            {
                start = end = null; selecting = false;
                if (IsLoaded) { ApplyBoundaries(); status.Text = "3D view closed  •  fuel selection cleared"; }
            };
            return window;
        });
    }

    private void ExportCsv(object? sender, RoutedEventArgs e)
    {
        if (rpm.Length == 0 || map.Length == 0) return;
        var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "fuel-table.csv" }; if (dialog.ShowDialog() != true) return;
        var csv = new StringBuilder();
        for (var row = 0; row < map.Length; row++)
        {
            csv.Append(FormatExactAxisValue(map[row]));
            for (var col = 0; col < rpm.Length; col++) csv.Append(',').Append(ve[row, col].ToString("0.###", CultureInfo.InvariantCulture));
            csv.AppendLine();
        }
        csv.Append("Engine RPM"); foreach (var value in rpm) csv.Append(',').Append(FormatExactAxisValue(value)); csv.AppendLine();
        File.WriteAllText(dialog.FileName, csv.ToString()); status.Text = $"Saved {Path.GetFileName(dialog.FileName)}";
    }

    private void ExportExcel(object? sender, RoutedEventArgs e)
    {
        if (rpm.Length == 0 || map.Length == 0) return;
        var dialog = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "fuel-table.xlsx" }; if (dialog.ShowDialog() != true) return;
        ExcelTimingExporter.Export(dialog.FileName, rpm, map, ve, mapUnit, Hsl(0, .96, .52), Hsl(150, .96, .52), Hsl(300, .96, .52), false, "Fuel Map", "Fueling Map", valueNumberFormat: VeExcelNumberFormat());
        status.Text = $"Saved {Path.GetFileName(dialog.FileName)} with heat-map formatting";
    }

    private void Handle3DSelectionAction(SurfaceSelectionAction action, int top, int bottom, int left, int right, IReadOnlyCollection<(int Row, int Col)> selectedCells, Action<double[,]> refresh)
    {
        if (action == SurfaceSelectionAction.Undo) { Undo(); refresh((double[,])ve.Clone()); return; }
        if (action == SurfaceSelectionAction.Redo) { Redo(); refresh((double[,])ve.Clone()); return; }
        pinnedFuelSelection.Clear(); foreach (var cell in selectedCells) pinnedFuelSelection.Add(cell);
        start = (top, left); end = (bottom, right); selecting = false; UpdateSelection();
        void Refresh() => refresh((double[,])ve.Clone());
        switch (action)
        {
            case SurfaceSelectionAction.Copy: CopySelection(); break;
            case SurfaceSelectionAction.Paste: PasteSelection(); Refresh(); break;
            case SurfaceSelectionAction.Offset:
                ModelessWindowManager.ShowOrActivate("Fuel.Offset", () => new OffsetSelectionWindow(selectionOffsetAmount, selectionOffsetIsPercentage, (direction, amount, percentage) => { ApplyOffset(top, bottom, left, right, direction, amount, percentage); Refresh(); }) { Owner = Window.GetWindow(this) }); break;
            case SurfaceSelectionAction.Smooth: SmoothSelection(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Interpolate: InterpolateSelection(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Refine:
                ModelessWindowManager.ShowOrActivate("Fuel.Refinement", () => new SmoothRefinementWindow(refinementStrength, refinementPasses, dialog => WorkingRunner.Run(this, () => { ApplyRefinement(dialog, top, bottom, left, right); Refresh(); })) { Owner = Window.GetWindow(this) }); break;
            case SurfaceSelectionAction.Advanced:
                var fuelSelection = selectedCells.ToArray();
                ModelessWindowManager.ShowOrActivate("Fuel.AdvancedSmoothing", () => new AdvancedSmoothingWindow(advancedSmoothingOptions, dialog => WorkingRunner.Run(this, () => { ApplyAdvancedSmoothing(dialog, fuelSelection); Refresh(); })) { Owner = Window.GetWindow(this) }); break;
            case SurfaceSelectionAction.SmoothRows: SmoothRows(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.SmoothColumns: SmoothColumns(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Clear: ClearSelected(this, new RoutedEventArgs()); Refresh(); break;
        }
    }

    private void PushUndo()
    {
        if (loading || ve.Length == 0) return;
        if (undoHistory.Count >= 50) { var retained = undoHistory.Reverse().Skip(1).ToArray(); undoHistory.Clear(); foreach (var item in retained) undoHistory.Push(item); }
        undoHistory.Push((double[,])ve.Clone()); redoHistory.Clear();
    }

    private void Undo()
    {
        if (undoHistory.Count == 0) { status.Text = "Nothing to undo in the fuel table"; return; }
        WorkingRunner.Run(this, () =>
        {
            redoHistory.Push((double[,])ve.Clone()); ve = undoHistory.Pop(); Save(); RefreshAll(); UpdateSelection(); status.Text = "Fuel change undone";
        });
    }

    private void Redo()
    {
        if (redoHistory.Count == 0) { status.Text = "Nothing to redo in the fuel table"; return; }
        WorkingRunner.Run(this, () =>
        {
            undoHistory.Push((double[,])ve.Clone()); ve = redoHistory.Pop(); Save(); RefreshAll(); UpdateSelection(); status.Text = "Fuel change redone";
        });
    }

    private void RefreshAll() { if (cells.Length == 0) return; loading = true; var displayed = DisplayValues(); var min = displayed.Cast<double>().Min(); var max = displayed.Cast<double>().Max(); for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++) { var value = displayed[row, col]; cells[row, col].Text = showFuelFlow ? value.ToString("0.0", CultureInfo.InvariantCulture) : FormatVeDisplayValue(value); cells[row, col].IsReadOnly = showFuelFlow; cells[row, col].Background = new SolidColorBrush(Heat((value - min) / Math.Max(.1, max - min))); } loading = false; }
    private bool Bounds(out int top, out int bottom, out int left, out int right) { top = bottom = left = right = 0; if (start is null || end is null) return false; top = Math.Min(start.Value.Row, end.Value.Row); bottom = Math.Max(start.Value.Row, end.Value.Row); left = Math.Min(start.Value.Col, end.Value.Col); right = Math.Max(start.Value.Col, end.Value.Col); return true; }
    private bool IsFuelCellSelected(int row, int col) => pinnedFuelSelection.Contains((row, col)) || Bounds(out var top, out var bottom, out var left, out var right) && row >= top && row <= bottom && col >= left && col <= right;
    private void PinActiveFuelSelection() { if (!Bounds(out var top, out var bottom, out var left, out var right)) return; for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) pinnedFuelSelection.Add((row, col)); }
    private HashSet<(int Row, int Col)> SelectedFuelCells() { var selected = new HashSet<(int Row, int Col)>(pinnedFuelSelection); if (Bounds(out var top, out var bottom, out var left, out var right)) for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) selected.Add((row, col)); return selected; }
    private void UpdateSelection() { var selectedCells = SelectedFuelCells(); if (selectedCells.Count == 0) return; for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++) { var selected = selectedCells.Contains((row, col)); var boundary = IsBoundary(row, col); cells[row, col].BorderBrush = selected ? Brushes.White : boundary ? Brushes.Black : new SolidColorBrush(Color.FromRgb(29, 42, 57)); cells[row, col].BorderThickness = new Thickness(selected ? 1.5 : boundary ? 3 : .5); } status.Text = $"Selected {selectedCells.Count} fuel cells"; }
    private void ApplyBoundaries()
    {
        idleBoundaryCol = Closest(rpm, idleBoundaryRpm); wotBoundaryRow = Closest(map, wotBoundaryMap);
        RenderBoundaries();
    }
    private void RenderBoundaries()
    {
        var displayed = DisplayValues(); var suffix = showFuelFlow ? " lb/hr" : "% VE";
        for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++)
        {
            var boundary = IsBoundary(row, col); cells[row, col].BorderBrush = boundary ? Brushes.Black : new SolidColorBrush(Color.FromRgb(29, 42, 57)); cells[row, col].BorderThickness = new Thickness(boundary ? 3 : .5);
            var region = col <= idleBoundaryCol
                ? row <= wotBoundaryRow ? "Idle High MAP" : "Idle Low MAP"
                : row <= wotBoundaryRow ? "Part Throttle to WOT" : "Cruise to Part Throttle";
            var displayedValue = showFuelFlow ? displayed[row, col].ToString("0.0", CultureInfo.InvariantCulture) : FormatVeDisplayValue(displayed[row, col]);
            cells[row, col].ToolTip = $"{region}  •  {rpm[col]:0} RPM  •  {FormatMap(map[row])} {mapUnit}  •  {displayedValue}{suffix}";
        }
        if (start is not null) UpdateSelection();
    }
    private bool IsBoundary(int row, int col) => col == idleBoundaryCol || row == wotBoundaryRow;
    private static int Closest(double[] axis, double value) { var best = 0; var distance = double.MaxValue; for (var i = 0; i < axis.Length; i++) { var current = Math.Abs(axis[i] - value); if (current < distance) { best = i; distance = current; } } return best; }
    private void AddAxisEditor(double value, int row, int column, bool isMap, int index)
    {
        var editor = new TextBox
        {
            Tag = (isMap, index), Text = FormatExactAxisValue(value),
            Foreground = new SolidColorBrush(Color.FromRgb(127, 227, 208)), Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 58, 76)), BorderThickness = new Thickness(.5), TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center, FontSize = isMap ? 11 : 10, FontWeight = FontWeights.Bold, Padding = new Thickness(2),
            ToolTip = isMap ? $"Edit fuel MAP breakpoint ({mapUnit})" : "Edit shared RPM breakpoint"
        };
        editor.GotKeyboardFocus += (_, _) => { start = end = null; selecting = false; RenderBoundaries(); var current = isMap ? map[index] : rpm[index]; axisEditOriginalValues[editor] = current; editor.Text = FormatExactAxisValue(current); editor.SelectAll(); };
        editor.PreviewMouseLeftButtonDown += AxisEditorMouseDown;
        editor.MouseEnter += AxisEditorMouseEnter;
        editor.PreviewMouseRightButtonDown += AxisEditorRightClick;
        var menu = new ContextMenu();
        var paste = new MenuItem { Header = "Paste axis values" }; paste.Click += (_, _) => PasteSelectedAxis(isMap, index); menu.Items.Add(paste);
        var autoFill = new MenuItem { Header = "Auto-fill selected axis values" }; autoFill.Click += (_, _) => AutoFillSelectedAxis(isMap); menu.Items.Add(autoFill); editor.ContextMenu = menu;
        editor.LostKeyboardFocus += AxisEditorEdited;
        editor.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Keyboard.ClearFocus(); e.Handled = true; } };
        if (isMap) mapAxisCells[index] = editor; else rpmAxisCells[index] = editor;
        Grid.SetRow(editor, row); Grid.SetColumn(editor, column); table.Children.Add(editor);
    }

    private void AxisEditorMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox editor || editor.Tag is not ValueTuple<bool, int> tag) return;
        var (isMap, index) = tag; var selected = isMap ? selectedMapAxis : selectedRpmAxis; var other = isMap ? selectedRpmAxis : selectedMapAxis;
        start = end = null; selecting = false; RenderBoundaries();
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && selected.Count > 0)
        {
            var anchor = selected.OrderBy(i => Math.Abs(i - index)).First(); selected.Clear(); other.Clear();
            for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++) selected.Add(i);
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            other.Clear(); if (!selected.Add(index)) selected.Remove(index); e.Handled = true;
        }
        else
        {
            selected.Clear(); other.Clear(); selected.Add(index); axisSelecting = true; axisDragIsMap = isMap; axisDragStart = index;
        }
        UpdateAxisSelectionVisuals(); status.Text = $"Selected {selected.Count} {(isMap ? "MAP" : "RPM")} breakpoint{(selected.Count == 1 ? "" : "s")}";
    }

    private void AxisEditorMouseEnter(object sender, MouseEventArgs e)
    {
        if (!axisSelecting || e.LeftButton != MouseButtonState.Pressed || sender is not TextBox { Tag: ValueTuple<bool, int> tag } || tag.Item1 != axisDragIsMap) return;
        var selected = axisDragIsMap ? selectedMapAxis : selectedRpmAxis; selected.Clear();
        for (var i = Math.Min(axisDragStart, tag.Item2); i <= Math.Max(axisDragStart, tag.Item2); i++) selected.Add(i);
        UpdateAxisSelectionVisuals(); status.Text = $"Selected {selected.Count} {(axisDragIsMap ? "MAP" : "RPM")} breakpoints";
    }

    private void AxisEditorRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<bool, int> tag }) return;
        var selected = tag.Item1 ? selectedMapAxis : selectedRpmAxis;
        if (!selected.Contains(tag.Item2))
        {
            selectedMapAxis.Clear(); selectedRpmAxis.Clear(); selected.Add(tag.Item2); UpdateAxisSelectionVisuals();
        }
    }

    private void AutoFillSelectedAxis(bool isMap)
    {
        var selected = (isMap ? selectedMapAxis : selectedRpmAxis).OrderBy(i => i).ToArray();
        if (selected.Length < 2) { Info("Select at least two MAP or RPM scale values before using Auto-fill."); return; }
        if (isMap) AutoFillFuelMapAxis(selected); else autoFillAxis(false, selected);
        status.Text = $"Auto-filled {selected.Length} {(isMap ? "fuel MAP" : "shared RPM")} breakpoints";
    }

    private void PasteSelectedAxis(bool isMap, int focusedIndex)
    {
        var selected = (isMap ? selectedMapAxis : selectedRpmAxis).OrderBy(i => i).ToArray();
        if (isMap) PasteFuelMapAxis(focusedIndex, selected); else pasteAxis(false, focusedIndex, selected);
    }

    private void ClearAxisSelection()
    {
        if (selectedMapAxis.Count == 0 && selectedRpmAxis.Count == 0) return;
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); axisSelecting = false; UpdateAxisSelectionVisuals();
    }

    private void UpdateAxisSelectionVisuals()
    {
        for (var i = 0; i < mapAxisCells.Length; i++) if (mapAxisCells[i] is not null)
        {
            var selected = selectedMapAxis.Contains(i); mapAxisCells[i].Background = new SolidColorBrush(selected ? Color.FromRgb(46, 91, 113) : Color.FromRgb(16, 31, 45));
            mapAxisCells[i].BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(38, 58, 76)); mapAxisCells[i].BorderThickness = new Thickness(selected ? 1.5 : .5);
        }
        for (var i = 0; i < rpmAxisCells.Length; i++) if (rpmAxisCells[i] is not null)
        {
            var selected = selectedRpmAxis.Contains(i); rpmAxisCells[i].Background = new SolidColorBrush(selected ? Color.FromRgb(46, 91, 113) : Color.FromRgb(15, 40, 51));
            rpmAxisCells[i].BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(38, 58, 76)); rpmAxisCells[i].BorderThickness = new Thickness(selected ? 1.5 : .5);
        }
    }

    private void AxisEditorEdited(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (loading || sender is not TextBox editor || editor.Tag is not ValueTuple<bool, int> tag) return;
        var (isMap, index) = tag;
        var currentEditors = isMap ? mapAxisCells : rpmAxisCells;
        if (index < 0 || index >= currentEditors.Length || !ReferenceEquals(editor, currentEditors[index])) return;
        var axis = isMap ? map : rpm;
        if (!double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentValue) || !double.IsFinite(currentValue))
            currentValue = double.NaN;
        if (axisEditOriginalValues.Remove(editor, out var originalValue) && currentValue.Equals(originalValue) && !axis[index].Equals(originalValue))
        {
            editor.Text = FormatExactAxisValue(axis[index]);
            editor.Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51));
            return;
        }
        if (double.IsFinite(currentValue) && currentValue.Equals(axis[index]))
        {
            editor.Text = FormatExactAxisValue(axis[index]);
            editor.Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51));
            return;
        }
        var updatedAxis = double.IsFinite(currentValue)
            ? isMap ? EditFuelMapAxisValue(index, currentValue) : editAxis(false, index, currentValue) : null;
        if (updatedAxis is null)
        {
            editor.Text = FormatExactAxisValue(axis[index]);
            editor.Background = new SolidColorBrush(Color.FromRgb(100, 30, 38));
            status.Text = isMap ? "MAP values must decrease from top to bottom" : "RPM values must increase from left to right";
            return;
        }
        if (isMap) map = updatedAxis; else rpm = updatedAxis;
        var editors = currentEditors;
        for (var i = 0; i < updatedAxis.Length && i < editors.Length; i++)
            if (editors[i] is not null) editors[i].Text = FormatExactAxisValue(updatedAxis[i]);
        ApplyBoundaries();
        var endpoint = index == (isMap ? 0 : updatedAxis.Length - 1) ? "maximum" : index == (isMap ? updatedAxis.Length - 1 : 0) ? "minimum" : null;
        status.Text = endpoint is not null
            ? $"Rescaled the {(isMap ? "fuel MAP" : "shared RPM")} axis to a {updatedAxis[index]:0} {endpoint}"
            : $"Updated {(isMap ? "fuel MAP" : "shared RPM")} breakpoint {index + 1}";
    }
    private static Border ControlGroup(string title, params UIElement[] controls)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = title, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls) row.Children.Add(control);
        content.Children.Add(row);
        return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Margin = new Thickness(0, 0, 7, 0), Child = content };
    }
    private Border DisplayPrecisionGroup()
    {
        UIElement Field(string label, ComboBox box, string tip)
        {
            var field = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 9, 0), ToolTip = tip };
            field.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 10, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            field.Children.Add(box); return field;
        }
        return ControlGroup("VE NUMBER DISPLAY",
            Field("LEADING DIGITS", leadingPrecisionBox, "Show trailing decimals when the VE value has fewer than this many digits before the decimal point."),
            Field("TRAILING DECIMALS", trailingPrecisionBox, "Number of decimal places shown for VE values below the leading-digit threshold."));
    }
    private static ComboBox PrecisionBox(int minimum, int maximum, int selected)
    {
        var box = new ComboBox { Width = 48, Height = 30, Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), Padding = new Thickness(6, 3, 6, 3) };
        for (var value = minimum; value <= maximum; value++) box.Items.Add(new ComboBoxItem { Content = value.ToString(CultureInfo.InvariantCulture), Tag = value, Foreground = Brushes.Black });
        box.SelectedIndex = Math.Clamp(selected - minimum, 0, box.Items.Count - 1); return box;
    }
    private void ApplyDisplayPrecision()
    {
        if (syncingDisplayPrecision || leadingPrecisionBox.SelectedItem is not ComboBoxItem { Tag: int leading } || trailingPrecisionBox.SelectedItem is not ComboBoxItem { Tag: int trailing }) return;
        leadingDisplayDigits = leading; trailingDisplayDecimals = trailing;
        if (ve.Length > 0) { RefreshAll(); ApplyBoundaries(); Save(); status.Text = $"VE display: decimals below {leadingDisplayDigits} leading digits • {trailingDisplayDecimals} trailing decimal place{(trailingDisplayDecimals == 1 ? "" : "s")}"; }
    }
    private static Button Button(string text, RoutedEventHandler click, bool primary = false) { var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 7, 0), Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Segoe UI") }; button.Click += click; return button; }
    private static TextBox MatrixSizeBox(string text) => new() { Text = text, Width = 44, Padding = new Thickness(6), Margin = new Thickness(0, 0, 6, 0), TextAlignment = TextAlignment.Center, Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), BorderThickness = new Thickness(1) };
    private static Color Heat(double t) => Hsl(Math.Clamp(t, 0, 1) * 300, .96, .52);
    private static Color Hsl(double h, double s, double l) { var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2; var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) }; return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)); }
    private static double Ease(double x) { var t = Math.Clamp(x, 0, 1); return t * t * (3 - 2 * t); }
    private static double[,] Resample(double[,] source, int rows, int cols) { var result = new double[rows, cols]; var oldRows = source.GetLength(0); var oldCols = source.GetLength(1); for (var r = 0; r < rows; r++) for (var c = 0; c < cols; c++) { var sr = r * (oldRows - 1d) / (rows - 1); var sc = c * (oldCols - 1d) / (cols - 1); var r0 = (int)Math.Floor(sr); var r1 = Math.Min(oldRows - 1, r0 + 1); var c0 = (int)Math.Floor(sc); var c1 = Math.Min(oldCols - 1, c0 + 1); var a = source[r0, c0] + (source[r0, c1] - source[r0, c0]) * (sc - c0); var b = source[r1, c0] + (source[r1, c1] - source[r1, c0]) * (sc - c0); result[r, c] = a + (b - a) * (sr - r0); } return result; }
    private void Save()
    {
        if (loading || ve.Length == 0) return;
        try
        {
            var rows = new double[ve.GetLength(0)][];
            for (var r = 0; r < rows.Length; r++)
            {
                rows[r] = new double[ve.GetLength(1)];
                for (var c = 0; c < rows[r].Length; c++) rows[r][c] = ve[r, c];
            }
            var state = new FuelState
            {
                Values = rows, MapAxis = map.ToArray(), MapUnit = mapUnit,
                DirectionalOuterToInner = directionalOuterToInner, DirectionalStrength = directionalStrength, DirectionalPasses = directionalPasses,
                RefinementStrength = refinementStrength, RefinementPasses = refinementPasses, AdvancedOptions = advancedSmoothingOptions,
                SelectionOffsetAmount = selectionOffsetAmount, SelectionOffsetIsPercentage = selectionOffsetIsPercentage,
                VeSetup = veSetupSettings, ShowFuelFlow = showFuelFlow,
                LeadingDisplayDigits = leadingDisplayDigits, TrailingDisplayDecimals = trailingDisplayDecimals
            };
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(state));
        }
        catch { }
    }

    private bool Load()
    {
        try
        {
            if (!File.Exists(SavePath)) return false;
            var state = JsonSerializer.Deserialize<FuelState>(File.ReadAllText(SavePath));
            if (state?.Values is null || state.Values.Length == 0 || state.Values.Any(row => row.Length != state.Values[0].Length)) return false;
            if (state.MapAxis is { Length: > 1 })
            {
                mapUnit = state.MapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? "PSI gauge" : "kPa absolute";
                map = state.MapAxis.Length == map.Length
                    ? state.MapAxis.ToArray()
                    : BuildMapAxis(state.MapAxis[^1], state.MapAxis[0], map.Length) ?? map;
            }
            var loaded = new double[state.Values.Length, state.Values[0].Length];
            for (var r = 0; r < loaded.GetLength(0); r++) for (var c = 0; c < loaded.GetLength(1); c++) loaded[r, c] = state.Values[r][c];
            ve = loaded.GetLength(0) == map.Length && loaded.GetLength(1) == rpm.Length ? loaded : Resample(loaded, map.Length, rpm.Length);
            directionalOuterToInner = state.DirectionalOuterToInner; directionalStrength = state.DirectionalStrength; directionalPasses = state.DirectionalPasses;
            refinementStrength = state.RefinementStrength; refinementPasses = state.RefinementPasses; advancedSmoothingOptions = state.AdvancedOptions ?? advancedSmoothingOptions;
            selectionOffsetAmount = state.SelectionOffsetAmount; selectionOffsetIsPercentage = state.SelectionOffsetIsPercentage;
            veSetupSettings = state.VeSetup ?? veSetupSettings; showFuelFlow = state.ShowFuelFlow;
            leadingDisplayDigits = Math.Clamp(state.LeadingDisplayDigits, 1, 4); trailingDisplayDecimals = Math.Clamp(state.TrailingDisplayDecimals, 0, 3);
            syncingDisplayPrecision = true; leadingPrecisionBox.SelectedIndex = leadingDisplayDigits - 1; trailingPrecisionBox.SelectedIndex = trailingDisplayDecimals; syncingDisplayPrecision = false;
            syncingConversion = true; conversionViewBox.IsChecked = showFuelFlow; syncingConversion = false;
            fuelTableTitle.Text = showFuelFlow ? "Fuel Table — Estimated Fuel Flow (lb/hr)" : "Fuel Table — VE (%)";
            return true;
        }
        catch { syncingConversion = false; return false; }
    }
    internal string ExportProjectState()
    {
        Save();
        return File.ReadAllText(SavePath);
    }
    internal static bool ValidateProjectState(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<FuelState>(json);
            if (state?.Values is null || state.Values.Length is < 8 or > 64 || state.Values[0].Length is < 8 or > 64 || state.Values.Any(row => row.Length != state.Values[0].Length)) return false;
            if (state.Values.SelectMany(row => row).Any(value => !double.IsFinite(value))) return false;
            if (state.MapAxis is null || state.MapAxis.Length is < 2 or > 64 || state.MapAxis.Any(value => !double.IsFinite(value))) return false;
            return true;
        }
        catch { return false; }
    }
    internal void ImportProjectState(string json)
    {
        if (!ValidateProjectState(json)) throw new InvalidDataException("The Fueling section is invalid.");
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!); File.WriteAllText(SavePath, json);
        if (!Load()) throw new InvalidDataException("The Fueling section could not be loaded.");
        undoHistory.Clear(); redoHistory.Clear(); ClearFuelSelection(); SyncMapUnitControl(); Build(); ApplyBoundaries(); Save();
        status.Text = "Fueling settings imported";
    }
    private static void Info(string text) => MessageBox.Show(text, "Fueling selection", MessageBoxButton.OK, MessageBoxImage.Information);
    private sealed class FuelState
    {
        public double[][] Values { get; set; } = [];
        public double[] MapAxis { get; set; } = [];
        public string MapUnit { get; set; } = "kPa absolute";
        public bool DirectionalOuterToInner { get; set; } = true;
        public double DirectionalStrength { get; set; } = .65;
        public int DirectionalPasses { get; set; } = 2;
        public double RefinementStrength { get; set; } = .45;
        public int RefinementPasses { get; set; } = 4;
        public AdvancedSmoothingOptions? AdvancedOptions { get; set; }
        public double SelectionOffsetAmount { get; set; } = 1;
        public bool SelectionOffsetIsPercentage { get; set; }
        public VeSetupSettings? VeSetup { get; set; }
        public bool ShowFuelFlow { get; set; }
        public int LeadingDisplayDigits { get; set; } = 3;
        public int TrailingDisplayDecimals { get; set; } = 1;
    }
}
