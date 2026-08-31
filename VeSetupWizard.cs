using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class VeSetupSettings
{
    public int Cylinders { get; set; } = 8;
    public double DisplacementCi { get; set; } = 350;
    public bool Boosted { get; set; }
    public int CamshaftDurationRange { get; set; }
    public int MapSensorBar { get; set; } = 1;
    public double InjectorFlowLbHr { get; set; } = 36;
    public double InjectorRatedPressurePsi { get; set; } = 43.5;
    public double FuelPressurePsi { get; set; } = 60;
    public bool ManualVeTargets { get; set; }
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
    public int RegionValueMode { get; set; } = 2;
    public double IdleAfr { get; set; } = 13.8;
    public double CruiseAfr { get; set; } = 14.7;
    public double WotAfr { get; set; } = 12.8;
    public double BoostAfr { get; set; } = 11.8;
    public double IntakeAirTemperatureF { get; set; } = 70;
    public double BarometricPressurePsi { get; set; } = 14.7;
    public bool SelectedCellsOnly { get; set; }
    public int ApplyMode { get; set; }
    public double BlendStrength { get; set; } = .7;
    public double ContourStrength { get; set; } = .7;
    public bool EnableFinalSmoothing { get; set; } = true;
    public bool EnableBoundarySmoothing { get; set; }
    public AdvancedSmoothingAlgorithm BoundarySmoothingAlgorithm { get; set; } = AdvancedSmoothingAlgorithm.ConstrainedSurface;
    public double BoundarySmoothingStrength { get; set; } = .6;
    public int BoundarySmoothingPasses { get; set; } = 3;
    public int HorizontalSmoothCells { get; set; } = 3;
    public int VerticalSmoothCells { get; set; } = 3;
    public bool PreserveOuterValues { get; set; }
}

public readonly record struct VeSelection(int Top, int Bottom, int Left, int Right);
public readonly record struct VeRegionBoundary(int IdleColumn, int WotRow);

public sealed class VeSetupWizard : Window
{
    private readonly double[,] current;
    private readonly double[] rpm;
    private double[] map;
    private string mapUnit;
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
    private TextBlock maximumMapLabel = null!, idleMapLabel = null!, idleHighMapLabel = null!, wotVeLabel = null!, boostVeLabel = null!, wotAfrLabel = null!, boostAfrLabel = null!, applicationModeNote = null!;
    private CheckBox boostedBox = null!, selectedOnlyBox = null!, preserveOuterBox = null!, smoothBoundariesBox = null!, manualVeTargetsBox = null!, finalSmoothingBox = null!;
    private ComboBox applyModeBox = null!, smoothingAlgorithmBox = null!, regionValueModeBox = null!, camshaftBox = null!, mapSensorBox = null!;
    private Slider blendSlider = null!, contourSlider = null!;
    private int pageIndex;

