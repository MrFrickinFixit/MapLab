param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.3',
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish\win-x64'))
$installerDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'installer'))

foreach ($target in @($publishDirectory, $installerDirectory)) {
    if (-not $target.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the artifacts directory: $target"
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

$applicationProject = Join-Path $repositoryRoot 'MapLab.csproj'
$installerProject = Join-Path $repositoryRoot 'Installer\MapLab.Installer.wixproj'

dotnet publish $applicationProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw 'Map Lab publish failed.' }

$dotnetRoot = Split-Path -Parent (Get-Command dotnet -ErrorAction Stop).Source
$dotnetLegalFiles = @{
    (Join-Path $dotnetRoot 'LICENSE.txt') = (Join-Path $publishDirectory 'DOTNET-LICENSE.txt')
    (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') = (Join-Path $publishDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt')
}

foreach ($entry in $dotnetLegalFiles.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Key)) {
        throw "Required .NET legal notice was not found: $($entry.Key)"
    }
    Copy-Item -LiteralPath $entry.Key -Destination $entry.Value -Force
}

$requiredPublishedFiles = @('MapLab-1.0.3-beta.exe', 'LICENSE.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt')
foreach ($fileName in $requiredPublishedFiles) {
    $publishedFile = Join-Path $publishDirectory $fileName
    if (-not (Test-Path -LiteralPath $publishedFile)) {
        throw "Required published file was not found: $publishedFile"
    }
}

dotnet build $installerProject `
    --configuration $Configuration `
    -p:Version=$Version `
    -p:PublishDir="$publishDirectory" `
    -p:OutputPath="$installerDirectory"

if ($LASTEXITCODE -ne 0) { throw 'Map Lab installer build failed.' }

$installer = Join-Path $installerDirectory "MapLab-$Version-beta-win-x64.msi"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer build completed but the expected MSI was not found: $installer"
}

Write-Host "Map Lab installer created: $installer"
