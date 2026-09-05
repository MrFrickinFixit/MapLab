namespace TimingTableCalculator;

internal static class OffsetMath
{
    public static double Apply(double value, int direction, double amount, bool percentage)
    {
        var sign = direction < 0 ? -1d : 1d;
        return percentage
            ? value * (1d + sign * amount / 100d)
            : value + sign * amount;
    }
}
