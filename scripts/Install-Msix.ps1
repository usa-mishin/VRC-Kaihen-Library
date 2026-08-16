[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = Resolve-Path -LiteralPath $PackagePath
Add-AppxPackage -Path $resolvedPackage.Path

$manifest = Get-AppxPackage -Name 'usa-mishin.VrcKaihenManager'
if (-not $manifest) {
    throw 'The installed MSIX package could not be found.'
}

Write-Output "VrcKaihenManager $($manifest.Version) was installed."
