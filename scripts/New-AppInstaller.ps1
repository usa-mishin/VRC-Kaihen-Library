[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$AppInstallerUri,
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$PackageUri,
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64', 'x86', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot 'artifacts\msix\VrcKaihenLibrary.appinstaller'
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$document = @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
  Uri="$AppInstallerUri"
  Version="$Version"
  xmlns="http://schemas.microsoft.com/appx/appinstaller/2018">
  <MainPackage
    Name="usa-mishin.VrcKaihenLibrary"
    Publisher="CN=usa-mishin"
    Version="$Version"
    ProcessorArchitecture="$Architecture"
    Uri="$PackageUri" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="0" ShowPrompt="true" UpdateBlocksActivation="false" />
  </UpdateSettings>
</AppInstaller>
"@

[System.IO.File]::WriteAllText($OutputPath, $document, (New-Object System.Text.UTF8Encoding($false)))
Write-Output (Resolve-Path -LiteralPath $OutputPath).Path
