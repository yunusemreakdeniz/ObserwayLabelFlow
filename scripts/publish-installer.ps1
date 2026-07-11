#Requires -Version 5.1
<#
.SYNOPSIS
  Obserway LabelFlow yayın paketi ve Inno Setup kurulum dosyası üretir.

.PARAMETER Version
  Kurulum sürüm numarası. Belirtilmezse csproj'dan okunur.

.PARAMETER SkipInstaller
  Sadece dotnet publish çalıştırır, Inno Setup derlemez.

.EXAMPLE
  .\scripts\publish-installer.ps1

.EXAMPLE
  .\scripts\publish-installer.ps1 -Version 1.0.1
#>
param(
    [string] $Version,
    [switch] $SkipInstaller
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "ObserwayLabelFlow.App\ObserwayLabelFlow.App.csproj"
$publishDir = Join-Path $repoRoot "publish\ObserwayLabelFlow"
$distDir = Join-Path $repoRoot "dist"
$installerScript = Join-Path $repoRoot "installer\ObserwayLabelFlow.iss"
$redistDir = Join-Path $repoRoot "installer\redist"
$webView2Bootstrapper = Join-Path $redistDir "MicrosoftEdgeWebview2Setup.exe"
$webView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"

function Get-ProjectVersion {
    param([string] $ProjectPath)

    [xml] $xml = Get-Content -Path $ProjectPath
    $version = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "ObserwayLabelFlow.App.csproj içinde <Version> bulunamadı."
    }

    return $version.Trim()
}

function Find-InnoSetupCompiler {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

if (-not (Test-Path $appProject)) {
    throw "Proje dosyası bulunamadı: $appProject"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -ProjectPath $appProject
}

Write-Host "Sürüm: $Version" -ForegroundColor Cyan
Write-Host "Publish hedefi: $publishDir" -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

Write-Host "`n[1/3] dotnet publish..." -ForegroundColor Yellow
dotnet publish $appProject `
    -c Release `
    -p:PublishProfile=win-x64-release `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion=$Version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish başarısız oldu."
}

$exePath = Join-Path $publishDir "ObserwayLabelFlow.exe"
if (-not (Test-Path $exePath)) {
    throw "Beklenen exe bulunamadı: $exePath"
}

Write-Host "Publish tamamlandı: $exePath" -ForegroundColor Green

if ($SkipInstaller) {
    Write-Host "SkipInstaller aktif; kurulum dosyası üretilmedi." -ForegroundColor Yellow
    exit 0
}

Write-Host "`n[2/3] WebView2 bootstrapper kontrolü..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $redistDir -Force | Out-Null

if (-not (Test-Path $webView2Bootstrapper)) {
    Write-Host "WebView2 bootstrapper indiriliyor..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $webView2Url -OutFile $webView2Bootstrapper -UseBasicParsing
}

Write-Host "`n[3/3] Inno Setup derleniyor..." -ForegroundColor Yellow
$iscc = Find-InnoSetupCompiler
if ($null -eq $iscc) {
    Write-Host @"

Inno Setup 6 bulunamadı — publish klasörü hazır, kurulum dosyası üretilmedi.
Kurulum: https://jrsoftware.org/isdl.php

Publish: $publishDir
Manuel derleme:
  `"<Inno Setup>\ISCC.exe`" /DMyAppVersion=$Version `"$installerScript`"
"@ -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null

& $iscc "/DMyAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup derlemesi başarısız oldu."
}

$setupFile = Join-Path $distDir "ObserwayLabelFlow-Setup-$Version.exe"
Write-Host "`nKurulum dosyası hazır:" -ForegroundColor Green
Write-Host $setupFile
