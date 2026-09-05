using System.Globalization;

namespace TimingTableCalculator;

internal static class MagnitudeNumberFormatter
{
    public static string Format(double value, int leadingDigits, int trailingDecimals, int trailingZeroPlaces = -1)
    {
        var decimalPlaces = DecimalPlaces(value, leadingDigits, trailingDecimals);
        trailingZeroPlaces = trailingZeroPlaces < 0 ? decimalPlaces : Math.Clamp(trailingZeroPlaces, 0, 4);
        var maximumPlaces = Math.Max(decimalPlaces, trailingZeroPlaces);
        var format = maximumPlaces > 0 ? "0." + new string('0', trailingZeroPlaces) + new string('#', maximumPlaces - trailingZeroPlaces) : "0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string FormatActual(double value, int trailingDecimals, int trailingZeroPlaces)
    {
        trailingDecimals = Math.Clamp(trailingDecimals, 0, 4);
        trailingZeroPlaces = Math.Clamp(trailingZeroPlaces, 0, 4);
        var maximumPlaces = Math.Max(trailingDecimals, trailingZeroPlaces);
        var format = maximumPlaces > 0 ? "0." + new string('0', trailingZeroPlaces) + new string('#', maximumPlaces - trailingZeroPlaces) : "0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static int DecimalPlaces(double value, int leadingDigits, int trailingDecimals)
    {
        leadingDigits = Math.Clamp(leadingDigits, 1, 4); trailingDecimals = Math.Clamp(trailingDecimals, 0, 4);
        var magnitude = Math.Abs(value);
        var actualLeadingDigits = magnitude < 1 ? 1 : (int)Math.Floor(Math.Log10(magnitude)) + 1;
        return actualLeadingDigits < leadingDigits ? trailingDecimals : 0;
    }

    public static string ExcelFormat(int leadingDigits, int trailingDecimals, int trailingZeroPlaces = -1)
    {
        trailingDecimals = Math.Clamp(trailingDecimals, 0, 4);
        trailingZeroPlaces = trailingZeroPlaces < 0 ? trailingDecimals : Math.Clamp(trailingZeroPlaces, 0, 4);
        var maximumPlaces = Math.Max(trailingDecimals, trailingZeroPlaces);
        var padded = maximumPlaces > 0 ? "0." + new string('0', trailingZeroPlaces) + new string('#', maximumPlaces - trailingZeroPlaces) : "0";
        if (leadingDigits <= 1) return padded;
        var threshold = Math.Pow(10, Math.Clamp(leadingDigits, 1, 4) - 1).ToString("0", CultureInfo.InvariantCulture);
        var thresholdFormat = trailingZeroPlaces > 0 ? "0." + new string('0', trailingZeroPlaces) : "0";
        return $"[>={threshold}]{thresholdFormat};[<=-{threshold}]{thresholdFormat};{padded}";
    }
}
