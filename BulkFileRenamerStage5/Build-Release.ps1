# HT Bulk File Renamer release build. Run from the Stage 5 folder in PowerShell:
#   1. runs the tests;
#   2. publishes a self-contained win-x64 build to HTBulkFileRenamer\bin\publish\win-x64;
#   3. Authenticode-signs HTREASURE's own binaries (when signing is configured);
#   4. compiles Installer\HTBulkFileRenamer.iss, signing Setup and its uninstaller;
#   5. builds the portable zip (with LICENSE.txt) and SHA256SUMS.txt in Installer\Output\v<version>;
#   6. verifies every signature.
#
# Code signing (same settings as HT ISO Ripper; see docs\CODE-SIGNING.md). Without it the release is UNSIGNED.
#   Route A - OV/EV certificate from a certificate authority, on a hardware token or in the certificate store:
#     $env:HT_SIGN_THUMBPRINT  SHA-1 thumbprint of HTREASURE LTD's code-signing certificate
#     $env:HT_TIMESTAMP_URL    RFC 3161 timestamp server URL of the certificate authority
#   Route B - Azure Artifact Signing (formerly Trusted Signing):
#     $env:HT_ARTIFACT_SIGNING_ENDPOINT  e.g. https://weu.codesigning.azure.net/
#     $env:HT_ARTIFACT_SIGNING_ACCOUNT   Artifact Signing account name
#     $env:HT_ARTIFACT_SIGNING_PROFILE   certificate profile name
#     $env:HT_ARTIFACT_SIGNING_DLIB      path to Azure.CodeSigning.Dlib.dll (x64) from Microsoft's signing client
#     Sign-in: Azure CLI (az login) locally, or AZURE_CLIENT_ID/AZURE_TENANT_ID/AZURE_CLIENT_SECRET in CI.
#   Self-signed, from a local file (internal and test PCs only): Tools\New-SelfSignedCodeSigningCert.ps1 creates it.
#     $env:HT_SIGN_PFX       path to the .pfx; its password is read from <pfx>.password (DPAPI-encrypted, this user only)
#     $env:HT_TIMESTAMP_URL  RFC 3161 timestamp server URL, e.g. http://timestamp.digicert.com
#     Windows reports these signatures as untrusted until HTREASURE-CodeSigning.cer (copied into the output folder)
#     is trusted on that PC.
#   Optional: $env:SIGNTOOL_PATH (signtool.exe from the Windows SDK, x64), $env:ISCC_PATH (Inno Setup 6.3+ ISCC.exe).
#
# Advisory snapshot (HTREASURE standards; a different key from code signing): release builds must sign the bundled
# advisory snapshot. Pass the offline key:  .\Build-Release.ps1 -SigningKey D:\offline\advisory.pem -KeyId <id>
# Without it the build is marked INTERNAL (unsigned snapshot), and Security Advisory History says so.
param([string]$SigningKey, [string]$KeyId)
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$solution = Join-Path $PSScriptRoot 'HTBulkFileRenamer.slnx'
$project = Join-Path $PSScriptRoot 'HTBulkFileRenamer\HTBulkFileRenamer.csproj'
$publishDir = Join-Path $PSScriptRoot 'HTBulkFileRenamer\bin\publish\win-x64'
$licence = Join-Path $PSScriptRoot '..\LICENSE'

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Invoke-Checked($exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path (Join-Path $PSScriptRoot '..\external\ht-software-standards\dotnet'))) {
    throw 'The HTREASURE template and standards submodules are missing. Run: git submodule update --init'
}
$version = (Select-Xml -Path $project -XPath '//Version').Node.InnerText
if (-not $version) { throw 'No <Version> in the .csproj.' }
$out = Join-Path $PSScriptRoot "Installer\Output\v$version"

