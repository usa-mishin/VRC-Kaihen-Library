[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64',
    [string]$Configuration = 'Release',
    [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'VrcKaihenLibrary\VrcKaihenLibrary.csproj'
$manifestPath = Join-Path $repositoryRoot 'VrcKaihenLibrary\Package.appxmanifest'
$outputRoot = Join-Path $repositoryRoot 'artifacts\msix'
$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

if (Test-Path -LiteralPath $vsWhere) {
    $msBuildPath = & $vsWhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
        Select-Object -First 1
}

if (-not $msBuildPath) {
    $msBuildPath = Get-ChildItem -Path "$env:ProgramFiles\Microsoft Visual Studio" -Filter MSBuild.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object FullName -Match '\\MSBuild\\Current\\Bin\\MSBuild\.exe$' |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $msBuildPath) {
    throw 'MSBuild.exe was not found. Install the .NET desktop development workload in Visual Studio.'
}

$signingEnabled = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
$arguments = @(
    $projectPath,
    '/restore',
    '/t:Build',
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    '/p:GenerateAppxPackageOnBuild=true',
    '/p:AppxBundle=Never',
    '/p:UapAppxPackageBuildMode=SideloadOnly',
    '/p:AppxSymbolPackageEnabled=false',
    "/p:Version=$Version",
    "/p:AppxPackageVersion=$Version",
    "/p:AppxPackageDir=$outputRoot\",
    "/p:AppxPackageSigningEnabled=$($signingEnabled.ToString().ToLowerInvariant())",
    '/v:minimal'
)

if ($signingEnabled) {
    $arguments += "/p:PackageCertificateThumbprint=$CertificateThumbprint"
}

$manifestContent = [System.IO.File]::ReadAllText($manifestPath)
$versionPattern = '(<Identity\b[^>]*\bVersion=")[^"]+("[^>]*>)'
if (-not [regex]::IsMatch($manifestContent, $versionPattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
    throw 'The package Identity version could not be located in Package.appxmanifest.'
}

try {
    $versionedManifest = [regex]::Replace(
        $manifestContent,
        $versionPattern,
        { param($match) $match.Groups[1].Value + $Version + $match.Groups[2].Value },
        [Text.RegularExpressions.RegexOptions]::Singleline)
    [System.IO.File]::WriteAllText($manifestPath, $versionedManifest, [Text.UTF8Encoding]::new($false))

    & $msBuildPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX build failed with exit code $LASTEXITCODE."
    }
} finally {
    [System.IO.File]::WriteAllText($manifestPath, $manifestContent, [Text.UTF8Encoding]::new($false))
}

$package = Get-ChildItem -LiteralPath $outputRoot -Filter '*.msix' -Recurse |
    Where-Object Name -Match "_$([regex]::Escape($Version))_$Platform\.msix$" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $package) {
    throw 'The build completed, but the generated MSIX could not be located.'
}

Write-Output $package.FullName
