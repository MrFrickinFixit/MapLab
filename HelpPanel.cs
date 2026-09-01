using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimingTableCalculator;

public sealed class HelpPanel : Grid
{
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(0, 103, 192));
    private static readonly SolidColorBrush Text = new(Color.FromRgb(32, 32, 32));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(94, 94, 94));

    public HelpPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        var heading = new StackPanel { Margin = new Thickness(4, 0, 0, 18) };
        heading.Children.Add(new TextBlock { Text = "MAP LAB", Foreground = Accent, FontSize = 12, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = "Help & Instructions", Foreground = Text, FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) });
        heading.Children.Add(new TextBlock { Text = "A quick reference for building, editing, smoothing, viewing, and exporting maps.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }); Children.Add(heading);

        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Foreground = Text, Background = Brushes.White, PagePadding = new Thickness(28), ColumnWidth = double.PositiveInfinity, LineHeight = 20 };
        Section(document, "Quick Start", Numbered(
            "Choose Fueling, Ignition Timing, or Map Sandbox.",
            "Set the matrix size and edit or paste the X and Y axis breakpoints.",
            "Drag across cells to select an area. Ctrl+click or Ctrl+drag adds another area.",
            "Type into a selected cell to apply that value to every selected cell.",
            "Interpolate or smooth the selection, then inspect it in the 3D view.",
            "Export the finished map to CSV or Excel. Setup and editing cards are above the map; display, output, and history cards are below it."));

        Section(document, "How the Tables Relate", Bullets(
            "Fueling and Ignition Timing keep separate MAP scales and MAP units.",
            "Fueling and Ignition Timing use coordinated matrix dimensions and RPM scaling.",
            "Map Sandbox is completely independent of both engine maps.",
            "Timing, fueling, and sandbox values each have separate Undo and Redo history."));

        Section(document, "Selecting and Editing Cells", Bullets(
            "Drag with the left mouse button to select a rectangular area.",
            "Ctrl+click or Ctrl+drag to add separated cells or another area.",
            "Click a single unselected cell to replace the previous selection. Ctrl+A selects the complete active table.",
            "With several cells selected, edit one selected cell and press Enter to apply the value to all selected cells.",
            "Right-click a selection for copy, paste, offset, interpolation, smoothing, and clear commands.",
            "After pasting a complete table of cells, the selection is cleared."));
        document.Blocks.Add(Illustration("Drag selects one area. Hold Ctrl while clicking or dragging to add separated areas.", SelectionDiagram()));

        Section(document, "Editing Axes", Bullets(
            "Click an axis breakpoint, enter a value, and press Enter. Breakpoints must remain ordered and unique.",
            "Changing a minimum or maximum endpoint automatically rescales that axis.",
            "Drag across axis values and right-click to Auto-fill or paste breakpoints.",
            "Pasted X, RPM, Y, or MAP breakpoint values are retained as entered; clicking back into the table commits the pasted scale.",
            "MAP uses whole-number kPa values and one decimal place for PSI.",
            "Fuel MAP changes never alter the Ignition Timing MAP scale, and timing MAP changes never alter Fueling.",
            "Sandbox X and Y units may be standard, Unitless, or custom. Custom-unit changes relabel the axis without converting values."));
        document.Blocks.Add(Illustration("Y-axis values run vertically; X-axis values run left to right along the bottom.", AxisDiagram()));

        Section(document, "Smoothing and Interpolation", Bullets(
            "Interpolate rebuilds the selected interior from its outer edge values.",
            "Smooth Rows blends horizontally between the outer selected columns; those columns remain anchors.",
            "Smooth Columns blends vertically between the outer selected rows; those rows remain anchors.",
            "Smooth Selected opens Advanced Smoothing and includes every selected cell, including separated Ctrl-selected areas.",
            "Shape-preserving favors gradual transitions. Constrained and standard weighted modes average nearby selected cells.",
            "Spike removal reduces isolated peaks or dips. Edge-preserving retains stronger transitions.",
            "Preserve selection perimeter fixes the outside cells. Prevent overshoot limits results to the selected value range.",
            "Every Apply action creates a separate Undo step."));
        document.Blocks.Add(Illustration("Row and column smoothing use the outside selected cells as anchors and reshape only the cells between them.", SmoothingDiagram()));

        Section(document, "Ignition Timing Regions", Bullets(
            "Choose Set boundaries, hover over the table, and click the intersecting cell to lock both lines.",
            "Lower-left is Idle Low MAP; upper-left is Idle High MAP.",
            "Lower-right is Cruise to Part Throttle; upper-right is Part Throttle to WOT.",
            "Regional profiles can fill directly or blend across their boundaries.",
            "Boost applies the entered change per pound of boost to selected cells. Negative values retard timing; positive values add timing."));
        document.Blocks.Add(Illustration("The intersecting boundary cell divides the table into four operating regions.", BoundaryDiagram()));

        Section(document, "Fueling and VE Setup", Bullets(
            "Fuel cells store editable volumetric-efficiency percentages. View as lb/hr changes only the display.",
            "VE Setup uses engine information and VE targets to create a preview before values are committed.",
            "Forced-induction setup displays MAP inputs in PSI and enables 1-, 2-, 3-bar, or custom MAP sensor selection. Custom ratings must be greater than 1 and no more than 10 bar.",
            "The selected sensor rating proposes an editable maximum boost range. Use Apply MAP Range to rescale the Fueling MAP breakpoints before continuing.",
            "Boosted Fueling setup is performed inside the VE wizard; there is no separate Convert to Boosted command.",
            "The wizard operates on the Fueling MAP axis only and cannot change the Ignition Timing MAP scale.",
            "Always review generated values before applying them to an ECU."));

        Section(document, "Map Sandbox", Bullets(
            "Use Sandbox for custom numeric tables that do not need operating-region boundaries.",
            "Set independent dimensions, breakpoints, and X/Y units. Choose Custom… to add or remove unit names.",
            "Sandbox includes selection editing, offsets, interpolation, all smoothing algorithms, 3D editing, history, autosave, CSV, and Excel export.",
            "Sandbox changes never modify Fueling or Ignition Timing."));

        Section(document, "3D View", Bullets(
            "Drag to orbit and use the mouse wheel to zoom.",
            "Hover over the surface for the live X/Y/value tooltip and crosshair.",
            "Choose Select surface cells, then click or drag. Hold Ctrl to add separated cells or areas.",
            "Right-click a 3D selection for the same editing and smoothing commands as the 2D table.",
            "Return to Rotation clears the 3D selection. Closing the viewer clears its corresponding 2D selection.",
            "3D Undo and Redo operate on that viewer's source table."));
        document.Blocks.Add(Illustration("Orbit in Rotation mode, zoom with the wheel, then switch to surface selection for editing.", ThreeDDiagram()));

        Section(document, "Copy, Paste, Export, and Recovery", Bullets(
            "Copied cells use tab-separated rows and can be moved between Map Lab tables and compatible tuning software.",
            "Select an axis starting point before pasting a copied row or column of breakpoints.",
            "CSV and Excel put the X scale at the bottom. Excel includes matching heat-map formatting without header filters.",
            "Timing, Fueling, and Sandbox autosave separately. Dialog settings are retained until changed.",
            "Use Undo immediately after an unwanted edit, paste, offset, smoothing operation, resize, or axis change."));

        Section(document, "Display and Long Operations", Bullets(
            "Use Number Display below each map to choose leading digits and trailing decimal precision without changing the stored values.",
            "Fueling values may retain up to three decimal places even when the table displays a rounded value.",
            "Large resizes, smoothing operations, and extensive Undo or Redo changes show a Working progress window.",
            "Only one instance of each modeless tool dialog is opened. Selecting its command again restores the existing dialog.",
            "Closing or cancelling a dialog returns focus to Map Lab and preserves the main window state."));

        document.Blocks.Add(Heading("Keyboard Shortcuts"));
        var shortcuts = new Table { CellSpacing = 0 }; shortcuts.Columns.Add(new TableColumn { Width = new GridLength(130) }); shortcuts.Columns.Add(new TableColumn()); var rows = new TableRowGroup(); shortcuts.RowGroups.Add(rows);
        Shortcut(rows, "Ctrl+A", "Select every cell in the active table"); Shortcut(rows, "Ctrl+C", "Copy selected cells"); Shortcut(rows, "Ctrl+V", "Paste cells or focused axis values"); Shortcut(rows, "Ctrl+Z", "Undo the active-table change"); Shortcut(rows, "Ctrl+Y", "Redo the active-table change"); Shortcut(rows, "Enter", "Commit a cell or axis edit"); Shortcut(rows, "Esc", "Cancel interactive timing-boundary selection"); document.Blocks.Add(shortcuts);

        document.Blocks.Add(new BlockUIContainer(new Border { Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 190, 92)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(14), Margin = new Thickness(0, 22, 0, 4), Child = new TextBlock { Text = "Calibration safety: Map Lab is a calculation and visualization tool. Verify exported values independently and use appropriate engine safeguards before applying a map to a vehicle.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(82, 62, 14)), FontWeight = FontWeights.SemiBold } }));
        var viewer = new FlowDocumentScrollViewer { Document = document, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, IsToolBarVisible = false, Background = Brushes.White };
        var frame = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), ClipToBounds = true, Child = viewer }; Grid.SetRow(frame, 1); Children.Add(frame);
    }

    private static void Section(FlowDocument document, string title, Block content) { document.Blocks.Add(Heading(title)); document.Blocks.Add(content); }
    private static Paragraph Heading(string text) => new(new Run(text)) { FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 18, 0, 8), KeepWithNext = true };
    private static List Bullets(params string[] items) => MakeList(TextMarkerStyle.Disc, items);
    private static List Numbered(params string[] items) => MakeList(TextMarkerStyle.Decimal, items);
    private static List MakeList(TextMarkerStyle marker, IEnumerable<string> items) { var list = new List { MarkerStyle = marker, Margin = new Thickness(22, 0, 0, 8) }; foreach (var item in items) list.ListItems.Add(new ListItem(new Paragraph(new Run(item)) { Margin = new Thickness(0, 2, 0, 4) })); return list; }
    private static void Shortcut(TableRowGroup group, string key, string description) { var row = new TableRow(); row.Cells.Add(Cell(key, true)); row.Cells.Add(Cell(description, false)); group.Rows.Add(row); }
    private static TableCell Cell(string text, bool bold) => new(new Paragraph(new Run(text)) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(8, 6, 8, 6) }) { BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)), BorderThickness = new Thickness(0, 0, 0, 1) };

    private static BlockUIContainer Illustration(string caption, UIElement visual)
    {
        var stack = new StackPanel();
        stack.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = visual });
        stack.Children.Add(new TextBlock { Text = caption, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 9, 2, 0) });
        return new BlockUIContainer(new Border { Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(205, 214, 225)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14), Margin = new Thickness(0, 6, 0, 16), Child = stack });
    }

    private static Canvas SelectionDiagram()
    {
        var canvas = DiagramCanvas(720, 170); DrawGrid(canvas, 28, 25, 9, 5, 48, 23, (row, col) => row is >= 1 and <= 3 && col is >= 1 and <= 4 ? Color.FromRgb(0, 103, 192) : Color.FromRgb(225, 231, 239));
        DrawGrid(canvas, 410, 25, 6, 5, 42, 23, (row, col) => row is >= 1 and <= 2 && col is >= 1 and <= 2 || row == 3 && col is >= 4 and <= 5 ? Color.FromRgb(54, 199, 173) : Color.FromRgb(225, 231, 239));
        AddLabel(canvas, "DRAG SELECTION", 28, 145, Accent, true); AddLabel(canvas, "+ CTRL: ADD AREAS", 410, 145, Accent, true);
        AddArrow(canvas, 72, 60, 220, 112, Color.FromRgb(255, 255, 255)); AddLabel(canvas, "drag", 137, 76, Brushes.White, true);
        return canvas;
    }

    private static Canvas AxisDiagram()
    {
        var canvas = DiagramCanvas(720, 170); DrawGrid(canvas, 120, 20, 8, 5, 55, 21, (_, _) => Color.FromRgb(225, 231, 239));
        AddArrow(canvas, 103, 126, 103, 26, Accent.Color); AddLabel(canvas, "Y AXIS", 30, 64, Accent, true);
        AddArrow(canvas, 120, 143, 555, 143, Color.FromRgb(54, 199, 173)); AddLabel(canvas, "X AXIS", 315, 147, new SolidColorBrush(Color.FromRgb(30, 130, 111)), true);
        AddLabel(canvas, "HIGH", 62, 17, Muted, true); AddLabel(canvas, "LOW", 68, 115, Muted, true); AddLabel(canvas, "LOW", 114, 147, Muted, true); AddLabel(canvas, "HIGH", 547, 147, Muted, true);
        return canvas;
    }

    private static Canvas SmoothingDiagram()
    {
        var canvas = DiagramCanvas(720, 190);
        DrawGrid(canvas, 28, 25, 8, 5, 36, 22, (row, col) => col is 0 or 7 ? Color.FromRgb(0, 103, 192) : Color.FromRgb((byte)(100 + col * 14), 205, 190));
        AddLabel(canvas, "SMOOTH ROWS", 28, 146, Accent, true); AddLabel(canvas, "anchors", 28, 163, Muted, false); AddLabel(canvas, "anchors", 252, 163, Muted, false);
        DrawGrid(canvas, 405, 25, 7, 5, 36, 22, (row, col) => row is 0 or 4 ? Color.FromRgb(0, 103, 192) : Color.FromRgb(110, (byte)(190 - row * 12), 220));
        AddLabel(canvas, "SMOOTH COLUMNS", 405, 146, Accent, true); AddLabel(canvas, "top anchor", 405, 163, Muted, false); AddLabel(canvas, "bottom anchor", 565, 163, Muted, false);
        return canvas;
    }

    private static Canvas BoundaryDiagram()
    {
        var canvas = DiagramCanvas(720, 220); var left = 95d; var top = 18d; var width = 520d; var height = 160d; var splitX = left + 190; var splitY = top + 72;
        AddRect(canvas, left, top, splitX - left, splitY - top, Color.FromRgb(255, 224, 146)); AddRect(canvas, splitX, top, left + width - splitX, splitY - top, Color.FromRgb(240, 158, 186));
        AddRect(canvas, left, splitY, splitX - left, top + height - splitY, Color.FromRgb(174, 220, 183)); AddRect(canvas, splitX, splitY, left + width - splitX, top + height - splitY, Color.FromRgb(139, 205, 232));
        canvas.Children.Add(new Line { X1 = splitX, X2 = splitX, Y1 = top, Y2 = top + height, Stroke = Brushes.Black, StrokeThickness = 4 }); canvas.Children.Add(new Line { X1 = left, X2 = left + width, Y1 = splitY, Y2 = splitY, Stroke = Brushes.Black, StrokeThickness = 4 });
        AddLabel(canvas, "IDLE HIGH MAP", 126, 43, Text, true); AddLabel(canvas, "PART THROTTLE → WOT", 367, 43, Text, true); AddLabel(canvas, "IDLE LOW MAP", 130, 126, Text, true); AddLabel(canvas, "CRUISE → PART THROTTLE", 360, 126, Text, true);
        AddLabel(canvas, "vertical boundary", splitX - 58, 186, Muted, false); AddLabel(canvas, "horizontal boundary", 618, splitY - 8, Muted, false); return canvas;
    }

    private static Canvas ThreeDDiagram()
    {
        var canvas = DiagramCanvas(720, 225); var surface = new Polygon { Points = new PointCollection { new(175, 42), new(520, 28), new(620, 139), new(105, 171) }, Fill = new LinearGradientBrush(Color.FromRgb(255, 75, 45), Color.FromRgb(113, 40, 220), 0), Stroke = Brushes.Black, StrokeThickness = 1.5 }; canvas.Children.Add(surface);
        for (var i = 1; i < 8; i++) { var x1 = 105 + (175 - 105) * i / 8d; var y1 = 171 + (42 - 171) * i / 8d; var x2 = 620 + (520 - 620) * i / 8d; var y2 = 139 + (28 - 139) * i / 8d; canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), StrokeThickness = 1 }); }
        for (var i = 1; i < 8; i++) { var x1 = 175 + (520 - 175) * i / 8d; var y1 = 42 + (28 - 42) * i / 8d; var x2 = 105 + (620 - 105) * i / 8d; var y2 = 171 + (139 - 171) * i / 8d; canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), StrokeThickness = 1 }); }
        AddArrow(canvas, 90, 67, 137, 31, Accent.Color); AddArrow(canvas, 137, 31, 190, 58, Accent.Color); AddLabel(canvas, "DRAG TO ORBIT", 25, 19, Accent, true);
        AddLabel(canvas, "WHEEL TO ZOOM", 528, 17, Muted, true); AddLabel(canvas, "SELECT SURFACE CELLS", 252, 190, new SolidColorBrush(Color.FromRgb(30, 130, 111)), true);
        var marker = new Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(Color.FromRgb(85, 214, 190)), Stroke = Brushes.White, StrokeThickness = 2 }; Canvas.SetLeft(marker, 390); Canvas.SetTop(marker, 92); canvas.Children.Add(marker); return canvas;
    }

    private static Canvas DiagramCanvas(double width, double height) => new() { Width = width, Height = height, Background = Brushes.Transparent };
    private static void DrawGrid(Canvas canvas, double left, double top, int columns, int rows, double cellWidth, double cellHeight, Func<int, int, Color> color)
    {
        for (var row = 0; row < rows; row++) for (var col = 0; col < columns; col++) AddRect(canvas, left + col * cellWidth, top + row * cellHeight, cellWidth - 2, cellHeight - 2, color(row, col));
    }
    private static void AddRect(Canvas canvas, double left, double top, double width, double height, Color color) { var rectangle = new Rectangle { Width = width, Height = height, Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromRgb(115, 130, 148)), StrokeThickness = 1 }; Canvas.SetLeft(rectangle, left); Canvas.SetTop(rectangle, top); canvas.Children.Add(rectangle); }
    private static void AddLabel(Canvas canvas, string text, double left, double top, Brush color, bool bold) { var label = new TextBlock { Text = text, Foreground = color, FontSize = 11, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal }; Canvas.SetLeft(label, left); Canvas.SetTop(label, top); canvas.Children.Add(label); }
    private static void AddLabel(Canvas canvas, string text, double left, double top, Color color, bool bold) => AddLabel(canvas, text, left, top, new SolidColorBrush(color), bold);
    private static void AddArrow(Canvas canvas, double x1, double y1, double x2, double y2, Color color)
    {
        var brush = new SolidColorBrush(color); canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 3 }); var angle = Math.Atan2(y2 - y1, x2 - x1); var size = 9d;
        canvas.Children.Add(new Polygon { Fill = brush, Points = new PointCollection { new(x2, y2), new(x2 - size * Math.Cos(angle - .55), y2 - size * Math.Sin(angle - .55)), new(x2 - size * Math.Cos(angle + .55), y2 - size * Math.Sin(angle + .55)) } });
    }
}
