# Windows için tek dosyalık VeriCat.exe ve SHA-256 özetini üretir.
# Kullanım: .\build\publish.ps1 [-Version 1.4.0] [-SkipTests]
#   -> artifacts\release\VeriCat.exe, artifacts\release\VeriCat.exe.sha256
param(
    [string]$Version = '0.0.0',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not $SkipTests) {
    dotnet test VeriCat.slnx -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet publish src/VeriCat.Desktop/VeriCat.Desktop.csproj -p:PublishProfile=win-x64 -p:Version=$Version --nologo -v quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = 'artifacts\release'
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item artifacts\publish\win-x64\VeriCat.exe "$out\VeriCat.exe" -Force
$hash = (Get-FileHash "$out\VeriCat.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$out\VeriCat.exe.sha256" "$hash  VeriCat.exe" -NoNewline -Encoding ascii

Write-Host "Hazır: $(Resolve-Path $out\VeriCat.exe) (v$Version)"
