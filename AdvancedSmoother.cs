namespace TimingTableCalculator;

public enum AdvancedSmoothingAlgorithm
{
    ShapePreserving,
    ConstrainedSurface,
    SpikeRemoval,
    EdgePreserving,
    WeightedCenterPerimeter,
    StandardWeighted
}

public sealed record AdvancedSmoothingOptions(
    AdvancedSmoothingAlgorithm Algorithm,
    double Strength,
    int Passes,
    bool PreservePerimeter,
    bool PreventOvershoot,
    double CenterInfluence);

public static class AdvancedSmoother
{
    public static double[,] Apply(double[,] source, IReadOnlyCollection<(int Row, int Col)> selectedCells, AdvancedSmoothingOptions options)
    {
        var selected = selectedCells.Where(cell => cell.Row >= 0 && cell.Row < source.GetLength(0) && cell.Col >= 0 && cell.Col < source.GetLength(1)).ToHashSet();
        if (selected.Count == 0) return (double[,])source.Clone();
        var work = (double[,])source.Clone(); var minimum = selected.Min(cell => source[cell.Row, cell.Col]); var maximum = selected.Max(cell => source[cell.Row, cell.Col]);
        bool IsPerimeter((int Row, int Col) cell) => !selected.Contains((cell.Row - 1, cell.Col)) || !selected.Contains((cell.Row + 1, cell.Col)) || !selected.Contains((cell.Row, cell.Col - 1)) || !selected.Contains((cell.Row, cell.Col + 1));
        var perimeter = selected.Where(IsPerimeter).ToArray();
        for (var pass = 0; pass < options.Passes; pass++)
        {
            var next = (double[,])work.Clone(); var centerAverage = selected.Average(cell => work[cell.Row, cell.Col]); var perimeterAverage = perimeter.Length == 0 ? centerAverage : perimeter.Average(cell => work[cell.Row, cell.Col]);
            foreach (var cell in selected)
            {
                if (options.PreservePerimeter && IsPerimeter(cell)) continue;
                var target = options.Algorithm switch
                {
                    AdvancedSmoothingAlgorithm.ShapePreserving => ShapeTarget(work, cell.Row, cell.Col, selected),
                    AdvancedSmoothingAlgorithm.SpikeRemoval => MedianTarget(work, cell.Row, cell.Col, selected),
                    AdvancedSmoothingAlgorithm.EdgePreserving => BilateralTarget(work, cell.Row, cell.Col, selected, Math.Max(.01, (maximum - minimum) * .15)),
                    AdvancedSmoothingAlgorithm.WeightedCenterPerimeter => NeighborAverage(work, cell.Row, cell.Col, selected) * .7 + (perimeterAverage + (centerAverage - perimeterAverage) * options.CenterInfluence) * .3,
                    _ => NeighborAverage(work, cell.Row, cell.Col, selected)
                };
                var value = work[cell.Row, cell.Col] + (target - work[cell.Row, cell.Col]) * options.Strength;
                next[cell.Row, cell.Col] = options.PreventOvershoot ? Math.Clamp(value, minimum, maximum) : value;
            }
            work = next;
        }
        return work;
    }

    private static double NeighborAverage(double[,] values, int row, int col, HashSet<(int Row, int Col)> selected)
    {
        double sum = values[row, col] * 4, weight = 4;
        foreach (var (dr, dc, w) in new[] { (-1, 0, 2), (1, 0, 2), (0, -1, 2), (0, 1, 2), (-1, -1, 1), (-1, 1, 1), (1, -1, 1), (1, 1, 1) })
            if (selected.Contains((row + dr, col + dc))) { sum += values[row + dr, col + dc] * w; weight += w; }
        return sum / weight;
    }

