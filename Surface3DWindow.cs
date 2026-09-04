using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TimingTableCalculator;

public enum SurfaceSelectionAction { Undo, Redo, Copy, Paste, Offset, SelectRing, Smooth, Refine, Advanced, SmoothRows, SmoothColumns, FlattenPath, SmoothPath, Clear }

public sealed class Surface3DWindow : Window
{
    private readonly PerspectiveCamera camera;
    private readonly AxisAngleRotation3D yaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D pitch = new(new Vector3D(1, 0, 0), 0);
    private readonly Viewport3D viewport;
    private readonly Canvas overlayLayer = new() { IsHitTestVisible = false, ClipToBounds = true };
    private Point lastPoint, orbitStartPoint;
    private bool rotating, orbitDragged, suppressNextContextMenu, selectingSurface;
    private readonly double[,] values;
    private readonly int rows, cols;
    private readonly double[] rpmAxis, mapAxis;
    private readonly string mapUnit, valueAxisTitle, mapAxisTitle, rpmAxisTitle, rpmFormat, valueFormat;
    private readonly Func<double, string>? valueFormatter;
    private const string MapFormat = "0.########";
    private string FormatMap(double value) => value.ToString(MapFormat, System.Globalization.CultureInfo.InvariantCulture);
    private readonly Func<int, int, int, int, double[,]> smoothSelection;
    private readonly Action<SurfaceSelectionAction, int, int, int, int, IReadOnlyCollection<(int Row, int Col)>, Action<double[,]>>? selectionAction;
    private readonly Func<double[,], IReadOnlyCollection<(int Row, int Col)>, double[,]>? sculptCommit;
    private readonly ModelVisual3D selectionVisual = new();
    private readonly ModelVisual3D hoverVisual = new();
    private readonly Transform3DGroup transforms = new();
    private GeometryModel3D surface = null!;
    private GeometryModel3D contourGrid = null!;
    private readonly bool useCustomColors;
    private readonly Color lowColor, highColor;
    private (int Row, int Col)? selectionStart, selectionEnd;
    private readonly HashSet<(int Row, int Col)> pinnedSurfaceSelection = [];
    private bool hideSelectionOverlay;
    private Button smoothButton = null!, selectButton = null!, flattenPathButton = null!, smoothPathButton = null!;
    private readonly List<ToggleButton> sculptButtons = [];
    private Slider brushRadius = null!, brushStrength = null!;
    private TextBox brushAmount = null!;
    private ComboBox brushFalloff = null!;
    private CheckBox limitSculptToSelection = null!, preventSculptOvershoot = null!;
    private TextBlock brushRadiusLabel = null!, brushStrengthLabel = null!;
    private TextBlock selectionStatus = null!;
    private Border hoverTip = null!;
    private TextBlock hoverText = null!;
    private (int Row, int Col)? hoverCell;
    private Border orientationGizmo = null!;
    private GeometryModel3D orientationSurface = null!, orientationGrid = null!;
    private readonly List<(TextBlock Label, Point3D LocalPosition)> scaleOverlayLabels = [];
    private readonly List<TextBlock> valueScaleLabels = [];
    private int transitionRingThickness = 1;
    private SurfaceSculptMode? sculptMode;
    private bool sculptingSurface;
    private double[,]? sculptStrokeOriginal;
    private (int Row, int Col)? lastSculptCell;
    private double flattenTarget;
    private readonly HashSet<(int Row, int Col)> sculptedCells = [];

