# Run from the Stage 5 root folder in PowerShell: runs the tests, then builds Release.
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$solution = Join-Path $PSScriptRoot 'HTBulkFileRenamer.slnx'
$project = Join-Path $PSScriptRoot 'HTBulkFileRenamer\HTBulkFileRenamer.csproj'
& $dotnet test $solution
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not building a release.' }
& $dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Tests passed and Release build complete. Compile Installer\HTBulkFileRenamer.iss with Inno Setup.'
