# Instalador (MSI · WiX v4)

Estado: **fuentes escritas, sin compilar** (el entorno de desarrollo que las generó no tiene .NET ni WiX).
Hay que compilarlas y probarlas en una VM Windows 10/11 antes de usarlas.

## Qué instala

| Componente | Dónde | Cómo |
|---|---|---|
| Servicio `Agent.Service` | `Program Files\DriverUpdater\Agent.Service.exe` | Servicio de Windows `DriverUpdater`, cuenta `LocalSystem`, **arranque bajo demanda** (no se ejecuta al encender el equipo). La UI lo inicia al abrirse; el instalador concede a usuarios interactivos el permiso de iniciar y detener el servicio, pero no de configurarlo |
| UI `Ui.Wpf` | `Program Files\DriverUpdater\Ui.Wpf.exe` | Acceso directo en el menú Inicio; se ejecuta sin elevación |

Al desinstalar, el servicio se detiene y se elimina antes de borrar los ficheros.

## Compilar

```powershell
dotnet tool install --global wix          # WiX v4
.\installer\build.ps1 -Version 1.0.0      # genera installer\artifacts\DriverUpdater-1.0.0-x64.msi
```

El build también genera `DriverUpdater-<versión>.cdx.json` (SBOM CycloneDX 1.5)
y `THIRD-PARTY-NOTICES.txt` en `installer\artifacts`. Ambos, junto con la licencia
MIT del proyecto y los textos Apache-2.0/BSD-3-Clause que correspondan, se
incluyen en `Program Files\DriverUpdater` dentro del MSI.
El SBOM se deriva de las dependencias NuGet resueltas para los ejecutables y de
los frameworks .NET incluidos. Los avisos NuGet identifican los paquetes y sus
licencias declaradas; revisa los avisos y licencias de cada paquete antes de
publicar una versión.

## Probar (solo en VM)

```powershell
msiexec /i installer\artifacts\DriverUpdater-1.0.0-x64.msi /l*v install.log
```

Comprobar: el servicio `DriverUpdater` está en marcha, la UI abre y conecta ("Servicio activo"), y el
desinstalador (`msiexec /x`) deja el sistema sin restos en Program Files ni servicio registrado.

## Pendiente

- Revisar la decisión de permiso `RPWP` para `IU` (iniciar/detener el servicio sin elevación) con el equipo de seguridad.
- Asistente gráfico: añadir la extensión `WixToolset.UI` (ahora la instalación es silenciosa por defecto).
- Firma de código del MSI y de los `.exe` (`signtool`), requisito del Hito 4.
- Revisar el SBOM y los avisos generados por versión antes de cada publicación.
- `%ProgramData%\DriverUpdater` (historial, logs y configuración) no se borra al desinstalar; el usuario debe decidir si elimina esos datos. Consulta [PRIVACY.md](../PRIVACY.md).
- Actualizaciones automáticas (autoactualización) quedan fuera de este MSI.
