namespace TimingTableCalculator;

internal static class DirectionalSmoother
{
    public static double[,] Apply(double[,] source, int top, int bottom, int left, int right, bool outerToInner, double strength, int passes)
    {
        var working = (double[,])source.Clone();
        var maximumLayer = Math.Min((bottom - top) / 2, (right - left) / 2);
        for (var pass = 0; pass < passes; pass++)
        {
            var next = (double[,])working.Clone();
            if (outerToInner)
                for (var layer = 1; layer <= maximumLayer; layer++) SmoothLayer(next, top, bottom, left, right, layer, true, strength);
            else
                for (var layer = maximumLayer - 1; layer >= 0; layer--) SmoothLayer(next, top, bottom, left, right, layer, false, strength);
            working = next;
        }
        return working;
    }

    private static void SmoothLayer(double[,] values, int top, int bottom, int left, int right, int layer, bool fromOuter, double strength)
    {
        for (var row = top; row <= bottom; row++) for (var col = left; col <= right; col++)
        {
            if (Layer(row, col, top, bottom, left, right) != layer) continue;
            double sum = 0; var count = 0;
            for (var dr = -1; dr <= 1; dr++) for (var dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                var rr = row + dr; var cc = col + dc;
                if (rr < top || rr > bottom || cc < left || cc > right) continue;
                var neighborLayer = Layer(rr, cc, top, bottom, left, right);
                if (fromOuter ? neighborLayer < layer : neighborLayer > layer) { sum += values[rr, cc]; count++; }
            }
            if (count > 0) values[row, col] += (sum / count - values[row, col]) * strength;
        }
    }

    private static int Layer(int row, int col, int top, int bottom, int left, int right) =>
        Math.Min(Math.Min(row - top, bottom - row), Math.Min(col - left, right - col));
}
