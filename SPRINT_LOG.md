# Registro de Sprints — DriverUpdater

Este documento registra de forma cronológica y detallada cada sprint ejecutado en el proyecto **DriverUpdater**, con las tareas completadas, decisiones de arquitectura, resultados de pruebas, problemas resueltos y notas esenciales para los siguientes agentes.

---

## Sprint 0 — Preparación del entorno y estructura base

- **Fecha**: 2026-10-12
- **Estado**: ✅ Completado
- **Hito**: Hito 0 (Fundamentos e infraestructura)

### Tareas completadas
- [x] **S0-T1: Verificación e integración de .NET 10 SDK** — SDK .NET 10.0.401 (LTS) verificado en el sistema (`C:\Program Files\dotnet\dotnet.exe`).
- [x] **S0-T2: Configuración de `global.json`** — Fijada la versión `10.0.401` con `rollForward: "latestFeature"`.
- [x] **S0-T3: Propiedades compartidas con `Directory.Build.props`** — Habilitado `Nullable: enable`, `ImplicitUsings: enable`, `LangVersion: 14`, `TreatWarningsAsErrors: true`, `AnalysisLevel: latest-recommended` y `EnforceCodeStyleInBuild: true`.
- [x] **S0-T4: Creación de solución y los 7 proyectos**:
  - `src/Core` (`net10.0`, biblioteca agnóstica de plataforma sin dependencias de Windows).
  - `src/Agent.Drivers` (`net10.0-windows`, acceso a subsistemas de Windows: WMI, COM, WinVerifyTrust).
  - `src/Agent.Service` (`net10.0-windows`, Worker Service para Windows Service en cuenta SYSTEM).
  - `src/Ui.Wpf` (`net10.0-windows`, interfaz de usuario en WPF con MVVM).
  - `src/Cli` (`net10.0-windows`, herramienta de diagnóstico y desarrollo por consola).
  - `tests/Core.Tests` (`net10.0`, tests unitarios con xUnit, FluentAssertions, NSubstitute).
  - `tests/Agent.Drivers.Tests` (`net10.0-windows`, tests de integración de hardware y subsistemas).
