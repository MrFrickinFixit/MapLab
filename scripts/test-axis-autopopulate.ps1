#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$name = $project.Project.PropertyGroup.AssemblyName
$build = Join-Path $root 'artifacts/build-check'
[Reflection.Assembly]::LoadFrom((Join-Path $build "$name.dll")) | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$script:requestedRpm = [int[]]@()
$resize = [Action[int,int]] { param($x,$y) }
$fill = [Action[bool,int[]]] { param($isMap,$indices) $script:requestedRpm = [int[]]$indices }
$paste = [Action[bool,System.Nullable[int],int[]]] { param($isMap,$focused,$indices) }
$edit = [Func[bool,int,double,double[]]] { param($isMap,$index,$value) return $null }
$path = Join-Path ([IO.Path]::GetTempPath()) ('maplab-axis-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    $panel = [TimingTableCalculator.FuelingPanel]::new($resize,$fill,$paste,$resize,$edit,$path)
    $panel.UpdateAxes([double[]](500,1000,1500,2000,2500,3000),[double[]](100,95,80,65,40,20),'kPa absolute',1000,80)
    $selectedMap = $panel.GetType().GetField('selectedMapAxis',$flags).GetValue($panel)
    $selectedMap.Add(1) | Out-Null; $selectedMap.Add(4) | Out-Null
    $panel.GetType().GetMethod('AutoFillSelectedAxis',$flags).Invoke($panel,@($true)) | Out-Null
    $map = $panel.GetType().GetField('map',$flags).GetValue($panel)
    if ($selectedMap.Count -ne 4 -or $map[1] -ne 95 -or $map[2] -ne 77 -or $map[3] -ne 58 -or $map[4] -ne 40) { throw 'Fuel MAP auto-populate did not fill the complete endpoint range.' }
    $panel.GetType().GetMethod('SelectEntireAxis',$flags).Invoke($panel,@($true)) | Out-Null
    if ($selectedMap.Count -ne 6) { throw 'Fuel MAP Select all did not select the complete axis.' }
    $selectedRpm = $panel.GetType().GetField('selectedRpmAxis',$flags).GetValue($panel)
    $selectedMap.Clear(); $selectedRpm.Add(1) | Out-Null; $selectedRpm.Add(4) | Out-Null
    $panel.GetType().GetMethod('AutoFillSelectedAxis',$flags).Invoke($panel,@($false)) | Out-Null
    if (($script:requestedRpm -join ',') -ne '1,2,3,4') { throw 'Shared RPM auto-populate did not forward the complete endpoint range.' }
    $mapEditors = $panel.GetType().GetField('mapAxisCells',$flags).GetValue($panel)
    $headers = @($mapEditors[0].ContextMenu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] } | ForEach-Object Header)
    if ($headers -notcontains 'Auto-populate selected range' -or $headers -notcontains 'Select all MAP values') { throw 'Axis context menu commands are missing.' }
    'Axis auto-populate and Select all tests passed.'
}
finally {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
