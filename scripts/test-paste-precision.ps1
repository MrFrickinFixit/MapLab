#requires -Version 7.4
#requires -PSEdition Core

$ErrorActionPreference = 'Stop'

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw 'This WPF clipboard test must be run with pwsh -Sta.'
}

$root = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $root 'bin\Release\net8.0-windows\MapLab-1.0.3.6-beta.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build MapLab.slnx in Release configuration before running this test. Missing: $assemblyPath"
}

Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
[void][Reflection.Assembly]::LoadFrom($assemblyPath)

$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$testAutosave = [IO.Path]::GetTempFileName()
$hadClipboardText = [System.Windows.Clipboard]::ContainsText()
$originalClipboardText = if ($hadClipboardText) { [System.Windows.Clipboard]::GetText() } else { $null }

try {
    $resize = [Action[int, int]] { param($columns, $rows) }
    $fillAxis = [Action[bool, int[]]] { param($isMap, $indices) }
    $pasteAxis = [Action[bool, Nullable[int], int[]]] { param($isMap, $index, $indices) }
    $boundaries = [Action[int, int]] { param($row, $column) }
    $editAxis = [Func[bool, int, double, double[]]] { param($isMap, $index, $value) return $null }
    $panel = [TimingTableCalculator.FuelingPanel]::new($resize, $fillAxis, $pasteAxis, $boundaries, $editAxis, $testAutosave)

    $panel.UpdateAxes(
        [double[]](500, 600, 700, 800, 900, 1000, 1100, 1200),
        [double[]](100, 90, 80, 70, 60, 50, 40, 30),
        'kPa absolute',
        800,
        80)

    $panel.GetType().GetField('start', $flags).SetValue($panel, [ValueTuple[int, int]]::new(0, 0))
    $panel.GetType().GetField('end', $flags).SetValue($panel, [ValueTuple[int, int]]::new(0, 0))
    [System.Windows.Clipboard]::SetText("87.125`t102.375`n64.875`t99.625")
    [void]$panel.GetType().GetMethod('PasteSelection', $flags).Invoke($panel, @())

    $values = $panel.GetType().GetField('ve', $flags).GetValue($panel)
    $expected = @(87.125, 102.375, 64.875, 99.625)
    $actual = @($values.GetValue(0, 0), $values.GetValue(0, 1), $values.GetValue(1, 0), $values.GetValue(1, 1))
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ([Math]::Abs($actual[$index] - $expected[$index]) -gt 1e-12) {
            throw "Paste precision mismatch at index ${index}: $($actual[$index]) != $($expected[$index])"
        }
    }

    $savedValues = (Get-Content -LiteralPath $testAutosave -Raw | ConvertFrom-Json).Values
    if ([Math]::Abs([double]$savedValues[0][0] - 87.125) -gt 1e-12) {
        throw 'Autosave changed the pasted value.'
    }

    $displayTrailingBox = $panel.GetType().GetField('trailingPrecisionBox', $flags).GetValue($panel)
    $displayZeroesBox = $panel.GetType().GetField('displayTrailingZeroesBox', $flags).GetValue($panel)
    $displayTrailingBox.SelectedIndex = 1
    $displayZeroesBox.SelectedIndex = 1
    $values = $panel.GetType().GetField('ve', $flags).GetValue($panel)
    if ([Math]::Abs($values.GetValue(0, 0) - 87.125) -gt 1e-12 -or [Math]::Abs($values.GetValue(0, 1) - 102.375) -gt 1e-12) {
        throw 'Lowering Display precision rewrote pasted VE values.'
    }
    $savedValues = (Get-Content -LiteralPath $testAutosave -Raw | ConvertFrom-Json).Values
    if ([Math]::Abs([double]$savedValues[0][0] - 87.125) -gt 1e-12 -or [Math]::Abs([double]$savedValues[0][1] - 102.375) -gt 1e-12) {
        throw 'Lowering Display precision rewrote pasted VE values in autosave.'
    }
    $formatFuelDisplay = $panel.GetType().GetMethod('FormatVeDisplayValue', $flags)
    if ($formatFuelDisplay.Invoke($panel, @(87.125)) -ne '87.1') { throw 'Lowered Fuel Display precision was not applied.' }
    $displayTrailingBox.SelectedIndex = 3
    $displayZeroesBox.SelectedIndex = 3
    $values = $panel.GetType().GetField('ve', $flags).GetValue($panel)
    $formatFuelActual = $panel.GetType().GetMethod('FormatStoredVeValue', $flags)
    if ([Math]::Abs($values.GetValue(0, 0) - 87.125) -gt 1e-12 -or $formatFuelDisplay.Invoke($panel, @(87.125)) -ne '87.125' -or $formatFuelActual.Invoke($panel, @(87.125)) -ne '87.125') {
        throw 'Restoring Display precision did not reveal the original pasted VE value.'
    }

    $panel.GetType().GetField('start', $flags).SetValue($panel, [ValueTuple[int, int]]::new(2, 0))
    $panel.GetType().GetField('end', $flags).SetValue($panel, [ValueTuple[int, int]]::new(2, 1))
    $cells = $panel.GetType().GetField('cells', $flags).GetValue($panel)
    $editedCell = $cells.GetValue(2, 0)
    $editOriginals = $panel.GetType().GetField('editOriginals', $flags).GetValue($panel)
    $editOriginals.Add($editedCell, $editedCell.Text)
    $editedCell.Text = '73.987'
    [void]$panel.GetType().GetMethod('CompleteFuelCellEdit', $flags).Invoke($panel, @($editedCell, $true))

    $values = $panel.GetType().GetField('ve', $flags).GetValue($panel)
    if ([Math]::Abs($values.GetValue(2, 0) - 73.987) -gt 1e-12 -or [Math]::Abs($values.GetValue(2, 1) - 73.987) -gt 1e-12) {
        throw 'A direct selected-group edit changed the entered precision.'
    }
    $savedValues = (Get-Content -LiteralPath $testAutosave -Raw | ConvertFrom-Json).Values
    if ([Math]::Abs([double]$savedValues[2][0] - 73.987) -gt 1e-12 -or [Math]::Abs([double]$savedValues[2][1] - 73.987) -gt 1e-12) {
        throw 'Autosave changed the directly edited value.'
    }

    $values.SetValue(111.111, 2, 2); $values.SetValue(123.456, 2, 3); $values.SetValue(139.999, 2, 4)
    $values.SetValue(147.777, 3, 2); $values.SetValue(198.111, 3, 3); $values.SetValue(166.666, 3, 4)
    $values.SetValue(173.333, 4, 2); $values.SetValue(187.777, 4, 3); $values.SetValue(191.234, 4, 4)
    [void]$panel.GetType().GetMethod('Smooth', $flags).Invoke($panel, @(2, 4, 2, 4, 2, 0.65))

    $values = $panel.GetType().GetField('ve', $flags).GetValue($panel)
    if ([Math]::Abs($values.GetValue(0, 1) - 102.375) -gt 1e-12) {
        throw 'Smoothing rounded an unselected exact value.'
    }
    $smoothedCenter = [double]$values.GetValue(3, 3)
    if ([Math]::Abs($smoothedCenter - [Math]::Round($smoothedCenter, 0, [MidpointRounding]::AwayFromZero)) -lt 1e-12) {
        throw "A smoothed VE value above 100 was rounded to a whole number: $smoothedCenter"
    }

    $fuelSmoothed = [double]$panel.GetType().GetMethod('RoundSmoothedVe', $flags).Invoke($panel, @(123.45678))
    if ([Math]::Abs($fuelSmoothed - 123.45678) -gt 1e-12) { throw "Fuel smoothing rounded the stored result: $fuelSmoothed" }

    $timing = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([TimingTableCalculator.MainWindow])
    $timingSmoothed = [double]$timing.GetType().GetMethod('RoundSmoothedTiming', $flags).Invoke($timing, @(123.45678))
    if ([Math]::Abs($timingSmoothed - 123.45678) -gt 1e-12) {
        throw "Timing smoothing rounded the stored result: $timingSmoothed"
    }

    $sandbox = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([TimingTableCalculator.SandboxPanel])
    $sandboxSmoothed = [double]$sandbox.GetType().GetMethod('RoundSmoothedValue', $flags).Invoke($sandbox, @(123.45678))
    if ([Math]::Abs($sandboxSmoothed - 123.45678) -gt 1e-12) {
        throw "Sandbox smoothing rounded the stored result: $sandboxSmoothed"
    }

    if ($formatFuelActual.Invoke($panel, @(12.3)) -ne '12.3') { throw 'Fuel stored-value formatting changed the underlying value text.' }

    $timing.GetType().GetField('timingLeadingDisplayDigits', $flags).SetValue($timing, 4)
    $timing.GetType().GetField('timingTrailingDisplayDecimals', $flags).SetValue($timing, 4)
    $timing.GetType().GetField('timingDisplayTrailingZeroPlaces', $flags).SetValue($timing, 4)
    $formatTimingDisplay = $timing.GetType().GetMethod('FormatTimingDisplayValue', $flags)
    if ($formatTimingDisplay.Invoke($timing, @(12.3)) -ne '12.3000') { throw 'Timing Display Zeroes did not pad four places.' }
    $timing.GetType().GetField('timingLeadingDisplayDigits', $flags).SetValue($timing, 3)
    if ($formatTimingDisplay.Invoke($timing, @(105.525)) -ne '106') { throw 'Timing display did not round a three-leading-digit value to a whole number.' }
    $formatTimingActual = $timing.GetType().GetMethod('FormatStoredTimingValue', $flags)
    if ($formatTimingActual.Invoke($timing, @(105.525)) -ne '105.525') { throw 'Timing stored-value formatting was changed by the display leading-digit cutoff.' }
    $timing.GetType().GetField('timingLeadingDisplayDigits', $flags).SetValue($timing, 4)
    $timing.GetType().GetField('timingDisplayTrailingZeroPlaces', $flags).SetValue($timing, 0)
    if ($formatTimingDisplay.Invoke($timing, @(12.3)) -ne '12.3') { throw 'Timing Display Zeroes did not suppress padding.' }

    $formatSandboxActual = $sandbox.GetType().GetMethod('FormatStoredValue', $flags)
    if ($formatSandboxActual.Invoke($sandbox, @(12.3)) -ne '12.3') { throw 'Sandbox stored-value formatting changed the underlying value text.' }

    "PASS Fuel paste preserved: $($actual -join ', ')"
    'PASS Display precision changes preserved pasted values and restored their original decimal text'
    'PASS Fuel selected-group edit and autosave preserved: 73.987'
    "PASS Fuel smoothing preserved unselected 102.375 and retained the complete calculated result: $smoothedCenter"
    "PASS Fuel, Timing, and Sandbox smoothing do not round stored results: $fuelSmoothed, $timingSmoothed, $sandboxSmoothed"
    'PASS Display Zeroes pads display text without changing stored-value text'
    'PASS Three-leading-digit display values round to whole numbers without changing stored values'
}
finally {
    if ($hadClipboardText) { [System.Windows.Clipboard]::SetText($originalClipboardText) }
    else { [System.Windows.Clipboard]::Clear() }
    if (Test-Path -LiteralPath $testAutosave) { Remove-Item -LiteralPath $testAutosave -Force }
}
