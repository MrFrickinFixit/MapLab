#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$name = $project.Project.PropertyGroup.AssemblyName
$build = Join-Path $root 'artifacts/build-check'
[Reflection.Assembly]::LoadFrom((Join-Path $build "$name.dll")) | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$viewport = [TimingTableCalculator.TableViewport]::new()
$table = [Windows.Controls.Grid]::new()
$table.Width = 1856
$table.Height = 740
$viewport.Table = $table
$setZoom = $viewport.GetType().GetMethod('SetManualZoom',$flags)
$zoomProperty = $viewport.GetType().GetProperty('Zoom',$flags)
$fitProperty = $viewport.GetType().GetProperty('IsFitToWindow',$flags)
$setZoom.Invoke($viewport,@([double]1.25))
if ($fitProperty.GetValue($viewport) -or [Math]::Abs($zoomProperty.GetValue($viewport) - 1.25) -gt .0001) { throw 'Manual table zoom was not applied.' }
if ($table.LayoutTransform.ScaleX -ne 1.25 -or $table.LayoutTransform.ScaleY -ne 1.25) { throw 'Table layout transform does not match the zoom level.' }
$setZoom.Invoke($viewport,@([double]10))
if ($zoomProperty.GetValue($viewport) -ne 2.5) { throw 'Maximum table zoom was not clamped.' }
$setZoom.Invoke($viewport,@([double].01))
if ($zoomProperty.GetValue($viewport) -ne .3) { throw 'Minimum table zoom was not clamped.' }
$manualSize = $viewport.GetType().GetField('manualSize',$flags).GetValue($viewport)
$manualSize.IsChecked = $false
if (-not $fitProperty.GetValue($viewport) -or $table.LayoutTransform.Value -ne [Windows.Media.Matrix]::Identity) { throw 'Fit-to-window mode was not restored.' }
'Table viewport tests passed.'
