<#
  Unity Actions lisansı ayarlamadan, kendi bilgisayarından sürüm yayınlamak için.

  Gerekenler: Unity 6000.4.7f1, .NET SDK 8, GitHub CLI (gh) -> `gh auth login` yapılmış olmalı.
  Kullanım:   powershell -ExecutionPolicy Bypass -File Tools\release.ps1 -Version v0.2.0
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

$slug = gh repo view --json nameWithOwner -q .nameWithOwner
Write-Host "Depo: $slug"

Write-Host "1/3 Oyun derleniyor (birkaç dakika sürebilir)..."
& $UnityExe -batchmode -quit -projectPath . -executeMethod Noctyra.EditorTools.BuildScript.BuildWindows -logFile Builds\unity-build.log
if ($LASTEXITCODE -ne 0) { throw "Unity derlemesi başarısız. Builds\unity-build.log dosyasına bak." }

Write-Host "2/3 Kurulum aracı derleniyor..."
dotnet build Launcher\NoctyraSetup.csproj -c Release -p:RepoSlug=$slug
if ($LASTEXITCODE -ne 0) { throw "Setup derlemesi başarısız." }

Write-Host "3/3 GitHub Release oluşturuluyor: $Version"
gh release create $Version `
    "Builds\Noctyra-win64.zip" `
    "Launcher\bin\Release\net48\NoctyraSetup.exe" `
    --title "Noctyra $Version" --generate-notes

Write-Host "Tamam! Release sayfası: https://github.com/$slug/releases/tag/$Version"