    public Surface3DWindow(double[,] values, double[] rpm, double[] map, string mapUnit, bool useCustomColors, Color lowColor, Color highColor, Func<int, int, int, int, double[,]> smoothSelection, string windowTitle = "3D Timing Map", string valueAxisTitle = "SPARK TIMING (°)", Action<SurfaceSelectionAction, int, int, int, int, IReadOnlyCollection<(int Row, int Col)>, Action<double[,]>>? selectionAction = null, string mapAxisTitle = "MAP", string rpmAxisTitle = "ENGINE RPM", string rpmFormat = "0", string valueFormat = "0.0", Func<double, string>? valueFormatter = null, Func<double[,], IReadOnlyCollection<(int Row, int Col)>, double[,]>? sculptCommit = null)
    {
        this.values = values; rows = values.GetLength(0); cols = values.GetLength(1); this.smoothSelection = smoothSelection;
        rpmAxis = rpm.ToArray(); mapAxis = map.ToArray(); this.mapUnit = mapUnit; this.valueAxisTitle = valueAxisTitle; this.mapAxisTitle = mapAxisTitle; this.rpmAxisTitle = rpmAxisTitle; this.rpmFormat = rpmFormat; this.valueFormat = valueFormat; this.valueFormatter = valueFormatter;
        this.selectionAction = selectionAction; this.sculptCommit = sculptCommit;
        this.useCustomColors = useCustomColors; this.lowColor = lowColor; this.highColor = highColor;
        Title = windowTitle; Width = 1100; Height = 760; MinWidth = 720; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(8, 13, 20));

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(4, 0, 0, 14) };
        heading.Children.Add(new TextBlock { Text = windowTitle.ToUpperInvariant(), Foreground = new SolidColorBrush(Color.FromRgb(85, 214, 190)), FontSize = 12, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = $"{rpm.First().ToString(rpmFormat)}–{rpm.Last().ToString(rpmFormat)} {rpmAxisTitle}  •  {FormatMap(map.Last())}–{FormatMap(map.First())} {mapUnit}", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        var selectionControls = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var undoButton = new Button { Content = "↶  Undo", Padding = new Thickness(12, 6, 12, 6), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        var redoButton = new Button { Content = "↷  Redo", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White };
        selectButton = new Button { Content = "Clear selection", Padding = new Thickness(12, 6, 12, 6), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, ToolTip = "Clear all selected surface cells. Left-drag selects; right-drag rotates." };
        selectButton.Margin = new Thickness(16, 0, 0, 0);
        smoothButton = new Button { Content = "Smooth selected…", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(54, 199, 173)), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, IsEnabled = false };
        flattenPathButton = new Button { Content = "↗  Flatten path", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(72, 82, 95)), ToolTip = "Select exactly two cells. Interior cells become a straight value ramp; endpoints stay fixed." };
        smoothPathButton = new Button { Content = "∿  Smooth path", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 38, 53)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(72, 82, 95)), ToolTip = "Select exactly two cells. Smooth only the cells along the path; endpoints stay fixed." };
        selectionStatus = new TextBlock { Text = "Left-drag selects  •  right-drag rotates", Foreground = new SolidColorBrush(Color.FromRgb(143, 161, 184)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        undoButton.IsEnabled = redoButton.IsEnabled = selectionAction is not null;
        undoButton.Click += (_, _) => RunHistoryAction(SurfaceSelectionAction.Undo); redoButton.Click += (_, _) => RunHistoryAction(SurfaceSelectionAction.Redo);
        selectButton.Click += (_, _) => ClearSurfaceSelection();
        smoothButton.Click += (_, _) => { if (selectionAction is not null) RunSelectionAction(SurfaceSelectionAction.Advanced); else ApplySelectedSmoothing(); };
        flattenPathButton.Click += (_, _) => RunSelectionAction(SurfaceSelectionAction.FlattenPath);
        smoothPathButton.Click += (_, _) => RunSelectionAction(SurfaceSelectionAction.SmoothPath);
        flattenPathButton.Visibility = smoothPathButton.Visibility = selectionAction is null ? Visibility.Collapsed : Visibility.Visible;
        selectionControls.Children.Add(undoButton); selectionControls.Children.Add(redoButton); selectionControls.Children.Add(selectButton); selectionControls.Children.Add(smoothButton); selectionControls.Children.Add(flattenPathButton); selectionControls.Children.Add(smoothPathButton); selectionControls.Children.Add(selectionStatus); heading.Children.Add(selectionControls);
        if (sculptCommit is not null) heading.Children.Add(CreateSculptControls());
        root.Children.Add(heading);

        viewport = new Viewport3D { ClipToBounds = true };
        if (selectionAction is not null) viewport.ContextMenu = CreateSelectionContextMenu();
        viewport.ContextMenuOpening += (_, e) => { if (suppressNextContextMenu) { suppressNextContextMenu = false; e.Handled = true; } };
        viewport.MouseLeftButtonDown += (_, e) => BeginLeftPointer(viewport, e);
        viewport.MouseLeftButtonUp += (_, _) => EndLeftPointer(viewport);
        viewport.MouseRightButtonDown += (_, e) => BeginOrbit(viewport, e);
        viewport.MouseRightButtonUp += (_, e) => EndOrbit(viewport, e);
        viewport.MouseMove += (_, e) => { MovePointer(viewport, e); UpdateHover(viewport, e.GetPosition(viewport)); };
        viewport.MouseLeave += (_, _) => ClearHover();
        viewport.MouseWheel += (_, e) => Zoom(e.Delta);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && sculptingSurface) { CancelSculptStroke(); e.Handled = true; } };
        camera = new PerspectiveCamera(new Point3D(0, 18, 28), new Vector3D(0, -15, -28), new Vector3D(0, 1, 0), 45);
        viewport.Camera = camera;

        surface = CreateSurface(values, useCustomColors, lowColor, highColor);
        contourGrid = CreateContourGrid(values);
        var axisFrame = CreateAxisFrame();
        transforms.Children.Add(new RotateTransform3D(pitch)); transforms.Children.Add(new RotateTransform3D(yaw));
        surface.Transform = transforms;
        contourGrid.Transform = transforms;
        axisFrame.Transform = transforms;
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(110, 120, 135)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3)));
        scene.Children.Add(axisFrame);
        scene.Children.Add(surface);
        scene.Children.Add(contourGrid);
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        selectionVisual.Transform = transforms; viewport.Children.Add(selectionVisual);
        hoverVisual.Transform = transforms; viewport.Children.Add(hoverVisual);
        AddRotatingScaleLabels(viewport, transforms, values, rpm, map, mapUnit, valueAxisTitle, mapAxisTitle, rpmAxisTitle, rpmFormat, valueFormat);

        hoverText = new TextBlock { Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, LineHeight = 18 };
        hoverTip = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 15, 24, 36)), BorderBrush = new SolidColorBrush(Color.FromRgb(85, 214, 190)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 6, 9, 6), Child = hoverText, Visibility = Visibility.Collapsed };
        overlayLayer.Children.Add(hoverTip);
        orientationGizmo = CreateOrientationGizmo(); overlayLayer.Children.Add(orientationGizmo);
        var viewportHost = new Grid(); viewportHost.Children.Add(viewport); viewportHost.Children.Add(overlayLayer);
        var frame = new Border { Background = new SolidColorBrush(Color.FromRgb(10, 16, 25)), BorderBrush = new SolidColorBrush(Color.FromRgb(36, 50, 71)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = viewportHost };
        Grid.SetRow(frame, 1); root.Children.Add(frame);
        var help = new TextBlock { Text = "Left-drag selects  •  Right-drag rotates  •  Ctrl+click or Ctrl+drag adds another area  •  Right-click opens tools  •  Wheel zooms", Foreground = new SolidColorBrush(Color.FromRgb(118, 135, 156)), FontSize = 12, Margin = new Thickness(4, 12, 0, 0) };
        Grid.SetRow(help, 2); root.Children.Add(help); Content = root;
        viewport.Loaded += (_, _) => UpdateScaleOverlayPositions();
        viewport.SizeChanged += (_, _) => UpdateScaleOverlayPositions();
    }

    private FrameworkElement CreateSculptControls()
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 9, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "SCULPT", Foreground = UiBrushCache.Cruise, FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 5) });
        foreach (var mode in Enum.GetValues<SurfaceSculptMode>())
        {
            var button = new ToggleButton { Content = mode.ToString(), MinWidth = 66, Height = 30, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(9, 4, 9, 4), Foreground = Brushes.White, Background = UiBrushCache.GridLine, BorderBrush = UiBrushCache.AxisLine, ToolTip = mode switch { SurfaceSculptMode.Raise => "Paint higher table values.", SurfaceSculptMode.Lower => "Paint lower table values.", SurfaceSculptMode.Smooth => "Blend values toward their local neighbors.", _ => "Move values toward the value sampled when the stroke begins." } };
            button.Click += (_, _) => ToggleSculptMode(mode);
            sculptButtons.Add(button); panel.Children.Add(button);
        }

        brushRadiusLabel = SculptLabel("Radius 2");
        brushRadius = new Slider { Minimum = 1, Maximum = 10, Value = 2, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 86, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Brush radius measured against the real RPM and MAP breakpoint spacing." };
        brushRadius.ValueChanged += (_, _) => brushRadiusLabel.Text = $"Radius {(int)brushRadius.Value}"; panel.Children.Add(SculptField(brushRadiusLabel, brushRadius));
        brushStrengthLabel = SculptLabel("Strength 35%");
        brushStrength = new Slider { Minimum = 1, Maximum = 100, Value = 35, TickFrequency = 1, Width = 86, VerticalAlignment = VerticalAlignment.Center, ToolTip = "How strongly each brush stamp changes the surface." };
        brushStrength.ValueChanged += (_, _) => brushStrengthLabel.Text = $"Strength {(int)brushStrength.Value}%"; panel.Children.Add(SculptField(brushStrengthLabel, brushStrength));
        brushAmount = new TextBox { Text = "1", Width = 50, Height = 28, Padding = new Thickness(5, 3, 5, 3), TextAlignment = TextAlignment.Right, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Center-point change per Raise or Lower brush stamp, in table units." }; panel.Children.Add(SculptField(SculptLabel("Amount"), brushAmount));
        brushFalloff = new ComboBox { Width = 82, Height = 28, ItemsSource = Enum.GetNames<SurfaceBrushFalloff>(), SelectedIndex = 0, ToolTip = "How the brush strength fades from its center to its edge." }; panel.Children.Add(SculptField(SculptLabel("Falloff"), brushFalloff));
        limitSculptToSelection = new CheckBox { Content = "Limit to selection", IsChecked = true, IsEnabled = false, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 5), ToolTip = "When selected cells exist, prevent the brush from changing cells outside them." }; panel.Children.Add(limitSculptToSelection);
        preventSculptOvershoot = new CheckBox { Content = "Prevent overshoot", IsChecked = false, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 5), ToolTip = "Keep sculpted values inside the table's range at the start of the stroke." }; panel.Children.Add(preventSculptOvershoot);
        return panel;
    }

    private static TextBlock SculptLabel(string text) => new() { Text = text, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };
    private static StackPanel SculptField(TextBlock label, Control control)
    {
        label.Margin = new Thickness(0); control.Margin = new Thickness(5, 0, 0, 0);
        var field = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 5), VerticalAlignment = VerticalAlignment.Center };
        field.Children.Add(label); field.Children.Add(control); return field;
    }

    private ContextMenu CreateSelectionContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ActionItem("Copy selected", SurfaceSelectionAction.Copy));
        menu.Items.Add(ActionItem("Paste", SurfaceSelectionAction.Paste));
        menu.Items.Add(ActionItem("Offset selection…", SurfaceSelectionAction.Offset));
        menu.Items.Add(ActionItem("Select transition ring…", SurfaceSelectionAction.SelectRing));
        menu.Items.Add(new Separator());
        menu.Items.Add(ActionItem("Smooth selected…", SurfaceSelectionAction.Advanced));
        menu.Items.Add(ActionItem("Smooth rows", SurfaceSelectionAction.SmoothRows));
        menu.Items.Add(ActionItem("Smooth columns", SurfaceSelectionAction.SmoothColumns));
        menu.Items.Add(new Separator());
        menu.Items.Add(ActionItem("Flatten between two selected points", SurfaceSelectionAction.FlattenPath));
        menu.Items.Add(ActionItem("Smooth between two selected points", SurfaceSelectionAction.SmoothPath));
        menu.Items.Add(new Separator());
        menu.Items.Add(ActionItem("Clear selected", SurfaceSelectionAction.Clear));
        menu.Opened += (_, _) => menu.IsOpen = SelectedSurfaceCells().Count > 0;
        return menu;
    }

    private MenuItem ActionItem(string header, SurfaceSelectionAction action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => RunSelectionAction(action);
        return item;
    }

    private void RunSelectionAction(SurfaceSelectionAction action)
    {
        if (selectionAction is null) return;
        var selected = SelectedSurfaceCells();
        if (action is SurfaceSelectionAction.FlattenPath or SurfaceSelectionAction.SmoothPath && selected.Count != 2)
        {
            MessageBox.Show(this, "Select exactly two surface cells using Ctrl+click, then choose the two-point tool again.", "Choose two points", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (selected.Count == 0) return;
        var top = selected.Min(cell => cell.Row); var bottom = selected.Max(cell => cell.Row);
        var left = selected.Min(cell => cell.Col); var right = selected.Max(cell => cell.Col);
        if (action == SurfaceSelectionAction.SelectRing && !TransitionRingSelection.TryGetRectangle(selected, out top, out bottom, out left, out right))
        {
            if (action == SurfaceSelectionAction.SelectRing) MessageBox.Show(this, "Select one solid rectangular area before creating a transition ring.", "Transition ring", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (action == SurfaceSelectionAction.SelectRing) { OpenTransitionRing(top, bottom, left, right); return; }
        selectionAction(action, top, bottom, left, right, selected, updated => UpdateSurfaceValues(updated));
        if (action is SurfaceSelectionAction.FlattenPath or SurfaceSelectionAction.SmoothPath)
            selectionStatus.Text = action == SurfaceSelectionAction.FlattenPath ? "Path flattened  •  endpoints preserved" : "Path smoothed  •  endpoints preserved";
        if (action == SurfaceSelectionAction.Paste)
        {
            hideSelectionOverlay = false; selectionStart = selectionEnd = null; pinnedSurfaceSelection.Clear(); selectionVisual.Content = null; UpdateSelectionActionState();
            UpdateSculptSelectionState();
            selectionStatus.Text = "Paste complete  •  selection cleared";
        }
    }

    private void OpenTransitionRing(int top, int bottom, int left, int right)
    {
        var maximum = TransitionRingSelection.MaximumThickness(top, bottom, left, right);
        if (maximum < 1) { MessageBox.Show(this, "Select at least a 3 × 3 area so the ring has an unselected center.", "Transition ring", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        void Apply(int width)
        {
            transitionRingThickness = width; var ring = TransitionRingSelection.Create(top, bottom, left, right, width);
            hideSelectionOverlay = false;
            pinnedSurfaceSelection.Clear(); foreach (var cell in ring) pinnedSurfaceSelection.Add(cell);
            selectionStart = selectionEnd = null; selectionVisual.Content = CreateSelectionOverlay(ring); UpdateSelectionActionState();
            UpdateSculptSelectionState();
            selectionStatus.Text = $"{ring.Count} transition-ring cells selected";
            selectionAction?.Invoke(SurfaceSelectionAction.SelectRing, top, bottom, left, right, ring, updated => UpdateSurfaceValues(updated));
        }
        var dialog = ModelessWindowManager.ShowOrActivate($"3D.{Title}.TransitionRing", () => new TransitionRingWindow(maximum, transitionRingThickness, Apply) { Owner = this });
        dialog.Configure(maximum, transitionRingThickness, Apply);
    }

    private void RunHistoryAction(SurfaceSelectionAction action)
    {
        selectionAction?.Invoke(action, 0, 0, 0, 0, Array.Empty<(int Row, int Col)>(), updated => UpdateSurfaceValues(updated));
    }

    internal void SetTableContext(double[,] updated, IReadOnlyCollection<(int Row, int Col)> tableSelection, bool hideImportedSelection = false)
    {
        if (updated.GetLength(0) != rows || updated.GetLength(1) != cols) return;
        CancelSculptStroke();
        if (!ReferenceEquals(updated, values)) UpdateSurfaceValues(updated, false);
        selectionStart = selectionEnd = null; selectingSurface = false;
        pinnedSurfaceSelection.Clear();
        foreach (var cell in tableSelection)
            if (cell.Row >= 0 && cell.Row < rows && cell.Col >= 0 && cell.Col < cols) pinnedSurfaceSelection.Add(cell);
        hideSelectionOverlay = hideImportedSelection && pinnedSurfaceSelection.Count > 0;
        selectionVisual.Content = pinnedSurfaceSelection.Count > 0 && !hideSelectionOverlay ? CreateSelectionOverlay(pinnedSurfaceSelection) : null;
        UpdateSelectionActionState();
        if (sculptCommit is not null && pinnedSurfaceSelection.Count > 0) limitSculptToSelection.IsChecked = true;
        UpdateSculptSelectionState();
        selectionStatus.Text = pinnedSurfaceSelection.Count > 0
            ? hideSelectionOverlay ? $"Cropped 2D workspace  •  {pinnedSurfaceSelection.Count} cells  •  close to choose another area" : $"{pinnedSurfaceSelection.Count} cells selected from the 2D table"
            : "Table synchronized  •  full surface available";
    }

    private void UpdateSurfaceValues(double[,] updated, bool updateStatus = true)
    {
        if (updated.GetLength(0) != rows || updated.GetLength(1) != cols) return;
        Array.Copy(updated, values, values.Length);
        var updatedSurface = CreateSurface(values, useCustomColors, lowColor, highColor);
        surface.Geometry = updatedSurface.Geometry; surface.Material = updatedSurface.Material; surface.BackMaterial = updatedSurface.BackMaterial;
        var updatedGrid = CreateContourGrid(values);
        contourGrid.Geometry = updatedGrid.Geometry; contourGrid.Material = updatedGrid.Material; contourGrid.BackMaterial = updatedGrid.BackMaterial;
        if (orientationSurface is not null)
        {
            var miniatureSurface = CreateSurface(values, useCustomColors, lowColor, highColor);
            orientationSurface.Geometry = miniatureSurface.Geometry; orientationSurface.Material = miniatureSurface.Material; orientationSurface.BackMaterial = miniatureSurface.BackMaterial;
            var miniatureGrid = CreateContourGrid(values);
            orientationGrid.Geometry = miniatureGrid.Geometry; orientationGrid.Material = miniatureGrid.Material; orientationGrid.BackMaterial = miniatureGrid.BackMaterial;
        }
        RefreshValueScaleLabels();
        var selected = SelectedSurfaceCells();
        selectionVisual.Content = selected.Count > 0 && !hideSelectionOverlay ? CreateSelectionOverlay(selected) : null;
        hoverCell = null; hoverVisual.Content = null;
        if (updateStatus) selectionStatus.Text = "Table updated  •  selection retained";
    }

    private static UIElement CreateAxisOverlay(double[,] values, double[] rpm, double[] map, string mapUnit)
    {
        var (timingMin, timingMax) = ValueRange(values);
        var overlay = new Grid { IsHitTestVisible = false, Margin = new Thickness(12) };
        overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        overlay.ColumnDefinitions.Add(new ColumnDefinition());
        overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        overlay.RowDefinitions.Add(new RowDefinition()); overlay.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

        var timingScale = MakeVerticalScale("SPARK TIMING (°)", timingMax, timingMin, "0.0");
        Grid.SetColumn(timingScale, 0); Grid.SetRow(timingScale, 0); overlay.Children.Add(timingScale);

        var mapScale = MakeVerticalScale($"MAP ({mapUnit})", map[0], map[^1], MapFormat);
        Grid.SetColumn(mapScale, 2); Grid.SetRow(mapScale, 0); overlay.Children.Add(mapScale);

        var rpmScale = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 12, 0) };
        rpmScale.ColumnDefinitions.Add(new ColumnDefinition()); rpmScale.ColumnDefinitions.Add(new ColumnDefinition()); rpmScale.ColumnDefinitions.Add(new ColumnDefinition());
        rpmScale.RowDefinitions.Add(new RowDefinition()); rpmScale.RowDefinitions.Add(new RowDefinition());
        AddScaleText(rpmScale, rpm[0].ToString("0"), 0, 0, HorizontalAlignment.Left);
        AddScaleText(rpmScale, ((rpm[0] + rpm[^1]) / 2).ToString("0"), 0, 1, HorizontalAlignment.Center);
        AddScaleText(rpmScale, rpm[^1].ToString("0"), 0, 2, HorizontalAlignment.Right);
        var rpmTitle = ScaleText("ENGINE RPM", true); rpmTitle.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(rpmTitle, 1); Grid.SetColumnSpan(rpmTitle, 3); rpmScale.Children.Add(rpmTitle);
        Grid.SetColumn(rpmScale, 1); Grid.SetRow(rpmScale, 1); overlay.Children.Add(rpmScale);
        return overlay;
    }

    private static Grid MakeVerticalScale(string title, double maximum, double minimum, string format)
    {
        var container = new Grid(); container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); container.ColumnDefinitions.Add(new ColumnDefinition());
        var titleText = ScaleText(title, true); titleText.LayoutTransform = new RotateTransform(-90); titleText.HorizontalAlignment = HorizontalAlignment.Center; titleText.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(titleText, 0); container.Children.Add(titleText);
        var ticks = new Grid { Margin = new Thickness(4, 26, 0, 26) }; ticks.RowDefinitions.Add(new RowDefinition()); ticks.RowDefinitions.Add(new RowDefinition()); ticks.RowDefinitions.Add(new RowDefinition());
        AddScaleText(ticks, maximum.ToString(format), 0, 0, HorizontalAlignment.Left, VerticalAlignment.Top);
        AddScaleText(ticks, ((maximum + minimum) / 2).ToString(format), 1, 0, HorizontalAlignment.Left, VerticalAlignment.Center);
        AddScaleText(ticks, minimum.ToString(format), 2, 0, HorizontalAlignment.Left, VerticalAlignment.Bottom);
        Grid.SetColumn(ticks, 1); container.Children.Add(ticks); return container;
    }

    private static void AddScaleText(Grid grid, string text, int row, int column, HorizontalAlignment horizontal, VerticalAlignment vertical = VerticalAlignment.Center)
    {
        var label = ScaleText(text); label.HorizontalAlignment = horizontal; label.VerticalAlignment = vertical; Grid.SetRow(label, row); Grid.SetColumn(label, column); grid.Children.Add(label);
    }

    private static TextBlock ScaleText(string text, bool title = false) => new()
    {
        Text = text, Foreground = title ? Brushes.White : new SolidColorBrush(Color.FromRgb(157, 177, 201)),
        FontSize = title ? 11 : 10, FontWeight = title ? FontWeights.Bold : FontWeights.SemiBold,
        Background = new SolidColorBrush(Color.FromArgb(145, 5, 9, 14)), Padding = new Thickness(3, 1, 3, 1)
    };

    private void AddRotatingScaleLabels(Viewport3D viewport, Transform3D transform, double[,] values, double[] rpm, double[] map, string mapUnit, string valueAxisTitle, string axisTitle, string xAxisTitle, string xFormat, string displayedValueFormat)
    {
        var (timingMin, timingMax) = ValueRange(values);
        AddScaleOverlay(xAxisTitle, new Point3D(0, -.15, 9.25), true);
        AddScaleOverlay(xAxisTitle, new Point3D(0, -.15, -9.25), true);
        for (var labelIndex = 0; labelIndex < 7; labelIndex++)
        {
            var fraction = labelIndex / 6d;
            var axisIndex = (int)Math.Round(fraction * (rpm.Length - 1));
            var x = -9.5 + 19 * fraction;
            var label = rpm[axisIndex].ToString(xFormat);
            AddScaleOverlay(label, new Point3D(x, -.15, 8.65));
            AddScaleOverlay(label, new Point3D(x, -.15, -8.65));
        }

        var mapTitle = axisTitle == "MAP"
            ? mapUnit.Contains("PSI", StringComparison.OrdinalIgnoreCase) ? "MAP (PSIG)" : "MAP (kPa)"
            : mapUnit.Equals("Unitless", StringComparison.OrdinalIgnoreCase) ? axisTitle : $"{axisTitle} ({mapUnit})";
        AddFloorLabel(viewport, transform, mapTitle, 11.25, 0, 1.25, 5.8, true, -90);
        AddFloorLabel(viewport, transform, mapTitle, -11.25, 0, 1.25, 5.8, true, 90);
        for (var labelIndex = 0; labelIndex < 7; labelIndex++)
        {
            var fraction = labelIndex / 6d;
            var axisIndex = (int)Math.Round(fraction * (map.Length - 1));
            var z = -7.7 + 15.4 * fraction;
            var label = FormatMap(map[axisIndex]);
            AddScaleOverlay(label, new Point3D(10.7, -.15, z));
            AddScaleOverlay(label, new Point3D(-10.7, -.15, z));
        }

        AddScaleOverlay(valueAxisTitle, new Point3D(-8.3, 7.75, -8.25), true);
        AddScaleOverlay(valueAxisTitle, new Point3D(8.3, 7.75, 8.25), true);
        for (var labelIndex = 0; labelIndex < 6; labelIndex++)
        {
            var fraction = labelIndex / 5d;
            var scaleValue = timingMin + (timingMax - timingMin) * fraction;
            var label = valueFormatter?.Invoke(scaleValue) ?? scaleValue.ToString(displayedValueFormat, System.Globalization.CultureInfo.InvariantCulture);
            var y = .15 + 6.7 * fraction;
            valueScaleLabels.Add(AddScaleOverlay(label, new Point3D(-10.7, y, -8.25)));
            valueScaleLabels.Add(AddScaleOverlay(label, new Point3D(10.7, y, 8.25)));
        }
        UpdateScaleOverlayPositions();
    }

    private static void AddFloorLabel(Viewport3D viewport, Transform3D transform, string text, double x, double z, double width, double depth, bool title = false, double textRotation = 0)
    {
        var points = new Point3DCollection
        {
            new(x - width / 2, -.15, z + depth / 2), new(x + width / 2, -.15, z + depth / 2),
            new(x - width / 2, -.15, z - depth / 2), new(x + width / 2, -.15, z - depth / 2)
        };
        AddTextPlane(viewport, transform, text, points, title, textRotation);
    }

    private static void AddVerticalLabel(Viewport3D viewport, Transform3D transform, string text, double x, double y, double z, double width, double height, bool title = false)
    {
        var points = new Point3DCollection
        {
            new(x - width / 2, y - height / 2, z), new(x + width / 2, y - height / 2, z),
            new(x - width / 2, y + height / 2, z), new(x + width / 2, y + height / 2, z)
        };
        AddTextPlane(viewport, transform, text, points, title, 0);
    }

    private static void AddTextPlane(Viewport3D viewport, Transform3D transform, string text, Point3DCollection points, bool title, double textRotation)
    {
        Border CreateLabelVisual()
        {
            var textBlock = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = title ? 22 : 18, FontWeight = title ? FontWeights.Bold : FontWeights.SemiBold, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (Math.Abs(textRotation) > .01) textBlock.LayoutTransform = new RotateTransform(textRotation);
            return new Border { Background = new SolidColorBrush(Color.FromArgb(185, 4, 8, 13)), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 2, 5, 2), Child = textBlock };
        }

        Viewport2DVisual3D CreateSide(PointCollection textureCoordinates, Int32Collection indices)
        {
            var mesh = new MeshGeometry3D { Positions = points, TextureCoordinates = textureCoordinates, TriangleIndices = indices };
            var material = new DiffuseMaterial(Brushes.White); Viewport2DVisual3D.SetIsVisualHostMaterial(material, true);
            return new Viewport2DVisual3D { Geometry = mesh, Material = material, Visual = CreateLabelVisual(), Transform = transform };
        }

        var front = CreateSide(
            new PointCollection { new(0, 1), new(1, 1), new(0, 0), new(1, 0) },
            new Int32Collection { 0, 1, 2, 2, 1, 3 });
        // Reverse the triangle winding and horizontal texture direction. This makes
        // the back face readable rather than mirrored when the map turns 180°.
        var back = CreateSide(
            new PointCollection { new(1, 1), new(0, 1), new(1, 0), new(0, 0) },
            new Int32Collection { 0, 2, 1, 2, 3, 1 });
        viewport.Children.Add(front); viewport.Children.Add(back);
    }

    private static Color HslToColor(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static GeometryModel3D CreateSurface(double[,] values, bool useCustomColors, Color lowColor, Color highColor)
    {
        var rows = values.GetLength(0); var cols = values.GetLength(1);
        var (min, max) = ValueRange(values); var span = Math.Max(.1, max - min);
        var mesh = new MeshGeometry3D();
        for (var r = 0; r < rows; r++) for (var c = 0; c < cols; c++)
        {
            var t = (values[r, c] - min) / span;
            mesh.Positions.Add(new Point3D(-10 + c * 20d / (cols - 1), t * 7, -8 + r * 16d / (rows - 1)));
            mesh.TextureCoordinates.Add(new Point(.5, 1 - t));
        }
        for (var r = 0; r < rows - 1; r++) for (var c = 0; c < cols - 1; c++)
        {
            var a = r * cols + c; var b = a + 1; var d = (r + 1) * cols + c; var e = d + 1;
            mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(d); mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(d); mesh.TriangleIndices.Add(e);
        }
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(0, 0) };
        if (useCustomColors)
        {
            gradient.GradientStops.Add(new GradientStop(lowColor, 0)); gradient.GradientStops.Add(new GradientStop(highColor, 1));
        }
        else
        {
            for (var i = 0; i <= 6; i++) gradient.GradientStops.Add(new GradientStop(HslToColor(i * 50, .96, .52), i / 6d));
        }
        mesh.Freeze(); gradient.Freeze(); var material = new DiffuseMaterial(gradient); material.Freeze();
        // Keep the model writable because the shared rotation transform is assigned
        // after construction. The expensive mesh and material resources are frozen.
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static GeometryModel3D CreateContourGrid(double[,] values)
    {
        var rows = values.GetLength(0); var cols = values.GetLength(1);
        var (min, max) = ValueRange(values); var span = Math.Max(.1, max - min);
        Point3D PointAt(int row, int col) => new(-10 + col * 20d / (cols - 1), (values[row, col] - min) / span * 7 + .045, -8 + row * 16d / (rows - 1));
        var mesh = new MeshGeometry3D();

        // RPM-direction lines: thin ribbons offset along the MAP/Z direction.
        for (var row = 0; row < rows; row++) for (var col = 0; col < cols - 1; col++)
            AddRibbon(mesh, PointAt(row, col), PointAt(row, col + 1), 0, .018);

        // MAP-direction lines: thin ribbons offset along the RPM/X direction.
        for (var col = 0; col < cols; col++) for (var row = 0; row < rows - 1; row++)
            AddRibbon(mesh, PointAt(row, col), PointAt(row + 1, col), .018, 0);

        mesh.Freeze(); var material = new DiffuseMaterial(Brushes.Black); material.Freeze();
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static GeometryModel3D CreateAxisFrame()
    {
        var mesh = new MeshGeometry3D(); const double left = -10.45, right = 10.45, back = -8.45, front = 8.45;
        // Lower and upper bounding rectangles.
        foreach (var y in new[] { -.2, 7.25 })
        {
            AddRibbon(mesh, new Point3D(left, y, back), new Point3D(right, y, back), 0, .022);
            AddRibbon(mesh, new Point3D(left, y, front), new Point3D(right, y, front), 0, .022);
            AddRibbon(mesh, new Point3D(left, y, back), new Point3D(left, y, front), .022, 0);
            AddRibbon(mesh, new Point3D(right, y, back), new Point3D(right, y, front), .022, 0);
        }
        AddVerticalRibbon(mesh, left, back); AddVerticalRibbon(mesh, right, back); AddVerticalRibbon(mesh, left, front); AddVerticalRibbon(mesh, right, front);

        // RPM and MAP tick marks around the base, plus timing ticks on the right axis.
        for (var i = 0; i <= 10; i++)
        {
            var x = left + (right - left) * i / 10; AddRibbon(mesh, new Point3D(x, -.19, front), new Point3D(x, -.19, front + .28), .014, 0);
            AddRibbon(mesh, new Point3D(x, -.19, back - .28), new Point3D(x, -.19, back), .014, 0);
            var z = back + (front - back) * i / 10; AddRibbon(mesh, new Point3D(right, -.19, z), new Point3D(right + .28, -.19, z), 0, .014);
            AddRibbon(mesh, new Point3D(left - .28, -.19, z), new Point3D(left, -.19, z), 0, .014);
        }
        for (var i = 0; i <= 5; i++)
        {
            var y = -.2 + 7.45 * i / 5; AddRibbon(mesh, new Point3D(right, y, back), new Point3D(right + .3, y, back), 0, .014);
            AddRibbon(mesh, new Point3D(left - .3, y, back), new Point3D(left, y, back), 0, .014);
            AddRibbon(mesh, new Point3D(right, y, front), new Point3D(right + .3, y, front), 0, .014);
        }
        mesh.Freeze(); var material = new DiffuseMaterial(UiBrushCache.Frozen(Color.FromRgb(72, 82, 95))); material.Freeze();
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static (double Min, double Max) ValueRange(double[,] source)
    {
        var min = double.PositiveInfinity; var max = double.NegativeInfinity;
        for (var row = 0; row < source.GetLength(0); row++) for (var col = 0; col < source.GetLength(1); col++) { var value = source[row, col]; if (value < min) min = value; if (value > max) max = value; }
        return (min, max);
    }

    private static void AddVerticalRibbon(MeshGeometry3D mesh, double x, double z)
    {
        var index = mesh.Positions.Count; const double halfWidth = .022;
        mesh.Positions.Add(new Point3D(x - halfWidth, -.2, z)); mesh.Positions.Add(new Point3D(x + halfWidth, -.2, z));
        mesh.Positions.Add(new Point3D(x - halfWidth, 7.25, z)); mesh.Positions.Add(new Point3D(x + halfWidth, 7.25, z));
        mesh.TriangleIndices.Add(index); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 3);
    }

    private static void AddRibbon(MeshGeometry3D mesh, Point3D start, Point3D end, double offsetX, double offsetZ)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(start.X - offsetX, start.Y, start.Z - offsetZ));
        mesh.Positions.Add(new Point3D(start.X + offsetX, start.Y, start.Z + offsetZ));
        mesh.Positions.Add(new Point3D(end.X - offsetX, end.Y, end.Z - offsetZ));
        mesh.Positions.Add(new Point3D(end.X + offsetX, end.Y, end.Z + offsetZ));
        mesh.TriangleIndices.Add(index); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 3);
    }

    private void ToggleSculptMode(SurfaceSculptMode mode)
    {
        CancelSculptStroke();
        var enable = sculptMode != mode;
        selectingSurface = rotating = false;
        SetSculptMode(enable ? mode : null);
        selectionStatus.Text = enable ? $"{mode} sculpt ready  •  left-drag on the surface  •  right-drag rotates" : "Left-drag selects  •  right-drag rotates";
    }

    private void SetSculptMode(SurfaceSculptMode? mode)
    {
        sculptMode = mode;
        foreach (var button in sculptButtons)
        {
            var active = mode is not null && string.Equals(button.Content?.ToString(), mode.ToString(), StringComparison.Ordinal);
            button.IsChecked = active;
            button.Background = active ? UiBrushCache.Idle : UiBrushCache.GridLine;
        }
        viewport.Cursor = mode is null ? Cursors.Arrow : Cursors.Cross;
    }

    private bool BeginSculptStroke((int Row, int Col) cell)
    {
        if (sculptMode is null || sculptCommit is null) return false;
        var amount = 1d;
        if (sculptMode is SurfaceSculptMode.Raise or SurfaceSculptMode.Lower &&
            (!double.TryParse(brushAmount.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out amount) || !double.IsFinite(amount) || amount < 0))
        {
            MessageBox.Show(this, "Amount must be a finite number of zero or greater.", "Check sculpt amount", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        sculptStrokeOriginal = (double[,])values.Clone();
        flattenTarget = values[cell.Row, cell.Col];
        lastSculptCell = null; sculptedCells.Clear(); sculptingSurface = true;
        ApplySculptThrough(cell, amount);
        return true;
    }

    private void ApplySculptThrough((int Row, int Col) cell, double? parsedAmount = null)
    {
        if (!sculptingSurface || sculptStrokeOriginal is null || sculptMode is null) return;
        var amount = parsedAmount ?? (double.TryParse(brushAmount.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) && parsed >= 0 ? parsed : 1);
        var centers = lastSculptCell is { } previous ? SurfaceSculptor.Line(previous, cell).Skip(1).ToArray() : [cell];
        lastSculptCell = cell;
        if (centers.Length == 0) return;
        var selected = SelectedSurfaceCells();
        IReadOnlySet<(int Row, int Col)>? mask = limitSculptToSelection.IsChecked == true && selected.Count > 0 ? selected : null;
        var options = new SurfaceSculptOptions(sculptMode.Value, (int)brushRadius.Value, brushStrength.Value / 100d, amount,
            (SurfaceBrushFalloff)Math.Max(0, brushFalloff.SelectedIndex), preventSculptOvershoot.IsChecked == true);
        var result = SurfaceSculptor.ApplyPath(values, sculptStrokeOriginal, centers, options, rpmAxis, mapAxis, flattenTarget, mask);
        sculptedCells.UnionWith(result.AffectedCells);
        UpdateSurfaceValues(result.Values, false);
        var deltas = sculptedCells.Select(point => values[point.Row, point.Col] - sculptStrokeOriginal[point.Row, point.Col]).ToArray();
        var range = deltas.Length == 0 ? "no value change" : $"delta {deltas.Min():+0.###;-0.###;0} to {deltas.Max():+0.###;-0.###;0}";
        selectionStatus.Text = $"{sculptMode} sculpt preview  •  {sculptedCells.Count} cells  •  {range}";
    }

    private void EndLeftPointer(Viewport3D target)
    {
        if (sculptingSurface) FinishSculptStroke();
        selectingSurface = false;
        if (ReferenceEquals(Mouse.Captured, target)) target.ReleaseMouseCapture();
    }

    private void FinishSculptStroke()
    {
        var original = sculptStrokeOriginal;
        sculptingSurface = false; sculptStrokeOriginal = null; lastSculptCell = null;
        if (original is null || sculptedCells.Count == 0 || sculptCommit is null) { sculptedCells.Clear(); return; }
        try
        {
            var committed = sculptCommit((double[,])values.Clone(), sculptedCells.ToArray());
            UpdateSurfaceValues(committed, false);
            selectionStatus.Text = $"Sculpt stroke committed  •  {sculptedCells.Count} cells  •  one Undo step";
        }
        catch (Exception exception)
        {
            UpdateSurfaceValues(original, false);
            MessageBox.Show(this, exception.Message, "Sculpting could not be applied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { sculptedCells.Clear(); }
    }

    private void CancelSculptStroke()
    {
        if (!sculptingSurface || sculptStrokeOriginal is null) return;
        var original = sculptStrokeOriginal;
        sculptingSurface = false; sculptStrokeOriginal = null; lastSculptCell = null; sculptedCells.Clear();
        UpdateSurfaceValues(original, false); selectionStatus.Text = "Sculpt preview cancelled";
    }

    private void UpdateSculptSelectionState()
    {
        if (sculptCommit is null) return;
        var hasSelection = SelectedSurfaceCells().Count > 0;
        limitSculptToSelection.IsEnabled = hasSelection;
        if (!hasSelection) limitSculptToSelection.IsChecked = true;
    }

    private void ClearSurfaceSelection()
    {
        CancelSculptStroke();
        selectionStart = selectionEnd = null; pinnedSurfaceSelection.Clear(); selectingSurface = false;
        selectionVisual.Content = null; UpdateSelectionActionState(); UpdateSculptSelectionState();
        selectionStatus.Text = sculptMode is null ? "Selection cleared  •  left-drag selects  •  right-drag rotates" : $"Selection cleared  •  {sculptMode} sculpt ready";
    }

    private void BeginLeftPointer(Viewport3D viewport, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(viewport);
        if (sculptMode is not null)
        {
            var cell = HitSurfaceCell(viewport, point);
            if (cell is null || !BeginSculptStroke(cell.Value)) return;
        }
        else
        {
            var cell = HitSurfaceCell(viewport, point);
            if (cell is null) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) PinActiveSurfaceSelection(); else pinnedSurfaceSelection.Clear();
            selectionStart = selectionEnd = cell; selectingSurface = true; UpdateSelectionHighlight();
        }
        viewport.CaptureMouse(); e.Handled = true;
    }

    private void BeginOrbit(Viewport3D target, MouseButtonEventArgs e)
    {
        CancelSculptStroke();
        orbitStartPoint = lastPoint = e.GetPosition(target); orbitDragged = false; suppressNextContextMenu = false; rotating = true;
        target.CaptureMouse();
    }

    private void EndOrbit(Viewport3D target, MouseButtonEventArgs e)
    {
        rotating = false;
        if (ReferenceEquals(Mouse.Captured, target)) target.ReleaseMouseCapture();
        if (!orbitDragged) return;
        suppressNextContextMenu = true; e.Handled = true;
        selectionStatus.Text = sculptMode is null ? "View rotated  •  left-drag selects" : $"View rotated  •  {sculptMode} sculpt remains active";
    }

    private void MovePointer(Viewport3D viewport, MouseEventArgs e)
    {
        var point = e.GetPosition(viewport);
        if (rotating)
        {
            if (!orbitDragged && (Math.Abs(point.X - orbitStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(point.Y - orbitStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance)) orbitDragged = true;
            Rotate(point);
        }
        else if (sculptingSurface)
        {
            var cell = HitSurfaceCell(viewport, point);
            if (cell is not null && cell != lastSculptCell) ApplySculptThrough(cell.Value);
        }
        else if (selectingSurface)
        {
            var cell = HitSurfaceCell(viewport, point);
            if (cell is not null && cell != selectionEnd) { selectionEnd = cell; UpdateSelectionHighlight(); }
        }
    }

    private (int Row, int Col)? HitSurfaceCell(Viewport3D viewport, Point point)
    {
        RayMeshGeometry3DHitTestResult? surfaceHit = null;
        VisualTreeHelper.HitTest(viewport, null, result =>
        {
            if (result is RayMeshGeometry3DHitTestResult ray && ReferenceEquals(ray.ModelHit, surface))
            { surfaceHit = ray; return HitTestResultBehavior.Stop; }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(point));
        if (surfaceHit is null) return null;
        var weighted = new[]
        {
            (surfaceHit.VertexIndex1, surfaceHit.VertexWeight1),
            (surfaceHit.VertexIndex2, surfaceHit.VertexWeight2),
            (surfaceHit.VertexIndex3, surfaceHit.VertexWeight3)
        };
        var vertex = weighted.MaxBy(item => item.Item2).Item1;
        return (Math.Clamp(vertex / cols, 0, rows - 1), Math.Clamp(vertex % cols, 0, cols - 1));
    }

    private void UpdateHover(Viewport3D viewport, Point point)
    {
        var cell = HitSurfaceCell(viewport, point);
        if (cell is null) { ClearHover(); return; }

        if (cell != hoverCell)
        {
            hoverCell = cell;
            hoverVisual.Content = CreateHoverCrosshair(cell.Value.Row, cell.Value.Col);
            var displayedValue = valueFormatter?.Invoke(values[cell.Value.Row, cell.Value.Col]) ?? values[cell.Value.Row, cell.Value.Col].ToString(valueFormat, System.Globalization.CultureInfo.InvariantCulture);
            hoverText.Text = $"{rpmAxisTitle}: {rpmAxis[cell.Value.Col].ToString(rpmFormat)}\n{mapAxisTitle}: {FormatMap(mapAxis[cell.Value.Row])} {mapUnit}\n{valueAxisTitle}: {displayedValue}";
            hoverTip.Visibility = Visibility.Visible;
            hoverTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        var desired = hoverTip.DesiredSize;
        var x = Math.Clamp(point.X + 16, 6, Math.Max(6, viewport.ActualWidth - desired.Width - 6));
        var y = Math.Clamp(point.Y + 16, 6, Math.Max(6, viewport.ActualHeight - desired.Height - 6));
        Canvas.SetLeft(hoverTip, x); Canvas.SetTop(hoverTip, y);
    }

    private void ClearHover()
    {
        hoverCell = null; hoverVisual.Content = null;
        if (hoverTip is not null) hoverTip.Visibility = Visibility.Collapsed;
    }

    private GeometryModel3D CreateHoverCrosshair(int hoverRow, int hoverCol)
    {
        var (min, max) = ValueRange(values); var span = Math.Max(.1, max - min);
        Point3D PointAt(int row, int col) => new(-10 + col * 20d / (cols - 1), (values[row, col] - min) / span * 7 + .16, -8 + row * 16d / (rows - 1));
        var mesh = new MeshGeometry3D();
        for (var col = 0; col < cols - 1; col++) AddRibbon(mesh, PointAt(hoverRow, col), PointAt(hoverRow, col + 1), 0, .045);
        for (var row = 0; row < rows - 1; row++) AddRibbon(mesh, PointAt(row, hoverCol), PointAt(row + 1, hoverCol), .045, 0);
        var materials = new MaterialGroup();
        materials.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(225, 255, 250))));
        materials.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(85, 214, 190))));
        return new GeometryModel3D(mesh, materials) { BackMaterial = materials };
    }

    private void UpdateSelectionHighlight()
    {
        if (selectionStart is null || selectionEnd is null) return;
        hideSelectionOverlay = false;
        var top = Math.Min(selectionStart.Value.Row, selectionEnd.Value.Row); var bottom = Math.Max(selectionStart.Value.Row, selectionEnd.Value.Row);
        var left = Math.Min(selectionStart.Value.Col, selectionEnd.Value.Col); var right = Math.Max(selectionStart.Value.Col, selectionEnd.Value.Col);
        var selectedCells = SelectedSurfaceCells(); selectionVisual.Content = CreateSelectionOverlay(selectedCells);
        UpdateSelectionActionState(selectedCells.Count);
        UpdateSculptSelectionState();
        selectionStatus.Text = $"{selectedCells.Count} surface cells selected";
    }

    private void PinActiveSurfaceSelection()
    {
        if (selectionStart is null || selectionEnd is null) return;
        var top = Math.Min(selectionStart.Value.Row, selectionEnd.Value.Row); var bottom = Math.Max(selectionStart.Value.Row, selectionEnd.Value.Row);
        var left = Math.Min(selectionStart.Value.Col, selectionEnd.Value.Col); var right = Math.Max(selectionStart.Value.Col, selectionEnd.Value.Col);
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) pinnedSurfaceSelection.Add((row, col));
    }

    private HashSet<(int Row, int Col)> SelectedSurfaceCells()
    {
        var selected = new HashSet<(int Row, int Col)>(pinnedSurfaceSelection);
        if (selectionStart is null || selectionEnd is null) return selected;
        var top = Math.Min(selectionStart.Value.Row, selectionEnd.Value.Row); var bottom = Math.Max(selectionStart.Value.Row, selectionEnd.Value.Row);
        var left = Math.Min(selectionStart.Value.Col, selectionEnd.Value.Col); var right = Math.Max(selectionStart.Value.Col, selectionEnd.Value.Col);
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++) selected.Add((row, col));
        return selected;
    }

    private void UpdateSelectionActionState(int? selectedCount = null)
    {
        var count = selectedCount ?? SelectedSurfaceCells().Count;
        smoothButton.IsEnabled = count > 0;
        // Keep these buttons in the normal dark toolbar theme. Disabled WPF buttons
        // can be rendered with a white system face while retaining our white text.
        flattenPathButton.IsEnabled = smoothPathButton.IsEnabled = selectionAction is not null;
    }

    private GeometryModel3D CreateSelectionOverlay(HashSet<(int Row, int Col)> selectedCells)
    {
        var (min, max) = ValueRange(values); var span = Math.Max(.1, max - min);
        Point3D PointAt(int row, int col) => new(-10 + col * 20d / (cols - 1), (values[row, col] - min) / span * 7 + .11, -8 + row * 16d / (rows - 1));
        var mesh = new MeshGeometry3D();
        var halfX = Math.Min(.22, 6d / cols); var halfZ = Math.Min(.22, 5d / rows);
        foreach (var (row, col) in selectedCells)
        {
            var point = PointAt(row, col); var index = mesh.Positions.Count; var y = point.Y + .07;
            mesh.Positions.Add(new Point3D(point.X - halfX, y, point.Z - halfZ)); mesh.Positions.Add(new Point3D(point.X + halfX, y, point.Z - halfZ));
            mesh.Positions.Add(new Point3D(point.X - halfX, y, point.Z + halfZ)); mesh.Positions.Add(new Point3D(point.X + halfX, y, point.Z + halfZ));
            mesh.TriangleIndices.Add(index); mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 1);
            mesh.TriangleIndices.Add(index + 1); mesh.TriangleIndices.Add(index + 2); mesh.TriangleIndices.Add(index + 3);
        }
        var brush = new SolidColorBrush(Color.FromArgb(125, 255, 240, 70));
        var material = new DiffuseMaterial(brush); return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private void ApplySelectedSmoothing()
    {
        if (selectionStart is null || selectionEnd is null || !smoothButton.IsEnabled) return;
        var top = Math.Min(selectionStart.Value.Row, selectionEnd.Value.Row); var bottom = Math.Max(selectionStart.Value.Row, selectionEnd.Value.Row);
        var left = Math.Min(selectionStart.Value.Col, selectionEnd.Value.Col); var right = Math.Max(selectionStart.Value.Col, selectionEnd.Value.Col);
        var updated = smoothSelection(top, bottom, left, right);
        if (updated.GetLength(0) != rows || updated.GetLength(1) != cols) return;
        UpdateSurfaceValues(updated);
        selectionStart = selectionEnd = null; pinnedSurfaceSelection.Clear(); selectionVisual.Content = null; UpdateSelectionActionState();
        selectionStatus.Text = "Table updated  •  drag to select another region";
    }

    private void Rotate(Point point)
    {
        if (!rotating) return;
        yaw.Angle += (point.X - lastPoint.X) * .45; pitch.Angle += (point.Y - lastPoint.Y) * .35;
        pitch.Angle = Math.Clamp(pitch.Angle, -80, 80); lastPoint = point; UpdateScaleOverlayPositions();
    }

    private TextBlock AddScaleOverlay(string text, Point3D localPosition, bool title = false)
    {
        var label = new TextBlock
        {
            Text = text, Foreground = Brushes.White, FontSize = title ? 14 : 12, FontWeight = title ? FontWeights.Bold : FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(title ? (byte)225 : (byte)205, 4, 8, 13)), Padding = new Thickness(title ? 6 : 4, title ? 2 : 1, title ? 6 : 4, title ? 2 : 1),
            IsHitTestVisible = false
        };
        scaleOverlayLabels.Add((label, localPosition)); overlayLayer.Children.Add(label);
        return label;
    }

    private Border CreateOrientationGizmo()
    {
        var miniatureViewport = new Viewport3D { ClipToBounds = true, IsHitTestVisible = false };
        miniatureViewport.Camera = new PerspectiveCamera(new Point3D(0, 18, 31), new Vector3D(0, -15, -31), new Vector3D(0, 1, 0), 47);
        orientationSurface = CreateSurface(values, useCustomColors, lowColor, highColor); orientationSurface.Transform = transforms;
        orientationGrid = CreateContourGrid(values); orientationGrid.Transform = transforms;
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(145, 155, 170)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3)));
        scene.Children.Add(orientationSurface); scene.Children.Add(orientationGrid);
        miniatureViewport.Children.Add(new ModelVisual3D { Content = scene });
        var host = new Grid(); host.Children.Add(miniatureViewport);
        host.Children.Add(new TextBlock
        {
            Text = "MAP ORIENTATION", Foreground = new SolidColorBrush(Color.FromRgb(174, 188, 207)), FontSize = 9,
            FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(185, 4, 8, 13)), Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 5, 0, 0)
        });
        return new Border
        {
            Width = 176, Height = 132, CornerRadius = new CornerRadius(7), Padding = new Thickness(3),
            Background = new SolidColorBrush(Color.FromArgb(205, 8, 13, 20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 54, 70, 91)), BorderThickness = new Thickness(1),
            Child = host, IsHitTestVisible = false
        };
    }

    private void UpdateOrientationGizmo()
    {
        if (orientationGizmo is null || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return;
        Canvas.SetLeft(orientationGizmo, 12);
        Canvas.SetTop(orientationGizmo, Math.Max(8, viewport.ActualHeight - orientationGizmo.Height - 12));
    }

    private void RefreshValueScaleLabels()
    {
        if (valueScaleLabels.Count != 12) return;
        var (minimum, maximum) = ValueRange(values);
        for (var labelIndex = 0; labelIndex < 6; labelIndex++)
        {
            var value = minimum + (maximum - minimum) * labelIndex / 5d;
            var text = valueFormatter?.Invoke(value) ?? value.ToString(valueFormat, System.Globalization.CultureInfo.InvariantCulture);
            valueScaleLabels[labelIndex * 2].Text = text;
            valueScaleLabels[labelIndex * 2 + 1].Text = text;
        }
    }

    private void UpdateScaleOverlayPositions()
    {
        if (viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return;
        foreach (var item in scaleOverlayLabels)
        {
            var projected = ProjectToViewport(item.LocalPosition);
            if (projected is null || IsOccludedBySurface(item.LocalPosition))
            {
                item.Label.Visibility = Visibility.Collapsed; continue;
            }
            item.Label.Visibility = Visibility.Visible;
            item.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(item.Label, projected.Value.X - item.Label.DesiredSize.Width / 2);
            Canvas.SetTop(item.Label, projected.Value.Y - item.Label.DesiredSize.Height / 2);
        }
        UpdateOrientationGizmo();
    }

    private bool IsOccludedBySurface(Point3D localLabelPosition)
    {
        if (surface.Geometry is not MeshGeometry3D mesh || mesh.TriangleIndices.Count < 3) return false;

        var worldToLocal = transforms.Value;
        if (!worldToLocal.HasInverse) return false;
        worldToLocal.Invert();
        var localCamera = worldToLocal.Transform(camera.Position);
        var ray = localLabelPosition - localCamera;
        var labelDistance = ray.Length;
        if (labelDistance < .001) return false;
        ray.Normalize();

        // Test the camera-to-label segment against the actual timing/fuel surface.
        // A small clearance keeps labels adjacent to an edge from hiding themselves.
        var maximumHitDistance = labelDistance - .28;
        for (var index = 0; index < mesh.TriangleIndices.Count; index += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[index]];
            var b = mesh.Positions[mesh.TriangleIndices[index + 1]];
            var c = mesh.Positions[mesh.TriangleIndices[index + 2]];
            if (RayIntersectsTriangle(localCamera, ray, a, b, c, out var hitDistance) &&
                hitDistance > .01 && hitDistance < maximumHitDistance)
                return true;
        }
        return false;
    }

    private static bool RayIntersectsTriangle(Point3D origin, Vector3D direction, Point3D a, Point3D b, Point3D c, out double distance)
    {
        const double epsilon = 1e-8;
        var edge1 = b - a;
        var edge2 = c - a;
        var cross = Vector3D.CrossProduct(direction, edge2);
        var determinant = Vector3D.DotProduct(edge1, cross);
        if (Math.Abs(determinant) < epsilon) { distance = 0; return false; }

        var inverse = 1d / determinant;
        var fromA = origin - a;
        var u = Vector3D.DotProduct(fromA, cross) * inverse;
        if (u < 0 || u > 1) { distance = 0; return false; }

        var q = Vector3D.CrossProduct(fromA, edge1);
        var v = Vector3D.DotProduct(direction, q) * inverse;
        if (v < 0 || u + v > 1) { distance = 0; return false; }

        distance = Vector3D.DotProduct(edge2, q) * inverse;
        return distance > epsilon;
    }

    private Point? ProjectToViewport(Point3D localPoint)
    {
        var worldPoint = transforms.Transform(localPoint);
        var forward = camera.LookDirection; if (forward.Length < .001) return null; forward.Normalize();
        var up = camera.UpDirection; if (up.Length < .001) return null; up.Normalize();
        var right = Vector3D.CrossProduct(forward, up); if (right.Length < .001) return null; right.Normalize();
        var trueUp = Vector3D.CrossProduct(right, forward); trueUp.Normalize();
        var fromCamera = worldPoint - camera.Position;
        var depth = Vector3D.DotProduct(fromCamera, forward); if (depth <= .05) return null;
        var focalLength = viewport.ActualWidth / (2 * Math.Tan(camera.FieldOfView * Math.PI / 360));
        return new Point(
            viewport.ActualWidth / 2 + Vector3D.DotProduct(fromCamera, right) * focalLength / depth,
            viewport.ActualHeight / 2 - Vector3D.DotProduct(fromCamera, trueUp) * focalLength / depth);
    }

    private void Zoom(int delta)
    {
        var direction = camera.Position - new Point3D(0, 3, 0); var scale = delta > 0 ? .88 : 1.12;
        var next = new Point3D(direction.X * scale, 3 + direction.Y * scale, direction.Z * scale);
        if ((next - new Point3D(0, 3, 0)).Length is > 13 and < 55) camera.Position = next;
        camera.LookDirection = new Point3D(0, 3, 0) - camera.Position;
        UpdateScaleOverlayPositions();
    }
}
