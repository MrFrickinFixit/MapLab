#requires -Version 7.4
param([int[]]$Sizes = @(31,64), [string]$OutputName = 'performance-check.json')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$build = Join-Path $root 'artifacts/build-check'
$assemblyPath = Join-Path $build "$($project.Project.PropertyGroup.AssemblyName).dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw 'Build Release into artifacts/build-check before running this script.' }
if (-not $Sizes.Count -or ($Sizes | Where-Object { $_ -lt 8 -or $_ -gt 64 })) { throw 'Sizes must be between 8 and 64.' }
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName) { throw 'OutputName must be a filename, not a path.' }
$runnerProject = Join-Path $PSScriptRoot 'PerformanceCheck.csproj'
dotnet build $runnerProject --configuration Release "-p:MapLabAssembly=$assemblyPath" -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw 'Performance runner build failed.' }
$runner = Join-Path $root 'artifacts/performance-runner/Release/net8.0-windows/PerformanceCheck.dll'
dotnet $runner (Join-Path $build $OutputName) @Sizes
if ($LASTEXITCODE -ne 0) { throw 'Performance check failed.' }
