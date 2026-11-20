# Creates a self-signed HTREASURE LTD code-signing certificate as local files, outside the repository:
#   HTREASURE-CodeSigning.pfx  certificate + private key, protected by a random password. Keep it private.
#   HTREASURE-CodeSigning.pfx.password  that password, encrypted with Windows DPAPI: only this Windows account on this
#                              PC can decrypt it, and Build-Release.ps1 reads it from there, so it is never typed or shown.
#   HTREASURE-CodeSigning.cer  public certificate only; safe to share with testers so their PCs trust the signature.
#
# Self-signed signatures are for HTREASURE's own and test PCs only: Windows shows "Unknown publisher" on any PC that
# hasn't trusted the .cer. For public releases use a certificate authority or Azure Artifact Signing (docs\CODE-SIGNING.md).
param([string]$OutDir = 'C:\HTSoftware\signing', [int]$Years = 3)
$ErrorActionPreference = 'Stop'

$pfx = Join-Path $OutDir 'HTREASURE-CodeSigning.pfx'
$cer = Join-Path $OutDir 'HTREASURE-CodeSigning.cer'
if (Test-Path $pfx) { throw "$pfx already exists. Delete it first if you really want a new certificate." }
New-Item -ItemType Directory -Force $OutDir | Out-Null

# Created in the user store only long enough to export it, then removed so the file is the only copy.
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=HTREASURE LTD, O=HTREASURE LTD, C=GB' `
    -FriendlyName 'HTREASURE LTD code signing (self-signed)' -CertStoreLocation Cert:\CurrentUser\My `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears($Years)
try {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $password = ConvertTo-SecureString ([Convert]::ToBase64String($bytes)) -AsPlainText -Force
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $password | Out-Null
    $password | ConvertFrom-SecureString | Set-Content -Encoding ASCII "$pfx.password"
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
} finally {
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey
}

Write-Host "Created $pfx"
Write-Host "    and $cer"
Write-Host "Thumbprint $($cert.Thumbprint), valid until $($cert.NotAfter.ToString('d MMMM yyyy'))."
Write-Host ''
Write-Host 'Build a self-signed release with:'
Write-Host "  `$env:HT_SIGN_PFX='$pfx'; `$env:HT_TIMESTAMP_URL='http://timestamp.digicert.com'; .\Build-Release.ps1"
