using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TimingTableCalculator;

public partial class MainWindow : Window
{
    private readonly FuelingPanel fuelingPanel;
    private readonly SandboxPanel sandboxPanel;
    private int RowCount = 31, ColumnCount = 31;
    private static readonly double[] DefaultRpmAxis =
    [
        500, 600, 700, 800, 900, 1000, 1200, 1400, 1600, 1800, 2000,
        2250, 2500, 2750, 3000, 3250, 3500, 3750, 4000, 4250, 4500,
        4750, 5000, 5250, 5500, 5750, 6000, 6250, 6500, 6750, 7000
    ];
    private TextBox[,] valueCells = new TextBox[31, 31];
    private double[,] timingValues = new double[31, 31];
    private int timingLeadingDisplayDigits = 3, timingTrailingDisplayDecimals = 1;
    private bool syncingTimingDisplayPrecision;
    private readonly Dictionary<TextBox, string> cellEditOriginalValues = [];
    private readonly Dictionary<TextBox, double> axisEditOriginalValues = [];
    private TextBox[] rpmAxisCells = new TextBox[31];
    private TextBox[] mapAxisCells = new TextBox[31];
    private TextBlock? mapAxisTitle;
    private readonly HashSet<int> selectedRpmAxis = [];
    private readonly HashSet<int> selectedMapAxis = [];
    private bool? activeAxisIsMap;
    private int? lastAxisIndex;
    private readonly DispatcherTimer autosaveTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool loadingState;
    private bool useCustomHeatColors;
    private Color customLowColor = Color.FromRgb(255, 20, 20);
    private Color customHighColor = Color.FromRgb(255, 0, 235);
    private double boostRetardPerPsi = 1, boostRetardLowMap, boostRetardHighMap = 15;
    private double refinementStrength = .5;
    private int refinementPasses = 3;
    private AdvancedSmoothingOptions advancedSmoothingOptions = new(AdvancedSmoothingAlgorithm.StandardWeighted, .65, 2, false, true, .5);
    private bool directionalOuterToInner = true;
    private double directionalStrength = .65;
    private int directionalPasses = 2;
    private double selectionOffsetAmount = 1;
    private bool selectionOffsetIsPercentage;
    private RegionTimingProfile[] regionTimingProfiles = [];
    private bool blendRegionTiming = true;
    private int verticalRegionSmoothCells = 3, horizontalRegionSmoothCells = 3;
    private readonly Stack<MapSnapshot> undoHistory = [];
    private readonly Stack<MapSnapshot> redoHistory = [];
    private static string AutosavePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimingTableCalculator", "autosave.json");
    private double[] rpmAxis = [];
    private double[] mapAxis = [];
    private (int Row, int Col)? selectionStart;
    private (int Row, int Col)? selectionEnd;
    private readonly HashSet<(int Row, int Col)> pinnedTimingSelection = [];
    private bool selecting;
    private bool axisSelecting, axisDragIsMap;
    private int axisDragStart;
    private int mapUnitIndex;
    private bool syncingMapUnitControls;
    private string MapUnit => mapUnitIndex == 0 ? "kPa absolute" : "PSI gauge";
    private double MapAxisIncrement => mapUnitIndex == 1 ? .1 : 1;
    private string MapAxisFormat => mapUnitIndex == 1 ? "0.0" : "0";
    private double RoundMapValue(double value) => Math.Round(value / MapAxisIncrement) * MapAxisIncrement;
    private string FormatMap(double value) => value.ToString(MapAxisFormat, CultureInfo.InvariantCulture);
    private string FormatTimingDisplayValue(double value) => MagnitudeNumberFormatter.Format(value, timingLeadingDisplayDigits, timingTrailingDisplayDecimals);
    private static double RoundEditableTiming(double value) => Math.Round(value, 3);
    private static string FormatEditableTiming(double value) => RoundEditableTiming(value).ToString("0.###", CultureInfo.InvariantCulture);
    private static string FormatExactAxisValue(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private double idleTransitionRpm = 1200, wotTransitionMap = 85;
    private RegionPointPick regionPointPick;
    private int idleMarkerCol, wotMarkerRow;

    public MainWindow()
    {
        InitializeComponent();
        fuelingPanel = new FuelingPanel(ResizeMatrixFromFuel, AutoFillAxisFromFuel, PasteAxisFromFuel, SetRegionBoundariesFromFuel, EditAxisFromFuel); FuelingHost.Content = fuelingPanel;
        sandboxPanel = new SandboxPanel(); SandboxHost.Content = sandboxPanel;
        HelpHost.Content = new HelpPanel();
        AboutHost.Content = new AboutPanel();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += (_, _) =>
        {
            TableGrid.PreviewMouseLeftButtonUp += (_, _) => { selecting = false; axisSelecting = false; };
            TableGrid.PreviewMouseMove += AxisDrag_MouseMove;
            if (!LoadState()) GenerateTable();
            autosaveTimer.Tick += (_, _) => SaveState(); autosaveTimer.Start();
        };
        Closing += (_, _) => { autosaveTimer.Stop(); SaveState(); };
    }
    private void RecalculateTiming_Click(object sender, RoutedEventArgs e) => RecalculateTimingValues();

    private void Application_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncingMapUnitControls) return;
        ChangeTimingMapUnit(ApplicationBox.SelectedIndex);
    }

    private void TimingMapUnit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncingMapUnitControls) return;
        ChangeTimingMapUnit(TimingMapUnitBox.SelectedIndex);
    }

    private void ChangeTimingMapUnit(int targetIndex)
    {
        if (!IsLoaded || loadingState || targetIndex is not (0 or 1)) return;
        if (targetIndex == mapUnitIndex) return;
        var fromPsi = mapUnitIndex == 1;
        var toPsi = targetIndex == 1;
        mapUnitIndex = targetIndex;
        SyncTimingMapUnitControls();

        for (var index = 0; index < mapAxis.Length; index++)
            mapAxis[index] = RoundMapValue(ConvertMapUnit(mapAxis[index], fromPsi, toPsi));
        wotTransitionMap = ConvertMapUnit(wotTransitionMap, fromPsi, toPsi);
        boostRetardLowMap = ConvertMapUnit(boostRetardLowMap, fromPsi, toPsi);
        boostRetardHighMap = ConvertMapUnit(boostRetardHighMap, fromPsi, toPsi);
        regionTimingProfiles = regionTimingProfiles.Select(profile => profile with
        {
            LowMap = ConvertMapUnit(profile.LowMap, fromPsi, toPsi),
            HighMap = ConvertMapUnit(profile.HighMap, fromPsi, toPsi)
        }).ToArray();

        RefreshTimingMapUnitPresentation();
        if (mapAxis.Length > 0)
        {
            MinMapBox.Text = FormatMap(mapAxis[^1]);
            MaxMapBox.Text = FormatMap(mapAxis[0]);
            for (var index = 0; index < mapAxis.Length; index++)
                if (mapAxisCells[index] is not null) mapAxisCells[index].Text = mapAxis[index].ToString(MapAxisFormat, CultureInfo.InvariantCulture);
        }
        if (double.TryParse(IdleMapBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var idleMap))
            IdleMapBox.Text = FormatMap(ConvertMapUnit(idleMap, fromPsi, toPsi));
        WotMapBox.Text = FormatMap(wotTransitionMap);
        selectedMapAxis.Clear(); if (activeAxisIsMap == true) activeAxisIsMap = null;
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization(); SaveState();
        SyncFuelingAxes();
        StatusText.Text = $"MAP scale converted to {MapUnit}  •  timing values preserved";
    }

    private void SyncTimingMapUnitControls()
    {
        syncingMapUnitControls = true;
        ApplicationBox.SelectedIndex = mapUnitIndex;
        TimingMapUnitBox.SelectedIndex = mapUnitIndex;
        syncingMapUnitControls = false;
    }

    private void RefreshTimingMapUnitPresentation()
    {
        var boosted = mapUnitIndex == 1;
        MinMapLabel.Text = boosted ? "MIN MAP (PSI GAUGE)" : "MIN MAP (kPa ABS)";
        MaxMapLabel.Text = boosted ? "MAX MAP (PSI GAUGE)" : "MAX MAP (kPa ABS)";
        IdleMapLabel.Text = boosted ? "IDLE -> CRUISE MAP (PSI)" : "IDLE -> CRUISE MAP (kPa)";
        WotMapLabel.Text = boosted ? "PART THROTTLE / WOT BOUNDARY (PSI)" : "PART THROTTLE / WOT BOUNDARY (kPa)";
        AxisHelpText.Text = boosted ? "MAP PSI gauge (Y) and RPM (X) axes are editable  •  Drag across timing cells to select" : "MAP kPa absolute (Y) and RPM (X) axes are editable  •  Drag across timing cells to select";
        if (mapAxisTitle is not null) mapAxisTitle.Text = boosted ? "MAP (PSIG)" : "MAP (kPa)";
    }

    private static double ConvertMapUnit(double value, bool fromPsi, bool toPsi)
    {
        if (fromPsi == toPsi) return value;
        return toPsi ? (value - 101.325) / 6.894757293168361 : value * 6.894757293168361 + 101.325;
    }

    private void UpdateRpm_Click(object sender, RoutedEventArgs e) => UpdateRpmAxis(true);
    private void UpdateMap_Click(object sender, RoutedEventArgs e) => UpdateMapAxis(true);

    private void UpdateRpmAxis(bool showErrors)
    {
        if (!TryNumber(MinRpmBox, out var minimum) || !TryNumber(MaxRpmBox, out var maximum) || minimum >= maximum)
        {
            if (showErrors) MessageBox.Show("Enter an RPM minimum lower than the maximum.", "Check RPM range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var scaled = BuildWholeNumberAxis(minimum, maximum, ColumnCount, true, false);
        if (scaled is null) { if (showErrors) MessageBox.Show("The RPM range is too narrow for this matrix size using whole-number breakpoints.", "Check RPM range", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        rpmAxis = scaled;
        for (var i = 0; i < ColumnCount; i++) if (rpmAxisCells[i] is not null) rpmAxisCells[i].Text = rpmAxis[i].ToString("0", CultureInfo.InvariantCulture);
        selectedRpmAxis.Clear(); if (activeAxisIsMap == false) activeAxisIsMap = null;
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization();
        SyncFuelingAxes();
        StatusText.Text = $"RPM axis updated  •  {minimum:0}–{maximum:0} RPM  •  timing preserved";
    }

    private void UpdateMapAxis(bool showErrors)
    {
        if (!TryNumber(MinMapBox, out var minimum) || !TryNumber(MaxMapBox, out var maximum) || minimum >= maximum)
        {
            if (showErrors) MessageBox.Show("Enter a MAP minimum lower than the maximum.", "Check MAP range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var scaled = BuildWholeNumberAxis(minimum, maximum, RowCount, false, true, MapAxisIncrement);
        if (scaled is null) { if (showErrors) MessageBox.Show($"The MAP range is too narrow for this matrix size using {(MapAxisIncrement < 1 ? "0.1 PSI" : "whole-number kPa")} breakpoints.", "Check MAP range", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        mapAxis = scaled;
        for (var i = 0; i < RowCount; i++) if (mapAxisCells[i] is not null) mapAxisCells[i].Text = mapAxis[i].ToString(MapAxisFormat, CultureInfo.InvariantCulture);
        MinMapBox.Text = FormatMap(mapAxis[^1]); MaxMapBox.Text = FormatMap(mapAxis[0]);
        selectedMapAxis.Clear(); if (activeAxisIsMap == true) activeAxisIsMap = null;
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization();
        SyncFuelingAxes();
        StatusText.Text = $"MAP axis updated  •  {FormatMap(mapAxis[^1])}–{FormatMap(mapAxis[0])} {MapUnit}  •  timing preserved";
    }

    private void RecalculateTimingValues()
    {
        if (!TryNumber(LowTimingBox, out var lowTiming) || !TryNumber(HighTimingBox, out var highTiming))
        {
            MessageBox.Show("Enter valid low-MAP and high-MAP timing values.", "Check timing values", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        PushUndo();
        var minimumMap = mapAxis[^1]; var maximumMap = mapAxis[0];
        for (var row = 0; row < RowCount; row++)
        {
            var mapFraction = (mapAxis[row] - minimumMap) / (maximumMap - minimumMap);
            var baseTiming = lowTiming + (highTiming - lowTiming) * mapFraction;
            for (var col = 0; col < ColumnCount; col++)
            {
                var timing = baseTiming - Math.Max(0, col / (double)(ColumnCount - 1) - .72) * 3;
                SetCellValue(row, col, timing);
            }
        }
        StatusText.Text = "Timing surface recalculated";
    }

    private void GenerateTable()
    {
        if (!TryReadInputs(out var minRpm, out var maxRpm, out var minMap, out var maxMap, out var lowTiming, out var highTiming)) return;
        rpmAxis = DefaultRpmAxis.ToArray();
        mapAxis = BuildWholeNumberAxis(minMap, maxMap, RowCount, false, true, MapAxisIncrement) ?? EvenRange(maxMap, minMap, RowCount).Select(RoundMapValue).ToArray();
        selectedRpmAxis.Clear(); selectedMapAxis.Clear(); activeAxisIsMap = null; lastAxisIndex = null;
        selectionStart = selectionEnd = null;
        BuildGrid(lowTiming, highTiming, minMap, maxMap);
        ReadAndApplyRegions(false);
        StatusText.Text = $"{ColumnCount} X × {RowCount} Y  •  {minRpm:0}–{maxRpm:0} RPM  •  {MapUnit}";
    }

    private bool TryReadInputs(out double minRpm, out double maxRpm, out double minMap, out double maxMap, out double lowTiming, out double highTiming)
    {
        var ok = TryNumber(MinRpmBox, out minRpm) & TryNumber(MaxRpmBox, out maxRpm) & TryNumber(MinMapBox, out minMap) & TryNumber(MaxMapBox, out maxMap) & TryNumber(LowTimingBox, out lowTiming) & TryNumber(HighTimingBox, out highTiming);
        if (!ok) { MessageBox.Show("Enter a valid number in every field.", "Check your inputs", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        if (minRpm >= maxRpm || minMap >= maxMap) { MessageBox.Show("Each minimum must be lower than its maximum.", "Check your ranges", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        return true;
    }

    private static bool TryNumber(TextBox box, out double value)
    {
        var valid = double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        box.BorderBrush = new SolidColorBrush(valid ? Color.FromRgb(43, 59, 82) : Color.FromRgb(239, 92, 92)); return valid;
    }

    private void BuildGrid(double lowTiming, double highTiming, double minMap, double maxMap)
    {
        axisEditOriginalValues.Clear();
        valueCells = new TextBox[RowCount, ColumnCount]; timingValues = new double[RowCount, ColumnCount]; rpmAxisCells = new TextBox[ColumnCount]; mapAxisCells = new TextBox[RowCount];
        TableGrid.Children.Clear(); TableGrid.RowDefinitions.Clear(); TableGrid.ColumnDefinitions.Clear();
        TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        for (var col = 0; col < ColumnCount; col++) TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var row = 0; row < RowCount; row++) TableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        for (var row = 0; row < RowCount; row++)
        {
            AddAxisEditor(mapAxis[row], row, 1, true, row);
            var mapFraction = (mapAxis[row] - minMap) / (maxMap - minMap);
            var baseTiming = lowTiming + (highTiming - lowTiming) * mapFraction;
            for (var col = 0; col < ColumnCount; col++)
            {
                var timing = baseTiming - Math.Max(0, col / (double)(ColumnCount - 1) - .72) * 3;
                var cell = CreateValueCell(timing, row, col); valueCells[row, col] = cell;
                Grid.SetRow(cell, row); Grid.SetColumn(cell, col + 2); TableGrid.Children.Add(cell);
            }
        }
        AddAxisTitle(mapUnitIndex == 0 ? "MAP (kPa)" : "MAP (PSIG)", true);
        for (var col = 0; col < ColumnCount; col++) AddAxisEditor(rpmAxis[col], RowCount, col + 2, false, col);
        AddAxisTitle("Engine RPM", false);
        SyncFuelingAxes();
    }

    private void SyncFuelingAxes()
    {
        if (rpmAxis.Length > 0 && mapAxis.Length > 0) fuelingPanel.UpdateAxes(rpmAxis, mapAxis, MapUnit, idleTransitionRpm, wotTransitionMap);
    }

    private TextBox CreateValueCell(double value, int row, int col)
    {
        timingValues[row, col] = RoundEditableTiming(value);
        var cell = new TextBox { Tag = (row, col), Text = FormatTimingDisplayValue(timingValues[row, col]), Foreground = Brushes.Black, Background = TimingBrush(value), BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 57)), BorderThickness = new Thickness(.5), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold, Padding = new Thickness(2) };
        cell.GotKeyboardFocus += (_, _) => { var point = ((int Row, int Col))cell.Tag; cell.Text = FormatEditableTiming(timingValues[point.Row, point.Col]); cellEditOriginalValues[cell] = cell.Text; cell.SelectAll(); }; cell.PreviewMouseLeftButtonDown += Cell_MouseDown; cell.MouseEnter += Cell_MouseEnter;
        cell.PreviewMouseRightButtonDown += TimingCell_RightClick; cell.ContextMenu = CreateTimingContextMenu();
        cell.LostFocus += (_, _) => CompleteCellEdit(cell); cell.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); }; return cell;
    }

    private ContextMenu CreateTimingContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ContextItem("Copy selected", (_, _) => CopySelection())); menu.Items.Add(ContextItem("Paste", (_, _) => PasteSelection()));
        menu.Items.Add(ContextItem("Offset selection…", OffsetSelection_Click));
        menu.Items.Add(new Separator()); menu.Items.Add(ContextItem("Interpolate selection", Interpolate_Click)); menu.Items.Add(ContextItem("Smooth selected…", AdvancedSmooth_Click));
        menu.Items.Add(ContextItem("Smooth rows", SmoothRows_Click)); menu.Items.Add(ContextItem("Smooth columns", SmoothColumns_Click));
        menu.Items.Add(new Separator()); menu.Items.Add(ContextItem("Clear selected", ClearSelectedTiming)); return menu;
    }

    private static MenuItem ContextItem(string header, RoutedEventHandler click) { var item = new MenuItem { Header = header }; item.Click += click; return item; }

    private void TimingCell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<int, int> point }) return;
        if (!IsInsideTimingSelection(point.Item1, point.Item2)) { pinnedTimingSelection.Clear(); selectionStart = selectionEnd = point; selecting = false; UpdateSelection(); }
    }

    private void ClearSelectedTiming(object sender, RoutedEventArgs e)
    {
        var selected = SelectedTimingCells(); if (selected.Count == 0) return;
        PushUndo(); foreach (var cell in selected) SetCellValue(cell.Row, cell.Col, 0);
        UpdateSelection(); SaveState(); StatusText.Text = $"Cleared {selected.Count} selected timing cells";
    }

    private void OffsetSelection_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.Offset")) return;
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right)) return;
        ModelessWindowManager.ShowOrActivate("Timing.Offset", () => new OffsetSelectionWindow(selectionOffsetAmount, selectionOffsetIsPercentage, (direction, amount, percentage) => ApplyTimingOffset(top, bottom, left, right, direction, amount, percentage)) { Owner = this });
    }

    private void ApplyTimingOffset(int top, int bottom, int left, int right, int direction, double amount, bool percentage)
    {
        selectionOffsetAmount = amount; selectionOffsetIsPercentage = percentage; PushUndo(); var selected = SelectedTimingCells();
        if (selected.Count == 0) for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) selected.Add((row, col));
        foreach (var cell in selected)
        {
            var value = timingValues[cell.Row, cell.Col];
            SetCellValue(cell.Row, cell.Col, percentage ? value * (1 + direction * amount / 100) : value + direction * amount);
        }
        UpdateSelection(); SaveState(); StatusText.Text = $"{selected.Count} timing cells {(direction > 0 ? "increased" : "decreased")} by {amount:0.###}{(percentage ? "%" : "°")}";
    }

    private void Cell_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox cell || cell.Tag is not ValueTuple<int, int> point) return;
        if (Keyboard.FocusedElement is TextBox { Tag: ValueTuple<int, int> } focusedCell && !ReferenceEquals(focusedCell, cell))
            CompleteCellEdit(focusedCell);
        if (regionPointPick != RegionPointPick.None)
        {
            SetRegionPointFromCell(regionPointPick, point.Item1, point.Item2);
            regionPointPick = RegionPointPick.None; e.Handled = true; return;
        }
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null; lastAxisIndex = null; axisSelecting = false;
        UpdateAxisSelectionVisuals();
        var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (!control && SelectedTimingCells().Take(2).Count() > 1 && IsInsideTimingSelection(point.Item1, point.Item2))
        {
            selecting = false; cell.Focus(); cell.SelectAll(); e.Handled = true; return;
        }
        if (control) PinActiveTimingSelection(); else pinnedTimingSelection.Clear();
        selectionStart = selectionEnd = point; selecting = true; UpdateSelection(); cell.Focus(); e.Handled = true;
    }

    private bool IsInsideTimingSelection(int row, int col) => pinnedTimingSelection.Contains((row, col)) || TryGetSelectionBounds(out var top, out var bottom, out var left, out var right)
        && row >= top && row <= bottom && col >= left && col <= right;

    private void PinActiveTimingSelection()
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right)) return;
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) pinnedTimingSelection.Add((row, col));
    }

    private HashSet<(int Row, int Col)> SelectedTimingCells()
    {
        var selected = new HashSet<(int Row, int Col)>(pinnedTimingSelection);
        if (TryGetSelectionBounds(out var top, out var bottom, out var left, out var right))
            for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) selected.Add((row, col));
        return selected;
    }

    private void CompleteCellEdit(TextBox cell)
    {
        var changed = cellEditOriginalValues.Remove(cell, out var original) && !string.Equals(original, cell.Text, StringComparison.Ordinal);
        if (cell.Tag is not ValueTuple<int, int> point) return;
        if (!double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { RefreshCellColor(cell); return; }
        value = RoundEditableTiming(value);
        if (!changed) { RefreshCellColor(cell); return; }
        if (!IsInsideTimingSelection(point.Item1, point.Item2)) { SetCellValue(point.Item1, point.Item2, value); SaveState(); return; }
        var selected = SelectedTimingCells(); if (selected.Count == 0) return;
        var before = CaptureSnapshot();
        if (double.TryParse(original, NumberStyles.Float, CultureInfo.InvariantCulture, out var oldValue)) before.Timing[point.Item1][point.Item2] = oldValue;
        PushUndo(before);
        foreach (var selectedCell in selected) SetCellValue(selectedCell.Row, selectedCell.Col, value);
        UpdateSelection(); SaveState(); StatusText.Text = $"Set {selected.Count} selected cells to {FormatEditableTiming(value)}";
    }

    private void SelectAllTimingCells()
    {
        pinnedTimingSelection.Clear();
        selectionStart = (0, 0); selectionEnd = (RowCount - 1, ColumnCount - 1); selecting = false;
        UpdateSelection(); StatusText.Text = $"Selected all {RowCount * ColumnCount} timing cells";
    }

    private void ResizeMatrix_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MatrixXSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var newColumns) || newColumns is < 8 or > 64 ||
            !int.TryParse(MatrixYSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var newRows) || newRows is < 8 or > 64)
        {
            MessageBox.Show("Both X and Y matrix sizes must be between 8 and 64.", "Check matrix size", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (newColumns == ColumnCount && newRows == RowCount) return;
        var oldRows = RowCount; var oldColumns = ColumnCount; var oldRpm = rpmAxis; var oldMap = mapAxis; var oldTiming = ReadTimingValues();
        var resizedRpm = BuildWholeNumberAxis(oldRpm[0], oldRpm[^1], newColumns, true, false);
        var resizedMap = BuildWholeNumberAxis(oldMap[^1], oldMap[0], newRows, false, true, MapAxisIncrement);
        if (resizedRpm is null || resizedMap is null)
        {
            MessageBox.Show("Each axis range must contain at least one whole-number step per breakpoint. Increase the axis range or reduce the matrix size.", "Cannot resize with whole-number axes", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        WorkingRunner.Run(this, () => ResizeMatrixCore(newColumns, newRows, oldRows, oldColumns, oldTiming, resizedRpm, resizedMap));
    }

    private void TimingDisplayPrecision_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncingTimingDisplayPrecision || loadingState || TimingLeadingPrecisionBox is null || TimingTrailingPrecisionBox is null) return;
        if (TimingLeadingPrecisionBox.SelectedItem is not ComboBoxItem leadingItem || TimingTrailingPrecisionBox.SelectedItem is not ComboBoxItem trailingItem) return;
        if (!int.TryParse(leadingItem.Content?.ToString(), out timingLeadingDisplayDigits) || !int.TryParse(trailingItem.Content?.ToString(), out timingTrailingDisplayDecimals)) return;
        for (var row = 0; row < valueCells.GetLength(0); row++)
            for (var col = 0; col < valueCells.GetLength(1); col++)
                if (valueCells[row, col] is not null) RefreshCellColor(valueCells[row, col]);
        SaveState();
        if (StatusText is not null) StatusText.Text = $"Timing display set to {timingLeadingDisplayDigits} leading digits / {timingTrailingDisplayDecimals} trailing decimals";
    }

    private void ResizeMatrixCore(int newColumns, int newRows, int oldRows, int oldColumns, double[,] oldTiming, double[] resizedRpm, double[] resizedMap)
    {
        PushUndo(); rpmAxis = resizedRpm; mapAxis = resizedMap;
        var resizedTiming = new double[newRows, newColumns];
        for (var row = 0; row < newRows; row++) for (var col = 0; col < newColumns; col++)
        {
            var sourceRow = row * (oldRows - 1d) / (newRows - 1); var sourceCol = col * (oldColumns - 1d) / (newColumns - 1);
            var r0 = (int)Math.Floor(sourceRow); var r1 = Math.Min(oldRows - 1, r0 + 1); var c0 = (int)Math.Floor(sourceCol); var c1 = Math.Min(oldColumns - 1, c0 + 1);
            var rowBlend = sourceRow - r0; var colBlend = sourceCol - c0;
            var topValue = oldTiming[r0, c0] + (oldTiming[r0, c1] - oldTiming[r0, c0]) * colBlend;
            var bottomValue = oldTiming[r1, c0] + (oldTiming[r1, c1] - oldTiming[r1, c0]) * colBlend;
            resizedTiming[row, col] = topValue + (bottomValue - topValue) * rowBlend;
        }
        RowCount = newRows; ColumnCount = newColumns; BuildGrid(42, 12, mapAxis[^1], mapAxis[0]);
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) SetCellValue(row, col, resizedTiming[row, col]);
        selectionStart = selectionEnd = null; selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null;
        ReadAndApplyRegions(false); SaveState(); StatusText.Text = $"Matrix resized to {ColumnCount} X × {RowCount} Y; axes and timing resampled";
    }

    private void ResizeMatrixFromFuel(int columns, int rows)
    {
        MatrixXSizeBox.Text = columns.ToString(CultureInfo.InvariantCulture); MatrixYSizeBox.Text = rows.ToString(CultureInfo.InvariantCulture);
        ResizeMatrix_Click(this, new RoutedEventArgs());
    }

    private void Cell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (regionPointPick == RegionPointPick.Both && sender is TextBox { Tag: ValueTuple<int, int> previewPoint })
        {
            PreviewRegionBoundaries(previewPoint.Item1, previewPoint.Item2); return;
        }
        if (!selecting || e.LeftButton != MouseButtonState.Pressed || sender is not TextBox cell || cell.Tag is not ValueTuple<int, int> point) return;
        selectionEnd = point; UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selectedCells = SelectedTimingCells(); if (selectedCells.Count == 0) return;
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++)
        {
            var selected = selectedCells.Contains((row, col));
            var marker = IsRegionMarker(row, col);
            valueCells[row, col].BorderBrush = selected ? Brushes.White : RegionOrMarkerBrush(row, col);
            valueCells[row, col].BorderThickness = new Thickness(selected ? 1.5 : marker ? 3 : .7);
        }
        StatusText.Text = $"Selected {selectedCells.Count} timing cells";
    }

    private void Smooth_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2 || right - left < 2)
        {
            MessageBox.Show("Smooth Selection requires at least 3 rows and 3 columns.", "Select a larger area", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        SmoothSelectionBounds(top, bottom, left, right);
    }

    private void Interpolate_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || !SelectionInterpolator.CanApply(top, bottom, left, right))
        { MessageBox.Show("Select at least three cells in a row or column, or a selection at least 3 × 3.", "Select a larger area", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        PushUndo(); var interpolated = SelectionInterpolator.Apply(ReadTimingValues(), top, bottom, left, right);
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) SetCellValue(row, col, interpolated[row, col]);
        UpdateSelection(); SaveState(); StatusText.Text = $"Interpolated {right - left + 1} × {bottom - top + 1} timing cells from the selected perimeter";
    }

    private void SmoothSelectionBounds(int top, int bottom, int left, int right)
    {
        PushUndo();
        var working = ReadTimingValues();
        for (var pass = 0; pass < 2; pass++)
        {
            var next = (double[,])working.Clone();
            for (var r = top; r <= bottom; r++) for (var c = left; c <= right; c++)
            {
                double weighted = 0, totalWeight = 0;
                for (var rowOffset = -1; rowOffset <= 1; rowOffset++) for (var colOffset = -1; colOffset <= 1; colOffset++)
                {
                    var sampleRow = r + rowOffset; var sampleCol = c + colOffset;
                    if (sampleRow < top || sampleRow > bottom || sampleCol < left || sampleCol > right) continue;
                    var weight = (rowOffset == 0 ? 2 : 1) * (colOffset == 0 ? 2 : 1);
                    weighted += working[sampleRow, sampleCol] * weight; totalWeight += weight;
                }
                var gaussian = weighted / totalWeight;
                next[r, c] = working[r, c] * .35 + gaussian * .65;
            }
            working = next;
        }
        for (var r = top; r <= bottom; r++) for (var c = left; c <= right; c++) SetCellValue(r, c, working[r, c]);
        UpdateSelection(); StatusText.Text = "All selected values blended with isolated two-pass smoothing";
    }

    private double[,] SmoothFrom3D(int top, int bottom, int left, int right)
    {
        selectionStart = (top, left); selectionEnd = (bottom, right); selecting = false;
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null;
        SmoothSelectionBounds(top, bottom, left, right); SaveState();
        StatusText.Text = $"Smoothed 3D selection  •  {right - left + 1} RPM columns × {bottom - top + 1} MAP rows";
        return ReadTimingValues();
    }

    private void Handle3DSelectionAction(SurfaceSelectionAction action, int top, int bottom, int left, int right, IReadOnlyCollection<(int Row, int Col)> selectedCells, Action<double[,]> refresh)
    {
        if (action == SurfaceSelectionAction.Undo) { Undo(); refresh(ReadTimingValues()); return; }
        if (action == SurfaceSelectionAction.Redo) { Redo(); refresh(ReadTimingValues()); return; }
        pinnedTimingSelection.Clear(); foreach (var cell in selectedCells) pinnedTimingSelection.Add(cell);
        selectionStart = (top, left); selectionEnd = (bottom, right); selecting = false;
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null; UpdateSelection();
        void Refresh() => refresh(ReadTimingValues());
        switch (action)
        {
            case SurfaceSelectionAction.Copy: CopySelection(); break;
            case SurfaceSelectionAction.Paste: PasteSelection(); Refresh(); break;
            case SurfaceSelectionAction.Offset:
                ModelessWindowManager.ShowOrActivate("Timing.Offset", () => new OffsetSelectionWindow(selectionOffsetAmount, selectionOffsetIsPercentage, (direction, amount, percentage) => { ApplyTimingOffset(top, bottom, left, right, direction, amount, percentage); Refresh(); }) { Owner = this }); break;
            case SurfaceSelectionAction.Smooth: Smooth_Click(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Interpolate: Interpolate_Click(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Refine:
                ModelessWindowManager.ShowOrActivate("Timing.Refinement", () => new SmoothRefinementWindow(refinementStrength, refinementPasses, dialog => WorkingRunner.Run(this, () => { ApplyRefinement(dialog, top, bottom, left, right); Refresh(); })) { Owner = this }); break;
            case SurfaceSelectionAction.Advanced:
                var timingSelection = selectedCells.ToArray();
                ModelessWindowManager.ShowOrActivate("Timing.AdvancedSmoothing", () => new AdvancedSmoothingWindow(advancedSmoothingOptions, dialog => WorkingRunner.Run(this, () => { ApplyAdvancedSmoothing(dialog, timingSelection); Refresh(); })) { Owner = this }); break;
            case SurfaceSelectionAction.SmoothRows: SmoothRows_Click(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.SmoothColumns: SmoothColumns_Click(this, new RoutedEventArgs()); Refresh(); break;
            case SurfaceSelectionAction.Clear: ClearSelectedTiming(this, new RoutedEventArgs()); Refresh(); break;
        }
    }

    private void Refine_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.Refinement")) return;
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2 || right - left < 2)
        {
            MessageBox.Show("Refinement requires at least 3 rows and 3 columns. The outer perimeter is preserved.", "Select a larger area", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        ModelessWindowManager.ShowOrActivate("Timing.Refinement", () => new SmoothRefinementWindow(refinementStrength, refinementPasses, applied => WorkingRunner.Run(this, () => ApplyRefinement(applied, top, bottom, left, right))) { Owner = this });
    }

    private void ApplyRefinement(SmoothRefinementWindow dialog, int top, int bottom, int left, int right)
    {
        refinementStrength = dialog.Strength; refinementPasses = dialog.Passes; PushUndo();
        var working = ReadTimingValues();
        for (var pass = 0; pass < refinementPasses; pass++)
        {
            var next = (double[,])working.Clone();
            for (var row = top + 1; row < bottom; row++) for (var col = left + 1; col < right; col++)
            {
                var gaussian = (working[row - 1, col - 1] + working[row - 1, col] * 2 + working[row - 1, col + 1]
                    + working[row, col - 1] * 2 + working[row, col] * 4 + working[row, col + 1] * 2
                    + working[row + 1, col - 1] + working[row + 1, col] * 2 + working[row + 1, col + 1]) / 16;
                next[row, col] = working[row, col] + (gaussian - working[row, col]) * refinementStrength;
            }
            working = next;
        }
        for (var row = top + 1; row < bottom; row++) for (var col = left + 1; col < right; col++) SetCellValue(row, col, working[row, col]);
        UpdateSelection(); SaveState();
        StatusText.Text = $"Selection refined  •  {refinementStrength * 100:0}% strength × {refinementPasses} passes  •  perimeter preserved";
    }

    private void AdvancedSmooth_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.AdvancedSmoothing")) return;
        var selected = SelectedTimingCells();
        if (selected.Count == 0)
        { MessageBox.Show("Select one or more timing cells first.", "No cells selected", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        ModelessWindowManager.ShowOrActivate("Timing.AdvancedSmoothing", () => new AdvancedSmoothingWindow(advancedSmoothingOptions, dialog => WorkingRunner.Run(this, () => ApplyAdvancedSmoothing(dialog, selected))) { Owner = this });
    }

    private void ApplyAdvancedSmoothing(AdvancedSmoothingWindow dialog, IReadOnlyCollection<(int Row, int Col)> selected)
    {
        advancedSmoothingOptions = dialog.Options; PushUndo();
        var result = AdvancedSmoother.Apply(ReadTimingValues(), selected, advancedSmoothingOptions);
        foreach (var cell in selected) SetCellValue(cell.Row, cell.Col, result[cell.Row, cell.Col]);
        UpdateSelection(); SaveState(); StatusText.Text = $"Smoothed {selected.Count} selected timing cells  •  {advancedSmoothingOptions.Algorithm}  •  {advancedSmoothingOptions.Passes} passes";
    }

    private void DirectionalSmooth_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.DirectionalSmoothing")) return;
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2 || right - left < 2)
        { MessageBox.Show("Directional smoothing requires at least 3 rows and 3 columns.", "Select a larger area", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        ModelessWindowManager.ShowOrActivate("Timing.DirectionalSmoothing", () => new DirectionalSmoothingWindow(directionalOuterToInner, directionalStrength, directionalPasses, applied => ApplyDirectional(applied, top, bottom, left, right)) { Owner = this });
    }

    private void ApplyDirectional(DirectionalSmoothingWindow dialog, int top, int bottom, int left, int right)
    {
        directionalOuterToInner = dialog.OuterToInner; directionalStrength = dialog.Strength; directionalPasses = dialog.Passes;
        PushUndo(); var result = DirectionalSmoother.Apply(ReadTimingValues(), top, bottom, left, right, dialog.OuterToInner, dialog.Strength, dialog.Passes);
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) SetCellValue(row, col, result[row, col]);
        UpdateSelection(); SaveState(); StatusText.Text = dialog.OuterToInner ? "Smoothed selected timing cells from outer perimeter inward" : "Smoothed selected timing cells from inner core outward";
    }

    private void SmoothColumns_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || bottom - top < 2)
        {
            MessageBox.Show("Select at least 3 rows. Each column's top and bottom selected values are preserved.", "Select a taller area", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        PushUndo(); var source = ReadTimingValues();
        for (var col = left; col <= right; col++) for (var row = top + 1; row < bottom; row++)
        {
            // Use real MAP spacing and an eased curve so the blend meets both anchors gradually.
            var fraction = (mapAxis[top] - mapAxis[row]) / (mapAxis[top] - mapAxis[bottom]);
            fraction = SmoothStep(fraction);
            SetCellValue(row, col, source[top, col] + (source[bottom, col] - source[top, col]) * fraction);
        }
        UpdateSelection(); StatusText.Text = "Columns blended vertically between preserved top and bottom values";
    }

    private void SmoothRows_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right) || right - left < 2)
        {
            MessageBox.Show("Select at least 3 columns. Each row's left and right selected values are preserved.", "Select a wider area", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        PushUndo(); var source = ReadTimingValues();
        for (var row = top; row <= bottom; row++) for (var col = left + 1; col < right; col++)
        {
            // Use real RPM spacing and an eased curve so the blend meets both anchors gradually.
            var fraction = (rpmAxis[col] - rpmAxis[left]) / (rpmAxis[right] - rpmAxis[left]);
            fraction = SmoothStep(fraction);
            SetCellValue(row, col, source[row, left] + (source[row, right] - source[row, left]) * fraction);
        }
        UpdateSelection(); StatusText.Text = "Rows blended horizontally between preserved left and right values";
    }

    private double[,] ReadTimingValues()
    {
        return (double[,])timingValues.Clone();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && regionPointPick != RegionPointPick.None)
        {
            regionPointPick = RegionPointPick.None; TableGrid.Cursor = Cursors.Arrow; SetRegionBoundariesButton.Content = "⌖  Set region boundaries";
            ApplyRegionVisualization(); StatusText.Text = "Boundary selection cancelled"; e.Handled = true; return;
        }
        if (fuelingPanel.IsKeyboardFocusWithin || sandboxPanel.IsKeyboardFocusWithin) return;
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.Z) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y) { Redo(); e.Handled = true; }
        else if (e.Key == Key.A) { SelectAllTimingCells(); e.Handled = true; }
        else if (e.Key == Key.C) { CopySelection(); e.Handled = true; }
        else if (e.Key == Key.V)
        {
            if (TryGetFocusedAxis(out var isMap, out var index)) PasteAxisValues(isMap, index);
            else if (activeAxisIsMap is not null && (activeAxisIsMap.Value ? selectedMapAxis.Count : selectedRpmAxis.Count) > 0)
                PasteAxisValues(activeAxisIsMap.Value, null);
            else PasteSelection();
            e.Handled = true;
        }
    }

    private static bool TryGetFocusedAxis(out bool isMap, out int index)
    {
        isMap = false; index = 0;
        if (Keyboard.FocusedElement is not TextBox { Tag: ValueTuple<bool, int> tag }) return false;
        isMap = tag.Item1; index = tag.Item2; return true;
    }

    private void PasteAxisValues(bool isMap, int? focusedIndex)
    {
        string clipboard;
        try { if (!Clipboard.ContainsText()) return; clipboard = Clipboard.GetText().Trim(); }
        catch (Exception) { MessageBox.Show("The clipboard is currently unavailable. Try again.", "Paste failed", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var tokens = clipboard.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split(['\n', '\t', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return;
        var pasted = new double[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out pasted[i]) || !double.IsFinite(pasted[i]))
            {
                MessageBox.Show("The copied axis column must contain only numeric values.", "Cannot paste axis", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }

        var axis = isMap ? mapAxis : rpmAxis;
        var selected = (isMap ? selectedMapAxis : selectedRpmAxis).OrderBy(i => i).ToArray();
        int[] targets;
        if (selected.Length > 1 && pasted.Length == selected.Length) targets = selected;
        else
        {
            var start = focusedIndex ?? (selected.Length > 0 ? selected[0] : 0);
            if (start + pasted.Length > axis.Length)
            {
                MessageBox.Show($"The copied column has {pasted.Length} values, but only {axis.Length - start} {(isMap ? "MAP" : "RPM")} positions remain from the selected starting point.", "Axis paste is too large", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            targets = Enumerable.Range(start, pasted.Length).ToArray();
        }

        var candidate = axis.ToArray();
        for (var i = 0; i < targets.Length; i++) candidate[targets[i]] = pasted[i];
        for (var i = 1; i < candidate.Length; i++)
            if (isMap ? candidate[i] >= candidate[i - 1] : candidate[i] <= candidate[i - 1])
            {
                MessageBox.Show(isMap ? "Pasted MAP values must decrease from top to bottom." : "Pasted RPM values must increase from left to right.", "Axis order is invalid", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }

        PushUndo();
        var editors = isMap ? mapAxisCells : rpmAxisCells;
        for (var i = 0; i < targets.Length; i++)
        {
            axis[targets[i]] = pasted[i];
            editors[targets[i]].Text = FormatExactAxisValue(pasted[i]);
        }
        if (isMap) { MinMapBox.Text = FormatExactAxisValue(mapAxis[^1]); MaxMapBox.Text = FormatExactAxisValue(mapAxis[0]); }
        selectedMapAxis.Clear(); selectedRpmAxis.Clear();
        foreach (var target in targets) (isMap ? selectedMapAxis : selectedRpmAxis).Add(target);
        activeAxisIsMap = isMap; lastAxisIndex = targets[^1];
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization(); SaveState();
        SyncFuelingAxes();
        StatusText.Text = $"Pasted {targets.Length} {(isMap ? "MAP" : "RPM")} breakpoint values as entered  •  no auto-scaling";
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => CopySelection();
    private void Paste_Click(object sender, RoutedEventArgs e) => PasteSelection();

    private void CopySelection()
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right))
        {
            MessageBox.Show("Drag across one or more timing cells first.", "Select cells to copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var text = new StringBuilder();
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++)
            {
                if (col > left) text.Append('\t');
                text.Append(FormatEditableTiming(timingValues[row, col]));
            }
            if (row < bottom) text.AppendLine();
        }
        try { Clipboard.SetText(text.ToString()); ClearTimingSelection(); StatusText.Text = $"Copied {right - left + 1} × {bottom - top + 1} cells  •  selection cleared"; }
        catch (Exception) { MessageBox.Show("The clipboard is currently unavailable. Try again.", "Copy failed", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void PasteSelection()
    {
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right))
        {
            MessageBox.Show("Select the destination cell or area first.", "Select paste destination", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string text;
        try { if (!Clipboard.ContainsText()) return; text = Clipboard.GetText().Trim(); }
        catch (Exception) { MessageBox.Show("The clipboard is currently unavailable. Try again.", "Paste failed", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(text)) return;

        var rows = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(line.Contains('\t') ? '\t' : ',', StringSplitOptions.TrimEntries)).ToArray();
        if (rows.Length == 0 || rows.Any(row => row.Length == 0)) return;

        // A single clipboard value fills the entire selected rectangle; a matrix starts at its upper-left cell.
        var fillSelection = rows.Length == 1 && rows[0].Length == 1;
        PushUndo();
        var changed = 0;
        if (fillSelection)
        {
            if (!double.TryParse(rows[0][0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) { ShowPasteFormatError(); return; }
            for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) { SetCellValue(row, col, value); changed++; }
        }
        else
        {
            for (var sourceRow = 0; sourceRow < rows.Length && top + sourceRow < RowCount; sourceRow++)
            for (var sourceCol = 0; sourceCol < rows[sourceRow].Length && left + sourceCol < ColumnCount; sourceCol++)
            {
                if (!double.TryParse(rows[sourceRow][sourceCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) { ShowPasteFormatError(); return; }
                SetCellValue(top + sourceRow, left + sourceCol, value); changed++;
            }
            selectionEnd = (Math.Min(RowCount - 1, top + rows.Length - 1), Math.Min(ColumnCount - 1, left + rows.Max(row => row.Length) - 1));
        }
        ClearTimingSelection(); StatusText.Text = $"Pasted {changed} cells  •  selection cleared";
    }

    private bool TryGetSelectionBounds(out int top, out int bottom, out int left, out int right)
    {
        top = bottom = left = right = 0;
        if (selectionStart is null || selectionEnd is null) return false;
        top = Math.Min(selectionStart.Value.Row, selectionEnd.Value.Row); bottom = Math.Max(selectionStart.Value.Row, selectionEnd.Value.Row);
        left = Math.Min(selectionStart.Value.Col, selectionEnd.Value.Col); right = Math.Max(selectionStart.Value.Col, selectionEnd.Value.Col);
        return true;
    }

    private void SetCellValue(int row, int col, double value)
    {
        timingValues[row, col] = RoundEditableTiming(value);
        valueCells[row, col].Text = FormatTimingDisplayValue(timingValues[row, col]);
        RefreshCellColor(valueCells[row, col]);
    }

    private static void ShowPasteFormatError() => MessageBox.Show("Clipboard cells must contain numeric timing values separated by tabs or commas.", "Cannot paste cells", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ApplyRegions_Click(object sender, RoutedEventArgs e) { PushUndo(); ReadAndApplyRegions(true); }

    private void BeginRegionBoundaryPick_Click(object sender, RoutedEventArgs e)
    {
        if (regionPointPick == RegionPointPick.Both)
        {
            regionPointPick = RegionPointPick.None; TableGrid.Cursor = Cursors.Arrow; SetRegionBoundariesButton.Content = "⌖  Set region boundaries";
            ApplyRegionVisualization(); StatusText.Text = "Boundary selection cancelled"; return;
        }
        regionPointPick = RegionPointPick.Both; selectionStart = selectionEnd = null; selecting = false;
        TableGrid.Cursor = Cursors.Cross; SetRegionBoundariesButton.Content = "×  Cancel boundary setting";
        StatusText.Text = "Hover over the timing map to preview both boundaries • click a cell to lock the intersection • Esc cancels";
    }

    private void PickIdlePoint_Click(object sender, RoutedEventArgs e)
    {
        regionPointPick = RegionPointPick.IdleToCruise;
        StatusText.Text = "Click any cell on the RPM column where the vertical Idle boundary should run";
    }

    private void PickWotPoint_Click(object sender, RoutedEventArgs e)
    {
        regionPointPick = RegionPointPick.CruiseToWot;
        StatusText.Text = "Click any cell on the MAP row where the Low / High MAP boundary should run across the table";
    }

    private void SetRegionPointFromCell(RegionPointPick pick, int row, int col)
    {
        PushUndo();
        if (pick is RegionPointPick.IdleToCruise or RegionPointPick.Both)
        {
            IdleRpmBox.Text = rpmAxis[col].ToString("0", CultureInfo.InvariantCulture);
        }
        if (pick is RegionPointPick.CruiseToWot or RegionPointPick.Both)
        {
            WotMapBox.Text = FormatMap(mapAxis[row]);
        }
        selectionStart = selectionEnd = null;
        ReadAndApplyRegions(false);
        TableGrid.Cursor = Cursors.Arrow; SetRegionBoundariesButton.Content = "⌖  Set region boundaries";
        StatusText.Text = pick switch
        {
            RegionPointPick.Both => $"Region boundaries locked at {rpmAxis[col]:0} RPM and {FormatMap(mapAxis[row])} {MapUnit}",
            RegionPointPick.IdleToCruise => $"Idle vertical boundary set at {rpmAxis[col]:0} RPM",
            _ => $"Low / High MAP horizontal boundary set at {FormatMap(mapAxis[row])} {MapUnit}"
        };
    }

    private void ReadAndApplyRegions(bool showErrors)
    {
        var valid = TryNumber(IdleRpmBox, out var idleRpm) & TryNumber(WotMapBox, out var wotMap);
        if (!valid)
        {
            if (showErrors) MessageBox.Show("Enter valid numbers for all region coordinates.", "Check region coordinates", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (rpmAxis.Length == 0 || idleRpm < rpmAxis[0] || idleRpm > rpmAxis[^1] || wotMap < mapAxis[^1] || wotMap > mapAxis[0])
        {
            if (showErrors) MessageBox.Show("Each transition coordinate must fall inside the current RPM and MAP ranges.", "Coordinate outside table", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        idleTransitionRpm = idleRpm; wotTransitionMap = wotMap;
        ApplyRegionVisualization();
        SyncFuelingAxes();
        if (showErrors) StatusText.Text = "Operating regions updated";
    }

    private void ApplyRegionVisualization()
    {
        idleMarkerCol = ClosestIndex(rpmAxis, idleTransitionRpm);
        wotMarkerRow = ClosestIndex(mapAxis, wotTransitionMap);
        RenderRegionVisualization();
    }

    private void PreviewRegionBoundaries(int row, int col)
    {
        idleMarkerCol = col; wotMarkerRow = row; RenderRegionVisualization();
        StatusText.Text = $"Preview: Idle regions through {rpmAxis[col]:0} RPM • High-MAP regions from {FormatMap(mapAxis[row])} {MapUnit} • click to lock";
    }

    private void RenderRegionVisualization()
    {
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++)
        {
            var region = RegionDisplayName(RegionNameAtMarkers(row, col));
            var idlePoint = col == idleMarkerCol; var wotPoint = row == wotMarkerRow;
            var marker = idlePoint && wotPoint ? "  •  Boundary intersection" : idlePoint ? "  •  Idle vertical boundary" : wotPoint ? "  •  Low / High MAP horizontal boundary" : "";
            valueCells[row, col].ToolTip = $"{region}  •  {rpmAxis[col]:0} RPM  •  {FormatMap(mapAxis[row])} {MapUnit}{marker}";
            valueCells[row, col].BorderBrush = idlePoint || wotPoint ? Brushes.Black : RegionBrush(row, col);
            valueCells[row, col].BorderThickness = new Thickness(idlePoint || wotPoint ? 3 : .7);
        }
        if (selectionStart is not null) UpdateSelection();
    }

    private static int ClosestIndex(double[] axis, double value)
    {
        var best = 0; var distance = double.MaxValue;
        for (var i = 0; i < axis.Length; i++) { var current = Math.Abs(axis[i] - value); if (current < distance) { distance = current; best = i; } }
        return best;
    }

    private string RegionName(int row, int col)
    {
        if (rpmAxis[col] <= idleTransitionRpm) return "Idle";
        return mapAxis[row] >= wotTransitionMap ? "WOT" : "Cruise";
    }

    private string RegionNameAtMarkers(int row, int col)
    {
        if (col <= idleMarkerCol) return row <= wotMarkerRow ? "IdleHigh" : "Idle";
        return row <= wotMarkerRow ? "WOT" : "Cruise";
    }

    private static string RegionDisplayName(string region) => region switch
    {
        "Cruise" => "Cruise to Part Throttle",
        "WOT" => "Part Throttle to WOT",
        "IdleHigh" => "Idle High MAP",
        _ => "Idle Low MAP"
    };

    private Brush RegionBrush(int row, int col) => RegionNameAtMarkers(row, col) switch
    {
        "Idle" => new SolidColorBrush(Color.FromRgb(67, 145, 208)),
        "IdleHigh" => new SolidColorBrush(Color.FromRgb(73, 119, 188)),
        "WOT" => new SolidColorBrush(Color.FromRgb(236, 138, 69)),
        _ => new SolidColorBrush(Color.FromRgb(54, 199, 173))
    };

    private bool IsRegionMarker(int row, int col) => col == idleMarkerCol || row == wotMarkerRow;
    private Brush RegionOrMarkerBrush(int row, int col) => IsRegionMarker(row, col) ? Brushes.Black : RegionBrush(row, col);

    private void View3D_Click(object sender, RoutedEventArgs e)
    {
        var values = ReadTimingValues();
        ModelessWindowManager.ShowOrActivate("Timing.3D", () =>
        {
            var window = new Surface3DWindow(values, rpmAxis, mapAxis, MapUnit, useCustomHeatColors, customLowColor, customHighColor, SmoothFrom3D, selectionAction: Handle3DSelectionAction, rpmFormat: "0.########", valueFormatter: FormatTimingDisplayValue) { Owner = this };
            window.Closed += (_, _) =>
            {
                selectionStart = selectionEnd = null; selecting = false;
                if (IsLoaded) { ApplyRegionVisualization(); StatusText.Text = "3D view closed  •  timing selection cleared"; }
            };
            return window;
        });
    }

    private void Colors_Click(object sender, RoutedEventArgs e)
    {
        ModelessWindowManager.ShowOrActivate("Timing.Colors", () => new ColorCustomizerWindow(useCustomHeatColors, customLowColor, customHighColor, ApplyColors) { Owner = this });
    }

    private void ApplyColors(ColorCustomizerWindow dialog)
    {
        useCustomHeatColors = dialog.UseCustomColors; customLowColor = dialog.LowColor; customHighColor = dialog.HighColor;
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) RefreshCellColor(valueCells[row, col]);
        StatusText.Text = useCustomHeatColors ? "Custom heat-map colors applied" : "Default spectrum heat map applied";
        SaveState();
    }

    private void BoostRetard_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.Boost")) return;
        if (mapUnitIndex != 1)
        {
            MessageBox.Show("Boost timing retard is available in Boosted / Forced Induction mode, where MAP is displayed in PSI gauge.", "Boosted mode required", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        if (!TryGetSelectionBounds(out var top, out var bottom, out var left, out var right))
        {
            MessageBox.Show("Select the timing cells that should receive boost retard first.", "Select timing cells", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        ModelessWindowManager.ShowOrActivate("Timing.Boost", () => new BoostRetardWindow(boostRetardPerPsi, boostRetardLowMap, boostRetardHighMap, applied => ApplyBoostOffset(applied, top, bottom, left, right)) { Owner = this });
    }

    private void ApplyBoostOffset(BoostRetardWindow dialog, int top, int bottom, int left, int right)
    {
        boostRetardPerPsi = dialog.RetardPerPsi; boostRetardLowMap = dialog.LowMap; boostRetardHighMap = dialog.HighMap;
        PushUndo(); var changed = 0; var largestChange = 0d;
        for (var row = top; row <= bottom; row++)
        {
            var effectiveBoost = Math.Clamp(mapAxis[row], boostRetardLowMap, boostRetardHighMap) - boostRetardLowMap;
            var timingChange = effectiveBoost * boostRetardPerPsi;
            if (Math.Abs(timingChange) < .0001) continue;
            if (Math.Abs(timingChange) > Math.Abs(largestChange)) largestChange = timingChange;
            for (var col = left; col <= right; col++)
            {
                SetCellValue(row, col, timingValues[row, col] + timingChange); changed++;
            }
        }
        UpdateSelection(); SaveState();
        StatusText.Text = changed == 0 ? "No timing change was applied above the selected start MAP" : $"Boost timing offset applied to {changed} cells  •  maximum {largestChange:+0.0;-0.0;0.0}°";
    }

    private void ConvertTimingToBoosted_Click(object sender, RoutedEventArgs e)
    {
        if (ModelessWindowManager.ActivateIfOpen("Timing.BoostConvert")) return;
        if (mapAxis.Length == 0) return;
        var confirm = MessageBox.Show(this, "Converting to a boosted table keeps the matrix size the same and redistributes the MAP scale to span the new boosted range. This cannot be reversed back to naturally aspirated with Undo, and the undo/redo history will be cleared.\n\nContinue with the conversion?", "Convert to boosted", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;
        var fromPsi = mapUnitIndex == 1;
        ModelessWindowManager.ShowOrActivate("Timing.BoostConvert", () => new BoostConversionWindow("Convert Timing Table to Boosted", mapAxis[^1], mapAxis[0], fromPsi, dialog => ApplyTimingBoostConversion(dialog)) { Owner = this });
    }

    private void ApplyTimingBoostConversion(BoostConversionWindow dialog)
    {
        if (dialog.Result is not { } result) return;
        var fromPsi = mapUnitIndex == 1;
        var currentMaxPsi = fromPsi ? mapAxis[0] : ConvertMapUnit(mapAxis[0], false, true);
        var currentMinPsi = fromPsi ? mapAxis[^1] : ConvertMapUnit(mapAxis[^1], false, true);
        var existingRows = mapAxis.Length;
        var newMinPsi = currentMinPsi;
        var newMaxPsi = result.MaxBoostPsi;

        undoHistory.Clear(); redoHistory.Clear();

        var newRowCount = existingRows;
        var newMapAxis = new double[newRowCount];
        for (var i = 0; i < newRowCount; i++)
        {
            var proportion = i / (double)(newRowCount - 1);
            newMapAxis[i] = Math.Round(newMaxPsi - proportion * (newMaxPsi - newMinPsi), 1);
        }

        var newTiming = new double[newRowCount, ColumnCount];
        var wotMap = currentMaxPsi;
        for (var row = 0; row < newRowCount; row++)
        {
            for (var col = 0; col < ColumnCount; col++)
            {
                var mapValue = newMapAxis[row];
                if (mapValue >= wotMap)
                {
                    var effectiveBoost = Math.Clamp(mapValue, boostRetardLowMap, boostRetardHighMap) - boostRetardLowMap;
                    var timingChange = effectiveBoost * boostRetardPerPsi;
                    newTiming[row, col] = result.Mode == BoostRescaleMode.GenerateBoostedRows ? timingValues[0, col] + timingChange : timingValues[0, col];
                }
                else
                {
                    var closestIdx = 0;
                    var closestDist = double.MaxValue;
                    for (var oldIdx = 0; oldIdx < mapAxis.Length; oldIdx++)
                    {
                        var oldMapPsi = fromPsi ? mapAxis[oldIdx] : ConvertMapUnit(mapAxis[oldIdx], false, true);
                        var dist = Math.Abs(oldMapPsi - mapValue);
                        if (dist < closestDist) { closestDist = dist; closestIdx = oldIdx; }
                    }
                    newTiming[row, col] = timingValues[closestIdx, col];
                }
            }
        }

        mapAxisCells = new TextBox[newRowCount];
        mapAxis = newMapAxis; timingValues = newTiming; RowCount = newRowCount;
        mapUnitIndex = 1; boostRetardHighMap = Math.Max(boostRetardHighMap, mapAxis[0]);

        wotMarkerRow = 0;
        for (var i = 0; i < mapAxis.Length; i++)
            if (mapAxis[i] >= currentMaxPsi) { wotMarkerRow = i; break; }
        wotMarkerRow = Math.Clamp(wotMarkerRow, 0, RowCount - 1);
        wotTransitionMap = mapAxis[wotMarkerRow];

        BuildGrid(42, 12, mapAxis[^1], mapAxis[0]);
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) SetCellValue(row, col, newTiming[row, col]);

        SyncTimingMapUnitControls(); RefreshTimingMapUnitPresentation();
        selectionStart = selectionEnd = null; selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null;
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization(); SaveState();
        StatusText.Text = $"Timing table converted to boosted  •  MAP now {FormatMap(mapAxis[^1])}–{FormatMap(mapAxis[0])} PSI gauge";
        dialog.Close();
    }

    private void RegionTiming_Click(object sender, RoutedEventArgs e)
    {
        if (regionTimingProfiles.Length != 3) regionTimingProfiles = CreateDefaultRegionProfiles();
        ModelessWindowManager.ShowOrActivate("Timing.Regions", () => new RegionTimingWindow(MapUnit, regionTimingProfiles, blendRegionTiming, verticalRegionSmoothCells, horizontalRegionSmoothCells, dialog => WorkingRunner.Run(this, () => ApplyRegionTiming(dialog))) { Owner = this });
    }

    private void ApplyRegionTiming(RegionTimingWindow dialog)
    {
        regionTimingProfiles = dialog.Profiles; blendRegionTiming = dialog.BlendValues;
        verticalRegionSmoothCells = dialog.VerticalSmoothCells; horizontalRegionSmoothCells = dialog.HorizontalSmoothCells; PushUndo();
        var profiles = regionTimingProfiles.ToDictionary(profile => profile.Region, StringComparer.OrdinalIgnoreCase);
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++)
        {
            var profile = profiles[RegionName(row, col)];
            var fraction = Math.Clamp((mapAxis[row] - profile.LowMap) / (profile.HighMap - profile.LowMap), 0, 1);
            SetCellValue(row, col, profile.LowTiming + (profile.HighTiming - profile.LowTiming) * fraction);
        }
        if (blendRegionTiming) SmoothRegionBoundaries();
        SaveState(); StatusText.Text = blendRegionTiming ? "Region profiles filled and boundary transitions smoothed" : "Regions completely filled from their low/high MAP timing profiles";
    }

    private void SmoothRegionBoundaries()
    {
        var source = ReadTimingValues();
        if (idleMarkerCol < ColumnCount - 1)
        {
            var (left, right) = SmoothingBand(idleMarkerCol, verticalRegionSmoothCells, ColumnCount);
            for (var row = 0; row < RowCount; row++)
            {
                var low = source[row, left]; var high = source[row, right];
                for (var col = left + 1; col < right; col++)
                {
                    var fraction = (rpmAxis[col] - rpmAxis[left]) / (rpmAxis[right] - rpmAxis[left]);
                    SetCellValue(row, col, low + (high - low) * SmoothStep(fraction));
                }
            }
        }

        source = ReadTimingValues();
        if (wotMarkerRow < RowCount - 1)
        {
            var (upper, lower) = SmoothingBand(wotMarkerRow, horizontalRegionSmoothCells, RowCount);
            for (var col = 0; col < ColumnCount; col++)
            {
                var highMap = source[upper, col]; var lowMap = source[lower, col];
                for (var row = upper + 1; row < lower; row++)
                {
                    var fraction = (mapAxis[upper] - mapAxis[row]) / (mapAxis[upper] - mapAxis[lower]);
                    SetCellValue(row, col, highMap + (lowMap - highMap) * SmoothStep(fraction));
                }
            }
        }
    }

    private static (int Start, int End) SmoothingBand(int boundaryIndex, int requestedCells, int totalCells)
    {
        // The requested width is the number of cells blended on EACH side of
        // the boundary between boundaryIndex and boundaryIndex + 1. Keep one
        // untouched anchor outside each side wherever the table has room.
        var start = Math.Max(0, boundaryIndex - requestedCells);
        var end = Math.Min(totalCells - 1, boundaryIndex + requestedCells + 1);
        return (start, end);
    }

    private static double SmoothStep(double fraction)
    {
        var t = Math.Clamp(fraction, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private RegionTimingProfile[] CreateDefaultRegionProfiles()
    {
        var lowTiming = double.TryParse(LowTimingBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var low) ? low : 42;
        var highTiming = double.TryParse(HighTimingBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var high) ? high : 12;
        return [new("Idle", mapAxis[^1], lowTiming, mapAxis[0], highTiming), new("Cruise", mapAxis[^1], lowTiming, mapAxis[0], highTiming), new("WOT", mapAxis[^1], lowTiming, mapAxis[0], highTiming)];
    }

    private Brush TimingBrush(double value)
    {
        var t = Math.Clamp((value - 12) / 34, 0, 1);
        if (useCustomHeatColors)
        {
            byte Blend(byte low, byte high) => (byte)Math.Round(low + (high - low) * t);
            return new SolidColorBrush(Color.FromRgb(Blend(customLowColor.R, customHighColor.R), Blend(customLowColor.G, customHighColor.G), Blend(customLowColor.B, customHighColor.B)));
        }
        // Default full-spectrum ignition heat map: red → yellow → green → cyan → blue → magenta.
        return new SolidColorBrush(HslToColor(t * 300, .96, .52));
    }
    private static Color HslToColor(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
    private void RefreshCellColor(TextBox cell)
    {
        if (cell.Tag is ValueTuple<int, int> point && point.Item1 >= 0 && point.Item1 < timingValues.GetLength(0) && point.Item2 >= 0 && point.Item2 < timingValues.GetLength(1))
        {
            var value = timingValues[point.Item1, point.Item2];
            cell.Text = FormatTimingDisplayValue(value); cell.Background = TimingBrush(value);
        }
        else cell.Background = new SolidColorBrush(Color.FromRgb(100, 30, 38));
    }
    private void AddLabel(string text, int row, int column, bool mapLabel)
    {
        var border = new Border { Background = new SolidColorBrush(mapLabel ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51)), BorderBrush = new SolidColorBrush(Color.FromRgb(38, 58, 76)), BorderThickness = new Thickness(.5) };
        border.Child = new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(127, 227, 208)), FontSize = mapLabel ? 11 : 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(border, row); Grid.SetColumn(border, column); TableGrid.Children.Add(border);
    }

    private void AddAxisTitle(string text, bool vertical)
    {
        var title = new TextBlock
        {
            Text = text, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        if (vertical)
        {
            title.LayoutTransform = new RotateTransform(-90); mapAxisTitle = title;
            Grid.SetRow(title, 0); Grid.SetRowSpan(title, RowCount); Grid.SetColumn(title, 0);
        }
        else
        {
            Grid.SetRow(title, RowCount + 1); Grid.SetColumn(title, 2); Grid.SetColumnSpan(title, ColumnCount);
        }
        TableGrid.Children.Add(title);
    }

    private void AddAxisEditor(double value, int row, int column, bool isMap, int index)
    {
        var editor = new TextBox
        {
            Tag = (isMap, index), Text = FormatExactAxisValue(value),
            Foreground = new SolidColorBrush(Color.FromRgb(127, 227, 208)),
            Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 58, 76)), BorderThickness = new Thickness(.5),
            TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = isMap ? 11 : 10, FontWeight = FontWeights.Bold, Padding = new Thickness(2),
            ToolTip = isMap ? $"Edit MAP breakpoint ({MapUnit})" : "Edit RPM breakpoint"
        };
        editor.GotKeyboardFocus += (_, _) => { var current = isMap ? mapAxis[index] : rpmAxis[index]; axisEditOriginalValues[editor] = current; editor.Text = FormatExactAxisValue(current); editor.SelectAll(); };
        editor.PreviewMouseLeftButtonDown += AxisEditor_MouseDown;
        editor.PreviewMouseRightButtonDown += AxisEditor_RightClick;
        var axisMenu = new ContextMenu();
        axisMenu.Items.Add(ContextItem("Paste axis values", (_, _) => PasteAxisValues(isMap, index)));
        axisMenu.Items.Add(ContextItem("Auto-fill selected axis values", AutoFillAxis_Click)); editor.ContextMenu = axisMenu;
        editor.LostKeyboardFocus += AxisEditor_LostFocus;
        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            CommitAxisEditor(editor);
            Keyboard.ClearFocus();
            e.Handled = true;
        };
        if (isMap) mapAxisCells[index] = editor; else rpmAxisCells[index] = editor;
        Grid.SetRow(editor, row); Grid.SetColumn(editor, column); TableGrid.Children.Add(editor);
    }

    private void AxisEditor_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox editor || editor.Tag is not ValueTuple<bool, int> tag) return;
        var (isMap, index) = tag; var selected = isMap ? selectedMapAxis : selectedRpmAxis;
        var other = isMap ? selectedRpmAxis : selectedMapAxis;
        ClearTimingSelection();
        if (activeAxisIsMap != isMap) { selected.Clear(); other.Clear(); lastAxisIndex = null; }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && lastAxisIndex is not null)
        {
            selected.Clear();
            for (var i = Math.Min(lastAxisIndex.Value, index); i <= Math.Max(lastAxisIndex.Value, index); i++) selected.Add(i);
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (!selected.Add(index)) selected.Remove(index); lastAxisIndex = index; e.Handled = true;
        }
        else
        {
            selected.Clear(); other.Clear(); selected.Add(index); lastAxisIndex = index;
            axisSelecting = true; axisDragIsMap = isMap; axisDragStart = index;
            // Leave an ordinary click unhandled so the TextBox receives its native
            // caret/focus behavior and the breakpoint can be typed over immediately.
        }
        activeAxisIsMap = isMap; UpdateAxisSelectionVisuals();
        StatusText.Text = $"Selected {selected.Count} {(isMap ? "MAP" : "RPM")} breakpoint{(selected.Count == 1 ? "" : "s")}";
    }

    private void AxisEditor_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox editor || editor.Tag is not ValueTuple<bool, int> tag) return;
        var (isMap, index) = tag; var selected = isMap ? selectedMapAxis : selectedRpmAxis;
        if (!selected.Contains(index))
        {
            selectedMapAxis.Clear(); selectedRpmAxis.Clear(); selected.Add(index); activeAxisIsMap = isMap; lastAxisIndex = index;
            ClearTimingSelection(); UpdateAxisSelectionVisuals();
        }
    }

    private void AxisDrag_MouseMove(object sender, MouseEventArgs e)
    {
        if (!axisSelecting || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(TableGrid);
        var index = axisDragIsMap ? GridRowIndexAt(point.Y) : GridColumnIndexAt(point.X);
        if (index < 0) return;
        var selected = axisDragIsMap ? selectedMapAxis : selectedRpmAxis;
        selected.Clear();
        for (var i = Math.Min(axisDragStart, index); i <= Math.Max(axisDragStart, index); i++) selected.Add(i);
        lastAxisIndex = index; activeAxisIsMap = axisDragIsMap; UpdateAxisSelectionVisuals();
        StatusText.Text = $"Selected {selected.Count} {(axisDragIsMap ? "MAP" : "RPM")} breakpoints";
    }

    private int GridRowIndexAt(double position)
    {
        var offset = 0d;
        for (var i = 0; i < RowCount; i++)
        {
            var size = TableGrid.RowDefinitions[i].ActualHeight;
            if (position >= offset && position < offset + size) return i;
            offset += size;
        }
        return -1;
    }


    private int GridColumnIndexAt(double position)
    {
        var offset = TableGrid.ColumnDefinitions[0].ActualWidth + TableGrid.ColumnDefinitions[1].ActualWidth;
        for (var i = 0; i < ColumnCount; i++)
        {
            var size = TableGrid.ColumnDefinitions[i + 2].ActualWidth;
            if (position >= offset && position < offset + size) return i;
            offset += size;
        }
        return -1;
    }

    private void ClearTimingSelection()
    {
        if (selectionStart is null && selectionEnd is null && pinnedTimingSelection.Count == 0) return;
        pinnedTimingSelection.Clear();
        selectionStart = selectionEnd = null; selecting = false;
        ApplyRegionVisualization();
    }

    private void SelectAllMap_Click(object sender, RoutedEventArgs e) => SelectEntireAxis(true);
    private void SelectAllRpm_Click(object sender, RoutedEventArgs e) => SelectEntireAxis(false);

    private void SelectEntireAxis(bool isMap)
    {
        ClearTimingSelection();
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); var selected = isMap ? selectedMapAxis : selectedRpmAxis;
        var count = isMap ? RowCount : ColumnCount;
        for (var i = 0; i < count; i++) selected.Add(i);
        activeAxisIsMap = isMap; lastAxisIndex = count - 1; UpdateAxisSelectionVisuals();
        StatusText.Text = $"Selected all {count} {(isMap ? "MAP" : "RPM")} breakpoints";
    }

    private void UpdateAxisSelectionVisuals()
    {
        for (var i = 0; i < RowCount; i++) if (mapAxisCells[i] is not null) { var selected = selectedMapAxis.Contains(i); mapAxisCells[i].Background = new SolidColorBrush(selected ? Color.FromRgb(46, 91, 113) : Color.FromRgb(16, 31, 45)); mapAxisCells[i].BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(255, 255, 255) : Color.FromRgb(38, 58, 76)); mapAxisCells[i].BorderThickness = new Thickness(selected ? 1.5 : .5); }
        for (var i = 0; i < ColumnCount; i++) if (rpmAxisCells[i] is not null) { var selected = selectedRpmAxis.Contains(i); rpmAxisCells[i].Background = new SolidColorBrush(selected ? Color.FromRgb(46, 91, 113) : Color.FromRgb(15, 40, 51)); rpmAxisCells[i].BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(255, 255, 255) : Color.FromRgb(38, 58, 76)); rpmAxisCells[i].BorderThickness = new Thickness(selected ? 1.5 : .5); }
    }

    private void AutoFillAxis_Click(object sender, RoutedEventArgs e)
    {
        if (activeAxisIsMap is null) { MessageBox.Show("Select MAP or RPM breakpoints first.", "Select an axis", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var isMap = activeAxisIsMap.Value; var selected = (isMap ? selectedMapAxis : selectedRpmAxis).OrderBy(i => i).ToArray();
        if (selected.Length < 2) { MessageBox.Show("Select at least two axis values. Ctrl-click individual values or Shift-click a range.", "Select more values", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var axis = isMap ? mapAxis : rpmAxis; var candidate = (double[])axis.Clone();
        var minimum = selected.Min(i => axis[i]); var maximum = selected.Max(i => axis[i]);
        var wholeValues = BuildWholeNumberAxis(minimum, maximum, selected.Length, !isMap, isMap, isMap ? MapAxisIncrement : 1);
        if (wholeValues is null)
        {
            MessageBox.Show($"The selected range is too narrow to assign a unique {(isMap && MapAxisIncrement < 1 ? "0.1 PSI value" : "whole number")} to every breakpoint.", "Cannot auto-fill axis", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        for (var position = 0; position < selected.Length; position++) candidate[selected[position]] = wholeValues[position];
        for (var i = 1; i < axis.Length; i++)
        {
            if (isMap ? candidate[i] >= candidate[i - 1] : candidate[i] <= candidate[i - 1])
            {
                MessageBox.Show("That fill would cross an unselected neighboring value. Select the full range between the endpoints and try again.", "Cannot auto-fill axis", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
        }
        PushUndo();
        var editors = isMap ? mapAxisCells : rpmAxisCells;
        foreach (var index in selected) { axis[index] = candidate[index]; editors[index].Text = axis[index].ToString(isMap ? MapAxisFormat : "0", CultureInfo.InvariantCulture); }
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization();
        SyncFuelingAxes();
        StatusText.Text = $"Auto-filled {selected.Length} {(isMap ? "MAP" : "RPM")} breakpoints from {minimum:0.0} to {maximum:0.0}";
    }

    private void AutoFillAxisFromFuel(bool isMap, int[] selectedIndices)
    {
        if (isMap) return;
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); var selected = isMap ? selectedMapAxis : selectedRpmAxis;
        foreach (var index in selectedIndices.Where(index => index >= 0 && index < (isMap ? RowCount : ColumnCount))) selected.Add(index);
        activeAxisIsMap = isMap; lastAxisIndex = selected.Count > 0 ? selected.Max() : null; UpdateAxisSelectionVisuals();
        AutoFillAxis_Click(this, new RoutedEventArgs());
    }

    private void PasteAxisFromFuel(bool isMap, int? focusedIndex, int[] selectedIndices)
    {
        if (isMap) return;
        selectedMapAxis.Clear(); selectedRpmAxis.Clear(); var selected = isMap ? selectedMapAxis : selectedRpmAxis;
        foreach (var index in selectedIndices.Where(index => index >= 0 && index < (isMap ? RowCount : ColumnCount))) selected.Add(index);
        activeAxisIsMap = isMap; lastAxisIndex = selected.Count > 0 ? selected.Max() : focusedIndex; UpdateAxisSelectionVisuals();
        PasteAxisValues(isMap, focusedIndex);
    }

    private double[]? EditAxisFromFuel(bool isMap, int index, double value)
    {
        if (isMap) return null;
        var updated = UpdateSharedAxisValue(false, index, value, false);
        if (updated is not null)
            StatusText.Text = index == (isMap ? 0 : updated.Length - 1) || index == (isMap ? updated.Length - 1 : 0)
                ? $"Rescaled the shared {(isMap ? "MAP" : "RPM")} axis from Fueling"
                : $"Updated shared {(isMap ? "MAP" : "RPM")} breakpoint {index + 1} from Fueling";
        return updated;
    }

    private double[]? UpdateSharedAxisValue(bool isMap, int index, double value, bool syncFueling)
    {
        var axis = isMap ? mapAxis : rpmAxis;
        if (index < 0 || index >= axis.Length || !double.IsFinite(value)) return null;
        value = isMap ? RoundMapValue(value) : Math.Round(value);
        var minimumIndex = isMap ? axis.Length - 1 : 0;
        var maximumIndex = isMap ? 0 : axis.Length - 1;
        var changingMinimum = index == minimumIndex;
        var changingMaximum = index == maximumIndex;
        if (changingMaximum)
        {
            var minimum = isMap ? axis[^1] : axis[0];
            if (value <= minimum) return null;
        }
        else if (changingMinimum)
        {
            var maximum = isMap ? axis[0] : axis[^1];
            if (value >= maximum) return null;
        }
        else if (index > 0 && (isMap ? value >= axis[index - 1] : value <= axis[index - 1]) ||
                 index < axis.Length - 1 && (isMap ? value <= axis[index + 1] : value >= axis[index + 1]))
            return null;

        double[]? rebuilt = null;
        if (changingMinimum || changingMaximum)
        {
            var rawMinimum = changingMinimum ? value : isMap ? axis[^1] : axis[0];
            var rawMaximum = changingMaximum ? value : isMap ? axis[0] : axis[^1];
            var minimum = isMap ? RoundMapValue(rawMinimum) : Math.Round(rawMinimum);
            var maximum = isMap ? RoundMapValue(rawMaximum) : Math.Round(rawMaximum);
            rebuilt = BuildWholeNumberAxis(minimum, maximum, axis.Length, !isMap, isMap, isMap ? MapAxisIncrement : 1);
            if (rebuilt is null) return null;
        }
        if (Math.Abs(axis[index] - value) < .000001 && rebuilt is null) return axis.ToArray();
        PushUndo();
        if (rebuilt is not null)
        {
            Array.Copy(rebuilt, axis, axis.Length);
        }
        else axis[index] = value;

        var timingEditors = isMap ? mapAxisCells : rpmAxisCells;
        for (var position = 0; position < axis.Length && position < timingEditors.Length; position++)
            if (timingEditors[position] is not null) timingEditors[position].Text = axis[position].ToString(isMap ? MapAxisFormat : "0", CultureInfo.InvariantCulture);
        if (isMap)
        {
            MinMapBox.Text = FormatMap(mapAxis[^1]);
            MaxMapBox.Text = FormatMap(mapAxis[0]);
        }
        else
        {
            MinRpmBox.Text = rpmAxis[0].ToString("0", CultureInfo.InvariantCulture);
            MaxRpmBox.Text = rpmAxis[^1].ToString("0", CultureInfo.InvariantCulture);
        }
        UpdateAxisSelectionVisuals(); ApplyRegionVisualization();
        if (syncFueling) SyncFuelingAxes();
        SaveState(); return axis.ToArray();
    }

    private void SetRegionBoundariesFromFuel(int row, int col)
    {
        if (row < 0 || row >= RowCount || col < 0 || col >= ColumnCount) return;
        PushUndo(); IdleRpmBox.Text = rpmAxis[col].ToString("0", CultureInfo.InvariantCulture); WotMapBox.Text = FormatMap(mapAxis[row]);
        selectionStart = selectionEnd = null; ReadAndApplyRegions(false);
        StatusText.Text = $"Region boundaries locked at {rpmAxis[col]:0} RPM and {FormatMap(mapAxis[row])} {MapUnit}";
    }

    private static double ScaledRpmFraction(int position, int count)
    {
        if (count <= 1) return 0;
        // Resample the reference breakpoint pattern so any RPM range retains dense
        // low-speed resolution and progressively wider mid/high-speed spacing.
        var referencePosition = position * (DefaultRpmAxis.Length - 1d) / (count - 1);
        var lower = (int)Math.Floor(referencePosition); var upper = Math.Min(DefaultRpmAxis.Length - 1, lower + 1);
        var blend = referencePosition - lower;
        var referenceValue = DefaultRpmAxis[lower] + (DefaultRpmAxis[upper] - DefaultRpmAxis[lower]) * blend;
        return (referenceValue - DefaultRpmAxis[0]) / (DefaultRpmAxis[^1] - DefaultRpmAxis[0]);
    }

    private static double[]? BuildWholeNumberAxis(double minimum, double maximum, int count, bool scaledRpm, bool descending, double increment = 1)
    {
        increment = Math.Max(.000001, increment); minimum = Math.Round(minimum / increment) * increment; maximum = Math.Round(maximum / increment) * increment;
        if (count < 2 || maximum - minimum + .0000001 < increment * (count - 1)) return null;
        var ascending = new double[count];
        for (var position = 0; position < count; position++)
        {
            var fraction = scaledRpm ? ScaledRpmFraction(position, count) : position / (double)(count - 1);
            var ideal = Math.Round((minimum + (maximum - minimum) * fraction) / increment) * increment;
            var lowerBound = position == 0 ? minimum : ascending[position - 1] + increment;
            var upperBound = maximum - increment * (count - 1 - position);
            ascending[position] = Math.Round(Math.Clamp(ideal, lowerBound, upperBound) / increment) * increment;
        }
        if (!descending) return ascending;
        Array.Reverse(ascending); return ascending;
    }

    private void AxisEditor_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox editor) CommitAxisEditor(editor);
    }

    private bool CommitAxisEditor(TextBox editor)
    {
        if (editor.Tag is not ValueTuple<bool, int> tag) return false;
        var (isMap, index) = tag; var editors = isMap ? mapAxisCells : rpmAxisCells;
        if (index < 0 || index >= editors.Length || !ReferenceEquals(editor, editors[index])) return true;
        var axis = isMap ? mapAxis : rpmAxis;
        if (!double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentValue) || !double.IsFinite(currentValue))
            currentValue = double.NaN;
        if (axisEditOriginalValues.Remove(editor, out var originalValue) && currentValue.Equals(originalValue) && !axis[index].Equals(originalValue))
        {
            editor.Text = FormatExactAxisValue(axis[index]);
            editor.Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51));
            return true;
        }
        if (double.IsFinite(currentValue) && currentValue.Equals(axis[index]))
        {
            editor.Text = FormatExactAxisValue(axis[index]);
            editor.Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51));
            return true;
        }
        var updated = double.IsFinite(currentValue) ? UpdateSharedAxisValue(isMap, index, currentValue, true) : null;
        if (updated is null)
        {
            editor.Text = axis[index].ToString(isMap ? MapAxisFormat : "0", CultureInfo.InvariantCulture);
            editor.Background = new SolidColorBrush(Color.FromRgb(100, 30, 38));
            StatusText.Text = isMap
                ? $"MAP value must remain between its neighboring breakpoints ({MapUnit})"
                : "RPM value must remain between its neighboring breakpoints";
            return false;
        }
        editor.Background = new SolidColorBrush(isMap ? Color.FromRgb(16, 31, 45) : Color.FromRgb(15, 40, 51));
        var endpoint = index == (isMap ? 0 : updated.Length - 1) ? "maximum" : index == (isMap ? updated.Length - 1 : 0) ? "minimum" : null;
        StatusText.Text = endpoint is not null
            ? $"Rescaled the {(isMap ? "MAP" : "RPM")} axis to a {(isMap ? FormatMap(updated[index]) : updated[index].ToString("0", CultureInfo.InvariantCulture))} {endpoint}"
            : $"Updated {(isMap ? "MAP" : "RPM")} breakpoint {index + 1}";
        return true;
    }
    private static double[] EvenRange(double start, double end, int count) => Enumerable.Range(0, count).Select(i => start + (end - start) * i / (count - 1)).ToArray();
    private void Clear_Click(object sender, RoutedEventArgs e) { PushUndo(); for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) SetCellValue(row, col, 0); StatusText.Text = "Timing values cleared"; }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (rpmAxis.Length == 0) return; var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "timing-table.csv" }; if (dialog.ShowDialog() != true) return;
        var csv = new StringBuilder();
        for (var row = 0; row < RowCount; row++) { csv.Append(FormatExactAxisValue(mapAxis[row])); for (var col = 0; col < ColumnCount; col++) csv.Append(',').Append(FormatEditableTiming(timingValues[row, col])); csv.AppendLine(); }
        csv.Append("Engine RPM"); foreach (var rpm in rpmAxis) csv.Append(',').Append(FormatExactAxisValue(rpm)); csv.AppendLine();
        File.WriteAllText(dialog.FileName, csv.ToString()); StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)}";
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (rpmAxis.Length == 0) return;
        var dialog = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "timing-table.xlsx" };
        if (dialog.ShowDialog() != true) return;

        var timing = ReadTimingValues();

        var lowColor = useCustomHeatColors ? customLowColor : HslToColor(0, .96, .52);
        var middleColor = useCustomHeatColors
            ? Color.FromRgb((byte)((customLowColor.R + customHighColor.R) / 2), (byte)((customLowColor.G + customHighColor.G) / 2), (byte)((customLowColor.B + customHighColor.B) / 2))
            : HslToColor(150, .96, .52);
        var highColor = useCustomHeatColors ? customHighColor : HslToColor(300, .96, .52);

        ExcelTimingExporter.Export(dialog.FileName, rpmAxis, mapAxis, timing, MapUnit, lowColor, middleColor, highColor, useCustomHeatColors, valueNumberFormat: MagnitudeNumberFormatter.ExcelFormat(timingLeadingDisplayDigits, timingTrailingDisplayDecimals));
        StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)} with heat-map formatting";
    }

    private void ExportMapSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Map Lab settings (*.map)|*.map", DefaultExt = ".map", AddExtension = true, FileName = "map-lab-settings.map" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var project = new MapLabSettingsFile
            {
                ExportedUtc = DateTimeOffset.UtcNow,
                Timing = ParseJsonElement(ExportTimingProjectState()),
                Fueling = ParseJsonElement(fuelingPanel.ExportProjectState()),
                Sandbox = ParseJsonElement(sandboxPanel.ExportProjectState())
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
            SettingsStatusText.Text = $"Exported {Path.GetFileName(dialog.FileName)}";
            StatusText.Text = "Complete Map Lab settings exported";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Settings export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportMapSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Map Lab settings (*.map)|*.map", DefaultExt = ".map", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 25 * 1024 * 1024) throw new InvalidDataException("The selected .map file is too large to be a Map Lab settings file.");
            var project = JsonSerializer.Deserialize<MapLabSettingsFile>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("The selected file is empty or invalid.");
            if (project.Format != MapLabSettingsFile.ExpectedFormat || project.Version != MapLabSettingsFile.CurrentVersion) throw new InvalidDataException("This file is not a supported Map Lab settings file.");
            if (project.Timing.ValueKind != JsonValueKind.Object || project.Fueling.ValueKind != JsonValueKind.Object || project.Sandbox.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The settings file is missing one or more table sections.");
            var timingJson = project.Timing.GetRawText(); var fuelingJson = project.Fueling.GetRawText(); var sandboxJson = project.Sandbox.GetRawText();
            if (!ValidateTimingProjectState(timingJson) || !FuelingPanel.ValidateProjectState(fuelingJson) || !SandboxPanel.ValidateProjectState(sandboxJson)) throw new InvalidDataException("One or more table sections contain invalid dimensions or values.");
            if (MessageBox.Show(this, "Importing replaces the current Ignition Timing, Fueling, and Map Sandbox settings. Continue?", "Import Map Lab settings", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var timingBackup = ExportTimingProjectState(); var fuelingBackup = fuelingPanel.ExportProjectState(); var sandboxBackup = sandboxPanel.ExportProjectState();
            var success = false; string? failure = null;
            WorkingRunner.Run(this, () =>
            {
                try
                {
                    ImportTimingProjectState(timingJson); fuelingPanel.ImportProjectState(fuelingJson); sandboxPanel.ImportProjectState(sandboxJson);
                    undoHistory.Clear(); redoHistory.Clear(); success = true;
                }
                catch (Exception exception)
                {
                    failure = exception.Message;
                    try { ImportTimingProjectState(timingBackup); fuelingPanel.ImportProjectState(fuelingBackup); sandboxPanel.ImportProjectState(sandboxBackup); }
                    catch { failure += " The previous workspace could not be fully restored."; }
                }
            }, "Importing Map Lab settings....");
            if (!success) throw new InvalidDataException(failure ?? "The settings file could not be imported.");
            SettingsStatusText.Text = $"Imported {Path.GetFileName(dialog.FileName)}"; StatusText.Text = "Complete Map Lab settings imported";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Settings import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static JsonElement ParseJsonElement(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
    private string ExportTimingProjectState() { SaveState(); return File.ReadAllText(AutosavePath); }
    private static bool ValidateTimingProjectState(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<AutosaveState>(json);
            return state is not null && state.RpmAxis.Length is >= 8 and <= 64 && state.MapAxis.Length is >= 8 and <= 64
                && state.Timing.Length == state.MapAxis.Length && state.Timing.All(row => row.Length == state.RpmAxis.Length)
                && state.RpmAxis.All(double.IsFinite) && state.MapAxis.All(double.IsFinite) && state.Timing.SelectMany(row => row).All(double.IsFinite);
        }
        catch { return false; }
    }
    private void ImportTimingProjectState(string json)
    {
        if (!ValidateTimingProjectState(json)) throw new InvalidDataException("The Ignition Timing section is invalid.");
        Directory.CreateDirectory(Path.GetDirectoryName(AutosavePath)!); File.WriteAllText(AutosavePath, json);
        if (!LoadState()) throw new InvalidDataException("The Ignition Timing section could not be loaded.");
        undoHistory.Clear(); redoHistory.Clear(); SaveState();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        if (undoHistory.Count == 0) { StatusText.Text = "Nothing to undo"; return; }
        WorkingRunner.Run(this, () =>
        {
            redoHistory.Push(CaptureSnapshot()); RestoreSnapshot(undoHistory.Pop()); StatusText.Text = "Change undone";
        });
    }

    private void Redo()
    {
        if (redoHistory.Count == 0) { StatusText.Text = "Nothing to redo"; return; }
        WorkingRunner.Run(this, () =>
        {
            undoHistory.Push(CaptureSnapshot()); RestoreSnapshot(redoHistory.Pop()); StatusText.Text = "Change redone";
        });
    }

    private void PushUndo(MapSnapshot? snapshot = null)
    {
        if (loadingState || rpmAxis.Length != ColumnCount || mapAxis.Length != RowCount) return;
        if (undoHistory.Count >= 50)
        {
            var retained = undoHistory.Reverse().Skip(1).ToArray(); undoHistory.Clear();
            foreach (var item in retained) undoHistory.Push(item);
        }
        undoHistory.Push(snapshot ?? CaptureSnapshot()); redoHistory.Clear();
    }

    private MapSnapshot CaptureSnapshot()
    {
        var timing = new double[RowCount][];
        for (var row = 0; row < RowCount; row++)
        {
            timing[row] = new double[ColumnCount];
            for (var col = 0; col < ColumnCount; col++) timing[row][col] = timingValues[row, col];
        }
        return new MapSnapshot(rpmAxis.ToArray(), mapAxis.ToArray(), timing, idleTransitionRpm, wotTransitionMap, LowTimingBox.Text, HighTimingBox.Text);
    }

    private void RestoreSnapshot(MapSnapshot snapshot)
    {
        loadingState = true;
        try
        {
            ColumnCount = snapshot.RpmAxis.Length; RowCount = snapshot.MapAxis.Length;
            MatrixXSizeBox.Text = ColumnCount.ToString(CultureInfo.InvariantCulture); MatrixYSizeBox.Text = RowCount.ToString(CultureInfo.InvariantCulture);
            rpmAxis = snapshot.RpmAxis.ToArray(); mapAxis = snapshot.MapAxis.ToArray();
            idleTransitionRpm = snapshot.IdleRpm; wotTransitionMap = snapshot.WotMap;
            IdleRpmBox.Text = idleTransitionRpm.ToString("0", CultureInfo.InvariantCulture);
            WotMapBox.Text = wotTransitionMap.ToString("0.0", CultureInfo.InvariantCulture);
            LowTimingBox.Text = snapshot.LowTiming; HighTimingBox.Text = snapshot.HighTiming;
            var low = double.TryParse(snapshot.LowTiming, NumberStyles.Float, CultureInfo.InvariantCulture, out var lowValue) ? lowValue : 42;
            var high = double.TryParse(snapshot.HighTiming, NumberStyles.Float, CultureInfo.InvariantCulture, out var highValue) ? highValue : 12;
            BuildGrid(low, high, mapAxis[^1], mapAxis[0]);
            for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) SetCellValue(row, col, snapshot.Timing[row][col]);
            selectionStart = selectionEnd = null; selectedMapAxis.Clear(); selectedRpmAxis.Clear(); activeAxisIsMap = null;
            ApplyRegionVisualization();
        }
        finally { loadingState = false; }
    }

    private sealed record MapSnapshot(double[] RpmAxis, double[] MapAxis, double[][] Timing, double IdleRpm, double WotMap, string LowTiming, string HighTiming);

    private void SaveState()
    {
        if (!IsLoaded || loadingState || rpmAxis.Length != ColumnCount || mapAxis.Length != RowCount) return;
        try
        {
            var timing = new double[RowCount][];
            for (var row = 0; row < RowCount; row++)
            {
                timing[row] = new double[ColumnCount];
                for (var col = 0; col < ColumnCount; col++) timing[row][col] = timingValues[row, col];
            }
            var state = new AutosaveState
            {
                ApplicationIndex = mapUnitIndex,
                MinRpm = MinRpmBox.Text, MaxRpm = MaxRpmBox.Text, MinMap = MinMapBox.Text, MaxMap = MaxMapBox.Text,
                LowTiming = LowTimingBox.Text, HighTiming = HighTimingBox.Text,
                IdleRpm = IdleRpmBox.Text, IdleMap = IdleMapBox.Text, WotRpm = WotRpmBox.Text, WotMap = WotMapBox.Text,
                RpmAxis = rpmAxis, MapAxis = mapAxis, Timing = timing,
                UseCustomHeatColors = useCustomHeatColors, LowHeatColor = customLowColor.ToString(), HighHeatColor = customHighColor.ToString(),
                BoostRetardPerPsi = boostRetardPerPsi, BoostRetardLowMap = boostRetardLowMap, BoostRetardHighMap = boostRetardHighMap,
                RefinementStrength = refinementStrength, RefinementPasses = refinementPasses, AdvancedOptions = advancedSmoothingOptions,
                DirectionalOuterToInner = directionalOuterToInner, DirectionalStrength = directionalStrength, DirectionalPasses = directionalPasses,
                SelectionOffsetAmount = selectionOffsetAmount, SelectionOffsetIsPercentage = selectionOffsetIsPercentage,
                RegionTimingProfiles = regionTimingProfiles,
                BlendRegionTiming = blendRegionTiming,
                VerticalRegionSmoothCells = verticalRegionSmoothCells,
                HorizontalRegionSmoothCells = horizontalRegionSmoothCells,
                TimingLeadingDisplayDigits = timingLeadingDisplayDigits,
                TimingTrailingDisplayDecimals = timingTrailingDisplayDecimals
            };
            Directory.CreateDirectory(Path.GetDirectoryName(AutosavePath)!);
            File.WriteAllText(AutosavePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* Autosave must never interrupt tuning work or application shutdown. */ }
    }

    private bool LoadState()
    {
        if (!File.Exists(AutosavePath)) return false;
        try
        {
            var state = JsonSerializer.Deserialize<AutosaveState>(File.ReadAllText(AutosavePath));
            if (state?.RpmAxis is null || state.MapAxis is null || state.Timing is null || state.RpmAxis.Length is < 8 or > 64 || state.MapAxis.Length is < 8 or > 64 || state.Timing.Length != state.MapAxis.Length || state.Timing.Any(row => row?.Length != state.RpmAxis.Length)) return false;
            loadingState = true;
            ColumnCount = state.RpmAxis.Length; RowCount = state.MapAxis.Length;
            MatrixXSizeBox.Text = ColumnCount.ToString(CultureInfo.InvariantCulture); MatrixYSizeBox.Text = RowCount.ToString(CultureInfo.InvariantCulture);
            mapUnitIndex = Math.Clamp(state.ApplicationIndex, 0, 1);
            SyncTimingMapUnitControls();
            RefreshTimingMapUnitPresentation();
            MinRpmBox.Text = state.MinRpm; MaxRpmBox.Text = state.MaxRpm; MinMapBox.Text = state.MinMap; MaxMapBox.Text = state.MaxMap;
            LowTimingBox.Text = state.LowTiming; HighTimingBox.Text = state.HighTiming;
            useCustomHeatColors = state.UseCustomHeatColors;
            if (ColorConverter.ConvertFromString(state.LowHeatColor) is Color lowColor) customLowColor = lowColor;
            if (ColorConverter.ConvertFromString(state.HighHeatColor) is Color highColor) customHighColor = highColor;
            boostRetardPerPsi = state.BoostRetardPerPsi; boostRetardLowMap = state.BoostRetardLowMap; boostRetardHighMap = state.BoostRetardHighMap;
            refinementStrength = state.RefinementStrength; refinementPasses = state.RefinementPasses; advancedSmoothingOptions = state.AdvancedOptions ?? advancedSmoothingOptions;
            directionalOuterToInner = state.DirectionalOuterToInner; directionalStrength = state.DirectionalStrength; directionalPasses = state.DirectionalPasses;
            selectionOffsetAmount = state.SelectionOffsetAmount; selectionOffsetIsPercentage = state.SelectionOffsetIsPercentage;
            regionTimingProfiles = state.RegionTimingProfiles?.Length == 3 ? state.RegionTimingProfiles : [];
            blendRegionTiming = state.BlendRegionTiming;
            verticalRegionSmoothCells = Math.Clamp(state.VerticalRegionSmoothCells, 3, 64);
            horizontalRegionSmoothCells = Math.Clamp(state.HorizontalRegionSmoothCells, 3, 64);
            timingLeadingDisplayDigits = Math.Clamp(state.TimingLeadingDisplayDigits, 1, 4);
            timingTrailingDisplayDecimals = Math.Clamp(state.TimingTrailingDisplayDecimals, 0, 3);
            syncingTimingDisplayPrecision = true;
            TimingLeadingPrecisionBox.SelectedIndex = timingLeadingDisplayDigits - 1;
            TimingTrailingPrecisionBox.SelectedIndex = timingTrailingDisplayDecimals;
            syncingTimingDisplayPrecision = false;
            IdleRpmBox.Text = state.IdleRpm; IdleMapBox.Text = state.IdleMap; WotRpmBox.Text = state.WotRpm; WotMapBox.Text = state.WotMap;
            rpmAxis = IsLegacyDefaultRpmAxis(state.RpmAxis) ? DefaultRpmAxis.ToArray() : state.RpmAxis;
            if (IsLegacyDefaultRpmAxis(state.RpmAxis)) { MinRpmBox.Text = "500"; MaxRpmBox.Text = "7000"; }
            mapAxis = state.MapAxis.ToArray();
            MinMapBox.Text = FormatExactAxisValue(mapAxis[^1]); MaxMapBox.Text = FormatExactAxisValue(mapAxis[0]);
            if (double.TryParse(WotMapBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var loadedWotMap)) WotMapBox.Text = FormatMap(RoundMapValue(loadedWotMap));
            if (double.TryParse(IdleMapBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var loadedIdleMap)) IdleMapBox.Text = FormatMap(RoundMapValue(loadedIdleMap));
            var lowTiming = double.TryParse(state.LowTiming, NumberStyles.Float, CultureInfo.InvariantCulture, out var low) ? low : 42;
            var highTiming = double.TryParse(state.HighTiming, NumberStyles.Float, CultureInfo.InvariantCulture, out var high) ? high : 12;
            BuildGrid(lowTiming, highTiming, mapAxis[^1], mapAxis[0]);
            for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) SetCellValue(row, col, state.Timing[row][col]);
            ReadAndApplyRegions(false);
            selectionStart = selectionEnd = null; selectedRpmAxis.Clear(); selectedMapAxis.Clear(); activeAxisIsMap = null;
            StatusText.Text = "Autosaved table restored";
            return true;
        }
        catch (Exception) { return false; }
        finally { loadingState = false; }
    }

    private sealed class AutosaveState
    {
        public int ApplicationIndex { get; set; }
        public string MinRpm { get; set; } = "500";
        public string MaxRpm { get; set; } = "7000";
        public string MinMap { get; set; } = "20";
        public string MaxMap { get; set; } = "100";
        public string LowTiming { get; set; } = "42";
        public string HighTiming { get; set; } = "12";
        public string IdleRpm { get; set; } = "1200";
        public string IdleMap { get; set; } = "45";
        public string WotRpm { get; set; } = "2500";
        public string WotMap { get; set; } = "85";
        public double[] RpmAxis { get; set; } = [];
        public double[] MapAxis { get; set; } = [];
        public double[][] Timing { get; set; } = [];
        public bool UseCustomHeatColors { get; set; }
        public string LowHeatColor { get; set; } = "#FFFF1414";
        public string HighHeatColor { get; set; } = "#FFFF00EB";
        public double BoostRetardPerPsi { get; set; } = 1;
        public double BoostRetardLowMap { get; set; }
        public double BoostRetardHighMap { get; set; } = 15;
        public double RefinementStrength { get; set; } = .5;
        public int RefinementPasses { get; set; } = 3;
        public AdvancedSmoothingOptions? AdvancedOptions { get; set; }
        public bool DirectionalOuterToInner { get; set; } = true;
        public double DirectionalStrength { get; set; } = .65;
        public int DirectionalPasses { get; set; } = 2;
        public double SelectionOffsetAmount { get; set; } = 1;
        public bool SelectionOffsetIsPercentage { get; set; }
        public RegionTimingProfile[] RegionTimingProfiles { get; set; } = [];
        public bool BlendRegionTiming { get; set; } = true;
        public int VerticalRegionSmoothCells { get; set; } = 3;
        public int HorizontalRegionSmoothCells { get; set; } = 3;
        public int TimingLeadingDisplayDigits { get; set; } = 3;
        public int TimingTrailingDisplayDecimals { get; set; } = 1;
    }

    private sealed class MapLabSettingsFile
    {
        public const string ExpectedFormat = "MapLab.Settings";
        public const int CurrentVersion = 1;
        public string Format { get; set; } = ExpectedFormat;
        public int Version { get; set; } = CurrentVersion;
        public DateTimeOffset ExportedUtc { get; set; }
        public JsonElement Timing { get; set; }
        public JsonElement Fueling { get; set; }
        public JsonElement Sandbox { get; set; }
    }

    private enum RegionPointPick { None, IdleToCruise, CruiseToWot, Both }

    private static bool IsLegacyDefaultRpmAxis(double[] axis)
    {
        if (axis.Length != 31) return false;
        for (var i = 0; i < 31; i++) if (Math.Abs(axis[i] - (600 + 220 * i)) > .01) return false;
        return true;
    }
}