- [x] **S0-T5: Referencias entre proyectos** — Establecidas conforme a la arquitectura (`Agent.Service` -> `Agent.Drivers`, `Core`; `Ui.Wpf` -> `Core`; `Cli` -> `Agent.Drivers`, `Core`; proyectos de tests referenciando sus respectivos objetivos).
- [x] **S0-T6: Dependencias y paquetes NuGet esenciales**:
  - `Cli`: `System.CommandLine`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Console` (6.1.1), `Serilog.Sinks.File` (7.0.0).
  - `Agent.Service`: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Grpc.AspNetCore` (2.76.0), `Serilog.Extensions.Logging`, `Serilog.Sinks.File`.
  - `Agent.Drivers`: `System.Management`, `Microsoft.Data.Sqlite`, `Serilog.Extensions.Logging`, `Serilog.Sinks.File`.
  - `Ui.Wpf`: `CommunityToolkit.Mvvm`, `Serilog.Extensions.Logging`.
  - `Core.Tests` y `Agent.Drivers.Tests`: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `NSubstitute`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`.
- [x] **S0-T7: Reglas de estilo y calidad (.editorconfig & .gitignore)**:
  - `.gitignore` estándar para .NET.
  - `.editorconfig` con formato, estilo y supresión de `CA1848` (no exigir generadores LoggerMessage para logging convencional) y `CA1707` en proyectos de prueba (permitir nombres de test legibles con guiones bajos).
- [x] **S0-T8: Verificación completa de compilación y pruebas**:
  - `dotnet build -c Release` compila con 0 errores y 0 warnings.
  - `dotnet test` ejecuta suites de prueba en verde.
  - `dotnet format --verify-no-changes` validado con 0 discrepancias de estilo.

### Decisiones técnicas tomadas
1. **Versiones de paquetes NuGet**:
   - `Serilog.Sinks.Console` se ajustó a `6.1.1` (la versión 7.0.0 no existe aún en NuGet).
   - `Grpc.AspNetCore` se fijó en `2.76.0` para evitar el warning NU1603 (que se convertía en error por `TreatWarningsAsErrors`).
2. **Convención de tests**:
   - En `.editorconfig`, se configuró `dotnet_diagnostic.CA1707.severity = none` para `[tests/**.cs]`, permitiendo la convención estándar `Should_Action_When_Condition` en métodos de prueba.

### Pruebas ejecutadas
- `dotnet build -c Release`: 7 proyectos compilados, 0 warnings, 0 errores.
- `dotnet test`: 2/2 tests ejecutados y pasados (smoke tests en `Core.Tests` y `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 diferencias detectadas.

### Notas para el siguiente sprint (Sprint 1)
- El siguiente sprint es **Sprint 1 — Modelos, interfaces y lógica pura (`Core`)**.
- Toda la lógica debe ir en `src/Core` sin dependencias de Windows (`net10.0`).
- Debe incluir modelos: `HardwareId`, `InstalledDriver`, `DriverUpdate`, `InstallResult`, `OperationLogEntry`, `DriverStatus`.
- Interfaces: `IDriverInventory`, `IUpdateSource`, `ISignatureVerifier`, `IRestorePointService`, `IDriverInstaller`.
- Servicios de lógica pura: `VersionComparer` (comparador numérico por segmentos) y `DriverMatcher`.
- Suite completa de tests unitarios en `tests/Core.Tests`.

---

## Sprint 1 — Modelos, interfaces y lógica pura (`Core`)

- **Fecha**: 2026-10-12
- **Estado**: ✅ Completado
- **Hito**: Hito 0 (Lógica de dominio aislada)

### Tareas completadas
- [x] **S1-T1: Modelo `HardwareId`** (`src/Core/Models/HardwareId.cs`):
  - Inmutable (`record`), encapsula el identificador PnP (ej: `PCI\VEN_8086&DEV_9A49&REV_03`).
  - Comparación insensible a mayúsculas/minúsculas (`Matches(HardwareId)` y `Matches(string)`).
- [x] **S1-T2: Modelo `InstalledDriver` y `DriverStatus`** (`src/Core/Models/`):
  - `InstalledDriver`: dispositivo, fabricante, versión, fecha, inf, hardware IDs, estado y clase.
  - `DriverStatus`: Ok, Error, Degraded, Unknown.
- [x] **S1-T3: Modelo `DriverUpdate`** (`src/Core/Models/DriverUpdate.cs`):
  - Id, título, descripción, versión nueva/actual, hardware ID coincidente, tamaño en bytes, obligatoriedad y fecha de lanzamiento.
- [x] **S1-T4: Modelo `InstallResult`** (`src/Core/Models/InstallResult.cs`):
  - Éxito, mensaje de error, flag de reinicio requerido, versiones previa y aplicada, timestamp.
- [x] **S1-T5: Modelo `OperationLogEntry`** (`src/Core/Models/OperationLogEntry.cs`):
  - Registro de auditoría para operaciones (Install, Rollback, Scan).
- [x] **S1-T6: Modelos adicionales e interfaces del dominio**:
  - `SignatureVerificationResult` (`src/Core/Models/SignatureVerificationResult.cs`): validez, firmante, motivo de fallo, flag WHQL.
  - `InstallProgress` y `InstallPhase` (`src/Core/Models/InstallProgress.cs`): fases completas (Downloading, Verifying, CreatingRestorePoint, Installing, Validating, Complete, Failed, RollingBack).
  - `IDriverInventory` (`src/Core/Interfaces/IDriverInventory.cs`): `GetInstalledDriversAsync(ct)`.
  - `IUpdateSource` (`src/Core/Interfaces/IUpdateSource.cs`): `IAsyncEnumerable<DriverUpdate> FindUpdatesAsync(installed, ct)`.
  - `ISignatureVerifier` (`src/Core/Interfaces/ISignatureVerifier.cs`): `VerifyAsync(filePath, ct)`.
  - `IRestorePointService` (`src/Core/Interfaces/IRestorePointService.cs`): `CreateRestorePointAsync(description, ct)`.
  - `IDriverInstaller` (`src/Core/Interfaces/IDriverInstaller.cs`): `InstallAsync(update, progress, ct)` y `RollbackAsync(operationId, ct)`.
  - `IOperationHistory` (`src/Core/Interfaces/IOperationHistory.cs`): `RecordAsync`, `GetAllAsync`, `GetByIdAsync`.
- [x] **S1-T7: Servicio `VersionComparer`** (`src/Core/Services/VersionComparer.cs`):
  - Comparador numérico por segmentos (`ulong`) para versiones tipo `major.minor.build.revision`.
  - Soporta segmentos faltantes como cero (ej: `"10.0"` == `"10.0.0.0"`).
  - Implementa `IComparer<string>` para ordenación con LINQ.
  - Validador sintáctico `IsValid(version)`.
  - Manejo robusto de errores (`ArgumentNullException`, `ArgumentException`, `FormatException`).
- [x] **S1-T8: Servicio `DriverMatcher`** (`src/Core/Services/DriverMatcher.cs`):
  - `FindBestMatch`: evalúa candidatos, cruza hardware IDs con el dispositivo de forma case-insensitive, descarta versiones inferiores o iguales y selecciona la más alta (desempatando por fecha de lanzamiento).
  - `FindAllMatches`: resolución por lotes para colecciones de dispositivos.
- [x] **S1-T9: Batería exhaustiva de tests unitarios** (`tests/Core.Tests/`):
  - `VersionComparerTests`: 10 casos de prueba con InlineData cubriendo pares numéricos, ceros omitidos, formatos no válidos, ordenamiento y excepciones.
  - `DriverMatcherTests`: 8 tests unitarios cubriendo selección óptima, versiones obsoletas, cruce de hardware IDs, desempate por fecha y batch matching.
  - `HardwareIdTests`: 5 tests verificando igualdad case-insensitive, validación de nulos y representación string.

### Decisiones técnicas tomadas
1. **Representación de versiones de drivers**:
   - `VersionComparer` analiza segmentos numéricos usando `ulong` para evitar overflow en builds largas de fabricantes, rellenando con `0UL` los segmentos ausentes cuando se comparan cadenas con diferente cantidad de puntos.
2. **Desempate en DriverMatcher**:
   - Cuando dos actualizaciones candidatas válidas tienen exactamente la misma versión numérica, se prioriza la de fecha de lanzamiento (`ReleaseDate`) más reciente.
3. **Persistencia e inversión de dependencias**:
   - Se definió `IOperationHistory` dentro de `Core.Interfaces` para permitir que el servicio y las pruebas dependan de la abstracción, sin acoplar `Core` a SQLite ni a Windows.

### Pruebas ejecutadas
- `dotnet build -c Release`: 7 proyectos compilados, 0 advertencias, 0 errores.
- `dotnet test -c Release`: 40/40 tests pasados (39 tests en `Core.Tests`, 1 en `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 discrepancias de formato.

### Notas para el siguiente sprint (Sprint 2)
- El siguiente sprint es **Sprint 2 — Inventario de drivers con WMI (`Agent.Drivers`)**.
- Objetivo: Implementar `WmiDriverInventory` consultando `Win32_PnPSignedDriver` en WMI (`System.Management`).
- Mapear DeviceID, DeviceName, Manufacturer, DriverVersion, DriverDate, InfName, HardwareID, Status y DeviceClass.
- Registrar `WmiDriverInventory` como singleton en `ServiceCollectionExtensions.AddDriverServices()`.
- Implementar el comando `scan` en `src/Cli` con tabla formateada por consola y logging con Serilog.
- Verificar ejecución de `dotnet run --project src/Cli -- scan` en el equipo de desarrollo.

---

## Sprint 2 — Inventario de drivers con WMI (`Agent.Drivers`)

- **Fecha**: 2026-10-12
- **Estado**: ✅ Completado
- **Hito**: Hito 0 (Inventario real en Windows)

### Tareas completadas
- [x] **S2-T1: `WmiDriverInventory`** (`src/Agent.Drivers/Inventory/WmiDriverInventory.cs`):
  - Implementación completa de `IDriverInventory` sobre WMI (`Win32_PnPSignedDriver`).
  - Mapeo de `DeviceID`, `DeviceName` (con fallback a `Description` y `Caption`), `Manufacturer`, `DriverVersion`, `InfName`, `DeviceClass`, `DriverDate` (usando `ManagementDateTimeConverter`) y `HardwareID`.
  - Normalización de estados PnP a `DriverStatus` (Ok, Error, Degraded, Unknown).
  - Consulta ejecutada en background thread (`Task.Run`) con soporte de `CancellationToken` y `ConfigureAwait(false)`.
- [x] **S2-T2: Contenedor DI en `Agent.Drivers`** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - Extensión `AddDriverServices` para registrar `WmiDriverInventory` como singleton de `IDriverInventory`.
- [x] **S2-T3: Comando `scan` en la CLI** (`src/Cli/Program.cs`):
  - Comando `dotnet run --project src/Cli -- scan` mediante `System.CommandLine`.
  - Muestra tabla formateada con columnas: Dispositivo, Fabricante, Versión, Fecha y Estado.
  - Ordenado por clase de dispositivo y nombre.
  - Temporizador de ejecución (`Stopwatch`).
- [x] **S2-T4: Logging con Serilog**:
  - Sink de archivo rotatorio diario en `logs/cli-.log` con formato invariante.
  - Registro de inicio y finalización con conteo total de controladores detectados.
- [x] **S2-T5: Tests de integración**:
  - `tests/Agent.Drivers.Tests/WmiDriverInventoryTests.cs` con tests marcados como `[Trait("Category", "Integration")]`:
    - Verifica recuperación de drivers reales con campos requeridos en Windows.
    - Verifica propagación de `OperationCanceledException`.

### Decisiones técnicas tomadas
1. **System.CommandLine**:
   - Se empleó `System.CommandLine 2.0.0-beta4.22272.1` por estabilidad de API con `SetHandler(async (context) => ...)`, pasando `context.GetCancellationToken()`.
2. **Robustez ante campos opcionales de WMI**:
   - `DriverDate` contempla fallos de formateo WMI intentando `DateTime.TryParse` antes de fallback a `DateTime.MinValue`.
   - `DeviceName` utiliza encadenamiento coalescente de `DeviceName`, `Description` y `Caption` para evitar nombres vacíos.
   - Colección de hardware IDs incluye tanto el `HardwareID` primario como el `DeviceID` para maximizar coincidencias con catálogos de actualización.

### Pruebas ejecutadas
- `dotnet run --project src/Cli -- scan`:
  - **Resultado**: 185 controladores reales listados en la máquina en **1.50 segundos** (criterio: < 30 segundos).
- `dotnet test -c Release`:
  - **Resultado**: 42/42 tests pasados (39 unitarios en `Core.Tests`, 3 en `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`:
  - **Resultado**: 0 discrepancias de estilo y formato.
- Log verificado en `logs/cli-20261012.log`.

### Notas para el siguiente sprint (Sprint 3)
- El siguiente sprint es **Sprint 3 — Búsqueda de actualizaciones con WUA**.
- Objetivo: Implementar `IUpdateSource` usando Windows Update Agent (`Microsoft.Update.Session` COM).
- Criterio de búsqueda: `"IsInstalled=0 AND Type='Driver'"`.
- Cruzar resultados con los drivers instalados usando `DriverMatcher` por hardware ID.
- Agregar comando `updates` a la CLI (`dotnet run --project src/Cli -- updates`).
- **Hito 0 — Decisión go/no-go**: Evaluar la disponibilidad y riqueza de actualizaciones ofrecidas por Windows Update.

---

## Infraestructura CI/CD — GitHub Actions

- **Fecha**: 2026-10-12
- **Estado**: ✅ Configurado y validado
- **Workflows añadidos**:
  1. `.github/workflows/ci.yml`:
     - Disparadores: `push` a ramas principales (`main`, `master`) y `pull_request`.
     - Runner: `windows-latest`.
     - Pasos: Setup .NET 10.0.x, `dotnet format --verify-no-changes`, `dotnet restore`, `dotnet build -c Release --no-restore` y `dotnet test -c Release --no-build` con reporte de cobertura `XPlat Code Coverage`.
  2. `.github/workflows/release.yml`:
     - Disparadores: Tags de versión `v*` (ej: `v1.0.0`) o ejecución manual vía `workflow_dispatch`.
     - Runner: `windows-latest`.
     - Pasos: Validación de formato y tests, publicación de paquetes win-x64 (`Cli`, `Agent.Service`, `Ui.Wpf`), compresión de artefactos `.zip` y creación automática de GitHub Release con `softprops/action-gh-release@v2`.
- **Verificación local**:
  - Compilación y pruebas validadas con 0 advertencias y 0 errores.
  - Empaquetado y ejecución de artefactos publicado verificado con éxito (`dist/cli/Cli.exe`).
  - `.gitignore` actualizado con exclusión de `dist/`.

---

## Sprint 3 — Búsqueda de actualizaciones con WUA (`Agent.Drivers`)

- **Fecha**: 2026-10-13
- **Estado**: ✅ Completado
- **Hito**: Hito 0 (Búsqueda de actualizaciones via Windows Update)

### Tareas completadas
- [x] **S3-T1: `WuaUpdateSource`** (`src/Agent.Drivers/Updates/WuaUpdateSource.cs`):
  - Implementación completa de `IUpdateSource` sobre Windows Update Agent (COM).
  - Creación de `Microsoft.Update.Session` vía `Type.GetTypeFromProgID` + `Activator.CreateInstance`.
  - Criterio de búsqueda: `"IsInstalled=0 AND Type='Driver'"`.
  - Mapeo de propiedades COM: `UpdateID`, `Title`, `Description`, `MaxDownloadSize`, `IsMandatory`, `LastDeploymentChangeTime`, `DriverHardwareID`.
  - Extracción de versión del título WUA mediante parsing de segmentos `"Fabricante - Categoría - Versión"` con fallback a tokens sueltos, validados con `VersionComparer.IsValid()`.
  - Cruce de hardware IDs con el inventario instalado para obtener `CurrentVersion` y `DeviceName`.
  - Búsqueda COM ejecutada en background thread (`Task.Run`) con soporte de `CancellationToken`.
  - Liberación correcta de objetos COM con `Marshal.ReleaseComObject` en bloques `finally`.
  - Manejo de `COMException` y `RuntimeBinderException` para actualizaciones sin `IWindowsDriverUpdate`.
- [x] **S3-T2: Registro en DI** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - `WuaUpdateSource` registrado como singleton de `IUpdateSource`.
- [x] **S3-T3: Comando `updates` en la CLI** (`src/Cli/Program.cs`):
  - Comando `dotnet run --project src/Cli -- updates`.
  - Flujo: escaneo de inventario → búsqueda WUA → tabla formateada con columnas: Dispositivo, Versión actual, Versión nueva, Tamaño y Título.
  - Formato de tamaño humano (B, KB, MB).
  - Mensaje verde de confirmación cuando no hay actualizaciones pendientes.
- [x] **S3-T4: Tests** (`tests/Agent.Drivers.Tests/WuaUpdateSourceTests.cs`):
  - Test de integración: `FindUpdatesAsync_Should_Return_Without_Throwing` — búsqueda real contra WUA con validación de campos requeridos.
  - Test de integración: `FindUpdatesAsync_Should_Propagate_Cancellation` — verificación de cancelación.
  - Test unitario: `FindUpdatesAsync_Should_Throw_When_InstalledDrivers_Is_Null`.
  - 6 tests con `[Theory]` para `ExtractVersionFromTitle` con formatos estándar de WUA.
  - 3 tests con `[Theory]` para entradas vacías/nulas.
  - Tests de fallback y caso sin versión.
- [x] **S3-T5: Configuración y calidad**:
  - `InternalsVisibleTo` añadido a `Agent.Drivers.csproj` para permitir tests de métodos internos.
  - `.editorconfig` corregido: regla de nombres separada para `const` (PascalCase) vs campos de instancia (`_camelCase`).

### Decisiones técnicas tomadas
1. **COM interop con `dynamic`**:
   - Se usa `dynamic` en lugar de interop tipado porque WUA no tiene un assembly de interop oficial para .NET moderno. `dynamic` con `Type.GetTypeFromProgID` es la forma estándar y mantenida.
2. **Extracción de versión del título**:
   - Los títulos WUA de drivers siguen el patrón "Fabricante - Categoría - Versión". Se extrae iterando segmentos desde el final y validando con `VersionComparer.IsValid()`. Como fallback, se buscan tokens sueltos tipo versión.
3. **Manejo de `RuntimeBinderException`**:
   - Se añadió `Microsoft.CSharp.RuntimeBinder` para capturar la excepción cuando una actualización no implementa `IWindowsDriverUpdate` y no expone `DriverHardwareID`.
4. **Convención de naming para constantes**:
   - Se separó la regla de naming en `.editorconfig`: campos de instancia privados usan `_camelCase`, campos `const` usan `PascalCase`. Esto sigue las convenciones estándar de C#.

### Pruebas ejecutadas
- `dotnet run --project src/Cli -- updates`:
  - **Resultado**: 1 actualización de driver real encontrada (Intel Extension 2.1.10103.18) en **6.31 segundos**.
  - 185 controladores escaneados en el inventario.
- `dotnet build -c Release`:
  - **Resultado**: 7 proyectos compilados, 0 advertencias, 0 errores.
- `dotnet test -c Release`:
  - **Resultado**: 56/56 tests pasados (39 en `Core.Tests`, 17 en `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`:
  - **Resultado**: 0 discrepancias de formato.

### Hito 0 — Decisión go/no-go
- ✅ **GO**: Windows Update Agent devuelve actualizaciones de drivers reales (1 encontrada en el equipo de desarrollo). La fuente es funcional y la integración COM es estable.
- La cantidad de actualizaciones variará según el equipo y su estado. El criterio `Type='Driver'` filtra correctamente.

### Notas para el siguiente sprint (Sprint 4A)
- Se procedió a completar el backend en **Sprint 4A**.

---

## Sprint 4A — Implementaciones Core del Backend (`Agent.Drivers`)

- **Fecha**: 2026-10-13
- **Estado**: ✅ Completado
- **Hito**: Hito 1 (Backend funcional de extremo a extremo)

### Tareas completadas
- [x] **S4-T1: `SqliteOperationHistory` — Persistencia en SQLite** (`src/Agent.Drivers/History/SqliteOperationHistory.cs`):
  - Implementación completa de `IOperationHistory` usando `Microsoft.Data.Sqlite`.
  - Creación automática de directorio y tabla de persistencia (`CREATE TABLE IF NOT EXISTS operations`).
  - Esquema estricto: `Id` (INTEGER PK AUTOINCREMENT), `OperationType`, `UpdateId`, `DeviceName`, `HardwareId`, `DriverTitle`, `PreviousVersion`, `NewVersion`, `Success`, `ErrorMessage`, `Timestamp` (ISO 8601).
  - Operaciones asíncronas con soporte de cancelación (`CancellationToken`).
  - Ruta de base de datos configurable con valor por defecto en `%ProgramData%\DriverUpdater\history.db`.
  - Suite de pruebas unitarias (`tests/Agent.Drivers.Tests/SqliteOperationHistoryTests.cs`): inserción, consulta global, consulta por ID, manejo de valores nulos y validación de argumentos.
- [x] **S4-T2: `WinTrustSignatureVerifier` — Verificación de firma digital** (`src/Agent.Drivers/Security/`):
  - `NativeMethods.cs`: P/Invoke a `wintrust.dll!WinVerifyTrust` con acción `WINTRUST_ACTION_GENERIC_VERIFY_V2`.
  - Mapeo de estructuras `WintrustData`, `WintrustFileInfo` con liberación manual de memoria no administrada (`Marshal.StringToCoTaskMemUni` / `FreeCoTaskMem`).
  - Extracción y validación de certificados y distinción de firmas WHQL / Microsoft.
  - Mapeo de códigos HRESULT nativos: `TRUST_E_NOSIGNATURE`, `TRUST_E_EXPLICIT_DISTRUST`, `TRUST_E_SUBJECT_NOT_TRUSTED`, `CRYPT_E_SECURITY_SETTINGS`, `CERT_E_EXPIRED`, `CERT_E_REVOKED`, `TRUST_E_BAD_DIGEST`.
  - Tests en `tests/Agent.Drivers.Tests/WinTrustSignatureVerifierTests.cs`: archivos inexistentes, archivos sin firma y verificación real de binarios firmados del sistema Windows (`kernel32.dll`).
- [x] **S4-T3: `WmiRestorePointService` — Puntos de restauración de Windows** (`src/Agent.Drivers/Restore/WmiRestorePointService.cs`):
  - Implementación de `IRestorePointService` mediante invocación WMI a `root\default:SystemRestore.CreateRestorePoint`.
  - Parámetros: `RestorePointType = 10` (`DEVICE_DRIVER_INSTALL`), `EventType = 100` (`BEGIN_SYSTEM_CHANGE`).
  - Manejo seguro de `ManagementException`, `UnauthorizedAccessException` y retorno booleano garantizado.
- [x] **S4-T4: `WuaDriverInstaller` — Orquestación de instalación y rollback** (`src/Agent.Drivers/Install/`):
  - Abstracción desacoplada `IWuaExecutor` y ejecutor COM `DefaultWuaExecutor` para descargas e instalaciones seguras.
  - Cumplimiento estricto de las reglas de seguridad de `AGENTS.md`:
    1. **Regla 1**: Aborta de inmediato si la firma digital no es válida o de confianza.
    2. **Regla 2**: Aborta de inmediato si falla la creación del punto de restauración del sistema previo a la instalación.
    3. **Regla 5**: Ejecución de comandos del sistema (`pnputil`) mediante `ProcessStartInfo.ArgumentList` sin concatenación de cadenas externas para evitar inyecciones.
    4. **Regla 8**: Registro exhaustivo y garantizado de cada operación (éxito o fallo) en `IOperationHistory`.
  - Notificación de progreso en 8 fases (`InstallProgress`): Downloading (10-25%), Verifying (30-45%), CreatingRestorePoint (50-65%), Installing (70-85%), Validating (90%), Complete (100%) o Failed.
  - Implementación de `RollbackAsync` consultando el historial y revirtiendo el controlador de forma segura.
  - Tests unitarios exhaustivos con dobles de prueba (`tests/Agent.Drivers.Tests/WuaDriverInstallerTests.cs`): flujo exitoso, aborto por firma inválida, aborto por fallo en restore point, aborto por fallo de red, y reversión con rollback.
- [x] **S4-T5: Registro en contenedor de inyección de dependencias** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - Registro de las 6 interfaces del dominio: `IDriverInventory`, `IUpdateSource`, `ISignatureVerifier`, `IRestorePointService`, `IOperationHistory` y `IDriverInstaller`.
  - Soporte para ruta configurable de base de datos SQLite en `AddDriverServices(databasePath)`.
- [x] **S4-T6: Ampliación de comandos CLI** (`src/Cli/Program.cs`):
  - `log`: consulta y presenta en tabla el historial de operaciones de auditoría.
  - `install <updateId>`: ejecuta el flujo de instalación con reporte de progreso en consola y advertencia de elevación de privilegios.
  - `rollback <operationId>`: revierte una instalación previa según su identificador en el registro.
- [x] **S4-T7: Verificación completa de calidad**:
  - Compilación Release: 0 advertencias, 0 errores en los 7 proyectos.
  - Pruebas automatizadas: 72 tests superados (39 en `Core.Tests`, 33 en `Agent.Drivers.Tests`).
  - Verificación de formato: `dotnet format --verify-no-changes` validado con código 0.

### Decisiones técnicas tomadas
1. **Desacoplamiento de `IWuaExecutor`**:
   - Para permitir pruebas unitarias exhaustivas del flujo de seguridad sin requerir privilegios administrativos ni invocar APIs COM reales durante la ejecución de CI/CD, se separó la interacción COM en la interfaz interna `IWuaExecutor`.
2. **Acceso a tipos internos para NSubstitute**:
   - Se configuró `<InternalsVisibleTo Include="DynamicProxyGenAssembly2" />` en `Agent.Drivers.csproj` para que el generador de proxies dinámicos de Castle/NSubstitute pueda instanciar dobles sobre interfaces internas sin comprometer el encapsulamiento público.
3. **Persistencia SQLite**:
   - Se utilizó `Microsoft.Data.Sqlite` con `ISO 8601` (`"O"`) en `InvariantCulture` para los campos de marca temporal, asegurando portabilidad de fechas independientemente de la configuración regional del sistema operativo.
4. **Seguridad en rollback**:
   - `pnputil.exe` se ejecuta a través de `ProcessStartInfo.ArgumentList`, pasando los identificadores de hardware como argumentos discretos para cumplir la regla 5 de seguridad contra inyección de comandos.

### Pruebas ejecutadas
- `dotnet build -c Release`: 7 proyectos compilados, 0 advertencias, 0 errores.
- `dotnet test -c Release`: 72/72 tests pasados (39 en `Core.Tests`, 33 en `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 discrepancias de formato.
- `dotnet run --project src/Cli -- log`: ejecución correcta en consola consultando base de datos SQLite.
- `dotnet run --project src/Cli -- --help`: comandos `scan`, `updates`, `log`, `install`, `rollback` verificados.

### Notas para el siguiente sprint (Sprint 4B o Sprint 5)
- El backend (`Core` + `Agent.Drivers`) está **100% completo, probado y verificado**.
- Los siguientes pasos en el MVP son:
  1. **Sprint 4B — Servicio de Windows (`Agent.Service`)**: Implementar Worker Service con gRPC sobre Named Pipes para comunicar la UI no privilegiada con el servicio SYSTEM.
  2. **Sprint 5 — Interfaz de usuario WPF (`Ui.Wpf`)**: Desarrollar la interfaz visual en MVVM (con CommunityToolkit.Mvvm) consumiendo el servicio gRPC o la capa de abstracción.

---

## Sprint 4B — Servicio de Windows con gRPC sobre Named Pipes (`Agent.Service` + `Agent.Ipc`)

- **Fecha**: 2026-10-19
- **Estado**: ✅ Completado
- **Hito**: Hito 2 (Servicio + UI — parte IPC y servicio)

### Tareas completadas
- [x] **S4B-T1: Proyecto `Agent.Ipc` — contrato IPC compartido** (`src/Agent.Ipc`):
  - `Protos/driveragent.proto` con el servicio `DriverAgent`: `Scan`, `CheckUpdates` (streaming), `Install` (streaming de progreso), `Rollback`, `GetHistory` y `GetStatus`. Codegen `GrpcServices="Both"`, namespace C# `Agent.Ipc.Grpc`.
  - `DriverAgentChannel.CreateChannel()`: fábrica de canal gRPC sobre named pipe usando `SocketsHttpHandler.ConnectCallback` con `NamedPipeClientStream` (cliente para UI y tests).
  - Constante `DefaultPipeName = "DriverUpdater"` (equivale a `\\.\pipe\DriverUpdater`, según DESIGN.md §5).
  - Referencia a `Grpc.AspNetCore 2.76.0` + `Grpc.Tools 2.76.0` explícito (los targets de `Grpc.Tools` no se importan como dependencia transitiva en este entorno; ver Decisiones).
- [x] **S4B-T2: `OperationQueue` — cola de operaciones** (`src/Agent.Service/Operations/`):
  - `IOperationQueue` / `OperationQueue`: exclusión mutua con `SemaphoreSlim(1,1)`; garantiza **una operación privilegiada a la vez** (instalaciones, rollbacks y búsquedas de WUA; la sesión COM no debe atenderlas en paralelo).
  - Cancelable: el token de la llamada gRPC interrumpe tanto la espera en cola como la operación en ejecución; la cola siempre queda libre (`finally`).
  - Estado observable inmutable (`OperationState`: `OperationInProgress`, `CurrentUpdateId`, `PercentComplete`, `Phase`, `PendingReboot`, `LastScanUtc`, `QueuedOperations`) intercambiado de forma thread-safe.
  - Latch `PendingReboot` que persiste hasta el reinicio del servicio (natural tras reiniciar el equipo).
  - Contador de operaciones en cola para la UI (`QueuedOperations`).
- [x] **S4B-T3: `DriverAgentGrpcService` — servidor gRPC** (`src/Agent.Service/Grpc/`):
  - `Scan`: inventario WMI mapeado a `InstalledDriverMsg`; actualiza `LastScanUtc`.
  - `CheckUpdates`: serializado por la cola durante la detección en WUA; desacopla la transmisión de actualizaciones de la cola para que clientes lentos no bloqueen operaciones posteriores.
  - `Install`: recibe **solo `update_ids`**; el servicio re-resuelve cada ID contra Windows Update en el instante de instalar (evita instalar metadatos anticuados o manipulados por el cliente). Instala secuencialmente, transmite las 8 fases de `InstallProgress` en **tiempo real** mediante `Channel<T>` y un mensaje final `Finished` por driver con resultado individual (`Success`, `ErrorMessage`, `RebootRequired`). IDs desconocidos o excepciones inesperadas generan resultado de fallo individual sin abortar el lote.
  - `Rollback`: valida estrictamente el formato del ID de operación (entero positivo sin signo `ulong`, ≤32 caracteres) y delega en `IDriverInstaller.RollbackAsync` vía la cola.
  - `GetHistory`: aplica `limit` (entradas más recientes) sobre `IOperationHistory.GetAllAsync`.
  - `GetStatus`: estado de la cola, `PendingReboot` y `LastScanUtc` para la UI.
  - Progreso notificado en tiempo real con `InlineProgress` hacia un `Channel<InstallProgress>` con lector en tarea dedicada hacia el stream gRPC; el stream nunca sufre escrituras concurrentes.
  - Validación de entrada: IDs no vacíos, longitud acotada, deduplicación case-insensitive; peticiones inválidas → `RpcException(InvalidArgument)`.
- [x] **S4B-T4: Seguridad IPC** (`src/Agent.Service/Ipc/`):
  - `NamedPipeSecurityFactory`: ACL del pipe con control completo para SYSTEM (`S-1-5-18`) y Administradores (`S-1-5-32-544`), y permisos `ReadWrite` para usuarios interactivos (`S-1-5-4`), impidiendo `WRITE_DAC`/`WRITE_OWNER` a usuarios interactivos no privilegiados (cumpliendo la regla 4 de AGENTS.md, "solo usuarios interactivos locales").
  - `CurrentUserOnly = false` en `NamedPipeTransportOptions`: la opción por defecto es incompatible con una ACL explícita y además bloquearía a los usuarios interactivos (el servicio corre como SYSTEM).
  - `NamedPipeStreamFactory`: hook `CreateNamedPipeServerStream` que normaliza `PipeOptions.FirstPipeInstance` (solo en la primera instancia real del pool) y crea cada instancia con la ACL del contexto. Sin él, el creador por defecto marca varias instancias con `FirstPipeInstance` y el bind falla con `Access Denied`.
  - `Program.cs`: `AddWindowsService("DriverUpdater")`, Serilog con fichero rotatorio en `%ProgramData%\DriverUpdater\logs\service-.log`, Kestrel `ListenNamedPipe` con `HttpProtocols.Http2`, `MapGrpcService<DriverAgentGrpcService>()`. Ejecutable como servicio de Windows o en consola para diagnóstico.
- [x] **S4B-T5: Proyecto de tests `Agent.Service.Tests`** (`tests/Agent.Service.Tests`):
  - `OperationQueueTests` (5+5 tests): ejecución y resultado, serialización estricta, cancelación en espera y en ejecución, liberación ante excepciones, estado (progreso, latch de reinicio, último escaneo, argumentos).
  - `GrpcMappersTests`: mapeo completo de modelos Core ↔ mensajes proto, normalización de fechas a UTC (Kind Utc/Local/Unspecified) y mapeo exhaustivo de enums.
  - `NamedPipeSecurityFactoryTests`: verificación de las 3 reglas de la ACL (SIDs, derechos y tipo).
  - `NamedPipeStreamFactoryTests`: normalización de `FirstPipeInstance`.
  - `DriverAgentGrpcServiceTests`: **integración IPC real** — servidor ASP.NET Core in-process sobre named pipe único por test + cliente real por `DriverAgentChannel`, con dominio simulado (NSubstitute): Scan, CheckUpdates (streaming desacoplado), Install (progreso en tiempo real con Channel, lote secuencial, tolerancia a fallos y excepciones inesperadas, IDs desconocidos, rechazo de peticiones vacías), Rollback (mapeo y validación estricta ulong), GetHistory (con y sin límite), `GetStatus` (estado y reinicio pendiente), **serialización de instalaciones concurrentes**, **liberación de la cola al cancelar el cliente** y **aplicación real de la ACL configurada** (captura vía hook).
- [x] **S4B-T6: Verificación end-to-end del binario real**:
  - Arranque en modo consola del binario publicado: servicio en marcha y `\\.\pipe\DriverUpdater` visible en el sistema.
  - Cliente real (Agente.Ipc) contra el servicio en vivo: `GetStatus` OK, `Scan` OK (266 controladores reales inventariados vía WMI a través del servicio), `GetHistory` OK.

### Decisiones técnicas tomadas
1. **Nuevo proyecto `Agent.Ipc`**:
   - El contrato gRPC y la fábrica de canal cliente viven en un proyecto compartido (`GrpcServices="Both"`) para que `Agent.Service` implemente el servidor y la futura UI consuma el cliente sin referenciar el ensamblado del servicio. Actualizada la estructura de `AGENTS.md` y `DESIGN.md` §4.6/§5 en consecuencia. La UI arrastrará dependencias de servidor de `Grpc.AspNetCore` en el publish; si pesa demasiado, se dividirá en contratos cliente/servidor en una fase posterior.
2. **Transporte de named pipes nativo de Kestrel** (incluido en el framework .NET 10 como `Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes`):
   - `ListenNamedPipe` + `HttpProtocols.Http2` en claro (gRPC puro; sin HTTP/1.1 en el pipe). No hace falta `UseNamedPipes` explícito: el transporte se registra automáticamente.
   - Cliente gRPC: `GrpcChannel.ForAddress("http://localhost")` con `SocketsHttpHandler.ConnectCallback` que devuelve un `NamedPipeClientStream` (dirección simbólica; el transporte real es el pipe).
3. **Peculiaridades del transporte y ACL restringida**:
   - `NamedPipeTransportOptions.CurrentUserOnly` viene activa por defecto y es **incompatible** con `PipeSecurity` explícita (`ArgumentException` en `NamedPipeServerStreamAcl.Create`); además bloquearía a los usuarios interactivos porque el servicio corre como SYSTEM → se desactiva y la restricción se aplica con la ACL.
   - El creador por defecto marca varias instancias del pool con `PipeOptions.FirstPipeInstance`; la segunda instancia falla con `Access Denied` ("pipe already in use"). Resuelto con `NamedPipeStreamFactory`, que elimina la flag salvo en la primera instancia real.
   - Seguridad de la ACL: SYSTEM y Administradores tienen `FullControl`, mientras que las sesiones interactivas (`S-1-5-4`) tienen `ReadWrite`. Se restringe `WRITE_DAC` y `WRITE_OWNER` para prevenir que un usuario interactivo sin privilegios modifique la seguridad del pipe.
4. **Re-resolución y tolerancia a fallos en `Install`**:
   - Por seguridad y consistencia con la CLI, la instalación solo acepta IDs; el servidor consulta el inventario y Windows Update, y empareja por `UpdateId`. Un ID que ya no existe o una excepción inesperada durante la instalación produce un resultado individual de fallo (`Finished` con `Success=false`), sin interrumpir el resto de actualizaciones del lote.
5. **Streaming de progreso en tiempo real con Channel**:
   - `InlineProgress` escribe a un `Channel<InstallProgress>` desacoplado, leído concurrentemente por una tarea asíncrona hacia `responseStream.WriteAsync`. Esto entrega los eventos de progreso en tiempo real a la UI a medida que se ejecutan las fases, manteniendo la seguridad de escritura concurrente de gRPC y drenando limpiamente al finalizar o fallar.
6. **Cancelación segura y auditoría obligatoria (Regla 8 de AGENTS.md)**:
   - Una cancelación en `WuaDriverInstaller` durante las fases previas a la instalación nativa registra de forma obligatoria el fallo en el historial persistente de SQLite utilizando `CancellationToken.None` (para evitar que la cancelación interrumpa el guardado) antes de re-lanzar la excepción. La fase de instalación nativa con WUA no se interrumpe para evitar estados inconsistentes en el sistema operativo.
6. **`Grpc.Tools` explícito**:
   - Los targets de `Grpc.Tools` (codegen proto) solo se importan si el paquete se referencia directamente; `Grpc.AspNetCore` no lo propaga en este entorno. Referenciado con `PrivateAssets="All"` en `Agent.Ipc`.
7. **Supresión de `CA1711`**:
   - En `.editorconfig`, con justificación, para `IOperationQueue`/`OperationQueue`: la "cola de operaciones" del diseño (MVP.md Hito 2) es genuinamente una cola serializada de una entrada.
8. **`global.json` ajustado al SDK instalado**:
   - El entorno pasó de tener el SDK 10.0.401 a 10.0.302; `global.json` actualizado a `10.0.302` (`rollForward: latestFeature`) para restaurar la compilación.

### Pruebas ejecutadas
- `dotnet build -c Release`: 9 proyectos compilados, 0 advertencias, 0 errores.
- `dotnet test -c Release`: **131/131 tests pasados** (39 en `Core.Tests`, 51 en `Agent.Service.Tests`, 41 en `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 discrepancias de formato.
- E2E del binario real: servicio iniciado en consola, pipe `\\.\pipe\DriverUpdater` publicado, cliente real con `Scan` de 266 controladores reales, `GetStatus` y `GetHistory` verificados.

### Notas para el siguiente sprint (Sprint 5)
- El canal IPC completo (servicio + contrato + cliente) está operativo y probado. Quedan para completar el Hito 2:
  1. **Sprint 5 — UI WPF (`Ui.Wpf`)**: MVVM con CommunityToolkit.Mvvm consumiendo `Agent.Ipc` (pantallas: inicio/escaneo, actualizaciones, historial, ajustes). Referenciar `Agent.Ipc` desde `Ui.Wpf`. Contemplar en UX la cancelación diferida de la fase de instalación nativa (*"Cancelando... esperando a que finalice el controlador en curso"*).
  2. **Ajustes y exclusiones (U7)**: `config.json` en `%ProgramData%\DriverUpdater\`, RPCs `GetSettings`/`UpdateSettings` en el proto y aplicación de exclusiones en `CheckUpdates`/`Install`.
  3. **Comprobación programada y notificaciones (U8)**: planificador en el servicio (sin polling), notificación toast si hay novedades y aviso de reinicio pendiente.
  4. **Instalación del servicio de Windows**: registrar el binario como servicio (`sc create` o WiX en el Hito 4). Probar en VM (nunca en el equipo de desarrollo con operaciones reales).
  5. **Validación E2E de la ACL en VM con cuenta estándar (reserva de auditoría)**: Verificar en VM que un usuario local interactivo sin privilegios administrativos puede conectar y realizar operaciones I/O en el pipe (`ReadWrite`), y certificar que cualquier intento de alterar la ACL/DACL es denegado con `Access Denied`.


