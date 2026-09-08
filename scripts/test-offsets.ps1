#requires -Version 7.4
#requires -PSEdition Core

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) { throw 'Run this WPF test with pwsh -Sta.' }

$root = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $root 'bin\Release\net8.0-windows\MapLab-1.0.3.7-beta.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw "Build the Release configuration first: $assemblyPath" }

Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$offsetType = $assembly.GetType('TimingTableCalculator.OffsetMath', $true)
$apply = $offsetType.GetMethod('Apply', $flags)

function Offset([double]$value, [int]$direction, [double]$amount, [bool]$percentage) {
    [double]$apply.Invoke($null, @($value, $direction, $amount, $percentage))
}
function Assert-Near([double]$actual, [double]$expected, [string]$name) {
    if ([Math]::Abs($actual - $expected) -gt 1e-12) { throw "$name failed: $actual != $expected" }
}

Assert-Near (Offset 80 1 10 $false) 90 'Numerical increase'
Assert-Near (Offset 80 -1 10 $false) 70 'Numerical decrease'
Assert-Near (Offset 80 1 10 $true) 88 'Percentage increase'
Assert-Near (Offset 80 -1 10 $true) 72 'Percentage decrease'

$callback = [Action[int, double, bool]] { param($direction, $amount, $percentage) }
$dialog = [TimingTableCalculator.OffsetSelectionWindow]::new(10, $true, $callback)
$unitBox = $dialog.GetType().GetField('unitBox', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($dialog)
if ($unitBox.SelectedIndex -ne 1) { throw 'Percentage mode was not selected when the offset dialog opened.' }
$dialog.Configure(5, $false, $callback)
if ($unitBox.SelectedIndex -ne 0) { throw 'Numerical mode was not selected when an existing offset dialog was reconfigured.' }
$dialog.Close()

$testAutosave = [IO.Path]::GetTempFileName()
try {
    $resize = [Action[int, int]] { param($columns, $rows) }
    $fillAxis = [Action[bool, int[]]] { param($isMap, $indices) }
    $pasteAxis = [Action[bool, Nullable[int], int[]]] { param($isMap, $index, $indices) }
    $boundaries = [Action[int, int]] { param($row, $column) }
    $editAxis = [Func[bool, int, double, double[]]] { param($isMap, $index, $value) return $null }
    $fuel = [TimingTableCalculator.FuelingPanel]::new($resize, $fillAxis, $pasteAxis, $boundaries, $editAxis, $testAutosave)
    $fuel.UpdateAxes([double[]](500,600,700,800,900,1000,1100,1200), [double[]](100,90,80,70,60,50,40,30), 'kPa absolute', 800, 80)
    $instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
    $values = $fuel.GetType().GetField('ve', $instanceFlags).GetValue($fuel)
    $fuel.GetType().GetField('leadingValueDigits', $instanceFlags).SetValue($fuel, 3)
    $fuel.GetType().GetField('trailingValueDecimals', $instanceFlags).SetValue($fuel, 3)
    $fuel.GetType().GetField('start', $instanceFlags).SetValue($fuel, [ValueTuple[int,int]]::new(0,0))
    $fuel.GetType().GetField('end', $instanceFlags).SetValue($fuel, [ValueTuple[int,int]]::new(0,0))
    $applyFuelOffset = $fuel.GetType().GetMethod('ApplyOffset', $instanceFlags)

    $values.SetValue(80.0, 0, 0)
    [void]$applyFuelOffset.Invoke($fuel, @(0, 0, 0, 0, 1, 10.0, $true))
    Assert-Near ([double]$values.GetValue(0,0)) 88 'Fuel percentage increase'

    $values.SetValue(80.0, 0, 0)
    [void]$applyFuelOffset.Invoke($fuel, @(0, 0, 0, 0, 1, 10.0, $false))
    Assert-Near ([double]$values.GetValue(0,0)) 90 'Fuel numerical increase'

    $values.SetValue(105.0, 0, 0)
    [void]$applyFuelOffset.Invoke($fuel, @(0, 0, 0, 0, 1, 0.5, $true))
    Assert-Near ([double]$values.GetValue(0,0)) 105.525 'Three-digit fuel percentage increase'

    $values.SetValue(105.0, 0, 0)
    [void]$applyFuelOffset.Invoke($fuel, @(0, 0, 0, 0, 1, 0.5, $false))
    Assert-Near ([double]$values.GetValue(0,0)) 105.5 'Three-digit fuel numerical increase'
}
finally {
    if (Test-Path -LiteralPath $testAutosave) { Remove-Item -LiteralPath $testAutosave -Force }
}

'PASS Numerical and percentage offsets use distinct, correct calculations'
'PASS Offset dialog initializes and reconfigures its unit selection correctly'
'PASS FuelingPanel applies 10% and numerical 10 as 88 and 90 respectively'
'PASS Three-digit fuel values retain underlying precision for percentage and numerical offsets'
