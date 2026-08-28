[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = Resolve-Path -LiteralPath $PackagePath
Add-AppxPackage -Path $resolvedPackage.Path

$manifest = Get-AppxPackage -Name 'usa-mishin.VrcKaihenLibrary'
if (-not $manifest) {
    throw 'The installed MSIX package could not be found.'
}

Write-Output "VrcKaihenLibrary $($manifest.Version) was installed."
