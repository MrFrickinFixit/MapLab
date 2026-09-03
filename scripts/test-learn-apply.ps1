#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`nusing System.Globalization;`nnamespace MapLabLearnTests;`n"
foreach ($file in @('AdvancedSmoother.cs', 'MagnitudeNumberFormatter.cs', 'LearnApplyTable.cs')) {
    $source += (Get-Content -LiteralPath (Join-Path $root $file) -Raw).Replace('namespace TimingTableCalculator;', '').Replace('using System.Globalization;', '') + "`n"
}
$tests = @'
public static class Tests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < 1e-9, $"Expected {expected}, got {actual}");
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected validation failure"); }
    private static LearnApplyTable Table() { var table = new LearnApplyTable(); table.Synchronize(new[] { 1000d, 2000, 3000 }, new[] { 100d, 50 }, "kPa absolute", 3, 1); return table; }
    public static string Run()
    {
        var count = 0;
        void Test(string name, Action action) { action(); count++; Console.WriteLine($"PASS {name}"); }
        Test("Signed offsets multiply VE, not percentage points", () => {
            double[,] ve = { { 80, 80, 80 } }; double[,] offsets = { { 10, -10, 0 } };
            var result = LearnApplyMath.Apply(ve, offsets, false, new[] { 1000d, 2000, 3000 }, new[] { 100d });
            Near(result[0, 0], 88); Near(result[0, 1], 72); Near(result[0, 2], 80); Near(ve[0, 0], 80);
        });
        Test("Three-decimal VE result and zero offsets preserve full original precision", () => {
            double[,] ve = { { 83.123, 80.123456 } }; double[,] offsets = { { 1.234, 0 } };
            var result = LearnApplyMath.Apply(ve, offsets, false, new[] { 1000d, 2000 }, new[] { 100d });
            Near(result[0, 0], Math.Round(83.123 * 1.01234, 3, MidpointRounding.AwayFromZero)); Near(result[0, 1], 80.123456);
        });
        Test("Minus 100% is allowed but lower offsets are rejected atomically", () => {
            double[,] ve = { { 80, 90 } }; double[,] offsets = { { -100, 0 } };
            Near(LearnApplyMath.Apply(ve, offsets, false, new[] { 1d, 2 }, new[] { 1d })[0, 0], 0);
            offsets[0, 1] = -100.001; Throws(() => LearnApplyMath.Apply(ve, offsets, false, new[] { 1d, 2 }, new[] { 1d })); Near(ve[0, 0], 80);
        });
        Test("NaN, infinity, and overflowing transfer are rejected", () => {
            Throws(() => LearnApplyMath.ValidateOffset(double.NaN)); Throws(() => LearnApplyMath.ValidateOffset(double.PositiveInfinity));
            double[,] ve = { { double.MaxValue } }; double[,] offset = { { 100 } };
            Throws(() => LearnApplyMath.Apply(ve, offset, false, new[] { 1d }, new[] { 1d }));
        });
        Test("Axis and matrix dimension mismatches are rejected", () => {
            Throws(() => LearnApplyMath.Apply(new double[2, 2], new double[1, 2], false, new[] { 1d, 2 }, new[] { 1d, 2 }));
            Throws(() => LearnApplyMath.Apply(new double[2, 2], new double[2, 2], false, new[] { 1d }, new[] { 1d, 2 }));
        });
        Test("Transfer smoothing changes only nonzero-offset cells", () => {
            double[,] ve = { { 80, 80, 80, 80 }, { 70, 70, 70, 70 } }; double[,] offsets = { { 0, 20, 20, 0 }, { 0, 0, 0, 0 } };
            var result = LearnApplyMath.Apply(ve, offsets, true, new[] { 1000d, 2000, 3000, 4000 }, new[] { 100d, 50 });
            Check(result[0, 1] < 96 && result[0, 1] > 70, "Correction was not smoothed");
            for (var r = 0; r < 2; r++) for (var c = 0; c < 4; c++) if (offsets[r, c] == 0) Near(result[r, c], ve[r, c]);
        });
        Test("Zero learn table is a no-op even with smoothing", () => {
            double[,] ve = { { 10, 100, 20 } };
            var result = LearnApplyMath.Apply(ve, new double[1, 3], true, new[] { 1d, 2, 3 }, new[] { 1d });
            for (var c = 0; c < 3; c++) Near(result[0, c], ve[0, c]);
        });
        Test("TSV supports signed percent suffixes and blank cells", () => {
            var result = LearnApplyMath.ParseClipboard("+2.345%\t-1.5\t\r\n0\t3\t-4%\r\n");
            Near(result[0, 0]!.Value, 2.345); Near(result[0, 1]!.Value, -1.5); Check(result[0, 2] is null, "Blank cell not retained"); Near(result[1, 2]!.Value, -4);
        });
        Test("CSV and space-separated numeric blocks", () => {
            Near(LearnApplyMath.ParseClipboard("1,-2\n3,4")[0, 1]!.Value, -2);
            Near(LearnApplyMath.ParseClipboard("1  -2\n3  4")[1, 1]!.Value, 4);
        });
        Test("Bad clipboard input does not partially parse", () => {
            foreach (var text in new[] { "", "1\t2\n3", "RPM\t1000", "NaN", "Infinity", "-100.01" }) Throws(() => LearnApplyMath.ParseClipboard(text));
            Throws(() => LearnApplyMath.ParseClipboard(string.Join("\t", Enumerable.Repeat("1", 65))));
        });
        Test("Learn values start at zero and retain three decimals", () => {
            var table = Table(); Check(table.ActiveCount == 0, "Nonzero defaults");
            table.SetCells(new[] { (0, 0, 1.2345), (1, 1, -2.3455) }); Near(table.GetValue(0, 0), 1.235); Near(table.GetValue(1, 1), -2.346);
        });
        Test("Multi-cell edits validate before changing anything", () => {
            var table = Table(); Throws(() => table.SetCells(new[] { (0, 0, 10d), (1, 1, -101d) })); Check(table.ActiveCount == 0 && !table.CanUndo, "Partial edit committed");
            Throws(() => table.SetCells(new[] { (0, 0, 10d), (8, 8, 2d) })); Check(table.ActiveCount == 0, "Out-of-bounds edit partially committed");
        });
        Test("Learn edits, clear, undo and redo are independent snapshots", () => {
            var table = Table(); table.SetCells(new[] { (0, 0, 10d) }); table.Clear(); Check(table.ActiveCount == 0, "Clear");
            table.Undo(); Near(table.GetValue(0, 0), 10); table.Undo(); Near(table.GetValue(0, 0), 0);
            table.Redo(); Near(table.GetValue(0, 0), 10); table.Redo(); Check(table.ActiveCount == 0, "Redo clear");
        });
        Test("Zero removes a correction rather than applying a multiplier of zero", () => {
            var table = Table(); table.SetCells(new[] { (0, 0, 10d) }); table.SetCells(new[] { (0, 0, 0d) }); Check(table.Capture().Corrections.Length == 0, "Stored zero");
        });
        Test("Axes regrid by exact coordinates, not by cell position", () => {
            var table = Table(); table.SetCells(new[] { (0, 1, 10d), (1, 2, -5d) });
            table.Synchronize(new[] { 1000d, 1500, 2000 }, new[] { 100d, 75, 50 }, "kPa absolute", 3, 1);
            Near(table.GetValue(0, 2), 10); Near(table.GetValue(0, 1), 0); Check(table.UnmatchedCount == 1, "Unmatched offset lost");
            table.Synchronize(new[] { 1000d, 2000, 3000 }, new[] { 100d, 50 }, "kPa absolute", 3, 1); Near(table.GetValue(1, 2), -5); Check(table.UnmatchedCount == 0, "Retained offset did not return");
        });
        Test("MAP unit conversion tracks Fueling rounding", () => {
            var table = Table(); table.SetCells(new[] { (0, 0, 10d) });
            var psi = table.Map.Select(v => Math.Round((v - 101.325) / 6.894757293168361, 1)).ToArray();
            table.Synchronize(table.Rpm, psi, "PSI gauge", 3, 1); Near(table.GetValue(0, 0), 10); Check(table.UnmatchedCount == 0, "Unit conversion detached offset");
            var kpa = psi.Select(v => Math.Round(v * 6.894757293168361 + 101.325)).ToArray();
            table.Synchronize(table.Rpm, kpa, "kPa absolute", 3, 1); Near(table.GetValue(0, 0), 10);
        });
        Test("Fueling precision changes display, not stored offsets", () => {
            var table = Table(); table.SetCells(new[] { (0, 0, -1.234) }); Check(table.Format(table.GetValue(0, 0)) == "-1.2", "Initial format");
            table.Synchronize(table.Rpm, table.Map, table.MapUnit, 3, 3); Check(table.Format(table.GetValue(0, 0)) == "-1.234", "Precision sync");
            Near(table.GetValue(0, 0), -1.234); Check(table.Format(100) == "100", "Magnitude precision");
        });
        Test("Save/load round trip includes unmatched corrections", () => {
            var table = Table(); table.SetCells(new[] { (0, 2, 4d) }); table.Synchronize(new[] { 1000d, 2000 }, table.Map, table.MapUnit, 3, 1);
            var state = System.Text.Json.JsonSerializer.Deserialize<LearnApplyState>(System.Text.Json.JsonSerializer.Serialize(table.Capture()));
            var restored = Table(); restored.Restore(state); Near(restored.GetValue(0, 2), 4); Check(restored.UnmatchedCount == 0, "Restore");
        });
        Test("Legacy files with no Learn Apply state load as empty", () => {
            var table = Table(); table.SetCells(new[] { (0, 0, 10d) }); table.Restore(null); Check(table.ActiveCount == 0 && !table.CanUndo, "Legacy restore");
        });
        Test("Invalid or duplicate stored corrections are rejected", () => {
            Check(!LearnApplyTable.IsValid(new LearnApplyState { Corrections = new[] { new LearnCorrection(1000, 100, -101) } }), "Invalid offset accepted");
            Check(!LearnApplyTable.IsValid(new LearnApplyState { Corrections = new[] { new LearnCorrection(1000, 100, 1), new LearnCorrection(1000, 100, 2) } }), "Duplicate accepted");
            var table = Table(); table.Restore(new LearnApplyState { Corrections = new[] { new LearnCorrection(1000, 100, .00001) } }); Check(table.Capture().Corrections.Length == 0, "Rounded zero retained");
        });
        Test("Clearing removes active and unmatched offsets", () => {
            var table = Table(); table.SetCells(new[] { (0, 2, 4d) }); table.Synchronize(new[] { 1000d }, table.Map, table.MapUnit, 3, 1);
            table.Clear(); Check(table.UnmatchedCount == 0 && table.ActiveCount == 0, "Clear retained offsets"); table.Undo(); Check(table.UnmatchedCount == 1, "Undo retained offsets");
        });
        Test("MAP rounding collisions are retained, not merged or applied", () => {
            var table = new LearnApplyTable(); table.Synchronize(new[] { 1000d }, new[] { 100d, 100.1 }, "kPa absolute", 3, 1);
            table.SetCells(new[] { (0, 0, 10d), (1, 0, 20d) });
            table.Synchronize(new[] { 1000d }, new[] { -.2d }, "PSI gauge", 3, 1);
            Check(table.ActiveCount == 0 && table.UnmatchedCount == 2, "Ambiguous corrections applied or discarded");
            var state = table.Capture(); var restored = new LearnApplyTable(); restored.Restore(state);
            restored.Synchronize(new[] { 1000d }, new[] { 100d, 100.1 }, "kPa absolute", 3, 1);
            Near(restored.GetValue(0, 0), 10); Near(restored.GetValue(1, 0), 20);
        });
        Test("Duplicate or non-finite fuel breakpoints block transfer", () => {
            Throws(() => LearnApplyMath.Apply(new double[1, 2], new double[1, 2], false, new[] { 1d, 1 }, new[] { 1d }));
            Throws(() => LearnApplyMath.Apply(new double[1, 1], new double[1, 1], false, new[] { double.NaN }, new[] { 1d }));
        });
        Test("Repeated transfer compounds percentages", () => {
            double[,] ve = { { 80 } }; double[,] offset = { { 10 } };
            var once = LearnApplyMath.Apply(ve, offset, false, new[] { 1d }, new[] { 1d });
            Near(LearnApplyMath.Apply(once, offset, false, new[] { 1d }, new[] { 1d })[0, 0], 96.8);
        });
        return $"{count} Learn Apply tests passed.";
    }
}
'@
Add-Type -TypeDefinition ($source + $tests)
[MapLabLearnTests.Tests]::Run()
