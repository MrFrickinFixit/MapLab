#requires -Version 7.4
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$output = Join-Path $root "bin\$Configuration\net8.0-windows"
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$assemblyName = "MapLab-$($project.Project.PropertyGroup.Version)-beta"
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $output "$assemblyName.dll"))
$values = [double[,]]::new(4,4)
$rpm = [double[]](500,1000,2000,4000)
$map = [double[]](100,75,50,25)
$smooth = [Func[int,int,int,int,double[,]]] { param($t,$b,$l,$r) return $values.Clone() }
$window = [TimingTableCalculator.Surface3DWindow]::new($values,$rpm,$map,'kPa absolute',$false,[Windows.Media.Colors]::Red,[Windows.Media.Colors]::Magenta,$smooth)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $window.GetType()
$buttons = $type.GetField('navigationButtons',$flags).GetValue($window)
if ($buttons.Count -ne 3) { throw "Expected Select, Orbit, and Pan controls; found $($buttons.Count)." }
$profileBox = [Windows.Controls.ComboBox]$type.GetField('inputProfileBox',$flags).GetValue($window)
if ($profileBox.Items.Count -ne 2) { throw 'Expected desktop mouse and laptop touchpad profiles.' }
$viewport = [Windows.Controls.Viewport3D]$type.GetField('viewport',$flags).GetValue($window)
$viewport.Measure([Windows.Size]::new(800,500)); $viewport.Arrange([Windows.Rect]::new(0,0,800,500))
$camera = [Windows.Media.Media3D.PerspectiveCamera]$type.GetField('camera',$flags).GetValue($window)
$before = $camera.Position
$type.GetField('panning',$flags).SetValue($window,$true)
$type.GetField('lastPoint',$flags).SetValue($window,[Windows.Point]::new(100,100))
$type.GetMethod('Pan',$flags).Invoke($window,@([Windows.Point]::new(140,120))) | Out-Null
if ($camera.Position -eq $before) { throw 'Pan did not move the camera.' }
$zoomBefore = ($camera.Position - $type.GetField('cameraTarget',$flags).GetValue($window)).Length
$type.GetMethod('Zoom',$flags).Invoke($window,@(120)) | Out-Null
$zoomAfter = ($camera.Position - $type.GetField('cameraTarget',$flags).GetValue($window)).Length
if ($zoomAfter -ge $zoomBefore) { throw 'Zoom did not preserve the panned target.' }
$source = Get-Content -LiteralPath (Join-Path $root 'Surface3DWindow.cs') -Raw
if ($source -notmatch 'Header = "Clear Selection"' -or $source -match 'ActionItem\("Clear selected"') { throw 'The 3D context menu does not use non-destructive Clear Selection.' }
foreach ($file in 'MainWindow.xaml.cs','FuelingPanel.cs','SandboxPanel.cs','LearnApplyPanel.cs') {
    $tableSource = Get-Content -LiteralPath (Join-Path $root $file) -Raw
    if ($tableSource -notmatch 'Clear Selection') { throw "$file does not expose Clear Selection in its cell context menu." }
}
$window.Close()
'PASS 3D Select/Orbit/Pan controls, desktop/touchpad profiles, pan-aware zoom, and non-destructive Clear Selection'
