namespace TimingTableCalculator;

public enum TwoPointSurfaceMode { Flatten, Smooth }

public sealed record TwoPointSurfaceResult(
    double[,] Values,
    IReadOnlyList<(int Row, int Col)> Path,
    IReadOnlyCollection<(int Row, int Col)> ChangedCells);

public static class TwoPointSurfaceEditor
{
    public static TwoPointSurfaceResult Apply(
        double[,] source,
        (int Row, int Col) first,
        (int Row, int Col) second,
        TwoPointSurfaceMode mode,
        int smoothingPasses = 3,
        double smoothingStrength = .7)
    {
        Validate(source, first, second, smoothingPasses, smoothingStrength);
        var path = SurfaceSculptor.Line(first, second);
        var result = (double[,])source.Clone();
        if (path.Count <= 2) return new(result, path, Array.Empty<(int Row, int Col)>());

        if (mode == TwoPointSurfaceMode.Flatten)
        {
            var firstValue = source[first.Row, first.Col];
            var secondValue = source[second.Row, second.Col];
            for (var index = 1; index < path.Count - 1; index++)
            {
                var fraction = index / (double)(path.Count - 1);
                var point = path[index];
                result[point.Row, point.Col] = firstValue + (secondValue - firstValue) * fraction;
            }
        }
        else
        {
            for (var pass = 0; pass < smoothingPasses; pass++)
            {
                var sample = (double[,])result.Clone();
                for (var index = 1; index < path.Count - 1; index++)
                {
                    var previous = path[index - 1]; var point = path[index]; var next = path[index + 1];
                    var target = (sample[previous.Row, previous.Col] + sample[next.Row, next.Col]) / 2d;
                    result[point.Row, point.Col] = sample[point.Row, point.Col] + (target - sample[point.Row, point.Col]) * smoothingStrength;
                }
            }
        }

        var changed = path.Skip(1).SkipLast(1).Where(point => result[point.Row, point.Col] != source[point.Row, point.Col]).ToArray();
        return new(result, path, changed);
    }

    private static void Validate(double[,] source, (int Row, int Col) first, (int Row, int Col) second, int passes, double strength)
    {
        if (source.GetLength(0) == 0 || source.GetLength(1) == 0 || source.Cast<double>().Any(value => !double.IsFinite(value)))
            throw new ArgumentException("The surface must contain finite values.");
        if (!Inside(source, first) || !Inside(source, second)) throw new ArgumentOutOfRangeException(nameof(first), "Both points must be inside the surface.");
        if (first == second) throw new ArgumentException("Choose two different surface cells.");
        if (passes is < 1 or > 20 || !double.IsFinite(strength) || strength is <= 0 or > 1)
            throw new ArgumentException("Check the two-point smoothing settings.");
    }

    private static bool Inside(double[,] source, (int Row, int Col) point) =>
        point.Row >= 0 && point.Row < source.GetLength(0) && point.Col >= 0 && point.Col < source.GetLength(1);
}
