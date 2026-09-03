using System.Globalization;

namespace TimingTableCalculator;

public sealed record LearnCorrection(double Rpm, double Map, double Offset, string? MapUnit = null);

public sealed class LearnApplyState
{
    public string MapUnit { get; set; } = "kPa absolute";
    public LearnCorrection[] Corrections { get; set; } = [];
}

public sealed class LearnApplyTable
{
    private Dictionary<(double Rpm, double Map, string Unit), double> corrections = [];
    private readonly Stack<Dictionary<(double Rpm, double Map, string Unit), double>> undo = [], redo = [];
    private bool needsBinding;
    public double[] Rpm { get; private set; } = [];
    public double[] Map { get; private set; } = [];
    public string MapUnit { get; private set; } = "kPa absolute";
    public int LeadingDigits { get; private set; } = 3;
    public int TrailingDecimals { get; private set; } = 1;
    public int GeometryVersion { get; private set; }
    public int ActiveCount => ActiveCells().Count;
    public int UnmatchedCount => Math.Max(0, corrections.Count - ActiveCount);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public event Action? Changed;

    public double GetValue(int row, int col) => corrections.GetValueOrDefault((Rpm[col], Map[row], MapUnit));
    public string Format(double value) => MagnitudeNumberFormatter.Format(value, LeadingDigits, TrailingDecimals);

    public void Synchronize(double[] rpm, double[] map, string unit, int leadingDigits, int trailingDecimals)
    {
        var geometryChanged = !Rpm.SequenceEqual(rpm) || !Map.SequenceEqual(map) || MapUnit != unit;
        if (!geometryChanged && !needsBinding && LeadingDigits == leadingDigits && TrailingDecimals == trailingDecimals) return;
        if (geometryChanged || needsBinding)
        {
            var candidates = corrections.GroupBy(pair => (pair.Key.Rpm, MapInUnit(pair.Key.Map, pair.Key.Unit, unit))).ToDictionary(group => group.Key, group => group.ToArray());
            var next = new Dictionary<(double Rpm, double Map, string Unit), double>(corrections);
            foreach (var rpmValue in rpm) foreach (var mapValue in map)
            {
                var key = (rpmValue, mapValue, unit);
                if (next.ContainsKey(key) || !candidates.TryGetValue((rpmValue, mapValue), out var matches) || matches.Length != 1) continue;
                // Never merge corrections that collide after MAP-unit rounding.
                // Ambiguous or off-axis entries remain stored in their original units.
                next.Remove(matches[0].Key); next[key] = matches[0].Value;
            }
            corrections = next; needsBinding = false;
        }
        Rpm = rpm.ToArray(); Map = map.ToArray(); MapUnit = unit;
        LeadingDigits = leadingDigits; TrailingDecimals = trailingDecimals;
        if (geometryChanged) { GeometryVersion++; undo.Clear(); redo.Clear(); }
        Changed?.Invoke();
    }

    public List<(int Row, int Col)> ActiveCells()
    {
        var result = new List<(int Row, int Col)>();
        for (var row = 0; row < Map.Length; row++) for (var col = 0; col < Rpm.Length; col++)
            if (GetValue(row, col) != 0) result.Add((row, col));
        return result;
    }

    public double[,] SnapshotValues()
    {
        var result = new double[Map.Length, Rpm.Length];
        for (var row = 0; row < Map.Length; row++) for (var col = 0; col < Rpm.Length; col++) result[row, col] = GetValue(row, col);
        return result;
    }

    public void SetCells(IEnumerable<(int Row, int Col, double Value)> edits)
    {
        var next = new Dictionary<(double Rpm, double Map, string Unit), double>(corrections);
        foreach (var (row, col, raw) in edits)
        {
            if (row < 0 || row >= Map.Length || col < 0 || col >= Rpm.Length) throw new ArgumentException("The destination is outside the Learn Apply Table.");
            LearnApplyMath.ValidateOffset(raw);
            var value = Math.Round(raw, 3, MidpointRounding.AwayFromZero);
            var key = (Rpm[col], Map[row], MapUnit);
            if (value == 0) next.Remove(key); else next[key] = value;
        }
        if (next.Count == corrections.Count && next.All(pair => corrections.TryGetValue(pair.Key, out var old) && old == pair.Value)) return;
        undo.Push(corrections); redo.Clear(); corrections = next; Changed?.Invoke();
    }

    public void Clear()
    {
        if (corrections.Count == 0) return;
        undo.Push(corrections); redo.Clear(); corrections = []; Changed?.Invoke();
    }
    public void Undo() { if (undo.Count == 0) return; redo.Push(corrections); corrections = undo.Pop(); Changed?.Invoke(); }
    public void Redo() { if (redo.Count == 0) return; undo.Push(corrections); corrections = redo.Pop(); Changed?.Invoke(); }

