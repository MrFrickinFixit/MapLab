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
            "Smooth the selection, then inspect it in the 3D view.",
            "Export the finished map to CSV or Excel. Setup and editing cards are above the map; display, output, and history cards are below it."));

        Section(document, "How the Tables Relate", Bullets(
            "Fueling and Ignition Timing keep separate MAP scales and MAP units.",
            "Fueling and Ignition Timing use coordinated matrix dimensions and RPM scaling.",
            "Map Sandbox is completely independent of both engine maps.",
            "Timing, fueling, and sandbox values each have separate Undo and Redo history."));

        Section(document, "Number Display and Stored Precision", Bullets(
            "Fueling, Ignition Timing, and Map Sandbox each have independent Leading Digits and Trailing Decimals display controls.",
            "Leading Digits sets the magnitude at which values switch to whole-number display. With the default setting of 3, 85.47 displays as 85.5 while 105.47 displays as 105.",
            "Trailing Decimals controls how many decimal places are shown below that threshold; it does not change the stored table value.",
            "Cell edits and pasted table values retain up to three decimal places even when the normal table display is rounded.",
            "The selected display format is also used by the 3D value scale, 3D tooltip, and Excel number formatting. Clipboard and CSV data retain the stored precision."));

        Section(document, "Selecting and Editing Cells", Bullets(
            "Drag with the left mouse button to select a rectangular area.",
            "Ctrl+click or Ctrl+drag to add separated cells or another area.",
            "Right-click one solid rectangular selection and choose Select transition ring to replace it with an adjustable-width perimeter band while leaving its center unselected.",
            "Choose Highlight region of interest to leave a red rectangular outline around the selected cells after the normal selection is cleared. Each 2D table keeps its own highlight; use Clear region of interest to remove it.",
            "Click a single unselected cell to replace the previous selection. Ctrl+A selects the complete active table.",
            "With several cells selected, edit one selected cell and press Enter to apply the value to all selected cells.",
            "Right-click a selection for copy, paste, offset, smoothing, and clear commands.",
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

        Section(document, "Smoothing", Bullets(
            "Rows (Smooth Rows in the right-click menu) reshapes each row between its left and right selected endpoints. It needs at least three columns and leaves those endpoint columns unchanged.",
            "Columns (Smooth Columns in the right-click menu) reshapes each column between its top and bottom selected endpoints. It needs at least three rows and leaves those endpoint rows unchanged.",
            "Smooth Selected opens Advanced Smoothing. It works on the exact selected cells, including separated Ctrl-selected areas; use it for irregular selections instead of Rows or Columns, which operate across the selection's bounding rectangle.",
            "For a pothole or raised feature, create a transition ring and use Smooth to Surroundings. The unselected center and the cells outside the original rectangle remain fixed shape anchors.",
            "Choose an algorithm inside Advanced Smoothing. Smooth to Surroundings is the option for blending a narrow wrinkle into the good surface beside it.",
            "These tools are available in Fueling, Ignition Timing, and Map Sandbox, including their editable 3D views. Fueling smoothing edits VE values; the lb/hr view is a calculated display."));
        document.Blocks.Add(Illustration("Row and column smoothing use the outside selected cells as anchors and reshape only the cells between them.", SmoothingDiagram()));

        Section(document, "Advanced Smoothing Algorithms", Bullets(
            "Standard weighted smoothing averages nearby selected cells, giving more weight to the center and its direct neighbors. It does not sample outside the selection.",
            "Constrained surface smoothing uses the same local weighted average in Smooth Selected; perimeter and overshoot controls provide the constraints.",
            "Shape-preserving interpolation blends toward horizontal and vertical trends from the selected endpoints. It can reshape interior peaks; it is not the removed standalone Interpolate command.",
            "Spike removal (median) blends toward the middle value of nearby selected cells to reduce isolated peaks or dips.",
            "Edge-preserving smoothing gives less influence to neighbors whose values differ sharply, helping retain stronger transitions.",
            "Weighted center / perimeter combines local smoothing with the selected area's overall and perimeter averages. Center Influence appears only for this algorithm: 0% favors the perimeter average; 100% favors the average of all selected cells."));

        Section(document, "Advanced Smoothing Controls", Bullets(
            "Strength (1-100%) controls how far each pass moves toward the smoothing result. Lower values make smaller changes; 100% applies the full result of that pass, not a guarantee that the map becomes flat.",
            "Passes (1-20) repeats smoothing on the preceding pass's result. More passes increase the cumulative change.",
            "Preserve selection perimeter leaves selected boundary cells unchanged in the original algorithms. Every cell in a one- or two-cell-wide strip is a boundary cell, so enabling this can prevent any change. This control is not used or shown for Smooth to Surroundings.",
            "Prevent value overshoot limits the original algorithms to the selection's starting minimum and maximum. Smooth to Surroundings instead uses each cell's sampled neighborhood, including outside references, so a raised strip can fall or a dipped strip can rise.",
            "Apply changes the current table immediately and leaves the dialog open. Each click creates one Undo step covering all its passes; applying again smooths the already adjusted table. Close does not undo applied changes."));

        Section(document, "Smooth to Surroundings", Bullets(
            "Select the cells to repair, open Smooth Selected, and choose Smooth to Surroundings in Algorithm. Only selected cells change; unselected surrounding cells remain fixed throughout all passes.",
            "Direction controls where samples come from: Across columns (left / right) works across a vertical strip; Across rows (above / below) works across a horizontal strip. Both directions includes horizontal, vertical, and diagonal neighbors.",
            "Neighbor Reach (1-10 cells per side) is measured from each selected cell, not from the selection's outer edge. Reach 2 samples up to two cells away in each enabled direction; it never enlarges the area being edited.",
            "Samples include selected and unselected neighbors within reach. Gaussian weights favor nearby positions using actual X/Y axis spacing, so uneven RPM or MAP breakpoints affect their influence. At a table edge, only available cells are sampled.",
            "Selected edge cells remain editable in this mode, including single cells and one- or two-cell-wide strips. If little changes, check the direction and whether Neighbor Reach reaches beyond the raised or dipped area. Similar neighboring values can also produce little change."));
        document.Blocks.Add(Illustration("Select the wrinkle, not the good surface around it. Arrows show the sampling direction; only the blue cells can change.", SurroundingsDiagram()));

        Section(document, "Remove a Two-Cell-Wide Wrinkle", Numbered(
            "Select just the two raised or dipped columns or rows along the wrinkle.",
            "Open Smooth Selected and choose Smooth to Surroundings. For two columns, choose Across columns (left / right); for two rows, choose Across rows (above / below).",
            "For a small first adjustment, try Neighbor Reach 2, Strength 50%, and 1 pass. Keep Prevent value overshoot enabled. These are example starting settings, not a calibration target.",
            "Click Apply, inspect the values and 3D contour, then adjust strength, reach, or passes as needed. Unselected reference cells stay unchanged. Use Undo to remove the last Apply before trying another setting."));

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
            "Smooth the generated map before committing runs Smooth Rows, then Smooth Columns across the generated table. It is separate from Smooth to Surroundings and does not use the Advanced Smoothing direction or neighbor-reach settings. Clear the checkbox to skip this final smoothing step.",
            "Forced-induction setup displays MAP inputs in PSI and enables 1-, 2-, 3-bar, or custom MAP sensor selection. Custom ratings must be greater than 1 and no more than 10 bar.",
            "The selected sensor rating proposes an editable maximum boost range. Use Apply MAP Range to rescale the Fueling MAP breakpoints before continuing.",
            "Boosted Fueling setup is performed inside the VE wizard; there is no separate Convert to Boosted command.",
            "The wizard operates on the Fueling MAP axis only and cannot change the Ignition Timing MAP scale.",
            "Always review generated values before applying them to an ECU."));

        Section(document, "Learn Apply Table", Bullets(
            "The Learn Apply Table beside Fueling holds signed percentage corrections, not absolute VE values. Its matrix size, RPM/MAP breakpoints, MAP units, and display precision follow Fueling; edit the axes and precision in Fueling.",
            "Paste a complete table or select the upper-left destination cell for a partial block. Paste cell values without axis headings; positive and negative numbers may include a percent sign. Blank pasted fields leave existing offsets unchanged; zero means no correction. Copy and paste clear the selection afterward.",
            "Offsets retain up to three decimal places. Values below -100%, non-numeric values, and blocks that do not fit are rejected without partially changing the table.",
            "Transfer to Fueling applies all nonzero offsets matching the current axes, not just the selected cells. New VE = current VE x (1 + offset / 100): 80 VE with +10% becomes 88 VE; with -10% it becomes 72 VE. This always edits underlying VE even when Fueling displays lb/hr, and the fuel-flow display is recalculated.",
            "Choose Transfer only for the exact corrections, or Transfer and smooth changed cells to apply Smooth to Surroundings afterward: both directions, reach 2, strength 65%, and 2 passes. Only nonzero-offset cells are smoothed; the rest of the fuel map stays fixed. Cancel transfers nothing.",
            "After transfer, choose whether to clear the Learn Apply Table or keep its values. Keeping them and transferring again compounds the correction on the updated VE. Clear removes all learn offsets, including retained off-axis entries.",
            "A transfer, including optional smoothing, is one Fueling Undo step. Learn edits and clearing have their own Undo/Redo in the Learn Apply tab; Fueling Undo does not restore cleared learn values.",
            "Offsets are attached to RPM/MAP coordinates. Axis changes retain unmatched or ambiguous offsets without transferring them; their count appears below the learn table. Matching offsets return when their coordinates are available again. Axis changes reset learn Undo/Redo history.",
            "Learn offsets autosave with Fueling and are included in .map exports. Importing an older file without Learn Apply data starts this table empty."));

        Section(document, "Map Sandbox", Bullets(
            "Use Sandbox for custom numeric tables that do not need operating-region boundaries.",
            "Set independent dimensions, breakpoints, and X/Y units. Choose Custom… to add or remove unit names.",
            "Sandbox includes selection editing, offsets, all smoothing algorithms, 3D editing, history, autosave, CSV, and Excel export.",
            "Sandbox changes never modify Fueling or Ignition Timing."));

        Section(document, "Fuel Delta Smoothing", Bullets(
            "Fueling's Delta Compare compares the pasted block with the current values. Use Pasted applies those values exactly, without smoothing.",
            "Smooth Delta applies the pasted values and then basic weighted smoothing within that pasted block. Cells outside the block remain unchanged and are not sampled.",
            "Delta Compare has its own strength and passes. To blend a pasted wrinkle toward outside neighbors, apply the paste, select the cells to repair, then use Smooth Selected with Smooth to Surroundings."));

        Section(document, "3D View", Bullets(
            "Drag to orbit and use the mouse wheel to zoom.",
            "Select one solid rectangular area of at least 2 rows by 2 columns in the 2D Timing, Fueling, or Sandbox table before opening 3D Map. Only those cells and their matching axis ranges are shown; opening 3D Map with no selection shows the full table.",
            "The selected rectangle is the fixed 3D workspace until that viewer closes. Close it and choose another 2D rectangle to work on a different area; edits and Undo still map to the original table cells.",
            "Hover over the surface for the live X/Y/value tooltip and crosshair.",
            "Choose Select surface cells, then click or drag. Hold Ctrl to add separated cells or areas.",
            "Right-click a 3D selection for the same editing and smoothing commands as the 2D table.",
            "For direct surface editing, choose Raise, Lower, Smooth, or Flatten in the Sculpt toolbar, then drag on the surface. Radius follows actual X/Y breakpoint spacing; Strength controls blending and Amount controls the center-point change for Raise and Lower.",
            "Soft and Medium falloff fade toward the brush edge; Hard applies full strength across the brush. Flatten samples its target value where the stroke begins. Smooth blends toward local neighboring cells.",
            "Limit to selection becomes available after selecting surface cells and prevents the brush from writing outside that mask. Prevent overshoot keeps results inside the table's value range at the start of the stroke.",
            "Sculpt changes preview while dragging and commit on mouse-up as one source-table Undo step. Press Escape before mouse-up to cancel the preview. Fuel sculpting is available in the editable VE view, not the calculated lb/hr view.",
            "Return to Rotation clears the 3D selection. Closing the viewer clears its corresponding 2D selection.",
            "3D Undo and Redo operate on that viewer's source table."));
        document.Blocks.Add(Illustration("Orbit in Rotation mode, zoom with the wheel, then switch to surface selection for editing.", ThreeDDiagram()));

        Section(document, "Copy, Paste, Export, and Recovery", Bullets(
            "Copied cells use tab-separated rows and can be moved between Map Lab tables and compatible tuning software.",
            "Select an axis starting point before pasting a copied row or column of breakpoints.",
            "CSV and Excel put the X scale at the bottom. Excel includes matching heat-map formatting without header filters.",
            "Settings → Export .map saves Timing, Fueling, Learn Apply, and Sandbox tables, axes, units, boundaries, colors, display preferences, smoothing choices, and VE setup options in one portable file.",
            "Settings → Import .map validates the complete file before replacing the workspaces, then autosaves the imported settings.",
            "Timing, Fueling, and Sandbox autosave separately; Learn Apply data is saved with Fueling. Dialog settings are retained until changed.",
            "Only one instance of each tool dialog is opened. Selecting its command again restores and activates the existing dialog.",
            "A Working… progress window is shown for longer map changes and extensive Undo or Redo operations.",
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

    private static Canvas SurroundingsDiagram()
    {
        var canvas = DiagramCanvas(720, 245);
        var fixedColor = Color.FromRgb(225, 231, 239);
        var arrowColor = Color.FromRgb(30, 130, 111);
        AddLabel(canvas, "ACROSS COLUMNS (LEFT / RIGHT)", 28, 12, Accent, true);
        DrawGrid(canvas, 28, 40, 6, 6, 42, 23, (_, col) => col is 2 or 3 ? Accent.Color : fixedColor);
        AddArrow(canvas, 72, 98, 126, 98, arrowColor);
        AddArrow(canvas, 234, 98, 178, 98, arrowColor);
        AddLabel(canvas, "Two selected columns", 28, 188, Muted, false);

        AddLabel(canvas, "ACROSS ROWS (ABOVE / BELOW)", 405, 12, Accent, true);
        DrawGrid(canvas, 405, 40, 6, 6, 42, 23, (row, _) => row is 2 or 3 ? Accent.Color : fixedColor);
        AddArrow(canvas, 530, 52, 530, 97, arrowColor);
        AddArrow(canvas, 530, 165, 530, 120, arrowColor);
        AddLabel(canvas, "Two selected rows", 405, 188, Muted, false);
        AddLabel(canvas, "BLUE: selected cells can change", 28, 221, Accent, true);
        AddLabel(canvas, "GRAY: surrounding cells stay fixed", 405, 221, Muted, true);
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