# ---- Code signing configuration (checked before anything is built)
$useArtifactSigning = [bool]$env:HT_ARTIFACT_SIGNING_ACCOUNT
$usePfx = [bool]$env:HT_SIGN_PFX -and -not $useArtifactSigning
$signing = [bool]$env:HT_SIGN_THUMBPRINT -or $usePfx -or $useArtifactSigning
if ($signing) {
    $signtool = $env:SIGNTOOL_PATH
    if (-not $signtool) { $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName }
    if (-not $signtool) { throw 'signtool.exe not found (install the Windows SDK or set SIGNTOOL_PATH).' }
    if ($useArtifactSigning) {
        foreach ($v in 'HT_ARTIFACT_SIGNING_ENDPOINT', 'HT_ARTIFACT_SIGNING_PROFILE', 'HT_ARTIFACT_SIGNING_DLIB') {
            if (-not [Environment]::GetEnvironmentVariable($v)) { throw "$v must be set for Azure Artifact Signing." }
        }
        if (-not (Test-Path $env:HT_ARTIFACT_SIGNING_DLIB)) { throw "Azure.CodeSigning.Dlib.dll not found at $env:HT_ARTIFACT_SIGNING_DLIB" }
        $metadata = Join-Path $env:TEMP 'htbulkfilerenamer-artifact-signing.json'
        [ordered]@{ Endpoint = $env:HT_ARTIFACT_SIGNING_ENDPOINT; CodeSigningAccountName = $env:HT_ARTIFACT_SIGNING_ACCOUNT
                    CertificateProfileName = $env:HT_ARTIFACT_SIGNING_PROFILE } | ConvertTo-Json | Set-Content -Encoding UTF8 $metadata
        # Microsoft's documented signtool invocation for Artifact Signing (its own timestamp service).
        $signArgs = @('sign', '/fd', 'SHA256', '/tr', 'http://timestamp.acs.microsoft.com', '/td', 'SHA256', '/d', 'HT Bulk File Renamer',
                      '/dlib', $env:HT_ARTIFACT_SIGNING_DLIB, '/dmdf', $metadata)
    } elseif ($usePfx) {
        if (-not $env:HT_TIMESTAMP_URL) { throw 'HT_TIMESTAMP_URL must be set when signing with a .pfx file.' }
        if (-not (Test-Path $env:HT_SIGN_PFX)) { throw "Signing certificate file not found: $env:HT_SIGN_PFX" }
        $pfxPasswordFile = "$env:HT_SIGN_PFX.password"
        if (-not (Test-Path $pfxPasswordFile)) { throw "Password file not found: $pfxPasswordFile" }
        $pfxPassword = Get-Content $pfxPasswordFile | ConvertTo-SecureString
        $pfxPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pfxPassword))
        $signArgs = @('sign', '/f', $env:HT_SIGN_PFX, '/p', $pfxPlain, '/fd', 'SHA256', '/tr', $env:HT_TIMESTAMP_URL, '/td', 'SHA256', '/d', 'HT Bulk File Renamer')
    } else {
        if (-not $env:HT_TIMESTAMP_URL) { throw 'HT_TIMESTAMP_URL must be set when signing with a certificate thumbprint.' }
        $signArgs = @('sign', '/sha1', $env:HT_SIGN_THUMBPRINT, '/fd', 'SHA256', '/tr', $env:HT_TIMESTAMP_URL, '/td', 'SHA256', '/d', 'HT Bulk File Renamer')
    }
}
# Self-signed: a local .pfx whose certificate issued itself. Its signatures are expected to show an untrusted root.
$selfSigned = $false
if ($usePfx) {
    $signCert = (Get-PfxData -FilePath $env:HT_SIGN_PFX -Password $pfxPassword).EndEntityCertificates | Select-Object -First 1
    if (-not $signCert) { throw "No certificate in $env:HT_SIGN_PFX" }
    $selfSigned = $signCert.Subject -eq $signCert.Issuer
    if ($selfSigned) { Write-Warning 'SELF-SIGNED release: Windows shows "Unknown publisher" unless HTREASURE-CodeSigning.cer is trusted on the PC.' }
}
$iscc = $env:ISCC_PATH
if (-not $iscc) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw 'Inno Setup 6.3+ (ISCC.exe) not found. Install it or set ISCC_PATH.' }

# ---- Test and publish
Step 'Running tests'
Invoke-Checked $dotnet @('test', $solution)

