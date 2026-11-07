# Instalador (MSI · WiX 4.0.5)

Estado: el workflow instala WiX 4.0.5 y la extensión Util 4.0.5; la compilación del MSI de esta revisión debe confirmarse en CI y probarse en una VM Windows 10/11 antes de distribuirla. WiX 7 no es compatible con esta configuración y requiere aceptar por separado su EULA OSMF.

## Qué instala

| Componente | Dónde | Cómo |
|---|---|---|
| Servicio `Agent.Service` | `Program Files\DriverUpdater\Agent.Service.exe` | Servicio de Windows `DriverUpdater`, cuenta `LocalSystem`, **arranque bajo demanda** (no se ejecuta al encender el equipo). La UI lo inicia al abrirse; el instalador concede a usuarios interactivos el permiso de iniciar y detener el servicio, pero no de configurarlo |
| UI `Ui.Wpf` | `Program Files\DriverUpdater\Ui.Wpf.exe` | Acceso directo en el menú Inicio; se ejecuta sin elevación |

Al desinstalar, el servicio se detiene y se elimina antes de borrar los ficheros.

## Compilar

```powershell
dotnet tool install --global wix --version 4.0.5
wix extension add -g WixToolset.Util.wixext/4.0.5
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

Comprobar: después de abrir la UI, esta inicia el servicio `DriverUpdater` y conecta ("Servicio activo").
Al desinstalar de forma interactiva, aparece una opción desmarcada por defecto para borrar
`%ProgramData%\DriverUpdater` (historial, ajustes y registros). Si no se marca, los datos se conservan;
la desinstalación silenciosa también los conserva. Las actualizaciones de versión nunca muestran la
opción ni eliminan los datos. Probar ambos resultados, además de confirmar que se detiene y elimina el
servicio y los archivos de `Program Files`.

## Pendiente

- Revisar la decisión de permiso `RPWP` para `IU` (iniciar/detener el servicio sin elevación) con el equipo de seguridad.
- Asistente gráfico: añadir la extensión `WixToolset.UI` (ahora la instalación es silenciosa por defecto).
- Firma de código del MSI y de los `.exe` (`signtool`), requisito del Hito 4.
- Revisar el SBOM y los avisos generados por versión antes de cada publicación.
- La limpieza opcional del desinstalador cubre `%ProgramData%\DriverUpdater`; los logs de la CLI guardados en el directorio desde el que se ejecuta quedan fuera de esa carpeta.
- Actualizaciones automáticas (autoactualización) quedan fuera de este MSI.
