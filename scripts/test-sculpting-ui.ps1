#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'artifacts/build-check'
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[Reflection.Assembly]::LoadFrom((Join-Path $build "$($project.Project.PropertyGroup.AssemblyName).dll")) | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class SculptCommitProbe
{
    public static int Count;
    public static double[,] Last = new double[0,0];
    public static readonly Func<double[,], IReadOnlyCollection<(int Row, int Col)>, double[,]> Handler = Commit;
    private static double[,] Commit(double[,] values, IReadOnlyCollection<(int Row, int Col)> affected) { Count++; Last = (double[,])values.Clone(); return (double[,])values.Clone(); }
}
'@
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$values = [double[,]]::new(8,8)
for ($r=0;$r -lt 8;$r++) { for ($c=0;$c -lt 8;$c++) { $values[$r,$c] = 70 + $r + $c } }
$rpm = [double[]](500,750,1000,1500,2250,3000,4500,6500)
$map = [double[]](100,90,80,70,60,50,40,30)
$smooth = [Func[int,int,int,int,double[,]]] { param($t,$b,$l,$r) return $values.Clone() }
$commit = [SculptCommitProbe]::Handler
$window = [TimingTableCalculator.Surface3DWindow]::new($values,$rpm,$map,'kPa absolute',$false,[Windows.Media.Colors]::Red,[Windows.Media.Colors]::Magenta,$smooth,'3D Sculpt Test','VALUE',$null,'MAP','RPM','0','0.0',$null,$commit)
$window.WindowStartupLocation = 'Manual'; $window.Left = -10000; $window.Top = -10000; $window.ShowActivated = $false; $window.ShowInTaskbar = $false

function Private-Field([string]$name) { return $window.GetType().GetField($name,$flags) }
function Invoke-Private([string]$name, [object[]]$arguments) { return $window.GetType().GetMethod($name,$flags).Invoke($window,$arguments) }
function Render([string]$name, [int]$width, [int]$height) {
    $window.Width=$width; $window.Height=$height; $window.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($window.ActualWidth),[int][Math]::Ceiling($window.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($window)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $path = Join-Path $build $name
    $stream = [IO.File]::Create($path); try { $encoder.Save($stream) } finally { $stream.Dispose() }
    if ((Get-Item -LiteralPath $path).Length -lt 20000) { throw "Sculpt view rendered an unexpectedly small image at $width x $height" }
}

try {
    $window.Show(); $window.UpdateLayout(); Write-Host 'Sculpt window shown'
    if ((Private-Field 'sculptButtons').GetValue($window).Count -ne 4) { throw 'Sculpt toolbar does not contain all four modes' }
    $radius = Private-Field 'brushRadius'; $strength = Private-Field 'brushStrength'; $falloff = Private-Field 'brushFalloff'; $amount = Private-Field 'brushAmount'
    $radius.GetValue($window).Value = 1; $strength.GetValue($window).Value = 100; $falloff.GetValue($window).SelectedIndex = 2; $amount.GetValue($window).Text = '5'
    $scaleLabels = (Private-Field 'valueScaleLabels').GetValue($window); $oldMaximumLabel = $scaleLabels[10].Text
    Invoke-Private 'ToggleSculptMode' @([TimingTableCalculator.SurfaceSculptMode]::Raise) | Out-Null
    $before = $values.Clone()
    Write-Host 'Beginning sculpt stroke'
    if (-not (Invoke-Private 'BeginSculptStroke' @([ValueTuple[int,int]]::new(3,2)))) { throw 'Sculpt stroke did not begin' }
    Write-Host 'First sculpt stamp applied'
    Invoke-Private 'ApplySculptThrough' @([ValueTuple[int,int]]::new(3,5),$null) | Out-Null
    Write-Host 'Sculpt path applied'
    Invoke-Private 'FinishSculptStroke' @() | Out-Null
    Write-Host 'Sculpt callback returned'
    if ([SculptCommitProbe]::Count -ne 1 -or [SculptCommitProbe]::Last[3,2] -le $before[3,2] -or [SculptCommitProbe]::Last[3,5] -le $before[3,5]) { throw 'Sculpt path was not committed once' }
    if ($scaleLabels[10].Text -eq $oldMaximumLabel) { throw 'The 3D value scale did not refresh after sculpting changed its range' }
    Write-Host 'Sculpt commit checked'

    $committed = [SculptCommitProbe]::Last.Clone()
    if (-not (Invoke-Private 'BeginSculptStroke' @([ValueTuple[int,int]]::new(4,4)))) { throw 'Cancellation stroke did not begin' }
    Invoke-Private 'CancelSculptStroke' @() | Out-Null
    $localValues = Private-Field 'values'; $afterCancel = $localValues.GetValue($window)
    if ([SculptCommitProbe]::Count -ne 1 -or $afterCancel[4,4] -ne $committed[4,4]) { throw 'Escape-style cancellation committed or retained its preview' }
    Write-Host 'Sculpt cancellation checked'

    $pinned = (Private-Field 'pinnedSurfaceSelection').GetValue($window); $pinned.Clear(); $pinned.Add([ValueTuple[int,int]]::new(2,2)) | Out-Null
    (Private-Field 'limitSculptToSelection').GetValue($window).IsChecked = $true
    Invoke-Private 'ToggleSculptMode' @([TimingTableCalculator.SurfaceSculptMode]::Lower) | Out-Null
    $maskBefore = $afterCancel.Clone()
    if (-not (Invoke-Private 'BeginSculptStroke' @([ValueTuple[int,int]]::new(2,2)))) { throw 'Masked sculpt stroke did not begin' }
    Invoke-Private 'FinishSculptStroke' @() | Out-Null
    if ([SculptCommitProbe]::Count -ne 2 -or [SculptCommitProbe]::Last[2,2] -ge $maskBefore[2,2] -or [SculptCommitProbe]::Last[2,3] -ne $maskBefore[2,3]) { throw 'Selection mask did not limit the sculpt stroke' }
    Write-Host 'Sculpt mask checked'

    Render 'sculpting-1100.png' 1100 760; Write-Host 'Large sculpt view rendered'
    Render 'sculpting-800.png' 800 620; Write-Host 'Compact sculpt view rendered'
} finally { $window.Close() }
'3D sculpt integration passed: one commit per stroke, path interpolation, cancellation, selection mask, responsive layouts, and nonblank rendering.'