    private static double MedianTarget(double[,] values, int row, int col, HashSet<(int Row, int Col)> selected)
    {
        var samples = new List<double>(9); for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++) if (selected.Contains((row + dr, col + dc))) samples.Add(values[row + dr, col + dc]);
        samples.Sort(); return samples[samples.Count / 2];
    }

    private static double BilateralTarget(double[,] values, int row, int col, HashSet<(int Row, int Col)> selected, double rangeSigma)
    {
        var center = values[row, col]; double sum = 0, weight = 0;
        for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++) if (selected.Contains((row + dr, col + dc)))
        { var difference = values[row + dr, col + dc] - center; var w = Math.Exp(-(dr * dr + dc * dc) / 2d) * Math.Exp(-(difference * difference) / (2 * rangeSigma * rangeSigma)); sum += values[row + dr, col + dc] * w; weight += w; }
        return weight > 0 ? sum / weight : center;
    }

    private static double ShapeTarget(double[,] values, int row, int col, HashSet<(int Row, int Col)> selected)
    {
        var rowCells = selected.Where(cell => cell.Row == row).OrderBy(cell => cell.Col).ToArray(); var colCells = selected.Where(cell => cell.Col == col).OrderBy(cell => cell.Row).ToArray();
        var horizontal = rowCells.Length > 1 ? values[row, rowCells[0].Col] + (values[row, rowCells[^1].Col] - values[row, rowCells[0].Col]) * (col - rowCells[0].Col) / Math.Max(1d, rowCells[^1].Col - rowCells[0].Col) : values[row, col];
        var vertical = colCells.Length > 1 ? values[colCells[0].Row, col] + (values[colCells[^1].Row, col] - values[colCells[0].Row, col]) * (row - colCells[0].Row) / Math.Max(1d, colCells[^1].Row - colCells[0].Row) : values[row, col];
        return (horizontal + vertical) / 2;
    }

    public static double[,] Apply(double[,] source, int top, int bottom, int left, int right, AdvancedSmoothingOptions options)
    {
        var work = (double[,])source.Clone();
        var minimum = double.MaxValue; var maximum = double.MinValue;
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++)
        { minimum = Math.Min(minimum, source[row, col]); maximum = Math.Max(maximum, source[row, col]); }

        for (var pass = 0; pass < options.Passes; pass++)
        {
            var next = (double[,])work.Clone();
            var centerAverage = CenterAverage(work, top, bottom, left, right);
            var perimeterAverage = PerimeterAverage(work, top, bottom, left, right);
            for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++)
            {
                if (options.PreservePerimeter && (row == top || row == bottom || col == left || col == right)) continue;
                var target = options.Algorithm switch
                {
                    AdvancedSmoothingAlgorithm.ShapePreserving => ShapeTarget(work, row, col, top, bottom, left, right),
                    AdvancedSmoothingAlgorithm.ConstrainedSurface => NeighborAverage(work, row, col, top, bottom, left, right),
                    AdvancedSmoothingAlgorithm.SpikeRemoval => MedianTarget(work, row, col, top, bottom, left, right),
                    AdvancedSmoothingAlgorithm.EdgePreserving => BilateralTarget(work, row, col, top, bottom, left, right, Math.Max(.01, (maximum - minimum) * .15)),
                    _ => WeightedTarget(work, row, col, top, bottom, left, right, centerAverage, perimeterAverage, options.CenterInfluence)
                };
                var value = work[row, col] + (target - work[row, col]) * options.Strength;
                next[row, col] = options.PreventOvershoot ? Math.Clamp(value, minimum, maximum) : value;
            }
            work = next;
        }
        return work;
    }

    private static double ShapeTarget(double[,] values, int row, int col, int top, int bottom, int left, int right)
    {
        var horizontal = values[row, left] + (values[row, right] - values[row, left]) * SmoothStep((col - left) / (double)Math.Max(1, right - left));
        var vertical = values[top, col] + (values[bottom, col] - values[top, col]) * SmoothStep((row - top) / (double)Math.Max(1, bottom - top));
        return (horizontal + vertical) / 2;
    }

    private static double NeighborAverage(double[,] values, int row, int col, int top, int bottom, int left, int right)
    {
        double sum = values[row, col] * 4, weight = 4;
        foreach (var (dr, dc, w) in new[] { (-1, 0, 2), (1, 0, 2), (0, -1, 2), (0, 1, 2), (-1, -1, 1), (-1, 1, 1), (1, -1, 1), (1, 1, 1) })
        {
            var r = row + dr; var c = col + dc; if (r < top || r > bottom || c < left || c > right) continue;
            sum += values[r, c] * w; weight += w;
        }
        return sum / weight;
    }

    private static double MedianTarget(double[,] values, int row, int col, int top, int bottom, int left, int right)
    {
        var samples = new List<double>(9);
        for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++)
        { var r = row + dr; var c = col + dc; if (r >= top && r <= bottom && c >= left && c <= right) samples.Add(values[r, c]); }
        samples.Sort(); return samples[samples.Count / 2];
    }

    private static double BilateralTarget(double[,] values, int row, int col, int top, int bottom, int left, int right, double rangeSigma)
    {
        var center = values[row, col]; double sum = 0, weight = 0;
        for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++)
        {
            var r = row + dr; var c = col + dc; if (r < top || r > bottom || c < left || c > right) continue;
            var spatial = Math.Exp(-(dr * dr + dc * dc) / 2d); var difference = values[r, c] - center;
            var range = Math.Exp(-(difference * difference) / (2 * rangeSigma * rangeSigma)); var w = spatial * range;
            sum += values[r, c] * w; weight += w;
        }
        return weight > 0 ? sum / weight : center;
    }

    private static double WeightedTarget(double[,] values, int row, int col, int top, int bottom, int left, int right, double centerAverage, double perimeterAverage, double centerInfluence)
    {
        var local = NeighborAverage(values, row, col, top, bottom, left, right);
        var global = perimeterAverage + (centerAverage - perimeterAverage) * centerInfluence;
        return local * .7 + global * .3;
    }

    private static double CenterAverage(double[,] values, int top, int bottom, int left, int right)
    {
        var rowRadius = Math.Max(0, (bottom - top) / 4); var colRadius = Math.Max(0, (right - left) / 4);
        var centerRow = (top + bottom) / 2; var centerCol = (left + right) / 2; double sum = 0; var count = 0;
        for (var row = centerRow - rowRadius; row <= centerRow + rowRadius; row++) for (var col = centerCol - colRadius; col <= centerCol + colRadius; col++) { sum += values[row, col]; count++; }
        return sum / Math.Max(1, count);
    }

    private static double PerimeterAverage(double[,] values, int top, int bottom, int left, int right)
    {
        double sum = 0; var count = 0;
        for (var col = left; col <= right; col++) { sum += values[top, col]; count++; if (bottom != top) { sum += values[bottom, col]; count++; } }
        for (var row = top + 1; row < bottom; row++) { sum += values[row, left]; count++; if (right != left) { sum += values[row, right]; count++; } }
        return sum / Math.Max(1, count);
    }

    private static double SmoothStep(double value) { var t = Math.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
}
