#requires -Version 7.4

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $root 'bin\Release\net8.0-windows\MapLab-1.0.3.7-beta.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw "Release build is missing: $assemblyPath" }

$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$store = $assembly.GetType('TimingTableCalculator.RecentMapFileStore', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$write = $store.GetMethod('Write', $flags)
$read = $store.GetMethod('Read', $flags)
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("MapLab-recent-file-" + [Guid]::NewGuid().ToString('N'))
$preferencePath = Join-Path $temporaryDirectory 'recent-file.json'
$mapPath = Join-Path $temporaryDirectory 'Last Tune.map'

try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    [IO.File]::WriteAllText($mapPath, '{}')
    if (-not $write.Invoke($null, [object[]]@([string]$preferencePath, [string]$mapPath))) { throw 'Recent-file preference could not be written.' }
    $loaded = [string]$read.Invoke($null, [object[]]@([string]$preferencePath))
    if ($loaded -ne [IO.Path]::GetFullPath($mapPath)) { throw "Recent-file path mismatch: $loaded" }
    [IO.File]::WriteAllText($preferencePath, '{ invalid json')
    if ($null -ne $read.Invoke($null, [object[]]@([string]$preferencePath))) { throw 'A damaged recent-file preference was not ignored.' }
    'PASS recent .map path is persisted and damaged preferences fail safely'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force }
}
