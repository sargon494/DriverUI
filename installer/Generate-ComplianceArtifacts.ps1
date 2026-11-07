param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Runtime
)

$ErrorActionPreference = 'Stop'

function Get-ProjectComponents {
    param(
        [Parameter(Mandatory)] [string] $AssetsPath,
        [Parameter(Mandatory)] [string] $Framework
    )

    if (-not (Test-Path $AssetsPath)) {
        throw "No se encontro el archivo de dependencias restauradas: $AssetsPath"
    }

    $assets = Get-Content $AssetsPath -Raw | ConvertFrom-Json
    $targetName = "$Framework/$Runtime"
    $targetProperty = $assets.targets.PSObject.Properties[$targetName]
    if ($null -eq $targetProperty) {
        throw "No se encontro el destino restaurado '$targetName' en $AssetsPath. Publica el proyecto para $Runtime antes de generar el SBOM."
    }

    $packageFolders = @($assets.packageFolders.PSObject.Properties | ForEach-Object { $_.Name })
    if ($packageFolders.Count -eq 0) {
        throw "No se encontro ninguna carpeta de paquetes NuGet en $AssetsPath."
    }

    $components = @()
    foreach ($targetEntry in $targetProperty.Value.PSObject.Properties) {
        $library = $assets.libraries.PSObject.Properties[$targetEntry.Name]
        if ($null -eq $library -or $library.Value.type -ne 'package') {
            continue
        }

        $runtimeAssets = $targetEntry.Value.runtime
        $nativeAssets = $targetEntry.Value.native
        $runtimeTargetAssets = $targetEntry.Value.runtimeTargets
        if ($null -eq $runtimeAssets -and $null -eq $nativeAssets -and $null -eq $runtimeTargetAssets) {
            continue
        }

        $parts = $targetEntry.Name.Split('/')
        if ($parts.Count -ne 2) {
            throw "Identificador de paquete inesperado: $($targetEntry.Name)"
        }

        $id = $parts[0]
        $packageVersion = $parts[1]
        $packagePath = $library.Value.path
        if ([string]::IsNullOrWhiteSpace($packagePath)) {
            throw "No se encontro la ruta NuGet para $($targetEntry.Name)."
        }

        $packageRoot = Join-Path $packageFolders[0] $packagePath
        $nuspecPath = Join-Path $packageRoot "$($id.ToLowerInvariant()).nuspec"
        if (-not (Test-Path $nuspecPath)) {
            $nuspecPath = Get-ChildItem $packageRoot -Filter '*.nuspec' -File | Select-Object -First 1 -ExpandProperty FullName
        }
        if ([string]::IsNullOrWhiteSpace($nuspecPath) -or -not (Test-Path $nuspecPath)) {
            throw "No se encontro el manifiesto NuGet para $($targetEntry.Name)."
        }

        [xml] $nuspec = Get-Content $nuspecPath -Raw
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
        $licenseExpression = if ($null -ne $licenseNode) { $licenseNode.InnerText.Trim() } else { '' }
        if ([string]::IsNullOrWhiteSpace($licenseExpression)) {
            throw "El paquete $($targetEntry.Name) no declara una licencia en su manifiesto NuGet; revisa manualmente antes de distribuir."
        }

        $nupkgPath = Join-Path $packageRoot "$($id.ToLowerInvariant()).$packageVersion.nupkg"
        if (-not (Test-Path $nupkgPath)) {
            $nupkgPath = Get-ChildItem $packageRoot -Filter '*.nupkg' -File | Select-Object -First 1 -ExpandProperty FullName
        }
        if ([string]::IsNullOrWhiteSpace($nupkgPath) -or -not (Test-Path $nupkgPath)) {
            throw "No se encontro el archivo .nupkg para calcular el hash de $($targetEntry.Name)."
        }

        $licenseUrlNode = $metadata.SelectSingleNode("*[local-name()='licenseUrl']")
        $projectUrlNode = $metadata.SelectSingleNode("*[local-name()='projectUrl']")
        $authorsNode = $metadata.SelectSingleNode("*[local-name()='authors']")
        $licenseUrl = if ($null -ne $licenseUrlNode) {
            $licenseUrlNode.InnerText.Trim()
        } elseif ($licenseExpression -match '^[A-Za-z0-9.-]+$') {
            "https://spdx.org/licenses/$licenseExpression.html"
        } else {
            ''
        }

        $components += [pscustomobject]@{
            Id = $id
            Version = $packageVersion
            License = $licenseExpression
            LicenseUrl = $licenseUrl
            Authors = if ($null -ne $authorsNode) { $authorsNode.InnerText.Trim() } else { '' }
            Copyright = if ($null -ne $metadata.SelectSingleNode("*[local-name()='copyright']")) {
                $metadata.SelectSingleNode("*[local-name()='copyright']").InnerText.Trim()
            } else {
                ''
            }
            ProjectUrl = if ($null -ne $projectUrlNode) { $projectUrlNode.InnerText.Trim() } else { '' }
            PackageUrl = "pkg:nuget/$id@$packageVersion"
            Hash = (Get-FileHash $nupkgPath -Algorithm SHA256).Hash.ToLowerInvariant()
            Dependencies = @(
                if ($null -ne $targetEntry.Value.dependencies) {
                    foreach ($dependency in $targetEntry.Value.dependencies.PSObject.Properties) {
                        "pkg:nuget/$($dependency.Name.ToLowerInvariant())@$($dependency.Value)"
                    }
                }
            )
        }
    }

    return $components
}

