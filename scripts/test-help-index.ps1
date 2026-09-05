#requires -Version 7.4
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "bin\$Configuration\net8.0-windows"
[AppContext]::SetData('APP_CONTEXT_BASE_DIRECTORY', $output + [IO.Path]::DirectorySeparatorChar)
$assemblyPath = Join-Path $output 'MapLab-1.0.3.4-beta.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw "Build Map Lab first: $assemblyPath" }

$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$helpType = $assembly.GetType('TimingTableCalculator.HelpPanel', $true)
$panel = [Activator]::CreateInstance($helpType)
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$index = [Windows.Controls.ListBox]$helpType.GetField('indexList', $instanceFlags).GetValue($panel)
if ($index.Items.Count -lt 20) { throw "The bundled Help index did not load: $($index.Items.Count) entries" }

for ($item = 0; $item -lt $index.Items.Count; $item++) {
    $index.SelectedIndex = $item
    $panel.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
}

$interactionType = $assembly.GetType('TimingTableCalculator.UiInteraction', $true)
$staticFlags = [Reflection.BindingFlags]'Static,Public'
$isDescendant = $interactionType.GetMethod('IsDescendantOf', $staticFlags)
$isInsideButton = $interactionType.GetMethod('IsInsideButton', $staticFlags)
$document = [Windows.Documents.FlowDocument]::new()
$paragraph = [Windows.Documents.Paragraph]::new()
$run = [Windows.Documents.Run]::new('Index topic text')
$paragraph.Inlines.Add($run)
$document.Blocks.Add($paragraph)
$documentList = [Windows.Documents.List]::new()
$documentList.ListItems.Add([Windows.Documents.ListItem]::new([Windows.Documents.Paragraph]::new([Windows.Documents.Run]::new('Indexed instruction'))))
$document.Blocks.Add($documentList)
$unrelatedGrid = [Windows.Controls.Grid]::new()

foreach ($content in @($run, $documentList)) {
    if ($isDescendant.Invoke($null, @($content, $unrelatedGrid))) { throw 'FlowDocument content was incorrectly treated as a table descendant.' }
    if ($isInsideButton.Invoke($null, @($content))) { throw 'FlowDocument content was incorrectly treated as a button.' }
}

"PASS exercised $($index.Items.Count) Help Index entries"
'PASS FlowDocument Run and List ancestry checks complete without a VisualTree exception'
