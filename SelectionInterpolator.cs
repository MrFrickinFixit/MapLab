namespace TimingTableCalculator;

public static class SelectionInterpolator
{
    public static bool CanApply(int top, int bottom, int left, int right) =>
        top == bottom ? right - left >= 2 :
        left == right ? bottom - top >= 2 :
        bottom - top >= 2 && right - left >= 2;

    public static double[,] Apply(double[,] source, int top, int bottom, int left, int right)
    {
        var result = (double[,])source.Clone();
        if (top == bottom)
        {
            for (var col = left + 1; col < right; col++)
            {
                var fraction = (col - left) / (double)(right - left);
                result[top, col] = Lerp(source[top, left], source[top, right], fraction);
            }
            return result;
        }
        if (left == right)
        {
            for (var row = top + 1; row < bottom; row++)
            {
                var fraction = (row - top) / (double)(bottom - top);
                result[row, left] = Lerp(source[top, left], source[bottom, left], fraction);
            }
            return result;
        }

        var topLeft = source[top, left]; var topRight = source[top, right];
        var bottomLeft = source[bottom, left]; var bottomRight = source[bottom, right];
        for (var row = top + 1; row < bottom; row++) for (var col = left + 1; col < right; col++)
        {
            var u = (col - left) / (double)(right - left); var v = (row - top) / (double)(bottom - top);
            var horizontalEdges = Lerp(source[row, left], source[row, right], u);
            var verticalEdges = Lerp(source[top, col], source[bottom, col], v);
            var cornerBlend = Lerp(Lerp(topLeft, topRight, u), Lerp(bottomLeft, bottomRight, u), v);
            result[row, col] = horizontalEdges + verticalEdges - cornerBlend;
        }
        return result;
    }

    private static double Lerp(double low, double high, double fraction) => low + (high - low) * fraction;
}