$serviceAssets = Join-Path $Root 'src\Agent.Service\obj\project.assets.json'
$uiAssets = Join-Path $Root 'src\Ui.Wpf\obj\project.assets.json'
$serviceComponents = Get-ProjectComponents -AssetsPath $serviceAssets -Framework 'net10.0-windows'
$uiComponents = Get-ProjectComponents -AssetsPath $uiAssets -Framework 'net10.0-windows'

$packageComponents = @()
foreach ($group in @($serviceComponents + $uiComponents | Group-Object { "$($_.Id.ToLowerInvariant())/$($_.Version)" })) {
    $package = $group.Group[0]
    $packageComponents += [pscustomobject]@{
        Id = $package.Id
        Version = $package.Version
        License = $package.License
        LicenseUrl = $package.LicenseUrl
        Authors = $package.Authors
        Copyright = $package.Copyright
        ProjectUrl = $package.ProjectUrl
        PackageUrl = $package.PackageUrl
        Hash = $package.Hash
        Dependencies = @($group.Group.Dependencies | Sort-Object -Unique)
    }
}
$packageComponents = @($packageComponents | Sort-Object Id, Version)

$knownLicenses = @('MIT', 'Apache-2.0', 'BSD-3-Clause')
foreach ($package in $packageComponents) {
    $licenseIds = $package.License -split '\s+(AND|OR|WITH)\s+|\(|\)' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -notin @('AND', 'OR', 'WITH') }
    foreach ($licenseId in $licenseIds) {
        if ($licenseId -notin $knownLicenses) {
            throw "La licencia '$($package.License)' declarada por $($package.Id) no tiene texto incluido. Anade y revisa el aviso de licencia antes de distribuir."
        }
    }
}

$runtimeFrameworks = @()
foreach ($app in @(
        @{ Name = 'Agent.Service'; Project = 'Agent.Service' },
        @{ Name = 'Ui.Wpf'; Project = 'Ui.Wpf' }
    )) {
    $runtimeConfigPath = Join-Path $Root "src\$($app.Project)\bin\Release\net10.0-windows\$Runtime\$($app.Name).runtimeconfig.json"
    if (-not (Test-Path $runtimeConfigPath)) {
        throw "No se encontro el runtimeconfig publicado: $runtimeConfigPath"
    }

    $runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
        $runtimeFrameworks += [pscustomobject]@{
            Name = $framework.name
            Version = $framework.version
        }
    }
}
$runtimeFrameworks = @($runtimeFrameworks |
    Group-Object { "$($_.Name)/$($_.Version)" } |
    ForEach-Object { $_.Group[0] } |
    Sort-Object Name, Version)