Step "Publishing $version (self-contained win-x64)"
# Runtime identifier and self-contained are set in the .csproj when HTSelfContainedRelease is true (see there why).
$publishArgs = @('publish', $project, '-c', 'Release', '-p:HTSelfContainedRelease=true', '-o', $publishDir)
if ($SigningKey) {
    if (-not $KeyId) { throw 'Pass -KeyId with -SigningKey.' }
    $publishArgs += "-p:HTAdvisorySigningKey=$SigningKey", "-p:HTAdvisoryKeyId=$KeyId"
} else {
    Write-Warning 'No advisory signing key: building an INTERNAL release with an unsigned advisory snapshot.'
    $publishArgs += '-p:HTRequireSignedSnapshot=false'
}
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
Invoke-Checked $dotnet $publishArgs

# ---- Sign HTREASURE's own binaries. The .NET runtime files are already signed by Microsoft and are left as they are.
$ownBinaries = @(Get-ChildItem $publishDir -Recurse -File -Include *.exe, *.dll | Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -eq 'NotSigned' })
if ($signing) {
    Step "Signing $($ownBinaries.Count) application binaries"
    Invoke-Checked $signtool ($signArgs + @($ownBinaries | ForEach-Object FullName))
} else {
    Write-Warning 'No code signing configured (HT_SIGN_THUMBPRINT or HT_ARTIFACT_SIGNING_*): building an UNSIGNED release.'
}

# ---- Installer
Step 'Compiling the installer'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null
$isccArgs = @('/Q', "/O$out")
if ($signing) {
    # Inno Setup sign-tool command: $q is a quote character and $f the (already quoted) file to sign.
    $innoArgs = foreach ($a in $signArgs) { if ($a -match '\s') { '$q' + $a + '$q' } else { $a } }
    $isccArgs += '/Shtsign=$q' + $signtool + '$q ' + ($innoArgs -join ' ') + ' $f'
    $isccArgs += '/DSignTool=htsign'
}
Invoke-Checked $iscc ($isccArgs + (Join-Path $PSScriptRoot 'Installer\HTBulkFileRenamer.iss'))

# ---- Portable zip. Windows' tar writes standard forward-slash paths (Compress-Archive in Windows PowerShell doesn't).
Step 'Packaging the portable zip'
$stage = Join-Path $env:TEMP "htbulkfilerenamer-portable-$version"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
Copy-Item $publishDir $stage -Recurse
Copy-Item $licence (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $PSScriptRoot 'README.md') (Join-Path $stage 'README.md')
$zip = Join-Path $out "HTBulkFileRenamer-$version-Portable-win-x64.zip"
Push-Location $stage
try { Invoke-Checked "$env:SystemRoot\System32\tar.exe" @('-a', '-c', '-f', $zip, '*') } finally { Pop-Location }
Remove-Item -Recurse -Force $stage

# ---- Verify signatures, then checksums
Step 'Verifying signatures'
$setup = Join-Path $out "HTBulkFileRenamer-$version-Setup.exe"
foreach ($file in @($setup) + @($ownBinaries | ForEach-Object FullName)) {
    $sig = Get-AuthenticodeSignature $file
    $status = $sig.Status
    if ($selfSigned) {
        # An untrusted root (UnknownError) is expected; the file must still be signed by this exact certificate, unmodified.
        if ($status -notin 'Valid', 'UnknownError' -or $sig.SignerCertificate.Thumbprint -ne $signCert.Thumbprint) {
            throw "$(Split-Path $file -Leaf) signature status: $status"
        }
        $status = 'SelfSigned'
    } elseif ($signing -and $status -ne 'Valid') { throw "$(Split-Path $file -Leaf) signature status: $status" }
    Write-Host ("  {0,-12} {1}" -f $status, (Split-Path $file -Leaf))
}
if ($selfSigned) { Export-Certificate -Cert $signCert -FilePath (Join-Path $out 'HTREASURE-CodeSigning.cer') | Out-Null }

$sums = Get-ChildItem $out -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'), [string[]]$sums, (New-Object Text.UTF8Encoding $false))

Step "Release $version ready in $out"
Get-ChildItem $out | ForEach-Object { Write-Host ("  {0,8:N1} MB  {1}" -f ($_.Length / 1MB), $_.Name) }
if (-not $signing) { Write-Warning 'Release is UNSIGNED (Windows SmartScreen will show "Unknown publisher"). See docs\CODE-SIGNING.md.' }
