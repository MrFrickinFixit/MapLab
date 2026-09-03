namespace TimingTableCalculator;

internal sealed record SurfaceTableRegion(int Top, int Bottom, int Left, int Right)
{
    public int RowCount => Bottom - Top + 1;
    public int ColumnCount => Right - Left + 1;

    public static bool TryCreate(int rows, int columns, IReadOnlyCollection<(int Row, int Col)> selection, out SurfaceTableRegion region, out string error)
    {
        region = new SurfaceTableRegion(0, rows - 1, 0, columns - 1); error = "";
        if (rows < 2 || columns < 2) { error = "A 3D surface requires at least 2 rows and 2 columns."; return false; }
        if (selection.Count == 0) return true;
        var distinct = selection.ToHashSet();
        if (distinct.Any(cell => cell.Row < 0 || cell.Row >= rows || cell.Col < 0 || cell.Col >= columns))
        { error = "Select one solid rectangular group of cells before opening the 3D view."; return false; }
        var top = distinct.Min(cell => cell.Row); var bottom = distinct.Max(cell => cell.Row);
        var left = distinct.Min(cell => cell.Col); var right = distinct.Max(cell => cell.Col);
        if (distinct.Count != (bottom - top + 1) * (right - left + 1))
        { error = "Select one solid rectangular group of cells before opening the 3D view."; return false; }
        if (bottom - top < 1 || right - left < 1)
        { error = "Select at least 2 rows and 2 columns to create a 3D surface."; return false; }
        region = new SurfaceTableRegion(top, bottom, left, right); return true;
    }

    public double[,] Extract(double[,] source)
    {
        if (source.GetLength(0) <= Bottom || source.GetLength(1) <= Right) throw new ArgumentException("The selected 3D region is outside the table.");
        var result = new double[RowCount, ColumnCount];
        for (var row = 0; row < RowCount; row++) for (var col = 0; col < ColumnCount; col++) result[row, col] = source[Top + row, Left + col];
        return result;
    }

    public double[] ExtractRows(double[] axis) => axis.Length > Bottom ? axis.Skip(Top).Take(RowCount).ToArray() : throw new ArgumentException("The selected 3D region is outside the row axis.");
    public double[] ExtractColumns(double[] axis) => axis.Length > Right ? axis.Skip(Left).Take(ColumnCount).ToArray() : throw new ArgumentException("The selected 3D region is outside the column axis.");
    public HashSet<(int Row, int Col)> AllLocalCells() => Enumerable.Range(0, RowCount).SelectMany(row => Enumerable.Range(0, ColumnCount).Select(col => (row, col))).ToHashSet();
    public (int Row, int Col) ToSource((int Row, int Col) local) => (Top + local.Row, Left + local.Col);
    public HashSet<(int Row, int Col)> ToSource(IReadOnlyCollection<(int Row, int Col)> local) => local.Select(ToSource).ToHashSet();

    public double[,] Merge(double[,] source, double[,] regionValues, IReadOnlyCollection<(int Row, int Col)> affectedLocalCells)
    {
        if (regionValues.GetLength(0) != RowCount || regionValues.GetLength(1) != ColumnCount) throw new ArgumentException("The edited 3D region has the wrong dimensions.");
        var result = (double[,])source.Clone();
        foreach (var local in affectedLocalCells)
        {
            if (local.Row < 0 || local.Row >= RowCount || local.Col < 0 || local.Col >= ColumnCount) throw new ArgumentException("An edited 3D cell is outside the selected region.");
            var sourceCell = ToSource(local); result[sourceCell.Row, sourceCell.Col] = regionValues[local.Row, local.Col];
        }
        return result;
    }
}
