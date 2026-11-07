<#
.SYNOPSIS
  Publica el servicio y la UI, y compila el MSI de DriverUpdater con WiX v4.
.EXAMPLE
  .\installer\build.ps1 -Version 1.0.0
  Requisitos: .NET 10 SDK, WiX 4.0.5 y WixToolset.Util.wixext 4.0.5.
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
New-Item -ItemType Directory -Path $art -Force | Out-Null

# Un solo .exe autocontenido por aplicación (no hace falta instalar .NET en el equipo)
dotnet publish (Join-Path $root 'src\Agent.Service\Agent.Service.csproj') -c Release -r $Runtime `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $svcDir
if ($LASTEXITCODE -ne 0) { throw "publish Agent.Service falló" }

dotnet publish (Join-Path $root 'src\Ui.Wpf\Ui.Wpf.csproj') -c Release -r $Runtime `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $uiDir
if ($LASTEXITCODE -ne 0) { throw "publish Ui.Wpf falló" }

$complianceScript = Join-Path $PSScriptRoot 'Generate-ComplianceArtifacts.ps1'
& $complianceScript -Root $root -OutputDirectory $art -Version $Version -Runtime $Runtime
if ($LASTEXITCODE -ne 0) { throw "La generación del SBOM o los avisos de terceros falló" }

$msi = Join-Path $art "DriverUpdater-$Version-x64.msi"

# Diagnóstico de WiX v4 con salida visible (sin silenciar errores)
Write-Host "== wix --version"
$wixVersion = (wix --version 2>&1 | Out-String).Trim()
Write-Host $wixVersion
if ($LASTEXITCODE -ne 0) { throw "No se pudo consultar la versión de WiX." }
if ($wixVersion -notmatch '^4\.') {
    throw "Se requiere WiX v4 para este proyecto (WiX v7 requiere aceptar la EULA OSMF). Instala WiX 4.0.5: dotnet tool install --global wix --version 4.0.5"
}
Write-Host "== wix extension list -g"
$extensionList = wix extension list -g 2>&1
$extensionListCode = $LASTEXITCODE
$extensionList | ForEach-Object { Write-Host $_ }
if ($extensionListCode -ne 0) {
    throw "No se pudo consultar la lista global de extensiones WiX (código $extensionListCode)."
}
if (($extensionList -join "`n") -notmatch 'WixToolset\.Util\.wixext') {
    throw "Falta WixToolset.Util.wixext 4.0.5. Instalala con: wix extension add -g WixToolset.Util.wixext/4.0.5"
}
Write-Host "== wix build"
$wixOut = wix build (Join-Path $PSScriptRoot 'DriverUpdater.wxs') -arch x64 `
    -ext WixToolset.Util.wixext `
    -d "Version=$Version" -d "AgentServiceDir=$svcDir" -d "UiDir=$uiDir" `
    -d "ProjectLicenseFile=$(Join-Path $root 'LICENSE')" `
    -d "ThirdPartyNoticesFile=$(Join-Path $art 'THIRD-PARTY-NOTICES.txt')" `
    -d "ApacheLicenseFile=$(Join-Path $PSScriptRoot 'licenses\Apache-2.0.txt')" `
    -d "BsdLicenseFile=$(Join-Path $PSScriptRoot 'licenses\BSD-3-Clause.txt')" `
    -d "SbomFile=$(Join-Path $art "DriverUpdater-$Version.cdx.json")" -o $msi 2>&1
$wixCode = $LASTEXITCODE
$wixOut | ForEach-Object { Write-Host $_ }
if ($wixCode -ne 0) { throw "wix build falló (código $wixCode):`n$($wixOut -join [Environment]::NewLine)" }

Write-Host "MSI generado: $msi"
Write-Host "Probar SOLO en una VM (AGENTS.md): msiexec /i `"$msi`" /l*v install.log"