$components = @()
$serviceReference = "pkg:generic/DriverUpdater.Agent.Service@$Version"
$uiReference = "pkg:generic/DriverUpdater.Ui.Wpf@$Version"
foreach ($application in @(
        @{ Name = 'Agent.Service'; Reference = $serviceReference; Packages = $serviceComponents },
        @{ Name = 'Ui.Wpf'; Reference = $uiReference; Packages = $uiComponents }
    )) {
    $components += @{
        type = 'application'
        name = $application.Name
        version = $Version
        'bom-ref' = $application.Reference
        licenses = @(@{ license = @{ id = 'MIT' } })
    }
}
foreach ($package in $packageComponents) {
    $licenses = @()
    if ($package.License -match '^[A-Za-z0-9.-]+$') {
        $licenses = @(@{ license = @{ id = $package.License } })
    } else {
        $licenses = @(@{ license = @{ expression = $package.License } })
    }

    $components += @{
        type = 'library'
        name = $package.Id
        version = $package.Version
        'bom-ref' = $package.PackageUrl
        purl = $package.PackageUrl
        licenses = $licenses
        hashes = @(@{ alg = 'SHA-256'; content = $package.Hash })
    }
}
$frameworkReferences = @{}
foreach ($framework in $runtimeFrameworks) {
    $frameworkReference = "pkg:generic/$($framework.Name)@$($framework.Version)"
    $frameworkReferences[$framework.Name] = $frameworkReference
    $components += @{
        type = 'framework'
        name = $framework.Name
        version = $framework.Version
        'bom-ref' = $frameworkReference
        purl = $frameworkReference
        licenses = @(@{ license = @{ id = 'MIT' } })
    }
}

$rootReference = "pkg:generic/DriverUpdater@$Version"
$dependencies = @(
    @{
        ref = $rootReference
        dependsOn = @($serviceReference, $uiReference)
    },
    @{
        ref = $serviceReference
        dependsOn = @(
            @($serviceComponents | ForEach-Object { $_.PackageUrl }) +
            @($runtimeFrameworks | ForEach-Object { $frameworkReferences[$_.Name] })
        )
    },
    @{
        ref = $uiReference
        dependsOn = @(
            @($uiComponents | ForEach-Object { $_.PackageUrl }) +
            @($runtimeFrameworks | ForEach-Object { $frameworkReferences[$_.Name] })
        )
    }
)
foreach ($package in $packageComponents) {
    $dependencies += @{
        ref = $package.PackageUrl
        dependsOn = @($package.Dependencies | Where-Object {
            $dependencyRef = $_
            $packageComponents.PackageUrl -contains $dependencyRef
        })
    }
}
foreach ($framework in $runtimeFrameworks) {
    $dependencies += @{
        ref = $frameworkReferences[$framework.Name]
        dependsOn = @()
    }
}

