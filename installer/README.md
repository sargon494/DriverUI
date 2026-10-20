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
- `%ProgramData%\DriverUpdater` (logs y configuración) no se borra al desinstalar; decidir si debe borrarse.
- Actualizaciones automáticas (autoactualización) quedan fuera de este MSI.
