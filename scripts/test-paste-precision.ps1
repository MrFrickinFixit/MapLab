#requires -Version 7.4
#requires -PSEdition Core

$ErrorActionPreference = 'Stop'

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw 'This WPF clipboard test must be run with pwsh -Sta.'
}

$root = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $root 'bin\Release\net8.0-windows\MapLab-1.0.3.3-beta.dll'
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

    "PASS Fuel paste preserved: $($actual -join ', ')"
    'PASS Fuel selected-group edit and autosave preserved: 73.987'
}
finally {
    if ($hadClipboardText) { [System.Windows.Clipboard]::SetText($originalClipboardText) }
    else { [System.Windows.Clipboard]::Clear() }
    if (Test-Path -LiteralPath $testAutosave) { Remove-Item -LiteralPath $testAutosave -Force }
}
