using System.Windows.Media;

namespace TimingTableCalculator;

/// <summary>Shared frozen brushes for map redraws. Reusing Freezables avoids thousands of short-lived allocations while dragging or refreshing.</summary>
internal static class UiBrushCache
{
    public static readonly SolidColorBrush GridLine = Frozen(Color.FromRgb(29, 42, 57));
    public static readonly SolidColorBrush AxisLine = Frozen(Color.FromRgb(38, 58, 76));
    public static readonly SolidColorBrush Idle = Frozen(Color.FromRgb(67, 145, 208));
    public static readonly SolidColorBrush IdleHigh = Frozen(Color.FromRgb(73, 119, 188));
    public static readonly SolidColorBrush Wot = Frozen(Color.FromRgb(236, 138, 69));
    public static readonly SolidColorBrush Cruise = Frozen(Color.FromRgb(54, 199, 173));
    public static readonly Brush[] Spectrum = CreateSpectrum();

    public static Brush SpectrumAt(double normalized) => Spectrum[(int)Math.Round(Math.Clamp(normalized, 0, 1) * (Spectrum.Length - 1))];

    public static Brush[] CreateLinearPalette(Color low, Color high)
    {
        var palette = new Brush[301];
        for (var index = 0; index < palette.Length; index++)
        {
            var t = index / (double)(palette.Length - 1);
            byte Blend(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
            palette[index] = Frozen(Color.FromRgb(Blend(low.R, high.R), Blend(low.G, high.G), Blend(low.B, high.B)));
        }
        return palette;
    }

    public static SolidColorBrush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    private static Brush[] CreateSpectrum()
    {
        var palette = new Brush[301];
        for (var index = 0; index < palette.Length; index++) palette[index] = Frozen(Hsl(index, .96, .52));
        return palette;
    }

    private static Color Hsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
