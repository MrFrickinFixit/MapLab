using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TimingTableCalculator;

public sealed class LearnApplyPanel : Grid
{
    private static readonly Brush[] PositivePalette = UiBrushCache.CreateLinearPalette(Color.FromRgb(245, 247, 249), Color.FromRgb(85, 200, 175));
    private static readonly Brush[] NegativePalette = UiBrushCache.CreateLinearPalette(Color.FromRgb(245, 247, 249), Color.FromRgb(240, 115, 139));
    private readonly LearnApplyTable model;
    private readonly Func<bool, int> transfer;
    private readonly Grid table = new() { Background = UiBrushCache.GridLine, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock status = new() { Text = "Learn table ready", Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
    private readonly TextBlock summary = new() { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock currentFileText = new() { Text = "Current file: Untitled", Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) };
    private readonly HashSet<(int Row, int Col)> selected = [], dragBase = [];
    private readonly Button undoButton, redoButton, transferButton;
    private TextBox[,] cells = new TextBox[0, 0];
    private TextBox? pendingCell;
    private string pendingOriginal = "";
    private (int Row, int Col) anchor;
    private bool selecting;
    private int geometryVersion = -1;

    public LearnApplyPanel(LearnApplyTable model, Func<bool, int> transfer)
    {
        this.model = model; this.transfer = transfer;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); heading.ColumnDefinitions.Add(new ColumnDefinition());
        var title = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; title.Children.Add(new TextBlock { Text = "Learn Apply Table - VE Offset (%)", FontSize = 25, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }); title.Children.Add(currentFileText); CompactTableHeading.Align(title); Grid.SetColumn(title, 1); heading.Children.Add(title);
        status.Margin = new Thickness(16, 2, 0, 2); title.Children.Add(status); Children.Add(heading);

        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        tools.Children.Add(Command("Copy", 0xE8C8, Copy)); tools.Children.Add(Command("Paste", 0xE77F, Paste));
        tools.Children.Add(Command("Clear selected", 0xE894, ClearSelected)); tools.Children.Add(Command("Clear table", 0xE74D, ClearTable));
        undoButton = Command("Undo", 0xE7A7, () => { CommitPending(); model.Undo(); Deselect(); });
        redoButton = Command("Redo", 0xE7A6, () => { CommitPending(); model.Redo(); Deselect(); });
        tools.Children.Add(undoButton); tools.Children.Add(redoButton);
        transferButton = Command("Transfer to Fueling", 0xE8B5, Transfer, true);
        transferButton.ToolTip = "Transfer all nonzero offsets on the current axes to Fueling VE, regardless of the Fueling display units.";
        tools.Children.Add(transferButton); Grid.SetRow(tools, 1); Children.Add(tools);
        var frame = new Border { BorderBrush = UiBrushCache.GridLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(3), Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)), Child = new TableViewport { Table = table } };
        Grid.SetRow(frame, 2); Children.Add(frame); summary.Margin = new Thickness(16, 2, 0, 2); summary.TextAlignment = TextAlignment.Left; title.Children.Add(summary);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (FrameworkElement item in title.Children) { item.VerticalAlignment = VerticalAlignment.Center; item.Margin = new Thickness(0, 2, 16, 2); if (item is TextBlock text) text.TextAlignment = TextAlignment.Left; }
        PreviewKeyDown += HandleKeys;
        table.PreviewMouseLeftButtonUp += (_, _) => selecting = false;
        model.Changed += Refresh;
        Refresh();
    }

    internal void SetCurrentFile(string displayName, string? fullPath)
    {
        currentFileText.Text = $"Current file: {displayName}"; currentFileText.ToolTip = fullPath;
    }

    private void Build()
    {
        pendingCell = null; selected.Clear(); selecting = false;
        geometryVersion = model.GeometryVersion;
        table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
        cells = new TextBox[model.Map.Length, model.Rpm.Length];
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TableLayoutMetrics.YAxisTitleWidth) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TableLayoutMetrics.CellWidth) });
        for (var col = 0; col < model.Rpm.Length; col++) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TableLayoutMetrics.CellWidth) });
        for (var row = 0; row < model.Map.Length; row++) table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TableLayoutMetrics.CellHeight) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TableLayoutMetrics.XAxisCellHeight) }); table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        if (model.Map.Length == 0 || model.Rpm.Length == 0) return;
        var title = AxisText(model.MapUnit.Contains("PSI") ? "MAP (PSIG)" : "MAP (kPa)"); title.LayoutTransform = new RotateTransform(-90);
        Grid.SetRowSpan(title, model.Map.Length); table.Children.Add(title);
        for (var row = 0; row < model.Map.Length; row++)
        {
            AddAxis(model.Map[row], row, 1);
            for (var col = 0; col < model.Rpm.Length; col++)
            {
                var cell = new TextBox { Tag = (row, col), ToolTip = "", TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 10, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(.5), Padding = new Thickness(1, 0, 1, 0) };
                cell.ToolTipOpening += (_, _) => { var point = ((int Row, int Col))cell.Tag; UpdateCellToolTip(point.Row, point.Col); };
                cell.PreviewMouseLeftButtonDown += CellDown;
                cell.MouseEnter += (_, e) => { if (selecting && e.LeftButton == MouseButtonState.Pressed) SelectRectangle(((int Row, int Col))cell.Tag); };
                cell.PreviewMouseRightButtonDown += (_, _) => { var point = ((int Row, int Col))cell.Tag; if (!selected.Contains(point)) { CommitPending(); selected.Clear(); selected.Add(point); RefreshSelection(); } };
                cell.GotKeyboardFocus += (_, _) => { var point = ((int Row, int Col))cell.Tag; pendingCell = cell; pendingOriginal = Editable(model.GetValue(point.Row, point.Col)); cell.Text = pendingOriginal; cell.SelectAll(); };
                cell.LostKeyboardFocus += (_, _) => { if (ReferenceEquals(pendingCell, cell)) CommitPending(); };
                var menu = new ContextMenu();
                foreach (var (name, action) in new (string, Action)[] { ("Copy selected", Copy), ("Paste", Paste), ("Auto-populate selected cells", AutoPopulateCells), ("Clear Selection", Deselect), ("Transfer all offsets to Fueling", Transfer) })
                { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); menu.Items.Add(item); }
                cell.ContextMenu = menu; cells[row, col] = cell; Grid.SetRow(cell, row); Grid.SetColumn(cell, col + 2); table.Children.Add(cell);
            }
        }
        for (var col = 0; col < model.Rpm.Length; col++) AddAxis(model.Rpm[col], model.Map.Length, col + 2);
        var rpmTitle = AxisText("Engine RPM"); Grid.SetRow(rpmTitle, model.Map.Length + 1); Grid.SetColumn(rpmTitle, 2); Grid.SetColumnSpan(rpmTitle, model.Rpm.Length); table.Children.Add(rpmTitle);
    }

    private void Refresh()
    {
        if (geometryVersion != model.GeometryVersion) Build();
        var active = model.ActiveCells();
        var max = Math.Max(1, active.Select(point => Math.Abs(model.GetValue(point.Row, point.Col))).DefaultIfEmpty(0).Max());
        for (var row = 0; row < model.Map.Length; row++) for (var col = 0; col < model.Rpm.Length; col++)
        {
            var value = model.GetValue(row, col); var cell = cells[row, col];
            if (!ReferenceEquals(cell, pendingCell)) { var text = model.Format(value); if (cell.Text != text) cell.Text = text; }
            var t = Math.Min(1, Math.Abs(value) / max);
            var background = (value >= 0 ? PositivePalette : NegativePalette)[(int)Math.Round(t * (PositivePalette.Length - 1))];
            if (!ReferenceEquals(cell.Background, background)) cell.Background = background;
            if (!ReferenceEquals(cell.Foreground, Brushes.Black)) cell.Foreground = Brushes.Black;
        }
        RefreshSelection();
        undoButton.IsEnabled = model.CanUndo; redoButton.IsEnabled = model.CanRedo; transferButton.IsEnabled = model.Map.Length > 0 && model.Rpm.Length > 0;
        summary.Text = $"{model.Rpm.Length} columns x {model.Map.Length} rows | {model.MapUnit} | {active.Count} nonzero offsets | Fueling precision: {model.TrailingDecimals} decimals below {model.LeadingDigits} leading digits";
        if (model.UnmatchedCount > 0) summary.Text += $" | {model.UnmatchedCount} retained offsets do not match the current axes and will not transfer";
    }

    private void UpdateCellToolTip(int row, int col) => cells[row, col].ToolTip = $"{model.Rpm[col]:0.########} RPM | {model.Map[row]:0.########} {model.MapUnit} | {Editable(model.GetValue(row, col))}% VE offset";

    private void CellDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: ValueTuple<int, int> point } cell) return;
        if (!ReferenceEquals(pendingCell, cell) && !CommitPending()) { e.Handled = true; return; }
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!control && selected.Count > 1 && selected.Contains(point)) { selecting = false; cell.Focus(); cell.SelectAll(); e.Handled = true; return; }
        dragBase.Clear(); if (control) dragBase.UnionWith(selected);
        anchor = point; selecting = true; SelectRectangle(point); cell.Focus(); e.Handled = true;
    }
    private void SelectRectangle((int Row, int Col) end)
    {
        selected.Clear(); selected.UnionWith(dragBase);
        for (var row = Math.Min(anchor.Row, end.Row); row <= Math.Max(anchor.Row, end.Row); row++)
        for (var col = Math.Min(anchor.Col, end.Col); col <= Math.Max(anchor.Col, end.Col); col++) selected.Add((row, col));
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        for (var row = 0; row < cells.GetLength(0); row++) for (var col = 0; col < cells.GetLength(1); col++)
        {
            var isSelected = selected.Contains((row, col)); var brush = isSelected ? Brushes.DodgerBlue : UiBrushCache.GridLine; var thickness = new Thickness(isSelected ? 1.5 : .5);
            if (!ReferenceEquals(cells[row, col].BorderBrush, brush)) cells[row, col].BorderBrush = brush;
            if (cells[row, col].BorderThickness != thickness) cells[row, col].BorderThickness = thickness;
        }
    }
    private void Deselect() { pendingCell = null; selected.Clear(); selecting = false; Keyboard.ClearFocus(); RefreshSelection(); }
    private bool CommitPending()
    {
        var cell = pendingCell; pendingCell = null;
        if (cell is null || cell.Text == pendingOriginal) return true;
        try
        {
            var parsed = LearnApplyMath.ParseClipboard(cell.Text);
            if (parsed.GetLength(0) != 1 || parsed.GetLength(1) != 1 || parsed[0, 0] is not { } value) throw new ArgumentException("Enter one percentage offset.");
            var point = ((int Row, int Col))cell.Tag;
            var targets = selected.Contains(point) ? selected.ToArray() : [point];
            model.SetCells(targets.Select(target => (target.Row, target.Col, value))); Refresh(); return true;
        }
        catch (ArgumentException ex) { Refresh(); Info(ex.Message); return false; }
    }

    private void Copy()
    {
        if (!CommitPending()) return;
        if (selected.Count == 0) { Info("Select cells to copy."); return; }
        var text = new StringBuilder();
        var top = selected.Min(point => point.Row); var bottom = selected.Max(point => point.Row);
        var left = selected.Min(point => point.Col); var right = selected.Max(point => point.Col);
        for (var row = top; row <= bottom; row++)
        {
            for (var col = left; col <= right; col++)
            { if (col > left) text.Append('\t'); if (selected.Contains((row, col))) text.Append(Editable(model.GetValue(row, col))); }
            text.AppendLine();
        }
        try { Clipboard.SetText(text.ToString().TrimEnd('\r', '\n')); Deselect(); status.Text = "Offsets copied; selection cleared"; }
        catch { Info("The clipboard is currently unavailable."); }
    }
    private void Paste()
    {
        if (!CommitPending()) return;
        try { PasteText(Clipboard.GetText()); }
        catch (Exception ex) { Info(ex is ArgumentException ? ex.Message : "The clipboard is currently unavailable."); }
    }
    internal void PasteText(string text)
    {
        var block = LearnApplyMath.ParseClipboard(text);
        var rows = block.GetLength(0); var cols = block.GetLength(1);
        var top = selected.Count > 0 ? selected.Min(point => point.Row) : 0;
        var left = selected.Count > 0 ? selected.Min(point => point.Col) : 0;
        if (rows == model.Map.Length && cols == model.Rpm.Length) top = left = 0;
        if (top + rows > model.Map.Length || left + cols > model.Rpm.Length) throw new ArgumentException("The pasted block does not fit at the selected starting cell. No values were pasted.");
        var edits = new List<(int Row, int Col, double Value)>();
        if (rows == 1 && cols == 1 && selected.Count > 1 && block[0, 0] is { } single)
            edits.AddRange(selected.Select(point => (point.Row, point.Col, single)));
        else for (var row = 0; row < rows; row++) for (var col = 0; col < cols; col++)
            if (block[row, col] is { } value) edits.Add((top + row, left + col, value));
        model.SetCells(edits); Deselect(); status.Text = $"Pasted {edits.Count} offsets; selection cleared";
    }
    private void ClearSelected()
    {
        if (!CommitPending()) return;
        if (selected.Count == 0) { Info("Select cells to clear."); return; }
        model.SetCells(selected.Select(point => (point.Row, point.Col, 0d))); Deselect(); status.Text = "Selected offsets cleared";
    }
    private void AutoPopulateCells()
    {
        if (!CommitPending()) return;
        var source = model.SnapshotValues(); var populated = TableAutoPopulate.Apply(source, selected, model.Rpm, model.Map);
        if (populated is null) { Info("Select one solid row, column, or rectangular block containing at least two Learn Apply cells."); return; }
        model.SetCells(selected.Select(point => (point.Row, point.Col, populated[point.Row, point.Col]))); Refresh(); status.Text = $"Auto-populated {selected.Count} Learn Apply cells from the selection endpoints";
    }
    private void ClearTable()
    {
        if (!CommitPending()) return;
        if (MessageBox.Show(Window.GetWindow(this), "Clear all learn offsets, including any retained offsets outside the current axes?", "Clear Learn Apply Table", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        model.Clear(); Deselect(); status.Text = "Learn table cleared";
    }
    private void Transfer()
    {
        if (!CommitPending()) return;
        var active = model.ActiveCells();
        if (active.Count == 0) { Info("There are no nonzero offsets on the current Fueling axes to transfer."); return; }
        var dialog = new LearnApplyTransferWindow(active.Count, model.UnmatchedCount) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var count = transfer(dialog.Smooth);
            if (count == 0) return;
            Deselect(); status.Text = $"Transferred {count} offsets to Fueling VE";
            if (MessageBox.Show(Window.GetWindow(this), "Transfer complete. Clear the Learn Apply Table now, including any retained offsets?\n\nKeeping the offsets allows reuse. Transferring them again will compound the correction on the updated VE values.", "Clear transferred learn offsets?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            { model.Clear(); status.Text = $"Transferred {count} offsets; learn table cleared"; }
        }
        catch (ArgumentException ex) { Info(ex.Message); }
    }
    internal void HandleKeys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { if (CommitPending()) Deselect(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { Deselect(); e.Handled = true; return; }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.A: CommitPending(); selected.Clear(); for (var row = 0; row < model.Map.Length; row++) for (var col = 0; col < model.Rpm.Length; col++) selected.Add((row, col)); RefreshSelection(); break;
            case Key.C: Copy(); break;
            case Key.V: Paste(); break;
            case Key.Z: CommitPending(); model.Undo(); Deselect(); break;
            case Key.Y: CommitPending(); model.Redo(); Deselect(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void AddAxis(double value, int row, int col)
    {
        var axis = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 31, 45)), BorderBrush = UiBrushCache.GridLine, BorderThickness = new Thickness(.5), Child = AxisText(value.ToString("0.########", CultureInfo.InvariantCulture)), ToolTip = "Axes follow the Fueling table." };
        Grid.SetRow(axis, row); Grid.SetColumn(axis, col); table.Children.Add(axis);
    }
    private static TextBlock AxisText(string text) => new() { Text = text, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private static string Editable(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private void Info(string text) => MessageBox.Show(Window.GetWindow(this), text, "Learn Apply Table", MessageBoxButton.OK, MessageBoxImage.Information);
    private static Button Command(string text, int icon, Action action, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = ((char)icon).ToString(), FontFamily = new FontFamily("Segoe MDL2 Assets"), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = primary ? Brushes.White : Brushes.Black });
        content.Children.Add(new TextBlock { Text = text, Foreground = primary ? Brushes.White : Brushes.Black });
        var button = new Button { Content = content, ToolTip = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 7, 3), Background = primary ? Brushes.RoyalBlue : Brushes.WhiteSmoke, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold };
        button.Click += (_, _) => action(); return button;
    }
}
