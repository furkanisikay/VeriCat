# Windows için tek dosyalık VeriCat.exe üretir.
# Kullanım: .\build\publish.ps1   ->  artifacts\publish\win-x64\VeriCat.exe
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

dotnet test VeriCat.slnx -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish src/VeriCat.Desktop/VeriCat.Desktop.csproj -p:PublishProfile=win-x64 --nologo -v quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Hazır: $(Resolve-Path artifacts\publish\win-x64\VeriCat.exe)"