    public LearnApplyState Capture() => new() { MapUnit = MapUnit, Corrections = corrections.Select(pair => new LearnCorrection(pair.Key.Rpm, pair.Key.Map, pair.Value, pair.Key.Unit)).ToArray() };
    public static bool IsValid(LearnApplyState? state) => state is null ||
        (state.MapUnit is "kPa absolute" or "PSI gauge" && state.Corrections is not null && state.Corrections.Length <= 65536 &&
         state.Corrections.All(value => value is not null && double.IsFinite(value.Rpm) && double.IsFinite(value.Map) && double.IsFinite(value.Offset) && value.Offset >= -100 && value.MapUnit is null or "kPa absolute" or "PSI gauge") &&
         state.Corrections.Select(value => (value.Rpm, value.Map, value.MapUnit ?? state.MapUnit)).Distinct().Count() == state.Corrections.Length);

    public void Restore(LearnApplyState? state)
    {
        if (!IsValid(state)) throw new ArgumentException("The saved Learn Apply Table contains invalid offsets.");
        corrections = state?.Corrections.Select(value => value with { Offset = Math.Round(value.Offset, 3, MidpointRounding.AwayFromZero) }).Where(value => value.Offset != 0).ToDictionary(value => (value.Rpm, value.Map, value.MapUnit ?? state.MapUnit), value => value.Offset) ?? [];
        MapUnit = state?.MapUnit ?? MapUnit;
        needsBinding = true; undo.Clear(); redo.Clear(); GeometryVersion++; Changed?.Invoke();
    }

    private static double MapInUnit(double value, string from, string to) => from == to ? value : to == "PSI gauge"
        ? Math.Round((value - 101.325) / 6.894757293168361, 1)
        : Math.Round(value * 6.894757293168361 + 101.325);
}

public static class LearnApplyMath
{
    public static void ValidateOffset(double value)
    {
        if (!double.IsFinite(value) || value < -100) throw new ArgumentException("Offsets must be finite percentages of -100% or greater. Values below -100% would produce negative VE.");
    }

    public static double?[,] ParseClipboard(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n').Split('\n');
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("The clipboard is empty.");
        var rows = lines.Select(line => line.Contains('\t') ? line.Split('\t') : line.Contains(',') ? line.Split(',') : line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var columns = rows[0].Length;
        if (columns == 0 || rows.Any(row => row.Length != columns)) throw new ArgumentException("Paste a rectangular block of offset values without RPM/MAP headings.");
        if (columns > 64 || rows.Length > 64) throw new ArgumentException("The pasted block exceeds the maximum 64 x 64 table size.");
        var result = new double?[rows.Length, columns];
        for (var row = 0; row < rows.Length; row++) for (var col = 0; col < columns; col++)
        {
            var token = rows[row][col].Trim();
            if (token.Length == 0) continue;
            if (token.EndsWith('%')) token = token[..^1].TrimEnd();
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException($"Invalid offset at pasted row {row + 1}, column {col + 1}.");
            ValidateOffset(value); result[row, col] = value;
        }
        return result;
    }

    public static double[,] Apply(double[,] ve, double[,] offsets, bool smooth, double[] rpm, double[] map)
    {
        if (ve.GetLength(0) != offsets.GetLength(0) || ve.GetLength(1) != offsets.GetLength(1) || map.Length != ve.GetLength(0) || rpm.Length != ve.GetLength(1))
            throw new ArgumentException("The Learn Apply and Fueling axes must match before transfer.");
        if (rpm.Any(value => !double.IsFinite(value)) || map.Any(value => !double.IsFinite(value)) || rpm.Distinct().Count() != rpm.Length || map.Distinct().Count() != map.Length)
            throw new ArgumentException("Fueling axes must contain finite, unique breakpoints before transferring learn offsets.");
        var result = (double[,])ve.Clone();
        var changed = new List<(int Row, int Col)>();
        for (var row = 0; row < map.Length; row++) for (var col = 0; col < rpm.Length; col++)
        {
            var offset = offsets[row, col]; ValidateOffset(offset);
            if (offset == 0) continue;
            var value = ve[row, col] * (1 + offset / 100d);
            if (!double.IsFinite(value) || value < 0) throw new ArgumentException($"The offset at row {row + 1}, column {col + 1} would produce invalid VE. No offsets were transferred.");
            result[row, col] = Math.Round(value, 3, MidpointRounding.AwayFromZero); changed.Add((row, col));
        }
        if (smooth && changed.Count > 0)
        {
            result = AdvancedSmoother.Apply(result, changed, new AdvancedSmoothingOptions(AdvancedSmoothingAlgorithm.Surroundings, .65, 2, false, true, .5)
                { NeighborReach = 2, Direction = SurroundingsDirection.Both }, rpm, map);
            foreach (var (row, col) in changed)
            {
                if (!double.IsFinite(result[row, col]) || result[row, col] < 0) throw new ArgumentException("Smoothing produced invalid VE. No offsets were transferred.");
                result[row, col] = Math.Round(result[row, col], 3, MidpointRounding.AwayFromZero);
            }
        }
        return result;
    }
}
