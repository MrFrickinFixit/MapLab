#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`n"
$source += ((Get-Content -LiteralPath (Join-Path $root 'SurfaceSculptor.cs') -Raw) -replace 'namespace TimingTableCalculator;', '')
$source += ((Get-Content -LiteralPath (Join-Path $root 'SurfaceTableRegion.cs') -Raw) -replace 'namespace TimingTableCalculator;', '')
$tests = @'
public static class SculptingTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-9, $"{message}: {actual} != {expected}");
    private static double[,] Grid(double center = 0) { var values = new double[5, 5]; values[2, 2] = center; return values; }
    private static readonly double[] Axis = { 0, 1, 2, 3, 4 };
    private static SurfaceSculptResult Apply(double[,] source, SurfaceSculptMode mode, SurfaceBrushFalloff falloff = SurfaceBrushFalloff.Hard,
        double amount = 2, double strength = 1, bool overshoot = false, IReadOnlySet<(int Row, int Col)>? mask = null,
        double[]? columns = null, double[]? rows = null, IReadOnlyCollection<(int Row, int Col)>? centers = null, double flatten = 10)
        => SurfaceSculptor.ApplyPath(source, (double[,])source.Clone(), centers ?? new[] { (2, 2) },
            new SurfaceSculptOptions(mode, 1, strength, amount, falloff, overshoot), columns ?? Axis, rows ?? Axis, flatten, mask);

    public static string Run()
    {
        var count = 0;
        void Test(string name, Action test) { test(); count++; Console.WriteLine($"PASS {name}"); }
        Test("Hard raise changes the center and radius-one neighbors", () => {
            var source = Grid(); var result = Apply(source, SurfaceSculptMode.Raise);
            Near(result.Values[2, 2], 2, "center"); Near(result.Values[1, 1], 2, "diagonal"); Near(result.Values[0, 2], 0, "outside");
            Check(result.AffectedCells.Count == 9, "affected count"); Near(source[2, 2], 0, "source mutated");
        });
        Test("Lower subtracts table units", () => Near(Apply(Grid(10), SurfaceSculptMode.Lower).Values[2, 2], 8, "lower"));
        Test("Soft falloff is strongest at the center", () => {
            var result = Apply(Grid(), SurfaceSculptMode.Raise, SurfaceBrushFalloff.Soft);
            Check(result.Values[2, 2] > result.Values[2, 1] && result.Values[2, 1] > result.Values[1, 1], "falloff order");
        });
        Test("Strength scales a brush stamp", () => Near(Apply(Grid(), SurfaceSculptMode.Raise, strength: .25).Values[2, 2], .5, "strength"));
        Test("Smooth reduces a spike", () => {
            var result = Apply(Grid(10), SurfaceSculptMode.Smooth);
            Check(result.Values[2, 2] < 10 && result.Values[2, 2] > 0, "spike not reduced");
        });
        Test("Flatten moves the brush toward the sampled target", () => {
            var result = Apply(Grid(), SurfaceSculptMode.Flatten, flatten: 12);
            Near(result.Values[2, 2], 12, "flatten center"); Near(result.Values[1, 1], 12, "flatten neighbor");
        });
        Test("Selection mask limits writes", () => {
            var mask = new HashSet<(int, int)> { (2, 2), (2, 3) };
            var result = Apply(Grid(), SurfaceSculptMode.Raise, mask: mask);
            Check(result.AffectedCells.SetEquals(mask), "mask mismatch"); Near(result.Values[1, 2], 0, "outside mask");
        });
        Test("Irregular physical axes affect brush distance", () => {
            double[] irregular = { 0, 1, 2, 20, 21 };
            var result = Apply(Grid(), SurfaceSculptMode.Raise, columns: irregular);
            Near(result.Values[2, 3], 0, "distant adjacent column included");
        });
        Test("Prevent overshoot keeps the starting table range", () => {
            var source = Grid(10); var result = Apply(source, SurfaceSculptMode.Raise, amount: 20, overshoot: true);
            Near(result.Values[2, 2], 10, "maximum exceeded"); Check(result.Values.Cast<double>().All(value => value is >= 0 and <= 10), "range exceeded");
        });
        Test("Line fills cells between sparse pointer samples", () => {
            var line = SurfaceSculptor.Line((0, 0), (4, 2));
            Check(line.First() == (0, 0) && line.Last() == (4, 2) && line.Count >= 5, "line endpoints or continuity");
            for (var i = 1; i < line.Count; i++) Check(Math.Abs(line[i].Row - line[i - 1].Row) <= 1 && Math.Abs(line[i].Col - line[i - 1].Col) <= 1, "line gap");
        });
        Test("Multiple path centers accumulate one result", () => {
            var result = Apply(Grid(), SurfaceSculptMode.Raise, centers: new[] { (2, 1), (2, 2), (2, 3) });
            Check(result.Values[2, 2] > result.Values[1, 1], "path did not accumulate overlap");
        });
        Test("Invalid settings are rejected without changing input", () => {
            var source = Grid(); var before = (double[,])source.Clone(); var failed = false;
            try { SurfaceSculptor.ApplyPath(source, before, new[] { (2, 2) }, new SurfaceSculptOptions(SurfaceSculptMode.Raise, 0, 1, 2, SurfaceBrushFalloff.Hard, false), Axis, Axis, 0); }
            catch (ArgumentException) { failed = true; }
            Check(failed && source.Cast<double>().SequenceEqual(before.Cast<double>()), "invalid operation was not atomic");
        });
        Test("A solid table selection becomes a cropped surface", () => {
            var selected = Enumerable.Range(1, 3).SelectMany(row => Enumerable.Range(2, 3).Select(col => (row, col))).ToArray();
            Check(SurfaceTableRegion.TryCreate(5, 6, selected, out var region, out var error), error);
            Check(region == new SurfaceTableRegion(1, 3, 2, 4), "region bounds");
            var source = new double[5, 6]; for (var row = 0; row < 5; row++) for (var col = 0; col < 6; col++) source[row, col] = row * 10 + col;
            var cropped = region.Extract(source);
            Check(cropped.GetLength(0) == 3 && cropped.GetLength(1) == 3, "cropped dimensions");
            Near(cropped[0, 0], 12, "cropped first value"); Near(cropped[2, 2], 34, "cropped last value");
            Check(region.ExtractRows(new double[] { 0, 10, 20, 30, 40 }).SequenceEqual(new double[] { 10, 20, 30 }), "row axis");
            Check(region.ExtractColumns(new double[] { 0, 100, 200, 300, 400, 500 }).SequenceEqual(new double[] { 200, 300, 400 }), "column axis");
        });
        Test("Cropped edits map back only to affected source cells", () => {
            var region = new SurfaceTableRegion(1, 3, 2, 4); var source = new double[5, 6]; var cropped = region.Extract(source);
            cropped[0, 0] = 12; cropped[1, 1] = 23;
            var merged = region.Merge(source, cropped, new[] { (1, 1) });
            Near(merged[2, 3], 23, "mapped edit"); Near(merged[1, 2], 0, "unaffected local edit leaked");
            Check(region.ToSource((2, 0)) == (3, 2), "coordinate mapping");
        });
        Test("Empty selection opens the full surface", () => {
            Check(SurfaceTableRegion.TryCreate(5, 6, Array.Empty<(int, int)>(), out var region, out var error), error);
            Check(region == new SurfaceTableRegion(0, 4, 0, 5) && region.AllLocalCells().Count == 30, "full region");
        });
        Test("Invalid crop shapes are rejected", () => {
            Check(!SurfaceTableRegion.TryCreate(5, 6, new[] { (1, 1), (1, 2), (2, 1) }, out _, out _), "disconnected rectangle accepted");
            Check(!SurfaceTableRegion.TryCreate(5, 6, new[] { (1, 1), (1, 2) }, out _, out _), "one-row surface accepted");
        });
        return $"{count} sculpting tests passed.";
    }
}
'@
Add-Type -TypeDefinition ($source + $tests) -Language CSharp
[SculptingTests]::Run()
