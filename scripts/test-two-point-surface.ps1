#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`n" +
    (Get-Content -LiteralPath (Join-Path $root 'SurfaceSculptor.cs') -Raw) +
    ((Get-Content -LiteralPath (Join-Path $root 'TwoPointSurfaceEditor.cs') -Raw) -replace 'namespace TimingTableCalculator;\s*', '')
$tests = @'

public static class TwoPointSurfaceTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, string message) { if (Math.Abs(actual - expected) > 1e-9) throw new Exception($"{message}: {actual} != {expected}"); }

    public static string Run()
    {
        var count = 0;
        void Test(string name, Action test) { test(); count++; Console.WriteLine($"PASS {name}"); }
        Test("Flatten creates a linear ramp and locks endpoints", () => {
            double[,] source = { { 0, 9, 9, 9, 8 } };
            var result = TwoPointSurfaceEditor.Apply(source, (0, 0), (0, 4), TwoPointSurfaceMode.Flatten);
            for (var col = 0; col < 5; col++) Near(result.Values[0, col], col * 2, "Linear value");
            Near(source[0, 1], 9, "Input changed");
        });
        Test("Smooth reduces a spike and locks endpoints", () => {
            double[,] source = { { 10, 10, 100, 10, 10 } };
            var result = TwoPointSurfaceEditor.Apply(source, (0, 0), (0, 4), TwoPointSurfaceMode.Smooth);
            Near(result.Values[0, 0], 10, "First endpoint"); Near(result.Values[0, 4], 10, "Second endpoint");
            Check(result.Values[0, 2] < 100 && result.Values[0, 2] > 10, "Spike was not softened");
        });
        Test("Diagonal path leaves cells outside the path unchanged", () => {
            double[,] source = { { 0, 50, 50 }, { 50, 100, 50 }, { 50, 50, 20 } };
            var result = TwoPointSurfaceEditor.Apply(source, (0, 0), (2, 2), TwoPointSurfaceMode.Flatten);
            Near(result.Values[1, 1], 10, "Diagonal midpoint"); Near(result.Values[0, 1], 50, "Off-path cell");
            Check(result.Path.SequenceEqual(new[] { (0, 0), (1, 1), (2, 2) }), "Unexpected diagonal path");
        });
        Test("Adjacent endpoints require no interior edit", () => {
            double[,] source = { { 1, 2 } };
            var result = TwoPointSurfaceEditor.Apply(source, (0, 0), (0, 1), TwoPointSurfaceMode.Flatten);
            Check(result.ChangedCells.Count == 0, "Adjacent cells reported a change");
        });
        return $"{count} two-point surface tests passed.";
    }
}
'@
Add-Type -TypeDefinition ($source + $tests)
[TimingTableCalculator.TwoPointSurfaceTests]::Run()
