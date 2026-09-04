using System.Globalization;

namespace TimingTableCalculator;

internal static class MagnitudeNumberFormatter
{
    public static string Format(double value, int leadingDigits, int trailingDecimals)
    {
        var decimalPlaces = DecimalPlaces(value, leadingDigits, trailingDecimals);
        var format = decimalPlaces > 0 ? "0." + new string('0', decimalPlaces) : "0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static int DecimalPlaces(double value, int leadingDigits, int trailingDecimals)
    {
        leadingDigits = Math.Clamp(leadingDigits, 1, 4); trailingDecimals = Math.Clamp(trailingDecimals, 0, 3);
        var magnitude = Math.Abs(value);
        var actualLeadingDigits = magnitude < 1 ? 1 : (int)Math.Floor(Math.Log10(magnitude)) + 1;
        return actualLeadingDigits < leadingDigits ? trailingDecimals : 0;
    }

    public static string ExcelFormat(int leadingDigits, int trailingDecimals)
    {
        if (trailingDecimals <= 0 || leadingDigits <= 1) return "0";
        var threshold = Math.Pow(10, Math.Clamp(leadingDigits, 1, 4) - 1).ToString("0", CultureInfo.InvariantCulture);
        return $"[>={threshold}]0;[<=-{threshold}]0;0.{new string('0', Math.Clamp(trailingDecimals, 0, 3))}";
    }
}
