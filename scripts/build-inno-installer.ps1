param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$CompilerPath,
    [switch]$PublishOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$innoRoot = Join-Path $repositoryRoot 'artifacts\inno'
$publishDirectory = Join-Path $innoRoot 'publish\win-x64'
$installerDirectory = Join-Path $innoRoot 'installer'
$applicationProject = Join-Path $repositoryRoot 'MapLab.csproj'
$innoScript = Join-Path $repositoryRoot 'Installer\MapLab.iss'

if (-not $PublishOnly) {
    if (-not $CompilerPath) {
        $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        $candidates = @(
            $command.Source
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        )
        $CompilerPath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    }
    if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
        throw 'Inno Setup 6 compiler not found. Install Inno Setup, or pass -CompilerPath with the path to ISCC.exe.'
    }
    $CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
}

$metadataOutput = & dotnet msbuild $applicationProject -nologo "-p:Configuration=$Configuration" -getProperty:Version,AssemblyName
if ($LASTEXITCODE -ne 0) { throw 'Could not read the Map Lab project metadata.' }
$metadata = ($metadataOutput -join [Environment]::NewLine) | ConvertFrom-Json
if (-not $Version) { $Version = ($metadata.Properties.Version -split '-', 2)[0] }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'The installer version must be major.minor.patch.' }
$executableName = "$($metadata.Properties.AssemblyName).exe"
if ([string]::IsNullOrWhiteSpace($metadata.Properties.AssemblyName) -or [IO.Path]::GetFileName($executableName) -ne $executableName) {
    throw 'The project AssemblyName must be a valid executable file name.'
}

# Only refresh Inno's dedicated staging folder; never clean the MSI artifacts.
$publishDirectory = [IO.Path]::GetFullPath($publishDirectory)
$allowedRoot = [IO.Path]::GetFullPath($innoRoot) + [IO.Path]::DirectorySeparatorChar
if (-not $publishDirectory.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a path outside the Inno artifacts directory: $publishDirectory"
}
if (Test-Path -LiteralPath $publishDirectory) {
    if ((Get-Item -LiteralPath $publishDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to clean a redirected publish folder: $publishDirectory"
    }
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

& dotnet publish $applicationProject --configuration $Configuration --runtime win-x64 `
    --self-contained true --output $publishDirectory "-p:Version=$Version" `
    "-p:FileVersion=$Version.0" "-p:AssemblyVersion=$Version.0" -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Map Lab publish failed.' }

$dotnetRoot = Split-Path -Parent (Get-Command dotnet -ErrorAction Stop).Source
foreach ($notice in @(
    @{ Source = 'LICENSE.txt'; Destination = 'DOTNET-LICENSE.txt' }
    @{ Source = 'ThirdPartyNotices.txt'; Destination = 'DOTNET-THIRD-PARTY-NOTICES.txt' }
)) {
    $sourcePath = Join-Path $dotnetRoot $notice.Source
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing .NET notice: $sourcePath" }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $publishDirectory $notice.Destination)
}
foreach ($fileName in @($executableName, 'coreclr.dll', 'PresentationFramework.dll', 'LICENSE.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $fileName) -PathType Leaf)) {
        throw "Required published file is missing: $fileName"
    }
}
if ($PublishOnly) {
    Write-Host "Published $executableName. Open $innoScript in Inno Setup Compiler and select Compile."
    return
}

$outputName = "MapLab-$Version-beta-win-x64-setup"
& $CompilerPath '/Qp' "/DProjectRoot=$repositoryRoot" "/DPublishDir=$publishDirectory" `
    "/DAppVersion=$Version" "/DAppExeName=$executableName" "/O$installerDirectory" "/F$outputName" $innoScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$installer = Join-Path $installerDirectory "$outputName.exe"
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw "Installer not found: $installer" }
Write-Host "Map Lab Inno installer created: $installer"
Get-FileHash -Algorithm SHA256 -LiteralPath $installer
