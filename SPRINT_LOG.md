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

