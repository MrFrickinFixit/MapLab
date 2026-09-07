namespace TimingTableCalculator;

internal static class TableAutoPopulate
{
    internal static double[,]? Apply(double[,] source, IReadOnlyCollection<(int Row, int Col)> selected, double[] xAxis, double[] yAxis)
    {
        if (selected.Count < 2 || source.GetLength(0) != yAxis.Length || source.GetLength(1) != xAxis.Length ||
            !TransitionRingSelection.TryGetRectangle(selected, out var top, out var bottom, out var left, out var right)) return null;

        var result = (double[,])source.Clone();
        if (top == bottom)
        {
            var start = source[top, left]; var end = source[top, right];
            for (var col = left; col <= right; col++) result[top, col] = Lerp(start, end, Fraction(xAxis[col], xAxis[left], xAxis[right], col - left, right - left));
            return result;
        }
        if (left == right)
        {
            var start = source[top, left]; var end = source[bottom, left];
            for (var row = top; row <= bottom; row++) result[row, left] = Lerp(start, end, Fraction(yAxis[row], yAxis[top], yAxis[bottom], row - top, bottom - top));
            return result;
        }

        var topLeft = source[top, left]; var topRight = source[top, right];
        var bottomLeft = source[bottom, left]; var bottomRight = source[bottom, right];
        for (var row = top; row <= bottom; row++)
        {
            var y = Fraction(yAxis[row], yAxis[top], yAxis[bottom], row - top, bottom - top);
            for (var col = left; col <= right; col++)
            {
                var x = Fraction(xAxis[col], xAxis[left], xAxis[right], col - left, right - left);
                result[row, col] = Lerp(Lerp(topLeft, topRight, x), Lerp(bottomLeft, bottomRight, x), y);
            }
        }
        return result;
    }

    private static double Fraction(double value, double start, double end, int position, int span) =>
        Math.Abs(end - start) > 1e-12 ? Math.Clamp((value - start) / (end - start), 0, 1) : position / (double)Math.Max(1, span);

    private static double Lerp(double start, double end, double fraction) => start + (end - start) * fraction;
}
