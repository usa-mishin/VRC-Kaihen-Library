[CmdletBinding()]
param(
    [string]$Publisher = 'CN=usa-mishin'
)

$ErrorActionPreference = 'Stop'

$existing = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

$certificate = if ($existing) {
    $existing
} else {
    New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -FriendlyName 'VrcKaihenLibrary development signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3') `
        -NotAfter (Get-Date).AddYears(2)
}

Write-Output $certificate.Thumbprint
