# Run from the extracted Stage 3 root folder in PowerShell
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'HTBulkFileRenamer\HTBulkFileRenamer.csproj'
& 'C:\Program Files\dotnet\dotnet.exe' restore $project
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
& 'C:\Program Files\dotnet\dotnet.exe' build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Release build complete. Compile Installer\HTBulkFileRenamer.iss with Inno Setup.'
