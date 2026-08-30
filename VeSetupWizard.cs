using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class VeSetupSettings
{
    public double DisplacementCi { get; set; } = 350;
    public bool Boosted { get; set; }
    public double IdleRpm { get; set; } = 850;
    public double PeakTorqueRpm { get; set; } = 4000;
    public double MaximumRpm { get; set; } = 7000;
    public double IdleMap { get; set; } = 40;
    public double MaximumMap { get; set; } = 100;
    public double IdleVe { get; set; } = 42;
    public double IdleHighMap { get; set; } = 75;
    public double IdleHighVe { get; set; } = 55;
    public double CruiseVe { get; set; } = 58;
    public double PartThrottleVe { get; set; } = 75;
    public double WotVe { get; set; } = 98;
    public double HighRpmVe { get; set; } = 88;
    public double BoostVe { get; set; } = 112;
    public int RegionValueMode { get; set; } = 1;
    public double IdleAfr { get; set; } = 13.8;
    public double CruiseAfr { get; set; } = 14.7;
    public double WotAfr { get; set; } = 12.8;
    public double BoostAfr { get; set; } = 11.8;
    public double IntakeAirTemperatureF { get; set; } = 70;
    public double BarometricPressurePsi { get; set; } = 14.7;
    public bool SelectedCellsOnly { get; set; }
    public int ApplyMode { get; set; }
    public double BlendStrength { get; set; } = .7;
    public bool EnableBoundarySmoothing { get; set; } = true;
    public AdvancedSmoothingAlgorithm BoundarySmoothingAlgorithm { get; set; } = AdvancedSmoothingAlgorithm.ConstrainedSurface;
    public double BoundarySmoothingStrength { get; set; } = .6;
    public int BoundarySmoothingPasses { get; set; } = 3;
    public int HorizontalSmoothCells { get; set; } = 3;
    public int VerticalSmoothCells { get; set; } = 3;
    public bool PreserveOuterValues { get; set; } = true;
}

public readonly record struct VeSelection(int Top, int Bottom, int Left, int Right);
public readonly record struct VeRegionBoundary(int IdleColumn, int WotRow);

public sealed class VeSetupWizard : Window
{
    private readonly double[,] current;
    private readonly double[] rpm;
    private double[] map;
    private readonly string mapUnit;
    private readonly VeSelection? selection;
    private VeRegionBoundary regionBoundary;
    private readonly Action requestBoundaryPick;
    private readonly Func<double, double, double[]?> rescaleMapAxis;
    private readonly Action<double[,], VeSetupSettings> apply;
    private readonly VeSetupSettings settings;
    private readonly Grid pageHost = new();
    private readonly TextBlock stepText = new(), validationText = new();
    private readonly Button backButton, nextButton, applyButton;
    private readonly List<FrameworkElement> pages = [];
    private readonly Dictionary<string, TextBox> inputs = [];
    private TextBlock maximumMapLabel = null!, idleMapLabel = null!, idleHighMapLabel = null!;
    private CheckBox boostedBox = null!, selectedOnlyBox = null!, preserveOuterBox = null!, smoothBoundariesBox = null!;
    private ComboBox applyModeBox = null!, smoothingAlgorithmBox = null!, regionValueModeBox = null!;
    private Slider blendSlider = null!;
    private int pageIndex;

