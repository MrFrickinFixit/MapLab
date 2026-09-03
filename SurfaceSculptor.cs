namespace TimingTableCalculator;

public enum SurfaceSculptMode { Raise, Lower, Smooth, Flatten }
public enum SurfaceBrushFalloff { Soft, Medium, Hard }

public sealed record SurfaceSculptOptions(
    SurfaceSculptMode Mode,
    int Radius,
    double Strength,
    double Amount,
    SurfaceBrushFalloff Falloff,
    bool PreventOvershoot);

public sealed record SurfaceSculptResult(double[,] Values, HashSet<(int Row, int Col)> AffectedCells);

public static class SurfaceSculptor
{
    public static SurfaceSculptResult ApplyPath(
        double[,] source,
        double[,] strokeOriginal,
        IReadOnlyCollection<(int Row, int Col)> centers,
        SurfaceSculptOptions options,
        double[] columnAxis,
        double[] rowAxis,
        double flattenTarget,
        IReadOnlySet<(int Row, int Col)>? selectionMask = null)
    {
        Validate(source, strokeOriginal, centers, options, columnAxis, rowAxis, flattenTarget);
        var result = (double[,])source.Clone();
        var affected = new HashSet<(int Row, int Col)>();
        var columnScale = TypicalGap(columnAxis);
        var rowScale = TypicalGap(rowAxis);
        var (minimum, maximum) = Range(strokeOriginal);
        (int Row, int Col)? previous = null;

        foreach (var center in centers)
        {
            if (previous == center) continue;
            previous = center;
            ApplyStamp(result, center, options, columnAxis, rowAxis, columnScale, rowScale,
                flattenTarget, minimum, maximum, selectionMask, affected);
        }
        return new SurfaceSculptResult(result, affected);
    }

    public static IReadOnlyList<(int Row, int Col)> Line((int Row, int Col) start, (int Row, int Col) end)
    {
        var result = new List<(int Row, int Col)>();
        var x0 = start.Col; var y0 = start.Row; var x1 = end.Col; var y1 = end.Row;
        var dx = Math.Abs(x1 - x0); var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0); var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            result.Add((y0, x0));
            if (x0 == x1 && y0 == y1) break;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
        return result;
    }

    private static void ApplyStamp(
        double[,] values,
        (int Row, int Col) center,
        SurfaceSculptOptions options,
        double[] columnAxis,
        double[] rowAxis,
        double columnScale,
        double rowScale,
        double flattenTarget,
        double minimum,
        double maximum,
        IReadOnlySet<(int Row, int Col)>? selectionMask,
        HashSet<(int Row, int Col)> affected)
    {
        var sample = (double[,])values.Clone();
        var reach = options.Radius + .5;
        for (var row = 0; row < values.GetLength(0); row++)
        for (var col = 0; col < values.GetLength(1); col++)
        {
            if (selectionMask is not null && !selectionMask.Contains((row, col))) continue;
            var rowDistance = Math.Abs(rowAxis[row] - rowAxis[center.Row]) / rowScale;
            var columnDistance = Math.Abs(columnAxis[col] - columnAxis[center.Col]) / columnScale;
            var distance = Math.Sqrt(rowDistance * rowDistance + columnDistance * columnDistance);
            if (distance > reach) continue;
            var falloff = Falloff(distance / reach, options.Falloff);
            var blend = options.Strength * falloff;
            if (blend <= 0) continue;

            var current = sample[row, col];
            var target = options.Mode switch
            {
                SurfaceSculptMode.Raise => current + options.Amount,
                SurfaceSculptMode.Lower => current - options.Amount,
                SurfaceSculptMode.Flatten => flattenTarget,
                _ => NeighborAverage(sample, row, col)
            };
            var updated = current + (target - current) * blend;
            if (options.PreventOvershoot) updated = Math.Clamp(updated, minimum, maximum);
            if (!double.IsFinite(updated) || updated == values[row, col]) continue;
            values[row, col] = updated;
            affected.Add((row, col));
        }
    }

    private static double NeighborAverage(double[,] values, int row, int col)
    {
        double sum = values[row, col] * 4; var weight = 4d;
        for (var dr = -1; dr <= 1; dr++)
        for (var dc = -1; dc <= 1; dc++)
        {
            if (dr == 0 && dc == 0) continue;
            var sampleRow = row + dr; var sampleCol = col + dc;
            if (sampleRow < 0 || sampleRow >= values.GetLength(0) || sampleCol < 0 || sampleCol >= values.GetLength(1)) continue;
            var sampleWeight = dr == 0 || dc == 0 ? 2d : 1d;
            sum += values[sampleRow, sampleCol] * sampleWeight; weight += sampleWeight;
        }
        return sum / weight;
    }

    private static double Falloff(double normalizedDistance, SurfaceBrushFalloff falloff)
    {
        var remaining = Math.Clamp(1 - normalizedDistance, 0, 1);
        return falloff switch
        {
            SurfaceBrushFalloff.Hard => 1,
            SurfaceBrushFalloff.Medium => remaining,
            _ => remaining * remaining * (3 - 2 * remaining)
        };
    }

    private static double TypicalGap(double[] axis)
    {
        var gaps = axis.Zip(axis.Skip(1), (left, right) => Math.Abs(right - left)).Where(gap => gap > 0).OrderBy(gap => gap).ToArray();
        if (gaps.Length == 0) throw new ArgumentException("Sculpting requires distinct axis breakpoints.");
        return gaps[gaps.Length / 2];
    }

    private static (double Minimum, double Maximum) Range(double[,] values)
    {
        var minimum = double.PositiveInfinity; var maximum = double.NegativeInfinity;
        foreach (var value in values) { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); }
        return (minimum, maximum);
    }

    private static void Validate(double[,] source, double[,] original, IReadOnlyCollection<(int Row, int Col)> centers,
        SurfaceSculptOptions options, double[] columnAxis, double[] rowAxis, double flattenTarget)
    {
        if (source.GetLength(0) != original.GetLength(0) || source.GetLength(1) != original.GetLength(1) ||
            source.GetLength(0) != rowAxis.Length || source.GetLength(1) != columnAxis.Length)
            throw new ArgumentException("The sculpting matrix and axes must have matching dimensions.");
        if (source.Length == 0 || columnAxis.Any(value => !double.IsFinite(value)) || rowAxis.Any(value => !double.IsFinite(value)) ||
            source.Cast<double>().Any(value => !double.IsFinite(value)) || original.Cast<double>().Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Sculpting values and axes must be finite.");
        if (options.Radius is < 1 or > 10 || !double.IsFinite(options.Strength) || options.Strength is <= 0 or > 1 ||
            !double.IsFinite(options.Amount) || options.Amount < 0 || !double.IsFinite(flattenTarget))
            throw new ArgumentException("Check the sculpting radius, strength, and amount.");
        if (centers.Any(center => center.Row < 0 || center.Row >= source.GetLength(0) || center.Col < 0 || center.Col >= source.GetLength(1)))
            throw new ArgumentException("A sculpting point is outside the surface.");
        _ = TypicalGap(columnAxis); _ = TypicalGap(rowAxis);
    }
}
