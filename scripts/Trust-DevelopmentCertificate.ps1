[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell window (Run as administrator).'
}

$certificate = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object Thumbprint -eq $CertificateThumbprint |
    Select-Object -First 1

if (-not $certificate) {
    throw "Certificate $CertificateThumbprint was not found in Cert:\CurrentUser\My."
}

$trusted = Get-ChildItem -Path Cert:\LocalMachine\TrustedPeople |
    Where-Object Thumbprint -eq $certificate.Thumbprint

if (-not $trusted) {
    $temporaryCertificate = Join-Path ([System.IO.Path]::GetTempPath()) "VrcKaihenLibrary-$($certificate.Thumbprint).cer"
    try {
        Export-Certificate -Cert $certificate -FilePath $temporaryCertificate -Force | Out-Null
        Import-Certificate -FilePath $temporaryCertificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    } finally {
        Remove-Item -LiteralPath $temporaryCertificate -Force -ErrorAction SilentlyContinue
    }
}

Write-Output $certificate.Thumbprint