    public VeSetupWizard(double[,] current, double[] rpm, double[] map, string mapUnit, VeSelection? selection, VeRegionBoundary regionBoundary, VeSetupSettings initial, Action requestBoundaryPick, Func<double, double, double[]?> rescaleMapAxis, Action<double[,], VeSetupSettings> apply)
    {
        this.current = (double[,])current.Clone(); this.rpm = rpm.ToArray(); this.map = map.ToArray(); this.mapUnit = mapUnit; this.selection = selection; this.regionBoundary = regionBoundary; this.requestBoundaryPick = requestBoundaryPick; this.rescaleMapAxis = rescaleMapAxis; this.apply = apply;
        settings = Clone(initial);
        SetBoundaryDerivedMapValues();
        Title = "VE Setup Wizard"; Width = 920; Height = Math.Min(860, SystemParameters.WorkArea.Height - 40); MinWidth = 760; MinHeight = Math.Min(700, Height); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(243, 243, 243)); FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new Grid { Margin = new Thickness(0, 0, 0, 18) }; heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel(); headingText.Children.Add(new TextBlock { Text = "FUELING LAB", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 12, FontWeight = FontWeights.Bold });
        headingText.Children.Add(new TextBlock { Text = "Volumetric Efficiency Setup", Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) });
        headingText.Children.Add(new TextBlock { Text = "Create a smooth starter surface without changing the MAP or RPM axes.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }); heading.Children.Add(headingText);
        stepText.Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)); stepText.FontWeight = FontWeights.SemiBold; stepText.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(stepText, 1); heading.Children.Add(stepText); root.Children.Add(heading);

        BuildPages();
        var pageScroller = new ScrollViewer { Content = pageHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var pageFrame = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(20), Child = pageScroller };
        Grid.SetRow(pageFrame, 1); root.Children.Add(pageFrame);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        validationText.Foreground = new SolidColorBrush(Color.FromRgb(170, 40, 45)); validationText.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(validationText);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        backButton = MakeButton("Back", false); backButton.Click += (_, _) => ShowPage(pageIndex - 1);
        nextButton = MakeButton("Next", true); nextButton.Click += (_, _) => { if (pageIndex == 0 && !ApplyMapRange(false)) return; if (ReadSettings()) ShowPage(pageIndex + 1); };
        applyButton = MakeButton("Apply VE Table", true); applyButton.Click += (_, _) => Apply();
        var close = MakeButton("Close", false); close.Click += (_, _) => Close();
        actions.Children.Add(backButton); actions.Children.Add(nextButton); actions.Children.Add(applyButton); actions.Children.Add(close); Grid.SetColumn(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root; ShowPage(0);
    }

    public void UpdateBoundaryMapValues(double[] mapAxis, VeRegionBoundary boundary)
    {
        map = mapAxis.ToArray(); regionBoundary = boundary; SetBoundaryDerivedMapValues();
        RefreshMapPresentation();
        if (pageIndex == pages.Count - 1) ShowPage(pageIndex);
    }

    public void CompleteBoundaryPick(double[] mapAxis, VeRegionBoundary boundary)
    {
        UpdateBoundaryMapValues(mapAxis, boundary); RestoreAfterBoundaryPick();
    }

    public void CancelBoundaryPick() => RestoreAfterBoundaryPick();

    private void BeginBoundaryPick()
    {
        requestBoundaryPick(); WindowState = WindowState.Minimized;
    }

    private void RestoreAfterBoundaryPick()
    {
        if (!IsLoaded) return; if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); Focus();
    }

    private void SetBoundaryDerivedMapValues()
    {
        var boundaryRow = Math.Clamp(regionBoundary.WotRow, 0, map.Length - 1);
        settings.IdleMap = map[^1]; settings.IdleHighMap = map[boundaryRow]; settings.MaximumMap = map[0];
    }

    private void SetInput(string key, double value)
    {
        if (inputs.TryGetValue(key, out var input)) input.Text = value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private bool NativeMapIsPsi => mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase);
    private bool DisplayMapAsPsi => boostedBox?.IsChecked == true;
    private string DisplayMapUnit => DisplayMapAsPsi ? "PSI gauge" : "kPa absolute";
    private static double ConvertMapUnit(double value, bool fromPsi, bool toPsi) => fromPsi == toPsi ? value : toPsi ? (value - 101.325) / 6.894757293168361 : value * 6.894757293168361 + 101.325;
    private double ToDisplayMap(double nativeValue) => ConvertMapUnit(nativeValue, NativeMapIsPsi, DisplayMapAsPsi);
    private double FromDisplayMap(double displayValue) => ConvertMapUnit(displayValue, DisplayMapAsPsi, NativeMapIsPsi);

    private void RefreshMapPresentation()
    {
        if (maximumMapLabel is not null) maximumMapLabel.Text = $"MAXIMUM MAP — TABLE TOP ({DisplayMapUnit})";
        if (idleMapLabel is not null) idleMapLabel.Text = $"MINIMUM MAP / IDLE LOW MAP — TABLE BOTTOM ({DisplayMapUnit})";
        if (idleHighMapLabel is not null) idleHighMapLabel.Text = $"IDLE HIGH MAP — HORIZONTAL BOUNDARY ({DisplayMapUnit})";
        SetInput("idleMap", ToDisplayMap(settings.IdleMap)); SetInput("idleHighMap", ToDisplayMap(settings.IdleHighMap)); SetInput("maxMap", ToDisplayMap(settings.MaximumMap));
    }

    private void BuildPages()
    {
        var engine = Page("1. Engine Setup", "These values position the VE curve. Displacement is saved for future fuel-flow diagnostics; it does not alter the percentage calculation in this version.");
        AddField(engine, "Displacement (cu in)", "displacement", settings.DisplacementCi); boostedBox = new CheckBox { Content = "Forced induction / boosted", IsChecked = settings.Boosted, Margin = new Thickness(0, 5, 0, 12) }; engine.Children.Add(boostedBox);
        AddField(engine, "Idle RPM", "idleRpm", settings.IdleRpm); AddField(engine, "Peak torque RPM", "peakRpm", settings.PeakTorqueRpm); AddField(engine, "Maximum RPM", "maxRpm", settings.MaximumRpm);
        idleMapLabel = AddField(engine, $"Minimum MAP / Idle Low MAP — table bottom ({mapUnit})", "idleMap", settings.IdleMap);
        maximumMapLabel = AddField(engine, $"Maximum MAP — table top ({mapUnit})", "maxMap", settings.MaximumMap);
        var applyMapRange = MakeButton("Apply MAP Range", true); applyMapRange.Margin = new Thickness(0, 2, 0, 0); applyMapRange.Click += (_, _) => ApplyMapRange(true); engine.Children.Add(applyMapRange);
        engine.Children.Add(new TextBlock { Text = "Rescales all MAP breakpoints. The horizontal region boundary moves to the nearest new breakpoint and can be adjusted again in Step 2.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 7, 0, 0) }); pages.Add(engine);

        var anchors = Page("2. VE Reference Points", "MAP setpoints come from the current table and locked horizontal boundary. Move the table boundary to change the low/high-load transition used by this wizard.");
        var boundaryControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var setBoundaries = MakeButton("⌖  Set Boundaries on Fuel Table", true); setBoundaries.Margin = new Thickness(0); setBoundaries.Click += (_, _) => BeginBoundaryPick(); boundaryControls.Children.Add(setBoundaries);
        boundaryControls.Children.Add(new TextBlock { Text = "Hover over the table and click the intersection to lock", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) }); anchors.Children.Add(boundaryControls);
        anchors.Children.Add(Label("REGION VALUE MODE"));
        regionValueModeBox = new ComboBox { Width = 390, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = Math.Clamp(settings.RegionValueMode, 0, 1), Margin = new Thickness(0, 0, 0, 6), Foreground = Brushes.Black, Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5) };
        regionValueModeBox.Items.Add(new ComboBoxItem { Content = "Fill quadrants with region values", Foreground = Brushes.Black });
        regionValueModeBox.Items.Add(new ComboBoxItem { Content = "Interpolate complete map with fixed boundary lines", Foreground = Brushes.Black }); anchors.Children.Add(regionValueModeBox);
        anchors.Children.Add(new TextBlock { Text = "Fill uses Idle Low, Idle High, Cruise, and WOT as the four quadrant values. Interpolate uses all load and RPM reference points, then interpolates every quadrant to the unchanged vertical and horizontal boundary cells.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        idleHighMapLabel = AddField(anchors, $"Idle High MAP — horizontal boundary ({mapUnit})", "idleHighMap", settings.IdleHighMap, true); AddField(anchors, "VE at Idle Low MAP (%)", "idleVe", settings.IdleVe); AddField(anchors, "VE at Idle High MAP (%)", "idleHighVe", settings.IdleHighVe);
        AddField(anchors, "Cruise VE — lower-right region (%)", "cruiseVe", settings.CruiseVe); AddField(anchors, "Part-throttle VE (%)", "partVe", settings.PartThrottleVe);
        AddField(anchors, "WOT / upper-right fill VE (%)", "wotVe", settings.WotVe); AddField(anchors, "High-RPM WOT VE (%)", "highVe", settings.HighRpmVe); AddField(anchors, "Maximum-boost VE (%)", "boostVe", settings.BoostVe); pages.Add(anchors);
        void UpdateRegionValueFields() { var interpolate = regionValueModeBox.SelectedIndex == 1; inputs["partVe"].IsEnabled = interpolate; inputs["highVe"].IsEnabled = interpolate; inputs["boostVe"].IsEnabled = interpolate; }
        regionValueModeBox.SelectionChanged += (_, _) => UpdateRegionValueFields(); UpdateRegionValueFields();
        boostedBox.Checked += (_, _) => RefreshMapPresentation(); boostedBox.Unchecked += (_, _) => RefreshMapPresentation(); RefreshMapPresentation();

        var conversion = Page("3. Fuel Flow Conversion", "These values calculate an estimated total fuel demand in lb/hr. VE remains the stored and editable table.");
        AddField(conversion, "Idle target AFR", "idleAfr", settings.IdleAfr); AddField(conversion, "Cruise target AFR", "cruiseAfr", settings.CruiseAfr); AddField(conversion, "WOT target AFR", "wotAfr", settings.WotAfr); AddField(conversion, "Boost target AFR", "boostAfr", settings.BoostAfr);
        AddField(conversion, "Reference intake-air temperature (°F)", "iatF", settings.IntakeAirTemperatureF); AddField(conversion, "Barometric pressure (psi absolute)", "baro", settings.BarometricPressurePsi); pages.Add(conversion);

        var application = Page("4. Application & Transitions", "Choose how the generated values are applied and whether the region/setup boundaries remain sharp or are smoothed.");
        selectedOnlyBox = new CheckBox { Content = selection is null ? "Selected cells only (no fuel-cell selection is active)" : $"Selected cells only ({selection.Value.Right - selection.Value.Left + 1} × {selection.Value.Bottom - selection.Value.Top + 1})", IsChecked = selection is not null && settings.SelectedCellsOnly, IsEnabled = selection is not null, Margin = new Thickness(0, 5, 0, 16) }; application.Children.Add(selectedOnlyBox);
        application.Children.Add(Label("APPLICATION MODE")); applyModeBox = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = Math.Clamp(settings.ApplyMode, 0, 2), Margin = new Thickness(0, 0, 0, 16) };
        applyModeBox.Items.Add("Replace current values"); applyModeBox.Items.Add("Blend with current values"); applyModeBox.Items.Add("Fill zero/empty values only"); application.Children.Add(applyModeBox);
        var blendOptions = new StackPanel { Visibility = settings.ApplyMode == 1 ? Visibility.Visible : Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
        blendOptions.Children.Add(Label("BLEND STRENGTH")); blendSlider = new Slider { Minimum = .1, Maximum = 1, Value = settings.BlendStrength, TickFrequency = .1, IsSnapToTickEnabled = true, Width = 320, HorizontalAlignment = HorizontalAlignment.Left }; blendOptions.Children.Add(blendSlider); application.Children.Add(blendOptions);
        applyModeBox.SelectionChanged += (_, _) => blendOptions.Visibility = applyModeBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        application.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)), Margin = new Thickness(0, 8, 0, 16) });
        application.Children.Add(Label("REGION TRANSITIONS"));
        smoothBoundariesBox = new CheckBox { Content = "Smooth values across region/setup boundaries", IsChecked = settings.EnableBoundarySmoothing, Margin = new Thickness(0, 2, 0, 4), FontWeight = FontWeights.SemiBold };
        application.Children.Add(smoothBoundariesBox);
        application.Children.Add(new TextBlock { Text = "Boundary-line cells remain fixed anchors; smoothing changes only the cells on both sides.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(20, 0, 0, 10) });
        var smoothingOptions = new StackPanel { Visibility = settings.EnableBoundarySmoothing ? Visibility.Visible : Visibility.Collapsed };
        smoothingOptions.Children.Add(Label("SMOOTHING ALGORITHM"));
        smoothingAlgorithmBox = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = (int)settings.BoundarySmoothingAlgorithm, Margin = new Thickness(0, 0, 0, 12), Foreground = Brushes.Black, Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5) };
        foreach (var name in new[] { "Shape-preserving interpolation", "Constrained surface smoothing", "Spike removal (median)", "Edge-preserving smoothing", "Weighted center / perimeter" }) smoothingAlgorithmBox.Items.Add(new ComboBoxItem { Content = name, Foreground = Brushes.Black });
        smoothingOptions.Children.Add(smoothingAlgorithmBox);
        AddField(smoothingOptions, "Cells on each side of vertical boundary (minimum 3)", "horizontal", settings.HorizontalSmoothCells);
        AddField(smoothingOptions, "Cells on each side of horizontal boundary (minimum 3)", "vertical", settings.VerticalSmoothCells);
        AddField(smoothingOptions, "Smoothing strength (1–100%)", "smoothStrength", settings.BoundarySmoothingStrength * 100);
        AddField(smoothingOptions, "Smoothing passes (1–20)", "smoothPasses", settings.BoundarySmoothingPasses);
        preserveOuterBox = new CheckBox { Content = "Preserve the outermost values of the applied area", IsChecked = settings.PreserveOuterValues, Margin = new Thickness(0, 8, 0, 0) };
        smoothBoundariesBox.Checked += (_, _) => smoothingOptions.Visibility = Visibility.Visible; smoothBoundariesBox.Unchecked += (_, _) => smoothingOptions.Visibility = Visibility.Collapsed;
        application.Children.Add(smoothingOptions); application.Children.Add(preserveOuterBox); pages.Add(application);
        pages.Add(new StackPanel());
    }

    private StackPanel Page(string title, string description)
    {
        var page = new StackPanel(); page.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)), Margin = new Thickness(0, 0, 0, 5) });
        page.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 0, 0, 18) }); return page;
    }

    private TextBlock AddField(Panel page, string label, string key, double value, bool isReadOnly = false)
    {
        var fieldLabel = Label(label.ToUpperInvariant()); page.Children.Add(fieldLabel); var box = new TextBox { Text = value.ToString("0.###", CultureInfo.InvariantCulture), Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 0, 10), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 184, 184)), IsReadOnly = isReadOnly, Background = isReadOnly ? new SolidColorBrush(Color.FromRgb(240, 240, 240)) : Brushes.White, ToolTip = isReadOnly ? "Derived from the current MAP axis and region boundary" : null }; inputs[key] = box; page.Children.Add(box); return fieldLabel;
    }

    private static TextBlock Label(string text) => new() { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private static Button MakeButton(string text, bool primary) => new() { Content = text, Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(7, 0, 0, 0), Background = new SolidColorBrush(primary ? Color.FromRgb(0, 103, 192) : Color.FromRgb(249, 249, 249)), Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32)), BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(0, 90, 170) : Color.FromRgb(190, 190, 190)), FontWeight = FontWeights.SemiBold };

    private bool ApplyMapRange(bool showConfirmation)
    {
        if (!double.TryParse(inputs["idleMap"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var displayedMinimum) ||
            !double.TryParse(inputs["maxMap"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var displayedMaximum) ||
            !double.IsFinite(displayedMinimum) || !double.IsFinite(displayedMaximum) || displayedMinimum >= displayedMaximum)
        { validationText.Text = $"Enter a valid minimum and maximum MAP range in {DisplayMapUnit}."; return false; }
        var minimum = FromDisplayMap(displayedMinimum); var maximum = FromDisplayMap(displayedMaximum);
        var previousBoundary = settings.IdleHighMap;
        var updated = rescaleMapAxis(minimum, maximum);
        if (updated is null)
        { validationText.Text = "The requested MAP range is too narrow for the current number of whole-number MAP breakpoints."; return false; }
        map = updated.ToArray();
        var adjustedBoundary = Math.Clamp(previousBoundary, map[^1], map[0]); var boundaryRow = 0; var distance = double.MaxValue;
        for (var row = 0; row < map.Length; row++) { var currentDistance = Math.Abs(map[row] - adjustedBoundary); if (currentDistance < distance) { boundaryRow = row; distance = currentDistance; } }
        regionBoundary = new VeRegionBoundary(regionBoundary.IdleColumn, boundaryRow); SetBoundaryDerivedMapValues(); RefreshMapPresentation();
        validationText.Foreground = new SolidColorBrush(Color.FromRgb(25, 120, 70));
        validationText.Text = showConfirmation ? $"MAP scale updated to {ToDisplayMap(map[^1]):0.#}–{ToDisplayMap(map[0]):0.#} {DisplayMapUnit}. The boundary was moved to the nearest breakpoint." : "";
        return true;
    }

    private void ShowPage(int index)
    {
        pageIndex = Math.Clamp(index, 0, pages.Count - 1); validationText.Text = ""; pageHost.Children.Clear();
        if (pageIndex == pages.Count - 1) BuildPreview(); else pageHost.Children.Add(pages[pageIndex]);
        stepText.Text = $"Step {pageIndex + 1} of {pages.Count}"; backButton.IsEnabled = pageIndex > 0; nextButton.Visibility = pageIndex < pages.Count - 1 ? Visibility.Visible : Visibility.Collapsed; applyButton.Visibility = pageIndex == pages.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ReadSettings()
    {
        bool Read(string key, out double value) => double.TryParse(inputs[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        var smoothingEnabled = smoothBoundariesBox.IsChecked == true;
        var horizontal = (double)settings.HorizontalSmoothCells; var vertical = (double)settings.VerticalSmoothCells; var smoothStrength = settings.BoundarySmoothingStrength * 100; var smoothPasses = (double)settings.BoundarySmoothingPasses;
        var smoothingValuesValid = !smoothingEnabled ||
            Read("horizontal", out horizontal) && Read("vertical", out vertical) && horizontal >= 3 && vertical >= 3 &&
            Read("smoothStrength", out smoothStrength) && smoothStrength is >= 1 and <= 100 && Read("smoothPasses", out smoothPasses) && smoothPasses is >= 1 and <= 20;
        if (!Read("displacement", out var displacement) || displacement <= 0 || !Read("idleRpm", out var idleRpm) || !Read("peakRpm", out var peakRpm) || !Read("maxRpm", out var maxRpm) || idleRpm >= peakRpm || peakRpm >= maxRpm ||
            !Read("idleMap", out var idleMap) || !Read("idleHighMap", out var idleHighMap) || !Read("maxMap", out var maxMap) || idleMap > idleHighMap || idleHighMap > maxMap ||
            !Read("idleVe", out var idleVe) || !Read("idleHighVe", out var idleHighVe) || !Read("cruiseVe", out var cruiseVe) || !Read("partVe", out var partVe) || !Read("wotVe", out var wotVe) || !Read("highVe", out var highVe) || !Read("boostVe", out var boostVe) ||
            !Read("idleAfr", out var idleAfr) || !Read("cruiseAfr", out var cruiseAfr) || !Read("wotAfr", out var wotAfr) || !Read("boostAfr", out var boostAfr) || new[] { idleAfr, cruiseAfr, wotAfr, boostAfr }.Any(value => value is < 5 or > 30) ||
            !Read("iatF", out var iatF) || iatF is < -100 or > 350 || !Read("baro", out var baro) || baro is < 8 or > 16 ||
            new[] { idleVe, idleHighVe, cruiseVe, partVe, wotVe, highVe, boostVe }.Any(value => value is < 1 or > 250) || !smoothingValuesValid)
        { validationText.Text = "Check the entries. The MAP axis must run from low at the table bottom to high at the top. VE must be 1–250%, AFR 5–30, smoothing widths at least 3, strength 1–100%, and passes 1–20."; return false; }
        idleMap = FromDisplayMap(idleMap); idleHighMap = FromDisplayMap(idleHighMap); maxMap = FromDisplayMap(maxMap);
        settings.DisplacementCi = displacement; settings.Boosted = boostedBox.IsChecked == true; settings.IdleRpm = idleRpm; settings.PeakTorqueRpm = peakRpm; settings.MaximumRpm = maxRpm; settings.IdleMap = idleMap; settings.MaximumMap = maxMap;
        settings.IdleVe = idleVe; settings.IdleHighMap = idleHighMap; settings.IdleHighVe = idleHighVe; settings.CruiseVe = cruiseVe; settings.PartThrottleVe = partVe; settings.WotVe = wotVe; settings.HighRpmVe = highVe; settings.BoostVe = boostVe;
        settings.RegionValueMode = Math.Clamp(regionValueModeBox.SelectedIndex, 0, 1);
        settings.IdleAfr = idleAfr; settings.CruiseAfr = cruiseAfr; settings.WotAfr = wotAfr; settings.BoostAfr = boostAfr; settings.IntakeAirTemperatureF = iatF; settings.BarometricPressurePsi = baro;
        settings.SelectedCellsOnly = selectedOnlyBox.IsChecked == true && selection is not null; settings.ApplyMode = applyModeBox.SelectedIndex; settings.BlendStrength = blendSlider.Value;
        settings.EnableBoundarySmoothing = smoothingEnabled; settings.BoundarySmoothingAlgorithm = (AdvancedSmoothingAlgorithm)Math.Max(0, smoothingAlgorithmBox.SelectedIndex); settings.BoundarySmoothingStrength = smoothStrength / 100; settings.BoundarySmoothingPasses = (int)Math.Round(smoothPasses);
        settings.HorizontalSmoothCells = (int)Math.Round(horizontal); settings.VerticalSmoothCells = (int)Math.Round(vertical); settings.PreserveOuterValues = preserveOuterBox.IsChecked == true; return true;
    }

    private void BuildPreview()
    {
        if (!ReadSettings()) { ShowPage(0); return; }
        var proposed = Generate(current, rpm, map, mapUnit, selection, regionBoundary, settings); var fuelFlow = ConvertToFuelFlow(proposed, rpm, map, mapUnit, settings);
        var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = "5. Preview", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(new TextBlock { Text = "Review the overall shape before applying. The full-resolution table remains editable after generation.", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 0, 0, 14) });
        var previews = new Grid(); previews.ColumnDefinitions.Add(new ColumnDefinition()); previews.ColumnDefinitions.Add(new ColumnDefinition()); previews.ColumnDefinitions.Add(new ColumnDefinition());
        var before = PreviewCard("CURRENT VE TABLE (%)", current, "%"); previews.Children.Add(before); var after = PreviewCard("PROPOSED VE TABLE (%)", proposed, "%"); Grid.SetColumn(after, 1); previews.Children.Add(after);
        var flow = PreviewCard("ESTIMATED FUEL FLOW (lb/hr)", fuelFlow, " lb/hr"); Grid.SetColumn(flow, 2); previews.Children.Add(flow); panel.Children.Add(previews);
        var scope = settings.SelectedCellsOnly && selection is not null ? "selected cells" : "entire table"; var mode = new[] { "replace", "blend", "fill zeros" }[settings.ApplyMode];
        panel.Children.Add(new TextBlock { Text = $"Scope: {scope}  •  Mode: {mode}  •  VE: {proposed.Cast<double>().Min():0.0}–{proposed.Cast<double>().Max():0.0}%  •  Fuel flow: {fuelFlow.Cast<double>().Min():0.0}–{fuelFlow.Cast<double>().Max():0.0} lb/hr", Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0) }); pageHost.Children.Add(panel);
        var idleColumn = Math.Clamp(regionBoundary.IdleColumn, 0, rpm.Length - 1); var wotRow = Math.Clamp(regionBoundary.WotRow, 0, map.Length - 1);
        panel.Children.Add(new TextBlock { Text = $"Regions: Idle Low MAP below and Idle High MAP above {ToDisplayMap(map[wotRow]):0.#} {DisplayMapUnit} on the left of {rpm[idleColumn]:0} RPM  •  Cruise below and Part Throttle/WOT above on the right", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 5, 0, 0) });
        panel.Children.Add(new TextBlock { Text = settings.EnableBoundarySmoothing ? $"Boundary smoothing: {settings.BoundarySmoothingAlgorithm}  •  {settings.BoundarySmoothingPasses} passes  •  {settings.BoundarySmoothingStrength:P0}" : "Boundary smoothing: Off — sharp region/setup boundaries", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
        panel.Children.Add(new TextBlock { Text = settings.RegionValueMode == 0 ? "Region values: Fill quadrants" : "Region values: Complete-map interpolation with fixed boundary lines", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
    }

    private Border PreviewCard(string title, double[,] values, string suffix)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; stack.Children.Add(Label(title));
        var grid = new UniformGrid { Rows = Math.Min(16, values.GetLength(0)), Columns = Math.Min(24, values.GetLength(1)), Height = 260 };
        var min = values.Cast<double>().Min(); var max = values.Cast<double>().Max();
        for (var rowIndex = 0; rowIndex < grid.Rows; rowIndex++) for (var colIndex = 0; colIndex < grid.Columns; colIndex++)
        {
            var row = (int)Math.Round(rowIndex * (values.GetLength(0) - 1d) / Math.Max(1, grid.Rows - 1)); var col = (int)Math.Round(colIndex * (values.GetLength(1) - 1d) / Math.Max(1, grid.Columns - 1));
            grid.Children.Add(new Border { Background = new SolidColorBrush(Heat((values[row, col] - min) / Math.Max(.1, max - min))), BorderBrush = new SolidColorBrush(Color.FromRgb(30, 40, 52)), BorderThickness = new Thickness(.35), ToolTip = $"{rpm[col]:0} RPM • {ToDisplayMap(map[row]):0.#} {DisplayMapUnit} • {values[row, col]:0.0}{suffix}" });
        }
        stack.Children.Add(grid); return new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = stack };
    }

    private void Apply()
    {
        if (!ReadSettings()) return;
        var updated = Generate(current, rpm, map, mapUnit, selection, regionBoundary, settings); apply(updated, Clone(settings)); Array.Copy(updated, current, current.Length);
        pageHost.Children.Clear(); BuildPreview(); validationText.Foreground = new SolidColorBrush(Color.FromRgb(25, 120, 70)); validationText.Text = "VE table applied. You can keep this wizard open and apply another revision.";
    }

    public static double[,] Generate(double[,] source, double[] rpm, double[] map, string mapUnit, VeSelection? selection, VeRegionBoundary regionBoundary, VeSetupSettings settings)
    {
        var rows = source.GetLength(0); var cols = source.GetLength(1); var generated = new double[rows, cols]; var wotMap = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? 0 : 100;
        var idleColumn = Math.Clamp(regionBoundary.IdleColumn, 0, cols - 1); var wotRow = Math.Clamp(regionBoundary.WotRow, 0, rows - 1);
        var cruiseLowMap = map[^1]; var regionMap = map[wotRow];
        for (var row = 0; row < rows; row++) for (var col = 0; col < cols; col++)
        {
            var fillQuadrants = settings.RegionValueMode == 0;
            var isIdleSide = col <= idleColumn;
            var isIdleHighMap = isIdleSide && row <= wotRow;
            var isCruise = !isIdleSide && row > wotRow;
            var load = isIdleSide
                ? isIdleHighMap ? settings.IdleHighVe : settings.IdleVe
                : isCruise
                    ? fillQuadrants ? settings.CruiseVe : Lerp(settings.CruiseVe, settings.PartThrottleVe, Smooth(Normalize(map[row], cruiseLowMap, regionMap)))
                    : fillQuadrants ? settings.WotVe : PowerRegionVe(map[row], settings, wotMap, regionMap);
            var lowRpmLoad = fillQuadrants || isIdleSide ? load : isCruise ? load * .92 : load * .82;
            var highLoad = fillQuadrants ? load : isIdleSide ? load * .9 : isCruise ? load * .94 : settings.HighRpmVe;
            generated[row, col] = rpm[col] <= settings.PeakTorqueRpm
                ? Lerp(lowRpmLoad, load, Smooth(Normalize(rpm[col], settings.IdleRpm, settings.PeakTorqueRpm)))
                : Lerp(load, highLoad, Smooth(Normalize(rpm[col], settings.PeakTorqueRpm, settings.MaximumRpm)));
        }
        if (settings.RegionValueMode == 1) generated = InterpolateCompleteMap(generated, idleColumn, wotRow);
        var result = (double[,])source.Clone(); var scope = settings.SelectedCellsOnly && selection is not null ? selection.Value : new VeSelection(0, rows - 1, 0, cols - 1);
        for (var row = scope.Top; row <= scope.Bottom; row++) for (var col = scope.Left; col <= scope.Right; col++)
            result[row, col] = settings.ApplyMode switch { 1 => Lerp(source[row, col], generated[row, col], settings.BlendStrength), 2 => Math.Abs(source[row, col]) < .0001 ? generated[row, col] : source[row, col], _ => generated[row, col] };
        if (settings.EnableBoundarySmoothing) result = SmoothBoundaryBands(result, scope, regionBoundary, settings);
        if (settings.PreserveOuterValues)
        {
            for (var col = scope.Left; col <= scope.Right; col++) { result[scope.Top, col] = source[scope.Top, col]; result[scope.Bottom, col] = source[scope.Bottom, col]; }
            for (var row = scope.Top; row <= scope.Bottom; row++) { result[row, scope.Left] = source[row, scope.Left]; result[row, scope.Right] = source[row, scope.Right]; }
        }
        for (var row = scope.Top; row <= scope.Bottom; row++) for (var col = scope.Left; col <= scope.Right; col++) result[row, col] = Math.Round(result[row, col], 1);
        return result;
    }

    public static double[,] ConvertToFuelFlow(double[,] veValues, double[] rpm, double[] map, string mapUnit, VeSetupSettings settings)
    {
        var result = new double[veValues.GetLength(0), veValues.GetLength(1)];
        var temperatureRankine = Math.Max(1, settings.IntakeAirTemperatureF + 459.67);
        const double airMassConstant = 144d * 60d / (53.35d * 1728d * 2d);
        var wotMap = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? 0 : 100;
        for (var row = 0; row < result.GetLength(0); row++) for (var col = 0; col < result.GetLength(1); col++)
        {
            var mapPsia = mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? map[row] + settings.BarometricPressurePsi : map[row] / 6.894757293168361;
            mapPsia = Math.Max(.1, mapPsia);
            var airPoundsPerHour = veValues[row, col] / 100d * settings.DisplacementCi * rpm[col] * mapPsia * airMassConstant / temperatureRankine;
            result[row, col] = Math.Round(airPoundsPerHour / TargetAfr(rpm[col], map[row], wotMap, settings), 1);
        }
        return result;
    }

    private static double TargetAfr(double rpmValue, double mapValue, double wotMap, VeSetupSettings settings)
    {
        var cruiseMap = Lerp(settings.IdleMap, wotMap, .45);
        if (mapValue <= cruiseMap)
        {
            var idleInfluence = 1 - Smooth(Normalize(rpmValue, settings.IdleRpm, settings.IdleRpm + 700));
            return Lerp(settings.CruiseAfr, settings.IdleAfr, idleInfluence);
        }
        if (mapValue <= wotMap || !settings.Boosted)
            return Lerp(settings.CruiseAfr, settings.WotAfr, Smooth(Normalize(mapValue, cruiseMap, wotMap)));
        return Lerp(settings.WotAfr, settings.BoostAfr, Smooth(Normalize(mapValue, wotMap, settings.MaximumMap)));
    }

    private static double PowerRegionVe(double mapValue, VeSetupSettings s, double wotMap, double regionMap)
    {
        if (mapValue <= wotMap || !s.Boosted) return Lerp(s.PartThrottleVe, s.WotVe, Smooth(Normalize(mapValue, regionMap, wotMap)));
        return Lerp(s.WotVe, s.BoostVe, Smooth(Normalize(mapValue, wotMap, s.MaximumMap)));
    }

    private static double[,] InterpolateCompleteMap(double[,] source, int idleColumn, int horizontalRow)
    {
        var result = (double[,])source.Clone(); var lastRow = source.GetLength(0) - 1; var lastColumn = source.GetLength(1) - 1;
        void InterpolateQuadrant(int top, int bottom, int left, int right)
        {
            // Each quadrant includes the shared boundary row/column as its
            // perimeter. SelectionInterpolator changes only interior cells.
            if (bottom - top < 2 || right - left < 2) return;
            result = SelectionInterpolator.Apply(result, top, bottom, left, right);
        }
        InterpolateQuadrant(0, horizontalRow, 0, idleColumn);
        InterpolateQuadrant(horizontalRow, lastRow, 0, idleColumn);
        InterpolateQuadrant(0, horizontalRow, idleColumn, lastColumn);
        InterpolateQuadrant(horizontalRow, lastRow, idleColumn, lastColumn);
        return result;
    }

    private static double[,] SmoothBoundaryBands(double[,] source, VeSelection area, VeRegionBoundary boundary, VeSetupSettings settings)
    {
        var result = (double[,])source.Clone(); var anchors = (double[,])source.Clone();
        var options = new AdvancedSmoothingOptions(settings.BoundarySmoothingAlgorithm, settings.BoundarySmoothingStrength, 1, false, true, .5);
        var idleColumn = Math.Clamp(boundary.IdleColumn, 0, source.GetLength(1) - 1);
        var horizontalRow = Math.Clamp(boundary.WotRow, 0, source.GetLength(0) - 1);

        var verticalLeft = Math.Max(area.Left, idleColumn - settings.HorizontalSmoothCells);
        var verticalRight = Math.Min(area.Right, idleColumn + settings.HorizontalSmoothCells + 1);
        var horizontalTop = Math.Max(area.Top, horizontalRow - settings.VerticalSmoothCells);
        var horizontalBottom = Math.Min(area.Bottom, horizontalRow + settings.VerticalSmoothCells + 1);

        for (var pass = 0; pass < settings.BoundarySmoothingPasses; pass++)
        {
            if (verticalLeft < verticalRight)
                result = AdvancedSmoother.Apply(result, area.Top, area.Bottom, verticalLeft, verticalRight, options);
            RestoreBoundaryAnchors(result, anchors, area, idleColumn, horizontalRow);
            if (horizontalTop < horizontalBottom)
                result = AdvancedSmoother.Apply(result, horizontalTop, horizontalBottom, area.Left, area.Right, options);
            RestoreBoundaryAnchors(result, anchors, area, idleColumn, horizontalRow);
        }

        return result;
    }

    private static void RestoreBoundaryAnchors(double[,] values, double[,] anchors, VeSelection area, int idleColumn, int horizontalRow)
    {
        if (idleColumn >= area.Left && idleColumn <= area.Right)
            for (var row = area.Top; row <= area.Bottom; row++) values[row, idleColumn] = anchors[row, idleColumn];
        if (horizontalRow >= area.Top && horizontalRow <= area.Bottom)
            for (var col = area.Left; col <= area.Right; col++) values[horizontalRow, col] = anchors[horizontalRow, col];
    }

    private static double Normalize(double value, double low, double high) => Math.Clamp((value - low) / Math.Max(.0001, high - low), 0, 1);
    private static double Smooth(double value) => value * value * (3 - 2 * value);
    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0, 1);
    private static VeSetupSettings Clone(VeSetupSettings value) => new() { DisplacementCi = value.DisplacementCi, Boosted = value.Boosted, IdleRpm = value.IdleRpm, PeakTorqueRpm = value.PeakTorqueRpm, MaximumRpm = value.MaximumRpm, IdleMap = value.IdleMap, MaximumMap = value.MaximumMap, IdleVe = value.IdleVe, IdleHighMap = value.IdleHighMap, IdleHighVe = value.IdleHighVe, CruiseVe = value.CruiseVe, PartThrottleVe = value.PartThrottleVe, WotVe = value.WotVe, HighRpmVe = value.HighRpmVe, BoostVe = value.BoostVe, RegionValueMode = value.RegionValueMode, IdleAfr = value.IdleAfr, CruiseAfr = value.CruiseAfr, WotAfr = value.WotAfr, BoostAfr = value.BoostAfr, IntakeAirTemperatureF = value.IntakeAirTemperatureF, BarometricPressurePsi = value.BarometricPressurePsi, SelectedCellsOnly = value.SelectedCellsOnly, ApplyMode = value.ApplyMode, BlendStrength = value.BlendStrength, EnableBoundarySmoothing = value.EnableBoundarySmoothing, BoundarySmoothingAlgorithm = value.BoundarySmoothingAlgorithm, BoundarySmoothingStrength = value.BoundarySmoothingStrength, BoundarySmoothingPasses = value.BoundarySmoothingPasses, HorizontalSmoothCells = value.HorizontalSmoothCells, VerticalSmoothCells = value.VerticalSmoothCells, PreserveOuterValues = value.PreserveOuterValues };
    private static Color Heat(double t) => Hsl(Math.Clamp(t, 0, 1) * 300, .96, .52);
    private static Color Hsl(double h, double s, double l) { var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2; var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) }; return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)); }
}
