#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`n" + (Get-Content -LiteralPath (Join-Path $root 'AdvancedSmoother.cs') -Raw)
$tests = @'

public static class SmoothingTests
{
    private static readonly AdvancedSmoothingOptions Options = new(AdvancedSmoothingAlgorithm.Surroundings, 1, 1, true, true, .5)
        { Direction = SurroundingsDirection.AcrossColumns, NeighborReach = 1 };
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-9, $"{message}: {actual} != {expected}");
    private static (int Row, int Col)[] All(double[,] values) => Enumerable.Range(0, values.GetLength(0)).SelectMany(r => Enumerable.Range(0, values.GetLength(1)).Select(c => (r, c))).ToArray();
    private static void Equal(double[,] actual, double[,] expected, string message) { foreach (var (r, c) in All(expected)) Near(actual[r, c], expected[r, c], message); }
    private static double[,] Apply(double[,] source, (int Row, int Col)[] cells, AdvancedSmoothingOptions? options = null, double[]? x = null, double[]? y = null)
        => AdvancedSmoother.Apply(source, cells, options ?? Options, x, y);
    private static void OutsideUnchanged(double[,] source, double[,] result, (int Row, int Col)[] cells)
    { foreach (var (r, c) in All(source)) if (!cells.Contains((r, c))) Check(source[r, c] == result[r, c], $"Unselected cell [{r},{c}] changed"); }

    public static string Run()
    {
        var count = 0;
        void Test(string name, Action test) { test(); count++; System.Console.WriteLine($"PASS {name}"); }
        Test("Two-column wrinkle moves toward fixed neighbors", () => {
            double[,] source = { { 50, 100, 100, 50 }, { 60, 110, 110, 60 }, { 70, 120, 120, 70 } };
            var before = (double[,])source.Clone();
            var cells = All(source).Where(p => p.Col == 1 || p.Col == 2).ToArray();
            var result = Apply(source, cells);
            var w = Math.Exp(-.5);
            Near(result[0, 1], (100 + 150 * w) / (1 + 2 * w), "Gaussian weights");
            Check(result[0, 1] < 100 && result[0, 1] > 50, "Wrinkle not reduced");
            Near(result[0, 1], result[0, 2], "Symmetric strip");
            OutsideUnchanged(source, result, cells); Equal(source, before, "Input mutated");
        });
        Test("Two-row wrinkle and descending Y axis", () => {
            double[,] source = { { 50, 60 }, { 100, 110 }, { 100, 110 }, { 50, 60 } };
            var cells = All(source).Where(p => p.Row == 1 || p.Row == 2).ToArray();
            var result = Apply(source, cells, Options with { Direction = SurroundingsDirection.AcrossRows }, new[] { 500d, 1000 }, new[] { 100d, 80, 60, 40 });
            Check(result[1, 0] < 100 && result[1, 0] > 50, "Horizontal wrinkle not reduced");
            Near(result[1, 0], result[2, 0], "Symmetric rows"); OutsideUnchanged(source, result, cells);
        });
        Test("Dipped strip rises with overshoot prevention enabled", () => {
            double[,] source = { { 50, 10, 10, 50 } };
            var result = Apply(source, new[] { (0, 1), (0, 2) });
            Check(result[0, 1] > 10 && result[0, 1] < 50, "Selection-only clamp blocked correction");
        });
        Test("Single selected cell works even with preserve perimeter saved", () => {
            double[,] source = { { 50, 100, 50 } };
            Check(Apply(source, new[] { (0, 1) })[0, 1] < 100, "Single cell frozen");
        });
        Test("Direction isolates the perpendicular axis", () => {
            double[,] source = { { 500, 500, 500 }, { 10, 100, 10 }, { 500, 500, 500 } };
            var cells = new[] { (1, 1) };
            Check(Apply(source, cells)[1, 1] < 100, "Across columns sampled another row");
            Check(Apply(source, cells, Options with { Direction = SurroundingsDirection.AcrossRows })[1, 1] > 100, "Across rows sampled another column");
        });
        Test("Both directions includes diagonals", () => {
            double[,] source = { { 100, 0, 100 }, { 0, 0, 0 }, { 100, 0, 100 } };
            var cells = new[] { (1, 1) };
            Near(Apply(source, cells)[1, 1], 0, "Horizontal result");
            Check(Apply(source, cells, Options with { Direction = SurroundingsDirection.Both })[1, 1] > 0, "Missing diagonal samples");
        });
        Test("Reach expands sampling without expanding edits", () => {
            double[,] source = { { 0, 100, 100, 100, 0 } };
            var cells = new[] { (0, 2) };
            Near(Apply(source, cells)[0, 2], 100, "Reach one leaked");
            var result = Apply(source, cells, Options with { NeighborReach = 2 });
            Check(result[0, 2] < 100, "Reach two did not sample outer cells"); OutsideUnchanged(source, result, cells);
        });
        Test("Partial strength blends from original value", () => {
            double[,] source = { { 50, 100, 50 } };
            var cells = new[] { (0, 1) };
            Near(Apply(source, cells, Options with { Strength = .25 })[0, 1], 100 + (Apply(source, cells)[0, 1] - 100) * .25, "Strength");
        });
        Test("Multiple passes reduce wrinkle and keep anchors fixed", () => {
            double[,] source = { { 50, 100, 100, 50 } };
            var cells = new[] { (0, 1), (0, 2) };
            var result = Apply(source, cells, Options with { Passes = 20 });
            Check(result[0, 1] < Apply(source, cells)[0, 1] && result[0, 1] >= 50, "Passes"); OutsideUnchanged(source, result, cells);
        });
        Test("Order-independent passes", () => {
            double[,] source = { { 20, 100, 70, 30 } };
            Equal(Apply(source, new[] { (0, 1), (0, 2) }, Options with { Passes = 5 }), Apply(source, new[] { (0, 2), (0, 1) }, Options with { Passes = 5 }), "Selection order");
        });
        Test("Disconnected selection preserves holes and unrelated cells", () => {
            double[,] source = { { 40, 100, 40, 40, 80, 40 }, { 60, 60, 60, 60, 60, 60 } };
            var cells = new[] { (0, 1), (0, 4) };
            var result = Apply(source, cells, Options with { NeighborReach = 3, Passes = 4, Direction = SurroundingsDirection.Both });
            OutsideUnchanged(source, result, cells); Check(result[0, 1] != 100 && result[0, 4] != 80, "Disconnected cells skipped");
        });
        Test("Map-edge weights normalize without padding", () => {
            double[,] source = { { 100, 50, 50 } };
            Near(Apply(source, new[] { (0, 0) })[0, 0], (100 + 50 * Math.Exp(-.5)) / (1 + Math.Exp(-.5)), "Edge weight");
        });
        Test("Constant surfaces and 1x1 tables remain constant", () => {
            double[,] source = { { 30, 30 }, { 30, 30 } };
            Equal(Apply(source, All(source), Options with { NeighborReach = 10, Passes = 20, Direction = SurroundingsDirection.Both }), source, "Constant surface");
            double[,] single = { { 30 } }; Equal(Apply(single, All(single)), single, "1x1");
        });
        Test("Irregular axes change distance weights", () => {
            double[,] source = { { 0, 100, 200, 0 } };
            var x = new[] { 0d, 1, 5, 6 };
            var result = Apply(source, new[] { (0, 1) }, x: x);
            var near = Math.Exp(-.5); var far = Math.Exp(-8);
            Near(result[0, 1], (100 + 200 * far) / (1 + near + far), "Physical distance weights");
        });
        Test("Axis unit conversion and reversal preserve results", () => {
            double[,] source = { { 0, 100, 200, 0 } }; var cells = new[] { (0, 1) };
            var x = new[] { 0d, 1, 5, 6 };
            var result = Apply(source, cells, x: x);
            Equal(result, Apply(source, cells, x: x.Select(v => v * 6.894757 + 101.325).ToArray()), "Unit conversion");
            Equal(result, Apply(source, cells, x: x.Select(v => -v).ToArray()), "Axis reversal");
        });
        Test("All results stay within sampled value range", () => {
            double[,] source = { { -20, 110, 30 }, { 90, -10, 60 }, { 30, 40, 50 } };
            var result = Apply(source, All(source), Options with { Direction = SurroundingsDirection.Both, Passes = 20, NeighborReach = 10 });
            foreach (var value in result) Check(value >= -20 && value <= 110 && double.IsFinite(value), "Overshoot");
        });
        Test("Bounds overload matches exact selection overload", () => {
            double[,] source = { { 50, 100, 100, 50 } };
            Equal(AdvancedSmoother.Apply(source, 0, 0, 1, 2, Options), Apply(source, new[] { (0, 1), (0, 2) }), "Bounds overload");
        });
        Test("Empty and stale selections are harmless", () => {
            double[,] source = { { 50, 100, 50 } };
            Equal(Apply(source, Array.Empty<(int, int)>()), source, "Empty selection");
            Equal(Apply(source, new[] { (-1, 0), (10, 10), (0, 1) }), Apply(source, new[] { (0, 1) }), "Stale selection");
        });
        Test("Existing standard smoothing still samples selected cells only", () => {
            double[,] source = { { 50, 100, 100, 50 } };
            Equal(Apply(source, new[] { (0, 1), (0, 2) }, Options with { Algorithm = AdvancedSmoothingAlgorithm.StandardWeighted, PreservePerimeter = false }), source, "Standard changed");
        });
        Test("Existing perimeter and weighted kernel remain unchanged", () => {
            double[,] source = { { 10, 20, 30 }, { 40, 100, 60 }, { 70, 80, 90 } };
            var result = Apply(source, All(source), Options with { Algorithm = AdvancedSmoothingAlgorithm.StandardWeighted });
            Near(result[1, 1], 62.5, "Existing 3x3 kernel"); OutsideUnchanged(source, result, new[] { (1, 1) });
        });
        Test("Old settings use compatible defaults and enum IDs", () => {
            var old = System.Text.Json.JsonSerializer.Deserialize<AdvancedSmoothingOptions>("{\"Algorithm\":5,\"Strength\":0.65,\"Passes\":2,\"PreservePerimeter\":false,\"PreventOvershoot\":true,\"CenterInfluence\":0.5}")!;
            Check(old.Algorithm == AdvancedSmoothingAlgorithm.StandardWeighted && old.NeighborReach == 2 && old.Direction == SurroundingsDirection.Both, "Legacy settings");
            var settings = Options with { NeighborReach = 4, Direction = SurroundingsDirection.AcrossRows };
            Check(System.Text.Json.JsonSerializer.Deserialize<AdvancedSmoothingOptions>(System.Text.Json.JsonSerializer.Serialize(settings)) == settings, "Settings round trip");
        });
        return $"{count} smoothing tests passed.";
    }
}
'@
Add-Type -TypeDefinition ($source + $tests)
[TimingTableCalculator.SmoothingTests]::Run()