$bom = @{
    bomFormat = 'CycloneDX'
    specVersion = '1.5'
    serialNumber = "urn:uuid:$([guid]::NewGuid())"
    version = 1
    metadata = @{
        timestamp = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        component = @{
            type = 'application'
            name = 'DriverUpdater'
            version = $Version
            'bom-ref' = $rootReference
            licenses = @(@{ license = @{ id = 'MIT' } })
        }
        properties = @(
            @{ name = 'build.runtime'; value = $Runtime },
            @{ name = 'build.service'; value = 'Agent.Service' },
            @{ name = 'build.ui'; value = 'Ui.Wpf' }
        )
    }
    components = $components
    dependencies = $dependencies
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$bomPath = Join-Path $OutputDirectory "DriverUpdater-$Version.cdx.json"
$bom | ConvertTo-Json -Depth 30 | Set-Content -Path $bomPath -Encoding UTF8

$noticePath = Join-Path $OutputDirectory 'THIRD-PARTY-NOTICES.txt'
$noticeLines = [System.Collections.Generic.List[string]]::new()
$noticeLines.Add('DriverUpdater third-party components')
$noticeLines.Add("Release: $Version")
$noticeLines.Add("Target runtime: $Runtime")
$noticeLines.Add('')
$noticeLines.Add('DriverUpdater is licensed under MIT. This file lists NuGet packages resolved with runtime assets for the service and UI, and their license expressions as declared in the corresponding NuGet package metadata. Package licenses and notices remain those of their respective authors.')
$noticeLines.Add('')
$noticeLines.Add('NuGet runtime dependencies')
$noticeLines.Add('==========================')
foreach ($package in $packageComponents) {
    $entry = "$($package.Id) $($package.Version) - $($package.License)"
    if (-not [string]::IsNullOrWhiteSpace($package.Copyright)) {
        $entry += " - $($package.Copyright)"
    }
    if (-not [string]::IsNullOrWhiteSpace($package.Authors)) {
        $entry += " - Authors: $($package.Authors)"
    }
    if (-not [string]::IsNullOrWhiteSpace($package.ProjectUrl)) {
        $entry += " - $($package.ProjectUrl)"
    }
    if (-not [string]::IsNullOrWhiteSpace($package.LicenseUrl)) {
        $entry += " - License: $($package.LicenseUrl)"
    }
    $noticeLines.Add($entry)
}
$noticeLines.Add('')
$noticeLines.Add('License texts')
$noticeLines.Add('=============')
$licenseFiles = @{
    'Apache-2.0' = Join-Path $PSScriptRoot 'licenses\Apache-2.0.txt'
    'BSD-3-Clause' = Join-Path $PSScriptRoot 'licenses\BSD-3-Clause.txt'
}
$declaredLicenses = @($packageComponents.License | Sort-Object -Unique)
if ($declaredLicenses | Where-Object { $_ -match '(^|[^A-Za-z0-9.-])MIT([^A-Za-z0-9.-]|$)' }) {
    $noticeLines.Add('')
    $noticeLines.Add('===== MIT (project license; third-party copyright holders are listed above) =====')
    $noticeLines.Add((Get-Content (Join-Path $Root 'LICENSE') -Raw))
}
foreach ($licenseId in @('Apache-2.0', 'BSD-3-Clause')) {
    if (-not ($declaredLicenses | Where-Object { $_ -match [regex]::Escape($licenseId) })) {
        continue
    }

    $licenseFile = $licenseFiles[$licenseId]
    if (-not (Test-Path $licenseFile)) {
        throw "No se encontro el texto de licencia requerido: $licenseFile"
    }
    $licenseText = Get-Content $licenseFile -Raw
    if ($licenseId -eq 'BSD-3-Clause') {
        $copyrights = @($packageComponents |
            Where-Object { $_.License -match 'BSD-3-Clause' } |
            ForEach-Object { $_.Copyright } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique)
        if ($copyrights.Count -eq 0) {
            throw 'Los paquetes BSD-3-Clause no declaran titulares de copyright en sus manifiestos NuGet.'
        }
        $licenseText = $licenseText.Replace('Copyright (c) <copyright notice above>', ($copyrights -join "`r`n"))
    }
    $noticeLines.Add('')
    $noticeLines.Add("===== $licenseId =====")
    $noticeLines.Add($licenseText)
}
$noticeLines.Add('')
$noticeLines.Add('Bundled .NET runtime notices')
$noticeLines.Add('============================')

$dotnetRoot = Split-Path (Get-Command dotnet -ErrorAction Stop).Source
foreach ($noticeFile in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    $sourcePath = Join-Path $dotnetRoot $noticeFile
    if (-not (Test-Path $sourcePath)) {
        throw "No se encontro el aviso redistribuible del runtime .NET: $sourcePath"
    }
    $noticeLines.Add('')
    $noticeLines.Add("===== .NET $noticeFile =====")
    $noticeLines.Add((Get-Content $sourcePath -Raw))
}
$noticeLines | Set-Content -Path $noticePath -Encoding UTF8

Write-Host "SBOM CycloneDX generado: $bomPath ($($packageComponents.Count) paquetes NuGet y $($runtimeFrameworks.Count) frameworks .NET)"
Write-Host "Avisos de terceros generados: $noticePath"
