<#
.SYNOPSIS
  Publica el servicio y la UI, y compila el MSI de DriverUpdater con WiX v4.
.EXAMPLE
  .\installer\build.ps1 -Version 1.0.0
  Requisitos: .NET 10 SDK y WiX v4 (dotnet tool install --global wix).
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$art  = Join-Path $PSScriptRoot 'artifacts'
$svcDir = Join-Path $art 'service'
$uiDir  = Join-Path $art 'ui'
Remove-Item $art -Recurse -Force -ErrorAction SilentlyContinue

# Un solo .exe autocontenido por aplicación (no hace falta instalar .NET en el equipo)
dotnet publish (Join-Path $root 'src\Agent.Service\Agent.Service.csproj') -c Release -r $Runtime `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $svcDir
if ($LASTEXITCODE -ne 0) { throw "publish Agent.Service falló" }

dotnet publish (Join-Path $root 'src\Ui.Wpf\Ui.Wpf.csproj') -c Release -r $Runtime `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $uiDir
if ($LASTEXITCODE -ne 0) { throw "publish Ui.Wpf falló" }

$msi = Join-Path $art "DriverUpdater-$Version-x64.msi"

# Diagnóstico: versión de WiX y aceptación de la EULA, con salida visible (sin silenciar errores)
Write-Host "== wix --version"
wix --version
Write-Host "== wix eula accept wix7"
wix eula accept wix7
Write-Host "== wix build"
$wixOut = wix build (Join-Path $PSScriptRoot 'DriverUpdater.wxs') -arch x64 `
    -d "Version=$Version" -d "AgentServiceDir=$svcDir" -d "UiDir=$uiDir" -o $msi 2>&1
$wixCode = $LASTEXITCODE
$wixOut | ForEach-Object { Write-Host $_ }
if ($wixCode -ne 0) { throw "wix build falló (código $wixCode):`n$($wixOut -join [Environment]::NewLine)" }

Write-Host "MSI generado: $msi"
Write-Host "Probar SOLO en una VM (AGENTS.md): msiexec /i `"$msi`" /l*v install.log"
