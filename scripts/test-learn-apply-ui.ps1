#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$name = $project.Project.PropertyGroup.AssemblyName
$build = Join-Path $root 'artifacts/build-check'
[Reflection.Assembly]::LoadFrom((Join-Path $build "$name.dll")) | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$path = Join-Path $build ('learn-test-' + [guid]::NewGuid().ToString() + '.json')
$resize = [Action[int,int]] { param($a,$b) }
$fill = [Action[bool,int[]]] { param($a,$b) }
$paste = [Action[bool,System.Nullable[int],int[]]] { param($a,$b,$c) }
$edit = [Func[bool,int,double,double[]]] { param($a,$b,$c) return $null }
$fuel = [TimingTableCalculator.FuelingPanel]::new($resize,$fill,$paste,$resize,$edit,$path)
function Assert-FuelSnapshot([double[,]]$expected) {
    $actual = $fuel.GetType().GetField('ve',$flags).GetValue($fuel)
    $actualFlow = $fuel.GetType().GetMethod('DisplayValues',$flags).Invoke($fuel,@())
    $currentRpm = $fuel.GetType().GetField('rpm',$flags).GetValue($fuel)
    $currentMap = $fuel.GetType().GetField('map',$flags).GetValue($fuel)
    $currentUnit = $fuel.GetType().GetField('mapUnit',$flags).GetValue($fuel)
    $currentSettings = $fuel.GetType().GetField('veSetupSettings',$flags).GetValue($fuel)
    $expectedFlow = [TimingTableCalculator.VeSetupWizard]::ConvertToFuelFlow($expected,$currentRpm,$currentMap,$currentUnit,$currentSettings)
    for ($r=0;$r -lt $expected.GetLength(0);$r++) {
        for ($c=0;$c -lt $expected.GetLength(1);$c++) {
            if ($actual[$r,$c] -ne $expected[$r,$c]) { throw "Fuel history did not restore VE at [$r,$c]" }
            if ($actualFlow[$r,$c] -ne $expectedFlow[$r,$c]) { throw "Fuel history did not refresh lb/hr at [$r,$c]" }
        }
    }
}
$rpm = [double[]](1000,1500,2000,2500,3000,3500,4000,4500)
$map = [double[]](100,90,80,70,60,50,40,30)
$fuel.UpdateAxes($rpm,$map,'kPa absolute',1500,80)
$model = $fuel.GetType().GetProperty('LearnApply',$flags).GetValue($fuel)
$ve = [double[,]]::new(8,8)
for ($r=0;$r -lt 8;$r++) { for ($c=0;$c -lt 8;$c++) { $ve[$r,$c]=80 } }
$fuel.GetType().GetField('ve',$flags).SetValue($fuel,$ve)
$transferMethod = $fuel.GetType().GetMethod('TransferLearnOffsets',$flags)
$callback = [Func[bool,int]] { param($smooth) return [int]$transferMethod.Invoke($fuel,@($smooth)) }
$panel = [TimingTableCalculator.LearnApplyPanel]::new($model,$callback)
$selected = $panel.GetType().GetField('selected',$flags).GetValue($panel)
$selected.Add([ValueTuple[int,int]]::new(2,3)) | Out-Null
$panel.GetType().GetMethod('PasteText',$flags).Invoke($panel,@("+10%`t-5%`n0`t1.234")) | Out-Null
if ($model.GetValue(2,3) -ne 10 -or $model.GetValue(2,4) -ne -5 -or $model.GetValue(3,4) -ne 1.234 -or $selected.Count -ne 0) { throw 'Partial paste/selection mismatch' }
$fuel.GetType().GetMethod('SetFuelFlowView',$flags).Invoke($fuel,@($true)) | Out-Null
$count = $transferMethod.Invoke($fuel,@($false))
$afterVe = $fuel.GetType().GetField('ve',$flags).GetValue($fuel)
$afterFlow = $fuel.GetType().GetMethod('DisplayValues',$flags).Invoke($fuel,@())
if ($count -ne 3 -or $afterVe[2,3] -ne 88 -or $afterVe[2,4] -ne 76 -or $afterVe[0,0] -ne 80) { throw 'Underlying VE transfer mismatch' }
$settings = $fuel.GetType().GetField('veSetupSettings',$flags).GetValue($fuel)
$expectedFlow = [TimingTableCalculator.VeSetupWizard]::ConvertToFuelFlow($afterVe,$rpm,$map,'kPa absolute',$settings)
$fuelCells = $fuel.GetType().GetField('cells',$flags).GetValue($fuel)
if ($afterFlow[2,3] -ne $expectedFlow[2,3] -or $fuelCells[2,3].Text -ne $expectedFlow[2,3].ToString('0.0',[Globalization.CultureInfo]::InvariantCulture)) { throw 'Rounded lb/hr display did not refresh' }
if ($fuel.GetType().GetField('undoHistory',$flags).GetValue($fuel).Count -ne 1) { throw 'Transfer did not make exactly one Undo step' }
# Execute Undo/Redo without opening the progress dialog in this isolated UI test.
$progressType = [TimingTableCalculator.WorkingRunner].Assembly.GetType('TimingTableCalculator.WorkingWindow')
$progress = [Activator]::CreateInstance($progressType,[object[]]@('Test'))
$currentProgress = [TimingTableCalculator.WorkingRunner].GetField('current',[Reflection.BindingFlags]'Static,NonPublic')
$currentProgress.SetValue($null,$progress)
try {
    $fuel.GetType().GetMethod('Undo',$flags).Invoke($fuel,@()) | Out-Null
    Assert-FuelSnapshot $ve
    if ($fuel.GetType().GetField('ve',$flags).GetValue($fuel)[2,3] -ne 80 -or $model.GetValue(2,3) -ne 10) { throw 'Fuel Undo affected learn data' }
    $fuel.GetType().GetMethod('Redo',$flags).Invoke($fuel,@()) | Out-Null
    Assert-FuelSnapshot $afterVe
    if ($fuel.GetType().GetField('ve',$flags).GetValue($fuel)[2,3] -ne 88) { throw 'Fuel Redo failed' }
} finally { $currentProgress.SetValue($null,$null); $progress.Complete() }
$restored = [TimingTableCalculator.FuelingPanel]::new($resize,$fill,$paste,$resize,$edit,$path)
$restored.UpdateAxes($rpm,$map,'kPa absolute',1500,80)
$restoredModel = $restored.GetType().GetProperty('LearnApply',$flags).GetValue($restored)
if ($restoredModel.GetValue(2,3) -ne 10 -or $restored.GetType().GetField('ve',$flags).GetValue($restored)[2,3] -ne 88) { throw 'Fuel/learn autosave restore failed' }
$saved = $fuel.GetType().GetMethod('ExportSettingsJson',$flags).Invoke($fuel,@())
if (-not $fuel.GetType().GetMethod('CanImportSettingsJson',$flags).Invoke($fuel,@($saved))) { throw 'Learn state rejected by .map validation' }
$legacy = $saved | ConvertFrom-Json
$legacy.PSObject.Properties.Remove('LearnApply')
$legacyJson = [string]($legacy | ConvertTo-Json -Depth 50 -Compress)
if (-not $restored.GetType().GetMethod('ImportSettingsJson',$flags).Invoke($restored,@($legacyJson)) -or $restoredModel.ActiveCount -ne 0) { throw 'Legacy import did not clear learn offsets' }
$fuel.GetType().GetMethod('ChangeFuelMapUnit',$flags).Invoke($fuel,@(1)) | Out-Null
if ($model.MapUnit -ne 'PSI gauge' -or $model.GetValue(2,3) -ne 10) { throw 'Fuel MAP sync failed' }
$fuel.GetType().GetField('leadingDisplayDigits',$flags).SetValue($fuel,4)
$fuel.GetType().GetField('trailingDisplayDecimals',$flags).SetValue($fuel,3)
$fuel.GetType().GetMethod('Save',$flags).Invoke($fuel,@()) | Out-Null
if ($model.TrailingDecimals -ne 3 -or $model.Format(1.234) -ne '1.234') { throw 'Precision sync failed' }
$beforeSmooth = $fuel.GetType().GetField('ve',$flags).GetValue($fuel).Clone()
$historyCount = $fuel.GetType().GetField('undoHistory',$flags).GetValue($fuel).Count
$transferMethod.Invoke($fuel,@($true)) | Out-Null
if ($fuel.GetType().GetField('undoHistory',$flags).GetValue($fuel).Count -ne $historyCount + 1) { throw 'Smoothed transfer Undo mismatch' }
$smoothed = $fuel.GetType().GetField('ve',$flags).GetValue($fuel)
for ($r=0;$r -lt 8;$r++) { for ($c=0;$c -lt 8;$c++) { if ($model.GetValue($r,$c) -eq 0 -and $smoothed[$r,$c] -ne $beforeSmooth[$r,$c]) { throw 'Smoothing changed a zero-offset cell' } } }
$model.Clear()
if ($model.ActiveCount -ne 0) { throw 'Learn clear failed' }
$progress = [Activator]::CreateInstance($progressType,[object[]]@('Test'))
$currentProgress.SetValue($null,$progress)
try {
    $fuel.GetType().GetMethod('Undo',$flags).Invoke($fuel,@()) | Out-Null
    Assert-FuelSnapshot $beforeSmooth
    if ($fuel.GetType().GetField('undoHistory',$flags).GetValue($fuel).Count -ne $historyCount) { throw 'Smoothed transfer did not undo in one step' }
    if ($model.ActiveCount -ne 0) { throw 'Fuel Undo restored cleared learn offsets' }
    $fuel.GetType().GetMethod('Redo',$flags).Invoke($fuel,@()) | Out-Null
    Assert-FuelSnapshot $smoothed
    if ($fuel.GetType().GetField('undoHistory',$flags).GetValue($fuel).Count -ne $historyCount + 1) { throw 'Smoothed transfer did not redo in one step' }
    if ($model.ActiveCount -ne 0) { throw 'Fuel Redo restored cleared learn offsets' }
} finally { $currentProgress.SetValue($null,$null); $progress.Complete() }
$model.Undo()
if ($model.GetValue(2,3) -ne 10) { throw 'Learn clear Undo failed' }
Assert-FuelSnapshot $smoothed
$full = (1..8 | ForEach-Object { ((1..8 | ForEach-Object { '2.125' }) -join "`t") }) -join "`n"
$panel.GetType().GetMethod('PasteText',$flags).Invoke($panel,@($full)) | Out-Null
if ($model.ActiveCount -ne 64 -or $selected.Count -ne 0) { throw 'Full paste failed' }

