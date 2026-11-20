# Run from the Stage 5 root folder in PowerShell: runs the tests, then publishes a self-contained win-x64 build
# to HTBulkFileRenamer\bin\publish\win-x64 for Installer\HTBulkFileRenamer.iss and the portable zip.
#
# HTREASURE standards: Release builds must sign the bundled advisory snapshot. Pass the offline signing key:
#   .\Build-Release.ps1 -SigningKey D:\offline\advisory.pem -KeyId htreasure-advisory-2026
# Without a key the build is marked INTERNAL (unsigned snapshot), and Security Advisory History says so.
param([string]$SigningKey, [string]$KeyId)
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$solution = Join-Path $PSScriptRoot 'HTBulkFileRenamer.slnx'
$project = Join-Path $PSScriptRoot 'HTBulkFileRenamer\HTBulkFileRenamer.csproj'
$publishDir = Join-Path $PSScriptRoot 'HTBulkFileRenamer\bin\publish\win-x64'

if (-not (Test-Path (Join-Path $PSScriptRoot '..\external\ht-software-standards\dotnet'))) {
    throw 'The HTREASURE template and standards submodules are missing. Run: git submodule update --init'
}

& $dotnet test $solution
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not building a release.' }

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
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Write-Host "Tests passed and the release is published to $publishDir. Compile Installer\HTBulkFileRenamer.iss with Inno Setup."