    public VeSetupWizard(double[,] current, double[] rpm, double[] map, string mapUnit, VeSelection? selection, VeRegionBoundary regionBoundary, VeSetupSettings initial, Action requestBoundaryPick, Func<double, double, double[]?> rescaleMapAxis, Action<double[,], VeSetupSettings> apply)
    {
        this.current = (double[,])current.Clone(); this.rpm = rpm.ToArray(); this.map = map.ToArray(); this.mapUnit = mapUnit; this.selection = selection; this.regionBoundary = regionBoundary; this.requestBoundaryPick = requestBoundaryPick; this.rescaleMapAxis = rescaleMapAxis; this.apply = apply;
        settings = Clone(initial);
        if (settings.RegionValueMode != 2) { settings.RegionValueMode = 2; settings.EnableBoundarySmoothing = false; settings.PreserveOuterValues = false; }
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

    public void UpdateMapAxisAndUnit(double[] mapAxis, string updatedMapUnit, VeRegionBoundary boundary)
    {
        mapUnit = updatedMapUnit;
        UpdateBoundaryMapValues(mapAxis, boundary);
        RefreshMapPresentation();
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
        if (inputs.TryGetValue(key, out var input)) input.Text = FormatDisplayMap(value);
    }

    private void SetNumberInput(string key, double value)
    {
        if (inputs.TryGetValue(key, out var input)) input.Text = value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private bool NativeMapIsPsi => mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase);
    private bool DisplayMapAsPsi => boostedBox?.IsChecked == true;
    private string DisplayMapUnit => DisplayMapAsPsi ? "PSI gauge" : "kPa absolute";
    private string DisplayMapFormat => DisplayMapAsPsi ? "0.0" : "0";
    private string FormatDisplayMap(double value) => value.ToString(DisplayMapFormat, CultureInfo.InvariantCulture);
    private static double ConvertMapUnit(double value, bool fromPsi, bool toPsi) => fromPsi == toPsi ? value : toPsi ? (value - 101.325) / 6.894757293168361 : value * 6.894757293168361 + 101.325;
    private double ToDisplayMap(double nativeValue) => ConvertMapUnit(nativeValue, NativeMapIsPsi, DisplayMapAsPsi);
    private double FromDisplayMap(double displayValue) => ConvertMapUnit(displayValue, DisplayMapAsPsi, NativeMapIsPsi);

    private void ApplyMapSensorDefault(bool force)
    {
        if (mapSensorBox is null || !inputs.ContainsKey("maxMap") || (!force && pageIndex != 0)) return;
        var bars = Math.Clamp(mapSensorBox.SelectedIndex + 1, 1, 3);
        var displayMaximum = DisplayMapAsPsi ? (bars - 1) * settings.BarometricPressurePsi : 100;
        inputs["maxMap"].Text = FormatDisplayMap(displayMaximum);
    }

    private void RefreshMapPresentation()
    {
        var boosted = DisplayMapAsPsi;
        if (maximumMapLabel is not null) maximumMapLabel.Text = boosted ? $"MAXIMUM BOOST MAP — TABLE TOP ({DisplayMapUnit})" : $"MAXIMUM MAP — TABLE TOP ({DisplayMapUnit})";
        if (idleMapLabel is not null) idleMapLabel.Text = $"MINIMUM MAP / IDLE LOW MAP — TABLE BOTTOM ({DisplayMapUnit})";
        if (idleHighMapLabel is not null) idleHighMapLabel.Text = $"IDLE HIGH MAP — HORIZONTAL BOUNDARY ({DisplayMapUnit})";
        if (wotVeLabel is not null) wotVeLabel.Text = boosted ? "VE AT 0 PSI (%)" : "WOT VE AT ATMOSPHERIC MAP (%)";
        if (wotAfrLabel is not null) wotAfrLabel.Text = boosted ? "AFR AT 0 PSI" : "WOT TARGET AFR";
        SetFieldVisibility(boostVeLabel, "boostVe", boosted); SetFieldVisibility(boostAfrLabel, "boostAfr", boosted);
        if (applicationModeNote is not null) applicationModeNote.Text = boosted
            ? "BOOSTED SETUP  •  MAP is shown in PSI gauge. VE and AFR targets above 0 PSI are enabled."
            : "NATURALLY ASPIRATED SETUP  •  MAP is shown in kPa absolute. Boost-only VE and AFR targets are hidden.";
        SetInput("idleMap", ToDisplayMap(settings.IdleMap)); SetInput("idleHighMap", ToDisplayMap(settings.IdleHighMap)); SetInput("maxMap", ToDisplayMap(settings.MaximumMap));
    }

    private void SetFieldVisibility(TextBlock? label, string key, bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (label is not null) label.Visibility = visibility;
        if (inputs.TryGetValue(key, out var input)) input.Visibility = visibility;
    }

    private void BuildPages()
    {
        var engine = Page("1. Engine & ECU Setup", "Enter physical setup details. Map Lab uses them to derive starter VE targets, then the existing contour generator builds the surface.");
        AddField(engine, "Cylinders", "cylinders", settings.Cylinders); AddField(engine, "Displacement (cu in)", "displacement", settings.DisplacementCi); boostedBox = new CheckBox { Content = "Forced induction / boosted", IsChecked = settings.Boosted, Margin = new Thickness(0, 5, 0, 5) }; engine.Children.Add(boostedBox);
        applicationModeNote = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) }; engine.Children.Add(applicationModeNote);
        engine.Children.Add(Label("CAMSHAFT DURATION AT 0.050-INCH LIFT")); camshaftBox = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = Math.Clamp(settings.CamshaftDurationRange, 0, 3), Margin = new Thickness(0, 0, 0, 12), Foreground = Brushes.Black, Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5) };
        foreach (var name in new[] { "Under 215 degrees - mild", "215-234 degrees - street", "235-254 degrees - large street/strip", "255+ degrees - race" }) camshaftBox.Items.Add(new ComboBoxItem { Content = name, Foreground = Brushes.Black }); engine.Children.Add(camshaftBox);
        AddField(engine, "Target hot idle RPM", "idleRpm", settings.IdleRpm); AddField(engine, "Peak torque RPM", "peakRpm", settings.PeakTorqueRpm); AddField(engine, "Maximum RPM", "maxRpm", settings.MaximumRpm);
        engine.Children.Add(Label("MAP SENSOR")); mapSensorBox = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = Math.Clamp(settings.MapSensorBar - 1, 0, 2), Margin = new Thickness(0, 0, 0, 12), Foreground = Brushes.Black, Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5) };
        foreach (var name in new[] { "1-bar", "2-bar", "3-bar" }) mapSensorBox.Items.Add(new ComboBoxItem { Content = name, Foreground = Brushes.Black }); engine.Children.Add(mapSensorBox);
        idleMapLabel = AddField(engine, $"Minimum MAP / Idle Low MAP — table bottom ({mapUnit})", "idleMap", settings.IdleMap);
        maximumMapLabel = AddField(engine, $"Maximum MAP — table top ({mapUnit})", "maxMap", settings.MaximumMap);
        var applyMapRange = MakeButton("Apply MAP Range", true); applyMapRange.Margin = new Thickness(0, 2, 0, 0); applyMapRange.Click += (_, _) => ApplyMapRange(true); engine.Children.Add(applyMapRange);
        engine.Children.Add(new TextBlock { Text = "Rescales all MAP breakpoints. The horizontal region boundary moves to the nearest new breakpoint and can be adjusted again in Step 2.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 7, 0, 0) }); pages.Add(engine);

        var anchors = Page("2. Fuel System & Targets", "These are the setup-wizard facts Map Lab uses for fuel-flow estimates and derived starter VE targets.");
        AddField(anchors, "Injector flow at rated pressure (lb/hr)", "injectorFlow", settings.InjectorFlowLbHr); AddField(anchors, "Injector rated pressure (psi)", "ratedPressure", settings.InjectorRatedPressurePsi); AddField(anchors, "Operating fuel pressure (psi)", "fuelPressure", settings.FuelPressurePsi);
        anchors.Children.Add(new TextBlock { Text = "Terminator X LS base calibrations commonly use 60 psi actual system fuel pressure. Enter the injector's rated pressure separately from the vehicle's operating pressure.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        anchors.Children.Add(new TextBlock { Text = "Terminator X ECUs require compatible high-impedance injectors; Map Lab uses the injector data only for estimates and warnings.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        AddField(anchors, "Idle target AFR", "idleAfr", settings.IdleAfr); AddField(anchors, "Cruise target AFR", "cruiseAfr", settings.CruiseAfr); wotAfrLabel = AddField(anchors, "AFR at 0 PSI", "wotAfr", settings.WotAfr); boostAfrLabel = AddField(anchors, "Boost target AFR", "boostAfr", settings.BoostAfr);
        AddField(anchors, "Reference intake-air temperature (°F)", "iatF", settings.IntakeAirTemperatureF); AddField(anchors, "Barometric pressure (psi absolute)", "baro", settings.BarometricPressurePsi);
        pages.Add(anchors);
        boostedBox.Checked += (_, _) => { RefreshMapPresentation(); ApplyMapSensorDefault(false); };
        boostedBox.Unchecked += (_, _) => RefreshMapPresentation();
        mapSensorBox.SelectionChanged += (_, _) => ApplyMapSensorDefault(false);
        RefreshMapPresentation();

        var shape = Page("3. Shape the VE Surface", "The wizard derives these targets from engine setup. Open the advanced section only when you want to hand-shape the starter table.");
        var boundaryControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var setBoundaries = MakeButton("⌖  Set Boundaries on Fuel Table", true); setBoundaries.Margin = new Thickness(0); setBoundaries.Click += (_, _) => BeginBoundaryPick(); boundaryControls.Children.Add(setBoundaries);
        boundaryControls.Children.Add(new TextBlock { Text = "Hover over the table and click the intersection to lock", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) }); shape.Children.Add(boundaryControls);
        regionValueModeBox = new ComboBox { Width = 390, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = 2, Visibility = Visibility.Collapsed };
        regionValueModeBox.Items.Add(new ComboBoxItem { Content = "Fill quadrants with region values", Foreground = Brushes.Black }); regionValueModeBox.Items.Add(new ComboBoxItem { Content = "Interpolate complete map with fixed boundary lines", Foreground = Brushes.Black }); regionValueModeBox.Items.Add(new ComboBoxItem { Content = "Continuous contoured surface", Foreground = Brushes.Black }); shape.Children.Add(regionValueModeBox);
        shape.Children.Add(Label("CONTOUR STRENGTH")); contourSlider = new Slider { Minimum = 0, Maximum = 1, Value = settings.ContourStrength, TickFrequency = .1, IsSnapToTickEnabled = true, Width = 340, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) }; shape.Children.Add(contourSlider);
        shape.Children.Add(new TextBlock { Text = "Lower values hold the region anchors more tightly. Higher values create broader, softer transitions between regions.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        var advancedVe = new StackPanel(); manualVeTargetsBox = new CheckBox { Content = "Override derived VE targets", IsChecked = settings.ManualVeTargets, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold }; advancedVe.Children.Add(manualVeTargetsBox);
        idleHighMapLabel = AddField(advancedVe, $"Idle High MAP — horizontal boundary ({mapUnit})", "idleHighMap", settings.IdleHighMap, true); AddField(advancedVe, "VE at Idle Low MAP (%)", "idleVe", settings.IdleVe); AddField(advancedVe, "VE at Idle High MAP (%)", "idleHighVe", settings.IdleHighVe);
        AddField(advancedVe, "Cruise VE — lower-right region (%)", "cruiseVe", settings.CruiseVe); AddField(advancedVe, "Part-throttle VE (%)", "partVe", settings.PartThrottleVe);
        wotVeLabel = AddField(advancedVe, "WOT VE at atmospheric MAP (%)", "wotVe", settings.WotVe); AddField(advancedVe, "High-RPM WOT VE (%)", "highVe", settings.HighRpmVe); boostVeLabel = AddField(advancedVe, "VE at maximum boost (%)", "boostVe", settings.BoostVe);
        shape.Children.Add(new Expander { Header = "Advanced VE targets", IsExpanded = settings.ManualVeTargets, Content = advancedVe, Margin = new Thickness(0, 0, 0, 8), Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)) }); pages.Add(shape);
        RefreshMapPresentation();

        var application = Page("4. Application & Transitions", "Choose how the generated values are applied. Additional boundary refinement is optional because the normal generator already produces one continuous surface.");
        selectedOnlyBox = new CheckBox { Content = selection is null ? "Selected cells only (no fuel-cell selection is active)" : $"Selected cells only ({selection.Value.Right - selection.Value.Left + 1} × {selection.Value.Bottom - selection.Value.Top + 1})", IsChecked = selection is not null && settings.SelectedCellsOnly, IsEnabled = selection is not null, Margin = new Thickness(0, 5, 0, 16) }; application.Children.Add(selectedOnlyBox);
        application.Children.Add(Label("APPLICATION MODE")); applyModeBox = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = Math.Clamp(settings.ApplyMode, 0, 2), Margin = new Thickness(0, 0, 0, 16) };
        applyModeBox.Items.Add("Replace current values"); applyModeBox.Items.Add("Blend with current values"); applyModeBox.Items.Add("Fill zero/empty values only"); application.Children.Add(applyModeBox);
        var blendOptions = new StackPanel { Visibility = settings.ApplyMode == 1 ? Visibility.Visible : Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
        blendOptions.Children.Add(Label("BLEND STRENGTH")); blendSlider = new Slider { Minimum = .1, Maximum = 1, Value = settings.BlendStrength, TickFrequency = .1, IsSnapToTickEnabled = true, Width = 320, HorizontalAlignment = HorizontalAlignment.Left }; blendOptions.Children.Add(blendSlider); application.Children.Add(blendOptions);
        applyModeBox.SelectionChanged += (_, _) => blendOptions.Visibility = applyModeBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        finalSmoothingBox = new CheckBox { Content = "Smooth the generated map before committing", IsChecked = settings.EnableFinalSmoothing, Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold };
        application.Children.Add(finalSmoothingBox);
        application.Children.Add(new TextBlock { Text = "When enabled, Map Lab applies the same result as selecting the full fuel table, clicking Smooth Rows, then clicking Smooth Columns.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(20, 0, 0, 12) });
        application.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)), Margin = new Thickness(0, 8, 0, 16) });
        var advancedTransitions = new StackPanel(); smoothBoundariesBox = new CheckBox { Content = "Smooth values across region/setup boundaries", IsChecked = settings.EnableBoundarySmoothing, Margin = new Thickness(0, 2, 0, 4), FontWeight = FontWeights.SemiBold }; advancedTransitions.Children.Add(smoothBoundariesBox);
        advancedTransitions.Children.Add(new TextBlock { Text = "The continuous contour normally needs no additional boundary smoothing. Enable this only for extra local refinement.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), FontSize = 11, Margin = new Thickness(20, 0, 0, 10) });
        var smoothingOptions = new StackPanel { Visibility = settings.EnableBoundarySmoothing ? Visibility.Visible : Visibility.Collapsed }; smoothingOptions.Children.Add(Label("SMOOTHING ALGORITHM"));
        smoothingAlgorithmBox = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = (int)settings.BoundarySmoothingAlgorithm, Margin = new Thickness(0, 0, 0, 12), Foreground = Brushes.Black, Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5) };
        foreach (var name in new[] { "Shape-preserving interpolation", "Constrained surface smoothing", "Spike removal (median)", "Edge-preserving smoothing", "Weighted center / perimeter", "Standard weighted smoothing" }) smoothingAlgorithmBox.Items.Add(new ComboBoxItem { Content = name, Foreground = Brushes.Black }); smoothingOptions.Children.Add(smoothingAlgorithmBox);
        AddField(smoothingOptions, "Cells on each side of vertical boundary (minimum 3)", "horizontal", settings.HorizontalSmoothCells); AddField(smoothingOptions, "Cells on each side of horizontal boundary (minimum 3)", "vertical", settings.VerticalSmoothCells); AddField(smoothingOptions, "Smoothing strength (1–100%)", "smoothStrength", settings.BoundarySmoothingStrength * 100); AddField(smoothingOptions, "Smoothing passes (1–20)", "smoothPasses", settings.BoundarySmoothingPasses);
        preserveOuterBox = new CheckBox { Content = "Preserve the outermost values of the applied area", IsChecked = settings.PreserveOuterValues, Margin = new Thickness(0, 8, 0, 0) };
        smoothBoundariesBox.Checked += (_, _) => smoothingOptions.Visibility = Visibility.Visible; smoothBoundariesBox.Unchecked += (_, _) => smoothingOptions.Visibility = Visibility.Collapsed;
        advancedTransitions.Children.Add(smoothingOptions); advancedTransitions.Children.Add(preserveOuterBox); application.Children.Add(new Expander { Header = "Advanced boundary refinement", IsExpanded = false, Content = advancedTransitions, Margin = new Thickness(0, 0, 0, 8), Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)) }); pages.Add(application);
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
        { validationText.Text = $"The requested MAP range is too narrow for the current number of {(DisplayMapAsPsi ? "0.1 PSI" : "whole-number kPa")} breakpoints."; return false; }
        map = updated.ToArray();
        var adjustedBoundary = Math.Clamp(previousBoundary, map[^1], map[0]); var boundaryRow = 0; var distance = double.MaxValue;
        for (var row = 0; row < map.Length; row++) { var currentDistance = Math.Abs(map[row] - adjustedBoundary); if (currentDistance < distance) { boundaryRow = row; distance = currentDistance; } }
        regionBoundary = new VeRegionBoundary(regionBoundary.IdleColumn, boundaryRow); SetBoundaryDerivedMapValues(); RefreshMapPresentation();
        validationText.Foreground = new SolidColorBrush(Color.FromRgb(25, 120, 70));
        validationText.Text = showConfirmation ? $"MAP scale updated to {FormatDisplayMap(ToDisplayMap(map[^1]))}–{FormatDisplayMap(ToDisplayMap(map[0]))} {DisplayMapUnit}. The boundary was moved to the nearest breakpoint." : "";
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
        bool ReadInt(string key, out int value)
        {
            if (double.TryParse(inputs[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
            {
                value = (int)Math.Round(parsed); return true;
            }
            value = 0; return false;
        }
        var smoothingEnabled = smoothBoundariesBox.IsChecked == true;
        var horizontal = (double)settings.HorizontalSmoothCells; var vertical = (double)settings.VerticalSmoothCells; var smoothStrength = settings.BoundarySmoothingStrength * 100; var smoothPasses = (double)settings.BoundarySmoothingPasses;
        var smoothingValuesValid = !smoothingEnabled || Read("horizontal", out horizontal) && Read("vertical", out vertical) && horizontal >= 3 && vertical >= 3 && Read("smoothStrength", out smoothStrength) && smoothStrength is >= 1 and <= 100 && Read("smoothPasses", out smoothPasses) && smoothPasses is >= 1 and <= 20;
        var manualVeTargets = manualVeTargetsBox.IsChecked == true;
        if (!ReadInt("cylinders", out var cylinders) || cylinders is < 1 or > 16 ||
            !Read("displacement", out var displacement) || displacement <= 0 || !Read("idleRpm", out var idleRpm) || !Read("peakRpm", out var peakRpm) || !Read("maxRpm", out var maxRpm) || idleRpm >= peakRpm || peakRpm >= maxRpm ||
            !Read("idleMap", out var idleMap) || !Read("idleHighMap", out var idleHighMap) || !Read("maxMap", out var maxMap) || idleMap > idleHighMap || idleHighMap > maxMap ||
            !Read("injectorFlow", out var injectorFlow) || injectorFlow is < 1 or > 500 || !Read("ratedPressure", out var ratedPressure) || ratedPressure is < 1 or > 200 || !Read("fuelPressure", out var fuelPressure) || fuelPressure is < 1 or > 200 ||
            !Read("idleAfr", out var idleAfr) || !Read("cruiseAfr", out var cruiseAfr) || !Read("wotAfr", out var wotAfr) || !Read("boostAfr", out var boostAfr) || new[] { idleAfr, cruiseAfr, wotAfr, boostAfr }.Any(value => value is < 5 or > 30) ||
            !Read("iatF", out var iatF) || iatF is < -100 or > 350 || !Read("baro", out var baro) || baro is < 8 or > 16 ||
            !smoothingValuesValid)
        { validationText.Text = "Check the entries. Cylinders must be 1–16, MAP must run low-to-high, injector and pressure values must be positive, AFR 5–30, and smoothing values inside range."; return false; }

        var idleVe = settings.IdleVe; var idleHighVe = settings.IdleHighVe; var cruiseVe = settings.CruiseVe; var partVe = settings.PartThrottleVe; var wotVe = settings.WotVe; var highVe = settings.HighRpmVe; var boostVe = settings.BoostVe;
        if (manualVeTargets)
        {
            if (!Read("idleVe", out idleVe) || !Read("idleHighVe", out idleHighVe) || !Read("cruiseVe", out cruiseVe) || !Read("partVe", out partVe) || !Read("wotVe", out wotVe) || !Read("highVe", out highVe) || !Read("boostVe", out boostVe) ||
                new[] { idleVe, idleHighVe, cruiseVe, partVe, wotVe, highVe, boostVe }.Any(value => value is < 1 or > 250))
            { validationText.Text = "Advanced VE targets must be valid percentages from 1–250."; return false; }
        }
        idleMap = FromDisplayMap(idleMap); idleHighMap = FromDisplayMap(idleHighMap); maxMap = FromDisplayMap(maxMap);
        settings.Cylinders = cylinders; settings.DisplacementCi = displacement; settings.Boosted = boostedBox.IsChecked == true; settings.CamshaftDurationRange = Math.Clamp(camshaftBox.SelectedIndex, 0, 3); settings.MapSensorBar = Math.Clamp(mapSensorBox.SelectedIndex + 1, 1, 3);
        settings.InjectorFlowLbHr = injectorFlow; settings.InjectorRatedPressurePsi = ratedPressure; settings.FuelPressurePsi = fuelPressure;
        settings.IdleRpm = idleRpm; settings.PeakTorqueRpm = peakRpm; settings.MaximumRpm = maxRpm; settings.IdleMap = idleMap; settings.MaximumMap = maxMap; settings.ManualVeTargets = manualVeTargets;
        if (!manualVeTargets)
        {
            ApplyDerivedVeTargets(settings, maxMap, NativeMapIsPsi);
            SetVeTargetInputs();
        }
        else
        {
            settings.IdleVe = idleVe; settings.IdleHighVe = idleHighVe; settings.CruiseVe = cruiseVe; settings.PartThrottleVe = partVe; settings.WotVe = wotVe; settings.HighRpmVe = highVe; settings.BoostVe = boostVe;
        }
        settings.IdleHighMap = idleHighMap;
        settings.RegionValueMode = 2; settings.ContourStrength = contourSlider.Value;
        settings.IdleAfr = idleAfr; settings.CruiseAfr = cruiseAfr; settings.WotAfr = wotAfr; settings.BoostAfr = boostAfr; settings.IntakeAirTemperatureF = iatF; settings.BarometricPressurePsi = baro;
        settings.SelectedCellsOnly = selectedOnlyBox.IsChecked == true && selection is not null; settings.ApplyMode = applyModeBox.SelectedIndex; settings.BlendStrength = blendSlider.Value;
        settings.EnableFinalSmoothing = finalSmoothingBox.IsChecked == true;
        settings.EnableBoundarySmoothing = smoothingEnabled; settings.BoundarySmoothingAlgorithm = (AdvancedSmoothingAlgorithm)Math.Max(0, smoothingAlgorithmBox.SelectedIndex); settings.BoundarySmoothingStrength = smoothStrength / 100; settings.BoundarySmoothingPasses = (int)Math.Round(smoothPasses);
        settings.HorizontalSmoothCells = (int)Math.Round(horizontal); settings.VerticalSmoothCells = (int)Math.Round(vertical); settings.PreserveOuterValues = preserveOuterBox.IsChecked == true; return true;
    }

    private void SetVeTargetInputs()
    {
        SetNumberInput("idleVe", settings.IdleVe); SetNumberInput("idleHighVe", settings.IdleHighVe); SetNumberInput("cruiseVe", settings.CruiseVe);
        SetNumberInput("partVe", settings.PartThrottleVe); SetNumberInput("wotVe", settings.WotVe); SetNumberInput("highVe", settings.HighRpmVe); SetNumberInput("boostVe", settings.BoostVe);
    }

    private static void ApplyDerivedVeTargets(VeSetupSettings s, double maximumMap, bool mapIsPsiGauge)
    {
        var cam = Math.Clamp(s.CamshaftDurationRange, 0, 3);
        var displacementPerCylinder = s.DisplacementCi / Math.Max(1, s.Cylinders);
        var engineScale = Math.Clamp((displacementPerCylinder - 43.75) / 18d, -.8, .8);
        var maximumGaugePsi = mapIsPsiGauge ? maximumMap : (maximumMap - 101.325) / 6.894757293168361;
        var boostPsi = s.Boosted ? Math.Max(0, maximumGaugePsi) : 0;

        s.IdleVe = Math.Round(38 + cam * 5 + engineScale * 2, 1);
        s.IdleHighVe = Math.Round(s.IdleVe + 11 + cam * 2, 1);
        s.CruiseVe = Math.Round(55 + cam * 2 + engineScale, 1);
        s.PartThrottleVe = Math.Round(72 + cam * 3 + engineScale * 1.5, 1);
        s.WotVe = Math.Round(92 + cam * 4 + engineScale * 2, 1);
        s.HighRpmVe = Math.Round(s.WotVe - Math.Max(4, 10 - cam * 2), 1);
        s.BoostVe = Math.Round(s.Boosted ? Math.Min(140, s.WotVe + 8 + boostPsi * .45) : s.WotVe + 4, 1);
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
        var correctedInjector = CorrectedInjectorFlow(settings); var injectorCapacity = correctedInjector * settings.Cylinders; var peakFuel = fuelFlow.Cast<double>().Max();
        var maxCornerFuel = fuelFlow[0, fuelFlow.GetLength(1) - 1]; var maxCornerVe = proposed[0, proposed.GetLength(1) - 1];
        panel.Children.Add(new TextBlock { Text = $"Setup: {settings.Cylinders} cylinders  •  {settings.DisplacementCi:0.#} cu in  •  {CamshaftDescription(settings.CamshaftDurationRange)}  •  {settings.MapSensorBar}-bar MAP", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 5, 0, 0) });
        panel.Children.Add(new TextBlock { Text = $"Calculation details: max RPM/max MAP cell {maxCornerVe:0.0}% VE, {maxCornerFuel:0.0} lb/hr total, {maxCornerFuel / Math.Max(1, settings.Cylinders):0.0} lb/hr per injector, {maxCornerFuel / Math.Max(.1, injectorCapacity):P0} duty  •  preview peak {peakFuel:0.0} lb/hr total, {peakFuel / Math.Max(.1, injectorCapacity):P0} duty  •  injector capacity {correctedInjector:0.0} lb/hr each, {injectorCapacity:0.0} total", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 0) });
        var idleColumn = Math.Clamp(regionBoundary.IdleColumn, 0, rpm.Length - 1); var wotRow = Math.Clamp(regionBoundary.WotRow, 0, map.Length - 1);
        panel.Children.Add(new TextBlock { Text = $"Regions: Idle Low MAP below and Idle High MAP above {FormatDisplayMap(ToDisplayMap(map[wotRow]))} {DisplayMapUnit} on the left of {rpm[idleColumn]:0} RPM  •  Cruise below and Part Throttle/WOT above on the right", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 5, 0, 0) });
        panel.Children.Add(new TextBlock { Text = settings.EnableBoundarySmoothing ? $"Advanced boundary refinement: {settings.BoundarySmoothingAlgorithm}  •  {settings.BoundarySmoothingPasses} passes  •  {settings.BoundarySmoothingStrength:P0}" : "Advanced boundary refinement: Off", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
        panel.Children.Add(new TextBlock { Text = $"Surface contour: Continuous  •  Strength {settings.ContourStrength:P0}", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
        panel.Children.Add(new TextBlock { Text = settings.EnableFinalSmoothing ? "Final smoothing before commit: Smooth Rows, then Smooth Columns" : "Final smoothing before commit: Off", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
        panel.Children.Add(new TextBlock { Text = settings.ManualVeTargets ? "VE targets: Manual advanced override" : "VE targets: Derived from engine, cam, induction, and MAP sensor setup", Foreground = new SolidColorBrush(Color.FromRgb(94, 94, 94)), Margin = new Thickness(0, 3, 0, 0) });
    }

    private Border PreviewCard(string title, double[,] values, string suffix)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; stack.Children.Add(Label(title));
        var grid = new UniformGrid { Rows = Math.Min(16, values.GetLength(0)), Columns = Math.Min(24, values.GetLength(1)), Height = 260 };
        var min = values.Cast<double>().Min(); var max = values.Cast<double>().Max();
        for (var rowIndex = 0; rowIndex < grid.Rows; rowIndex++) for (var colIndex = 0; colIndex < grid.Columns; colIndex++)
        {
            var row = (int)Math.Round(rowIndex * (values.GetLength(0) - 1d) / Math.Max(1, grid.Rows - 1)); var col = (int)Math.Round(colIndex * (values.GetLength(1) - 1d) / Math.Max(1, grid.Columns - 1));
            grid.Children.Add(new Border { Background = new SolidColorBrush(Heat((values[row, col] - min) / Math.Max(.1, max - min))), BorderBrush = new SolidColorBrush(Color.FromRgb(30, 40, 52)), BorderThickness = new Thickness(.35), ToolTip = $"{rpm[col]:0} RPM • {FormatDisplayMap(ToDisplayMap(map[row]))} {DisplayMapUnit} • {values[row, col]:0.0}{suffix}" });
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
        if (settings.RegionValueMode == 2)
        {
            generated = GenerateContinuousSurface(rpm, map, settings, idleColumn, regionMap, wotMap);
        }
        else for (var row = 0; row < rows; row++) for (var col = 0; col < cols; col++)
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
            var highLoad = fillQuadrants ? load : isIdleSide ? load * .9 : isCruise ? load * .94 : HighRpmPowerVe(map[row], settings, wotMap, regionMap, load);
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
        if (settings.RegionValueMode == 2 && settings.EnableFinalSmoothing)
        {
            result = SmoothAllRows(result, rpm);
            result = SmoothAllColumns(result, map);
        }
        var roundingArea = settings.RegionValueMode == 2 ? new VeSelection(0, rows - 1, 0, cols - 1) : scope;
        for (var row = roundingArea.Top; row <= roundingArea.Bottom; row++) for (var col = roundingArea.Left; col <= roundingArea.Right; col++) result[row, col] = Math.Round(result[row, col], 1);
        return result;
    }

    private static double[,] SmoothAllRows(double[,] source, double[] rpm)
    {
        var rows = source.GetLength(0); var cols = source.GetLength(1); var result = (double[,])source.Clone();
        if (cols < 3 || Math.Abs(rpm[^1] - rpm[0]) < .000001) return result;
        for (var row = 0; row < rows; row++) for (var col = 1; col < cols - 1; col++)
        {
            var fraction = Smooth((rpm[col] - rpm[0]) / (rpm[^1] - rpm[0]));
            result[row, col] = source[row, 0] + (source[row, cols - 1] - source[row, 0]) * fraction;
        }
        return result;
    }

    private static double[,] SmoothAllColumns(double[,] source, double[] map)
    {
        var rows = source.GetLength(0); var cols = source.GetLength(1); var result = (double[,])source.Clone();
        if (rows < 3 || Math.Abs(map[0] - map[^1]) < .000001) return result;
        for (var col = 0; col < cols; col++) for (var row = 1; row < rows - 1; row++)
        {
            var fraction = Smooth((map[0] - map[row]) / (map[0] - map[^1]));
            result[row, col] = source[0, col] + (source[rows - 1, col] - source[0, col]) * fraction;
        }
        return result;
    }

    private static double[,] GenerateContinuousSurface(double[] rpm, double[] map, VeSetupSettings settings, int idleColumn, double regionMap, double zeroPsiMap)
    {
        var rows = map.Length; var cols = rpm.Length; var result = new double[rows, cols];
        var contour = Math.Clamp(settings.ContourStrength, 0, 1);
        var transitionRadius = Math.Max(1, (int)Math.Round((1 + contour * Math.Max(2, cols / 8d))));
        var transitionLeft = Math.Max(0, idleColumn - transitionRadius); var transitionRight = Math.Min(cols - 1, idleColumn + transitionRadius);
        var minimumMap = map[^1]; var maximumMap = map[0];

        double Curve(double value) => Lerp(value, Smooth(value), contour);
        for (var row = 0; row < rows; row++)
        {
            var currentMap = map[row];
            var lowLoadProgress = Curve(Normalize(currentMap, minimumMap, regionMap));
            var idleLoad = Lerp(settings.IdleVe, settings.IdleHighVe, lowLoadProgress);
            var drivingLoad = currentMap <= regionMap
                ? Lerp(settings.CruiseVe, settings.PartThrottleVe, lowLoadProgress)
                : PowerRegionVe(currentMap, settings, zeroPsiMap, regionMap);
            if (currentMap > regionMap)
            {
                var upperProgress = Curve(Normalize(currentMap, regionMap, maximumMap));
                idleLoad = Lerp(settings.IdleHighVe, drivingLoad, upperProgress * .35);
            }
            var overallLoad = Curve(Normalize(currentMap, minimumMap, maximumMap));

            for (var col = 0; col < cols; col++)
            {
                var regionBlend = Curve(Normalize(col, transitionLeft, transitionRight));
                var value = Lerp(idleLoad, drivingLoad, regionBlend);
                var highRpmProgress = Curve(Normalize(rpm[col], settings.PeakTorqueRpm, settings.MaximumRpm));
                var highRpmTarget = HighRpmPowerVe(currentMap, settings, zeroPsiMap, regionMap, drivingLoad);
                value = Lerp(value, highRpmTarget, highRpmProgress);
                result[row, col] = value;
            }
        }
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

    private static double HighRpmPowerVe(double mapValue, VeSetupSettings s, double wotMap, double regionMap, double currentLoad)
    {
        if (mapValue <= regionMap) return currentLoad * .96;
        if (mapValue <= wotMap || !s.Boosted) return Lerp(currentLoad * .96, s.HighRpmVe, Smooth(Normalize(mapValue, regionMap, wotMap)));
        return Lerp(s.HighRpmVe, s.BoostVe, Smooth(Normalize(mapValue, wotMap, s.MaximumMap)));
    }

    private static double CorrectedInjectorFlow(VeSetupSettings settings) => settings.InjectorFlowLbHr * Math.Sqrt(Math.Max(.1, settings.FuelPressurePsi) / Math.Max(.1, settings.InjectorRatedPressurePsi));

    private static string CamshaftDescription(int index) => Math.Clamp(index, 0, 3) switch
    {
        0 => "mild cam",
        1 => "street cam",
        2 => "large street/strip cam",
        _ => "race cam"
    };

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
    private static VeSetupSettings Clone(VeSetupSettings value) => new() { Cylinders = value.Cylinders, DisplacementCi = value.DisplacementCi, Boosted = value.Boosted, CamshaftDurationRange = value.CamshaftDurationRange, MapSensorBar = value.MapSensorBar, InjectorFlowLbHr = value.InjectorFlowLbHr, InjectorRatedPressurePsi = value.InjectorRatedPressurePsi, FuelPressurePsi = value.FuelPressurePsi, ManualVeTargets = value.ManualVeTargets, IdleRpm = value.IdleRpm, PeakTorqueRpm = value.PeakTorqueRpm, MaximumRpm = value.MaximumRpm, IdleMap = value.IdleMap, MaximumMap = value.MaximumMap, IdleVe = value.IdleVe, IdleHighMap = value.IdleHighMap, IdleHighVe = value.IdleHighVe, CruiseVe = value.CruiseVe, PartThrottleVe = value.PartThrottleVe, WotVe = value.WotVe, HighRpmVe = value.HighRpmVe, BoostVe = value.BoostVe, RegionValueMode = value.RegionValueMode, IdleAfr = value.IdleAfr, CruiseAfr = value.CruiseAfr, WotAfr = value.WotAfr, BoostAfr = value.BoostAfr, IntakeAirTemperatureF = value.IntakeAirTemperatureF, BarometricPressurePsi = value.BarometricPressurePsi, SelectedCellsOnly = value.SelectedCellsOnly, ApplyMode = value.ApplyMode, BlendStrength = value.BlendStrength, ContourStrength = value.ContourStrength, EnableFinalSmoothing = value.EnableFinalSmoothing, EnableBoundarySmoothing = value.EnableBoundarySmoothing, BoundarySmoothingAlgorithm = value.BoundarySmoothingAlgorithm, BoundarySmoothingStrength = value.BoundarySmoothingStrength, BoundarySmoothingPasses = value.BoundarySmoothingPasses, HorizontalSmoothCells = value.HorizontalSmoothCells, VerticalSmoothCells = value.VerticalSmoothCells, PreserveOuterValues = value.PreserveOuterValues };
    private static Color Heat(double t) => Hsl(Math.Clamp(t, 0, 1) * 300, .96, .52);
    private static Color Hsl(double h, double s, double l) { var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2; var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) }; return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)); }
}