function Render-Window($window, [string]$filename) {
    $window.UpdateLayout()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($window.ActualWidth),[int][Math]::Ceiling($window.ActualHeight),96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($window)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $build $filename))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
function Hide-Offscreen($window) {
    $window.WindowStartupLocation = 'Manual'; $window.Left = -10000; $window.Top = -10000
    $window.ShowActivated = $false; $window.ShowInTaskbar = $false
}
$previewModel = [TimingTableCalculator.LearnApplyTable]::new()
$previewModel.Synchronize([double[]](0..30 | ForEach-Object { 500+$_*200 }),[double[]](0..30 | ForEach-Object { 100-$_*2 }),'kPa absolute',3,2)
$preview = [TimingTableCalculator.LearnApplyPanel]::new($previewModel,[Func[bool,int]] { param($smooth) return 0 })
$data = (0..30 | ForEach-Object { $row=$_; (0..30 | ForEach-Object { if ($row -ge 4 -and $row -lt 18 -and $_ -ge 2 -and $_ -lt 20) { ((($_-$row)*0.275)).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture) } else { '0' } }) -join "`t" }) -join "`n"
$preview.GetType().GetMethod('PasteText',$flags).Invoke($preview,@($data)) | Out-Null
$window = [System.Windows.Window]::new(); $window.Content = $preview; Hide-Offscreen $window
try {
    $window.Show()
    foreach ($size in @(@(1440,900),@(1120,700))) {
        $window.Width=$size[0]; $window.Height=$size[1]
        Render-Window $window "learn-apply-$($size[0]).png"
    }
} finally { $window.Close() }
$dialog = [TimingTableCalculator.LearnApplyTransferWindow]::new(64,2); Hide-Offscreen $dialog
try {
    $dialog.Show()
    if ($dialog.Smooth) { throw 'Transfer-only is not the default' }
    $dialog.GetType().GetField('smooth',$flags).GetValue($dialog).IsChecked = $true
    if (-not $dialog.Smooth) { throw 'Smoothing selection failed' }
    Render-Window $dialog 'learn-transfer-dialog.png'
} finally { $dialog.Close() }
[xml]$xaml = Get-Content -LiteralPath (Join-Path $root 'MainWindow.xaml')
$tabs = @($xaml.Window.TabControl.TabItem)
if ($tabs[0].Header -ne 'FUELING' -or $tabs[1].Header -ne 'LEARN APPLY TABLE') { throw 'Incorrect tab placement' }
'Learn Apply integration passed: partial/full pastes, deselection, underlying VE in lb/hr view, conversion refresh, full-table fuel Undo/Redo for plain and smoothed transfers even after clearing learn offsets, autosave/restore, .map validation, legacy import, MAP/precision sync, independent learn clear/Undo, dialog options and tab order.'
