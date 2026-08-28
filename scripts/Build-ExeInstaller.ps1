[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64')]
    [string]$Platform = 'x64',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'VrcKaihenLibrary\VrcKaihenLibrary.csproj'
$publishDirectory = Join-Path $repositoryRoot "artifacts\unpackaged\VrcKaihenLibrary-$Version-$Platform"
$installerOutputDirectory = Join-Path $repositoryRoot 'artifacts\installer'
$installerScript = Join-Path $repositoryRoot 'installer\VrcKaihenLibrary.iss'

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$iscc = $isccCandidates | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup -e'
}

New-Item -ItemType Directory -Path $publishDirectory, $installerOutputDirectory -Force | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -r "win-$Platform" `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -p:PublishDir="$publishDirectory\"

if ($LASTEXITCODE -ne 0) {
    throw "Unpackaged publish failed with exit code $LASTEXITCODE."
}

& $iscc `
    "/DSourceDir=$publishDirectory" `
    "/DAppVersion=$Version" `
    "/DOutputDir=$installerOutputDirectory" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup build failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $installerOutputDirectory "VrcKaihenLibrary-$Version-$Platform-setup.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw 'The installer build completed, but the generated EXE could not be located.'
}

Write-Output $installerPath
