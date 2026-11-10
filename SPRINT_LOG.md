# Sprint Log — DriverUpdater

This document chronologically and in detail records each sprint executed in the **DriverUpdater** project, with completed tasks, architecture decisions, test results, resolved issues and essential notes for the following agents.

This is a historical log, not a description of the current state. Tasks and decisions reflect what was known when each sprint closed; for the current state, see `MVP.md`, `ARCHITECTURE.md`, `DESIGN.md` and `TESTING.md`.

---

## Sprint 0 — Environment preparation and base structure

- **Date**: 2026-10-12
- **Status**: ✅ Completed
- **Milestone**: Milestone 0 (Foundations and infrastructure)

### Completed tasks
- [x] **S0-T1: Verification and integration of .NET 10 SDK** — SDK .NET 10.0.401 (LTS) verified on the system (`C:\Program Files\dotnet\dotnet.exe`).
- [x] **S0-T2: `global.json` configuration** — Version `10.0.401` pinned with `rollForward: "latestFeature"`.
- [x] **S0-T3: Shared properties with `Directory.Build.props`** — Enabled `Nullable: enable`, `ImplicitUsings: enable`, `LangVersion: 14`, `TreatWarningsAsErrors: true`, `AnalysisLevel: latest-recommended` and `EnforceCodeStyleInBuild: true`.
- [x] **S0-T4: Solution creation and the 7 projects**:
  - `src/Core` (`net10.0`, platform-agnostic library with no Windows dependencies).
  - `src/Agent.Drivers` (`net10.0-windows`, access to Windows subsystems: WMI, COM, WinVerifyTrust).
  - `src/Agent.Service` (`net10.0-windows`, Worker Service for a Windows Service running under the SYSTEM account).
  - `src/Ui.Wpf` (`net10.0-windows`, WPF user interface with MVVM).
  - `src/Cli` (`net10.0-windows`, console diagnostic and development tool).
  - `tests/Core.Tests` (`net10.0`, unit tests with xUnit, FluentAssertions, NSubstitute).
  - `tests/Agent.Drivers.Tests` (`net10.0-windows`, hardware and subsystem integration tests).
- [x] **S0-T5: Project references** — Established according to the architecture (`Agent.Service` -> `Agent.Drivers`, `Core`; `Ui.Wpf` -> `Core`; `Cli` -> `Agent.Drivers`, `Core`; test projects referencing their respective targets).
- [x] **S0-T6: Essential NuGet dependencies and packages**:
  - `Cli`: `System.CommandLine`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Console` (6.1.1), `Serilog.Sinks.File` (7.0.0).
  - `Agent.Service`: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Grpc.AspNetCore` (2.76.0), `Serilog.Extensions.Logging`, `Serilog.Sinks.File`.
  - `Agent.Drivers`: `System.Management`, `Microsoft.Data.Sqlite`, `Serilog.Extensions.Logging`, `Serilog.Sinks.File`.
  - `Ui.Wpf`: `CommunityToolkit.Mvvm`, `Serilog.Extensions.Logging`.
  - `Core.Tests` and `Agent.Drivers.Tests`: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `NSubstitute`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`.
- [x] **S0-T7: Style and quality rules (.editorconfig & .gitignore)**:
  - Standard .NET `.gitignore`.
  - `.editorconfig` with formatting and style rules, and suppression of `CA1848` (do not require LoggerMessage generators for conventional logging) and `CA1707` in test projects (allow readable test names with underscores).
- [x] **S0-T8: Full build and test verification**:
  - `dotnet build -c Release` builds with 0 errors and 0 warnings.
  - `dotnet test` runs the test suites green.
  - `dotnet format --verify-no-changes` validated with 0 style discrepancies.

### Technical decisions made
1. **NuGet package versions**:
   - `Serilog.Sinks.Console` was set to `6.1.1` (version 7.0.0 does not exist on NuGet yet).
   - `Grpc.AspNetCore` was pinned to `2.76.0` to avoid warning NU1603 (which was turned into an error by `TreatWarningsAsErrors`).
2. **Test convention**:
   - In `.editorconfig`, `dotnet_diagnostic.CA1707.severity = none` was set for `[tests/**.cs]`, allowing the standard `Should_Action_When_Condition` convention for test methods.

### Tests run
- `dotnet build -c Release`: 7 projects built, 0 warnings, 0 errors.
- `dotnet test`: 2/2 tests run and passed (smoke tests in `Core.Tests` and `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 differences detected.

### Notes for the next sprint (Sprint 1)
- The next sprint is **Sprint 1 — Models, interfaces and pure logic (`Core`)**.
- All logic must go in `src/Core` without Windows dependencies (`net10.0`).
- Must include models: `HardwareId`, `InstalledDriver`, `DriverUpdate`, `InstallResult`, `OperationLogEntry`, `DriverStatus`.
- Interfaces: `IDriverInventory`, `IUpdateSource`, `ISignatureVerifier`, `IRestorePointService`, `IDriverInstaller`.
- Pure logic services: `VersionComparer` (numeric segment-by-segment comparer) and `DriverMatcher`.
- Full unit test suite in `tests/Core.Tests`.

---

## Sprint 1 — Models, interfaces and pure logic (`Core`)

- **Date**: 2026-10-12
- **Status**: ✅ Completed
- **Milestone**: Milestone 0 (Isolated domain logic)

### Completed tasks
- [x] **S1-T1: `HardwareId` model** (`src/Core/Models/HardwareId.cs`):
  - Immutable (`record`), encapsulates the PnP identifier (e.g. `PCI\VEN_8086&DEV_9A49&REV_03`).
  - Case-insensitive comparison (`Matches(HardwareId)` and `Matches(string)`).
- [x] **S1-T2: `InstalledDriver` and `DriverStatus` models** (`src/Core/Models/`):
  - `InstalledDriver`: device, manufacturer, version, date, inf, hardware IDs, status and class.
  - `DriverStatus`: Ok, Error, Degraded, Unknown.
- [x] **S1-T3: `DriverUpdate` model** (`src/Core/Models/DriverUpdate.cs`):
  - Id, title, description, new/current version, matching hardware ID, size in bytes, mandatory flag and release date.
- [x] **S1-T4: `InstallResult` model** (`src/Core/Models/InstallResult.cs`):
  - Success, error message, restart-required flag, previous and applied versions, timestamp.
- [x] **S1-T5: `OperationLogEntry` model** (`src/Core/Models/OperationLogEntry.cs`):
  - Audit record for operations (Install, Rollback, Scan).
- [x] **S1-T6: Additional models and domain interfaces**:
  - `SignatureVerificationResult` (`src/Core/Models/SignatureVerificationResult.cs`): validity, signer, failure reason, WHQL flag.
  - `InstallProgress` and `InstallPhase` (`src/Core/Models/InstallProgress.cs`): full phases (Downloading, Verifying, CreatingRestorePoint, Installing, Validating, Complete, Failed, RollingBack).
  - `IDriverInventory` (`src/Core/Interfaces/IDriverInventory.cs`): `GetInstalledDriversAsync(ct)`.
  - `IUpdateSource` (`src/Core/Interfaces/IUpdateSource.cs`): `IAsyncEnumerable<DriverUpdate> FindUpdatesAsync(installed, ct)`.
  - `ISignatureVerifier` (`src/Core/Interfaces/ISignatureVerifier.cs`): `VerifyAsync(filePath, ct)`.
  - `IRestorePointService` (`src/Core/Interfaces/IRestorePointService.cs`): `CreateRestorePointAsync(description, ct)`.
  - `IDriverInstaller` (`src/Core/Interfaces/IDriverInstaller.cs`): `InstallAsync(update, progress, ct)` and `RollbackAsync(operationId, ct)`.
  - `IOperationHistory` (`src/Core/Interfaces/IOperationHistory.cs`): `RecordAsync`, `GetAllAsync`, `GetByIdAsync`.
- [x] **S1-T7: `VersionComparer` service** (`src/Core/Services/VersionComparer.cs`):
  - Numeric segment-by-segment comparer (`ulong`) for versions of the form `major.minor.build.revision`.
  - Supports missing segments as zero (e.g. `"10.0"` == `"10.0.0.0"`).
  - Implements `IComparer<string>` for ordering with LINQ.
  - Syntactic validator `IsValid(version)`.
  - Robust error handling (`ArgumentNullException`, `ArgumentException`, `FormatException`).
- [x] **S1-T8: `DriverMatcher` service** (`src/Core/Services/DriverMatcher.cs`):
  - `FindBestMatch`: evaluates candidates, matches hardware IDs against the device case-insensitively, discards equal or lower versions and selects the highest one (breaking ties by release date).
  - `FindAllMatches`: batch resolution for device collections.
- [x] **S1-T9: Exhaustive unit test battery** (`tests/Core.Tests/`):
  - `VersionComparerTests`: 10 test cases with InlineData covering numeric pairs, omitted zeros, invalid formats, ordering and exceptions.
  - `DriverMatcherTests`: 8 unit tests covering optimal selection, outdated versions, hardware ID matching, tie-breaking by date and batch matching.
  - `HardwareIdTests`: 5 tests verifying case-insensitive equality, null validation and string representation.

### Technical decisions made
1. **Driver version representation**:
   - `VersionComparer` parses numeric segments using `ulong` to avoid overflow in long manufacturer builds, padding missing segments with `0UL` when comparing strings with a different number of dots.
2. **Tie-breaking in `DriverMatcher`**:
   - When two valid candidate updates have exactly the same numeric version, the one with the most recent release date (`ReleaseDate`) is prioritized.
3. **Persistence and dependency inversion**:
   - `IOperationHistory` was defined in `Core.Interfaces` so that the service and the tests depend on the abstraction, without coupling `Core` to SQLite or Windows.

### Tests run
- `dotnet build -c Release`: 7 projects built, 0 warnings, 0 errors.
- `dotnet test -c Release`: 40/40 tests passed (39 tests in `Core.Tests`, 1 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 format discrepancies.

### Notes for the next sprint (Sprint 2)
- The next sprint is **Sprint 2 — WMI driver inventory (`Agent.Drivers`)**.
- Goal: Implement `WmiDriverInventory` querying `Win32_PnPSignedDriver` in WMI (`System.Management`).
- Map DeviceID, DeviceName, Manufacturer, DriverVersion, DriverDate, InfName, HardwareID, Status and DeviceClass.
- Register `WmiDriverInventory` as a singleton in `ServiceCollectionExtensions.AddDriverServices()`.
- Implement the `scan` command in `src/Cli` with a console-formatted table and logging with Serilog.
- Verify execution of `dotnet run --project src/Cli -- scan` on the development machine.

---

## Sprint 2 — WMI driver inventory (`Agent.Drivers`)

- **Date**: 2026-10-12
- **Status**: ✅ Completed
- **Milestone**: Milestone 0 (Real inventory on Windows)

### Completed tasks
- [x] **S2-T1: `WmiDriverInventory`** (`src/Agent.Drivers/Inventory/WmiDriverInventory.cs`):
  - Full implementation of `IDriverInventory` over WMI (`Win32_PnPSignedDriver`).
  - Mapping of `DeviceID`, `DeviceName` (with fallback to `Description` and `Caption`), `Manufacturer`, `DriverVersion`, `InfName`, `DeviceClass`, `DriverDate` (using `ManagementDateTimeConverter`) and `HardwareID`.
  - Normalization of PnP states to `DriverStatus` (Ok, Error, Degraded, Unknown).
  - Query executed on a background thread (`Task.Run`) with `CancellationToken` support and `ConfigureAwait(false)`.
- [x] **S2-T2: DI container in `Agent.Drivers`** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - `AddDriverServices` extension to register `WmiDriverInventory` as a singleton of `IDriverInventory`.
- [x] **S2-T3: `scan` command in the CLI** (`src/Cli/Program.cs`):
  - Command `dotnet run --project src/Cli -- scan` via `System.CommandLine`.
  - Displays a console-formatted table with columns: Device, Manufacturer, Version, Date and Status.
  - Sorted by device class and name.
  - Execution timer (`Stopwatch`).
- [x] **S2-T4: Logging with Serilog**:
  - Daily rolling file sink at `logs/cli-.log` with invariant format.
  - Start and end logging with the total count of drivers detected.
- [x] **S2-T5: Integration tests**:
  - `tests/Agent.Drivers.Tests/WmiDriverInventoryTests.cs` with tests marked `[Trait("Category", "Integration")]`:
    - Verifies retrieval of real drivers with the required fields on Windows.
    - Verifies propagation of `OperationCanceledException`.

### Technical decisions made
1. **System.CommandLine**:
   - `System.CommandLine 2.0.0-beta4.22272.1` was used for API stability with `SetHandler(async (context) => ...)`, passing `context.GetCancellationToken()`.
2. **Robustness against optional WMI fields**:
   - `DriverDate` handles WMI formatting failures by trying `DateTime.TryParse` before falling back to `DateTime.MinValue`.
   - `DeviceName` uses null-coalescing chaining of `DeviceName`, `Description` and `Caption` to avoid empty names.
   - The hardware ID collection includes both the primary `HardwareID` and the `DeviceID` to maximize matches with update catalogs.

### Tests run
- `dotnet run --project src/Cli -- scan`:
  - **Result**: 185 real drivers listed on the machine in **1.50 seconds** (criterion: < 30 seconds).
- `dotnet test -c Release`:
  - **Result**: 42/42 tests passed (39 unit tests in `Core.Tests`, 3 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`:
  - **Result**: 0 style and format discrepancies.
- Log verified in `logs/cli-20261012.log`.

### Notes for the next sprint (Sprint 3)
- The next sprint is **Sprint 3 — Update search with WUA (`Agent.Drivers`)**.
- Goal: Implement `IUpdateSource` using the Windows Update Agent (`Microsoft.Update.Session` COM).
- Search criterion: `"IsInstalled=0 AND Type='Driver'"`.
- Cross-reference the results with the installed drivers using `DriverMatcher` by hardware ID.
- Add the `updates` command to the CLI (`dotnet run --project src/Cli -- updates`).
- **Milestone 0 — Go/no-go decision**: Evaluate the availability and richness of the updates offered by Windows Update.

---

## Infrastructure CI/CD — GitHub Actions

- **Date**: 2026-10-12
- **Status**: ✅ Configured and validated
- **Workflows added**:
  1. `.github/workflows/ci.yml`:
     - Triggers: `push` to main branches (`main`, `master`) and `pull_request`.
     - Runner: `windows-latest`.
     - Steps: Set up .NET 10.0.x, `dotnet format --verify-no-changes`, `dotnet restore`, `dotnet build -c Release --no-restore` and `dotnet test -c Release --no-build` with an `XPlat Code Coverage` coverage report.
  2. `.github/workflows/release.yml`:
     - Triggers: version tags `v*` (e.g. `v1.0.0`) or manual execution via `workflow_dispatch`.
     - Runner: `windows-latest`.
     - Steps: Format and test validation, publishing of win-x64 packages (`Cli`, `Agent.Service`, `Ui.Wpf`), compression of `.zip` artifacts and automatic creation of a GitHub Release with `softprops/action-gh-release@v2`.
- **Local verification**:
  - Build and tests validated with 0 warnings and 0 errors.
  - Packaging and execution of the published artifact verified successfully (`dist/cli/Cli.exe`).
  - `.gitignore` updated to exclude `dist/`.

---

## Sprint 3 — Update search with WUA (`Agent.Drivers`)

- **Date**: 2026-10-13
- **Status**: ✅ Completed
- **Milestone**: Milestone 0 (Update search via Windows Update)

### Completed tasks
- [x] **S3-T1: `WuaUpdateSource`** (`src/Agent.Drivers/Updates/WuaUpdateSource.cs`):
  - Full implementation of `IUpdateSource` over the Windows Update Agent (COM).
  - Creation of `Microsoft.Update.Session` via `Type.GetTypeFromProgID` + `Activator.CreateInstance`.
  - Search criterion: `"IsInstalled=0 AND Type='Driver'"`.
  - Mapping of COM properties: `UpdateID`, `Title`, `Description`, `MaxDownloadSize`, `IsMandatory`, `LastDeploymentChangeTime`, `DriverHardwareID`.
  - Extraction of the version from the WUA title by parsing `"Manufacturer - Category - Version"` segments, with fallback to loose tokens, validated with `VersionComparer.IsValid()`.
  - Cross-referencing hardware IDs with the installed inventory to obtain `CurrentVersion` and `DeviceName`.
  - WUA search executed on a background thread (`Task.Run`) with `CancellationToken` support.
  - Correct release of COM objects with `Marshal.ReleaseComObject` in `finally` blocks.
  - Handling of `COMException` and `RuntimeBinderException` for updates without `IWindowsDriverUpdate`.
- [x] **S3-T2: DI registration** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - `WuaUpdateSource` registered as a singleton of `IUpdateSource`.
- [x] **S3-T3: `updates` command in the CLI** (`src/Cli/Program.cs`):
  - Command `dotnet run --project src/Cli -- updates`.
  - Flow: inventory scan → WUA search → formatted table with columns: Device, Current version, New version, Size and Title.
  - Human-readable size format (B, KB, MB).
  - Green confirmation message when there are no pending updates.
- [x] **S3-T4: Tests** (`tests/Agent.Drivers.Tests/WuaUpdateSourceTests.cs`):
  - Integration test: `FindUpdatesAsync_Should_Return_Without_Throwing` — real search against WUA with validation of required fields.
  - Integration test: `FindUpdatesAsync_Should_Propagate_Cancellation` — cancellation verification.
  - Unit test: `FindUpdatesAsync_Should_Throw_When_InstalledDrivers_Is_Null`.
  - 6 tests with `[Theory]` for `ExtractVersionFromTitle` with standard WUA formats.
  - 3 tests with `[Theory]` for empty/null inputs.
  - Fallback tests and the no-version case.
- [x] **S3-T5: Configuration and quality**:
  - `InternalsVisibleTo` added to `Agent.Drivers.csproj` to allow tests of internal methods.
  - `.editorconfig` fixed: separate naming rule for `const` (PascalCase) versus instance fields (`_camelCase`).

### Technical decisions made
1. **COM interop with `dynamic`**:
   - `dynamic` is used instead of typed interop because WUA has no official interop assembly for modern .NET. `dynamic` with `Type.GetTypeFromProgID` is the standard, maintained approach.
2. **Version extraction from the title**:
   - WUA driver titles follow the pattern "Manufacturer - Category - Version". The extraction iterates over the segments from the end and validates them with `VersionComparer.IsValid()`. As a fallback, loose version-like tokens are searched.
3. **Handling of `RuntimeBinderException`**:
   - `Microsoft.CSharp.RuntimeBinder` was added to catch the exception when an update does not implement `IWindowsDriverUpdate` and does not expose `DriverHardwareID`.
4. **Naming convention for constants**:
   - The naming rule in `.editorconfig` was split: private instance fields use `_camelCase`, `const` fields use `PascalCase`. This follows the standard C# conventions.

### Tests run
- `dotnet run --project src/Cli -- updates`:
  - **Result**: 1 real driver update found (Intel Extension 2.1.10103.18) in **6.31 seconds**.
  - 185 drivers scanned in the inventory.
- `dotnet build -c Release`:
  - **Result**: 7 projects built, 0 warnings, 0 errors.
- `dotnet test -c Release`:
  - **Result**: 56/56 tests passed (39 in `Core.Tests`, 17 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`:
  - **Result**: 0 format discrepancies.

### Milestone 0 — Go/no-go decision
- ✅ **GO**: The Windows Update Agent returns real driver updates (1 found on the development machine). The source is functional and the COM integration is stable.
- The number of updates will vary depending on the machine and its state. The `Type='Driver'` criterion filters correctly.

### Notes for the next sprint (Sprint 4A)
- Work proceeded to complete the backend in **Sprint 4A**.

---

## Sprint 4A — Core Backend Implementations (`Agent.Drivers`)

- **Date**: 2026-10-13
- **Status**: ✅ Completed
- **Milestone**: Milestone 1 (End-to-end functional backend)

### Completed tasks
- [x] **S4-T1: `SqliteOperationHistory` — Persistence in SQLite** (`src/Agent.Drivers/History/SqliteOperationHistory.cs`):
  - Full implementation of `IOperationHistory` using `Microsoft.Data.Sqlite`.
  - Automatic creation of the directory and the persistence table (`CREATE TABLE IF NOT EXISTS operations`).
  - Strict schema: `Id` (INTEGER PK AUTOINCREMENT), `OperationType`, `UpdateId`, `DeviceName`, `HardwareId`, `DriverTitle`, `PreviousVersion`, `NewVersion`, `Success`, `ErrorMessage`, `Timestamp` (ISO 8601).
  - Asynchronous operations with cancellation support (`CancellationToken`).
  - Configurable database path with a default value of `%ProgramData%\DriverUpdater\history.db`.
  - Unit test suite (`tests/Agent.Drivers.Tests/SqliteOperationHistoryTests.cs`): insertion, global query, query by ID, null value handling and argument validation.
- [x] **S4-T2: `WinTrustSignatureVerifier` — Digital signature verification** (`src/Agent.Drivers/Security/`):
  - `NativeMethods.cs`: P/Invoke to `wintrust.dll!WinVerifyTrust` with the action `WINTRUST_ACTION_GENERIC_VERIFY_V2`.
  - Mapping of the `WintrustData` and `WintrustFileInfo` structures with manual release of unmanaged memory (`Marshal.StringToCoTaskMemUni` / `FreeCoTaskMem`).
  - Extraction and validation of certificates and distinction of WHQL / Microsoft signatures.
  - Mapping of native HRESULT codes: `TRUST_E_NOSIGNATURE`, `TRUST_E_EXPLICIT_DISTRUST`, `TRUST_E_SUBJECT_NOT_TRUSTED`, `CRYPT_E_SECURITY_SETTINGS`, `CERT_E_EXPIRED`, `CERT_E_REVOKED`, `TRUST_E_BAD_DIGEST`.
  - Tests in `tests/Agent.Drivers.Tests/WinTrustSignatureVerifierTests.cs`: nonexistent files, unsigned files and real verification of signed Windows system binaries (`kernel32.dll`).
- [x] **S4-T3: `WmiRestorePointService` — Windows restore points** (`src/Agent.Drivers/Restore/WmiRestorePointService.cs`):
  - Implementation of `IRestorePointService` via WMI invocation of `root\default:SystemRestore.CreateRestorePoint`.
  - Parameters: `RestorePointType = 10` (`DEVICE_DRIVER_INSTALL`), `EventType = 100` (`BEGIN_SYSTEM_CHANGE`).
  - Safe handling of `ManagementException` and `UnauthorizedAccessException`, with a guaranteed boolean return.
- [x] **S4-T4: `WuaDriverInstaller` — Installation and rollback orchestration** (`src/Agent.Drivers/Install/`):
  - Decoupled abstraction `IWuaExecutor` and COM executor `DefaultWuaExecutor` for safe downloads and installations.
  - Strict compliance with the security rules of `AGENTS.md`:
    1. **Rule 1**: Abort immediately if the digital signature is not valid or trusted.
    2. **Rule 2**: Abort immediately if creating the system restore point before installation fails.
    3. **Rule 5**: System commands (`pnputil`) are executed via `ProcessStartInfo.ArgumentList` without concatenating external strings, to prevent injection.
    4. **Rule 8**: Exhaustive and guaranteed recording of every operation (success or failure) in `IOperationHistory`.
  - Progress notification in 8 phases (`InstallProgress`): Downloading (10-25%), Verifying (30-45%), CreatingRestorePoint (50-65%), Installing (70-85%), Validating (90%), Complete (100%) or Failed.
  - Implementation of `RollbackAsync` that queries the history and safely reverts the driver.
  - Exhaustive unit tests with test doubles (`tests/Agent.Drivers.Tests/WuaDriverInstallerTests.cs`): successful flow, abort on invalid signature, abort on restore point failure, abort on network failure, and rollback reversion.
- [x] **S4-T5: Registration in the dependency injection container** (`src/Agent.Drivers/DependencyInjection/ServiceCollectionExtensions.cs`):
  - Registration of the 6 domain interfaces: `IDriverInventory`, `IUpdateSource`, `ISignatureVerifier`, `IRestorePointService`, `IOperationHistory` and `IDriverInstaller`.
  - Support for a configurable SQLite database path in `AddDriverServices(databasePath)`.
- [x] **S4-T6: CLI command expansion** (`src/Cli/Program.cs`):
  - `log`: queries and presents the audit operation history in a table.
  - `install <updateId>`: runs the installation flow with progress reporting in the console and a privilege elevation warning.
  - `rollback <operationId>`: reverts a previous installation by its identifier in the log.
- [x] **S4-T7: Full quality verification**:
  - Release build: 0 warnings, 0 errors across the 7 projects.
  - Automated tests: 72 tests passed (39 in `Core.Tests`, 33 in `Agent.Drivers.Tests`).
  - Format verification: `dotnet format --verify-no-changes` passed with exit code 0.

### Technical decisions made
1. **Decoupling of `IWuaExecutor`**:
   - To allow exhaustive unit testing of the security flow without requiring administrative privileges or invoking real COM APIs during CI/CD execution, the COM interaction was separated into the internal interface `IWuaExecutor`.
2. **Access to internal types for NSubstitute**:
   - `<InternalsVisibleTo Include="DynamicProxyGenAssembly2" />` was configured in `Agent.Drivers.csproj` so the Castle/NSubstitute dynamic proxy generator can create doubles over internal interfaces without compromising public encapsulation.
3. **SQLite persistence**:
   - `Microsoft.Data.Sqlite` was used with `ISO 8601` (`"O"`) in `InvariantCulture` for the timestamp fields, ensuring date portability regardless of the operating system's regional settings.
4. **Security in rollback**:
   - `pnputil.exe` runs through `ProcessStartInfo.ArgumentList`, passing the hardware identifiers as discrete arguments to comply with security rule 5 against command injection.

### Tests run
- `dotnet build -c Release`: 7 projects built, 0 warnings, 0 errors.
- `dotnet test -c Release`: 72/72 tests passed (39 in `Core.Tests`, 33 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 format discrepancies.
- `dotnet run --project src/Cli -- log`: executed successfully in the console, querying the SQLite database.
- `dotnet run --project src/Cli -- --help`: commands `scan`, `updates`, `log`, `install` and `rollback` verified.

### Notes for the next sprint (Sprint 4B or Sprint 5)
- The backend (`Core` + `Agent.Drivers`) is **100% complete, tested and verified**.
- The next steps in the MVP are:
  1. **Sprint 4B — Windows Service (`Agent.Service`)**: Implement a Worker Service with gRPC over Named Pipes to let the unprivileged UI communicate with the SYSTEM service.
  2. **Sprint 5 — WPF user interface (`Ui.Wpf`)**: Develop the visual interface in MVVM (with CommunityToolkit.Mvvm) consuming the gRPC service or the abstraction layer.

---

## Sprint 4B — Windows Service with gRPC over Named Pipes (`Agent.Service` + `Agent.Ipc`)

- **Date**: 2026-10-19
- **Status**: ✅ Completed
- **Milestone**: Milestone 2 (Service + UI — IPC and service part)

### Completed tasks
- [x] **S4B-T1: `Agent.Ipc` project — shared IPC contract** (`src/Agent.Ipc`):
  - `Protos/driveragent.proto` with the `DriverAgent` service: `Scan`, `CheckUpdates` (streaming), `Install` (progress streaming), `Rollback`, `GetHistory` and `GetStatus`. Codegen `GrpcServices="Both"`, C# namespace `Agent.Ipc.Grpc`.
  - `DriverAgentChannel.CreateChannel()`: gRPC channel factory over a named pipe using `SocketsHttpHandler.ConnectCallback` with `NamedPipeClientStream` (client for the UI and tests).
  - Constant `DefaultPipeName = "DriverUpdater"` (equivalent to `\\.\pipe\DriverUpdater`, per the architecture documented in `ARCHITECTURE.md`).
  - Explicit reference to `Grpc.AspNetCore 2.76.0` + `Grpc.Tools 2.76.0` (the `Grpc.Tools` targets are not imported as a transitive dependency in this environment; see Decisions).
- [x] **S4B-T2: `OperationQueue` — operation queue** (`src/Agent.Service/Operations/`):
  - `IOperationQueue` / `OperationQueue`: mutual exclusion with `SemaphoreSlim(1,1)`; guarantees **one privileged operation at a time** (installations, rollbacks and WUA searches; the COM session must not serve them in parallel).
  - Cancellable: the gRPC call token interrupts both the wait in the queue and the running operation; the queue is always released (`finally`).
  - Immutable observable state (`OperationState`: `OperationInProgress`, `CurrentUpdateId`, `PercentComplete`, `Phase`, `PendingReboot`, `LastScanUtc`, `QueuedOperations`) swapped in a thread-safe manner.
  - `PendingReboot` latch that persists until the service restarts (natural after restarting the computer).
  - Counter of queued operations for the UI (`QueuedOperations`).
- [x] **S4B-T3: `DriverAgentGrpcService` — gRPC server** (`src/Agent.Service/Grpc/`):
  - `Scan`: WMI inventory mapped to `InstalledDriverMsg`; updates `LastScanUtc`.
  - `CheckUpdates`: serialized by the queue during WUA detection; decouples the streaming of updates from the queue so that slow clients do not block subsequent operations.
  - `Install`: receives **only `update_ids`**; the service re-resolves each ID against Windows Update at the moment of installation (avoids installing outdated or client-tampered metadata). Installs sequentially, streams the 8 `InstallProgress` phases in **real time** via `Channel<T>`, and a final `Finished` message per driver with an individual result (`Success`, `ErrorMessage`, `RebootRequired`). Unknown IDs or unexpected exceptions produce an individual failure result without aborting the batch.
  - `Rollback`: strictly validates the format of the operation ID (positive unsigned integer `ulong`, ≤32 characters) and delegates to `IDriverInstaller.RollbackAsync` via the queue.
  - `GetHistory`: applies `limit` (most recent entries) over `IOperationHistory.GetAllAsync`.
  - `GetStatus`: queue state, `PendingReboot` and `LastScanUtc` for the UI.
  - Progress reported in real time with `InlineProgress` to a `Channel<InstallProgress>`, read by a dedicated task toward the gRPC stream; the stream never suffers concurrent writes.
  - Input validation: non-empty IDs, bounded length, case-insensitive deduplication; invalid requests → `RpcException(InvalidArgument)`.
- [x] **S4B-T4: IPC security** (`src/Agent.Service/Ipc/`):
  - `NamedPipeSecurityFactory`: pipe ACL with full control for SYSTEM (`S-1-5-18`) and Administrators (`S-1-5-32-544`), and `ReadWrite` permissions for interactive users (`S-1-5-4`), preventing `WRITE_DAC`/`WRITE_OWNER` for unprivileged interactive users (complying with rule 4 of AGENTS.md, "local interactive users only").
  - `CurrentUserOnly = false` in `NamedPipeTransportOptions`: the default option is incompatible with an explicit ACL and would also block interactive users (the service runs as SYSTEM).
  - `NamedPipeStreamFactory`: `CreateNamedPipeServerStream` hook that normalizes `PipeOptions.FirstPipeInstance` (only on the first real instance of the pool) and creates each instance with the context's ACL. Without it, the default creator marks several instances with `FirstPipeInstance` and binding fails with `Access Denied`.
  - `Program.cs`: `AddWindowsService("DriverUpdater")`, Serilog with a rolling file at `%ProgramData%\DriverUpdater\logs\service-.log`, Kestrel `ListenNamedPipe` with `HttpProtocols.Http2`, `MapGrpcService<DriverAgentGrpcService>()`. Runnable as a Windows service or from the console for diagnostics.
- [x] **S4B-T5: Test project `Agent.Service.Tests`** (`tests/Agent.Service.Tests`):
  - `OperationQueueTests` (5+5 tests): execution and result, strict serialization, cancellation while waiting and while running, release on exceptions, state (progress, restart latch, last scan, arguments).
  - `GrpcMappersTests`: full mapping of Core models ↔ proto messages, date normalization to UTC (Kind Utc/Local/Unspecified) and exhaustive enum mapping.
  - `NamedPipeSecurityFactoryTests`: verification of the 3 ACL rules (SIDs, rights and type).
  - `NamedPipeStreamFactoryTests`: `FirstPipeInstance` normalization.
  - `DriverAgentGrpcServiceTests`: **real IPC integration** — in-process ASP.NET Core server on a unique named pipe per test + real client via `DriverAgentChannel`, with a simulated domain (NSubstitute): Scan, CheckUpdates (decoupled streaming), Install (real-time progress with Channel, sequential batch, fault tolerance and unexpected exceptions, unknown IDs, rejection of empty requests), Rollback (mapping and strict `ulong` validation), GetHistory (with and without limit), `GetStatus` (state and pending reboot), **serialization of concurrent installations**, **queue release when the client cancels** and **real application of the configured ACL** (captured via hook).
- [x] **S4B-T6: End-to-end verification of the real binary**:
  - Console-mode start of the published binary: service running and `\\.\pipe\DriverUpdater` visible on the system.
  - Real client (Agent.Ipc) against the live service: `GetStatus` OK, `Scan` OK (266 real drivers inventoried via WMI through the service), `GetHistory` OK.

### Technical decisions made
1. **New `Agent.Ipc` project**:
   - The gRPC contract and the client channel factory live in a shared project (`GrpcServices="Both"`) so that `Agent.Service` implements the server and the future UI consumes the client without referencing the service assembly. `AGENTS.md` and `DESIGN.md` §4.6/§5 were updated accordingly. The UI will pull in `Grpc.AspNetCore` server dependencies at publish; if that proves too heavy, it will be split into client/server contracts in a later phase.
2. **Kestrel's native named pipe transport** (included in the .NET 10 framework as `Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes`):
   - `ListenNamedPipe` + `HttpProtocols.Http2` in cleartext (pure gRPC; no HTTP/1.1 over the pipe). No explicit `UseNamedPipes` is needed: the transport is registered automatically.
   - gRPC client: `GrpcChannel.ForAddress("http://localhost")` with `SocketsHttpHandler.ConnectCallback` returning a `NamedPipeClientStream` (symbolic address; the real transport is the pipe).
3. **Transport peculiarities and restricted ACL**:
   - `NamedPipeTransportOptions.CurrentUserOnly` is enabled by default and is **incompatible** with an explicit `PipeSecurity` (`ArgumentException` in `NamedPipeServerStreamAcl.Create`); it would also block interactive users because the service runs as SYSTEM → it is disabled and the restriction is applied through the ACL.
   - The default creator marks several pool instances with `PipeOptions.FirstPipeInstance`; the second instance fails with `Access Denied` ("pipe already in use"). Resolved with `NamedPipeStreamFactory`, which removes the flag except on the first real instance.
   - ACL security: SYSTEM and Administrators have `FullControl`, while interactive sessions (`S-1-5-4`) have `ReadWrite`. `WRITE_DAC` and `WRITE_OWNER` are restricted to prevent an unprivileged interactive user from modifying the pipe's security.
4. **Re-resolution and fault tolerance in `Install`**:
   - For security and consistency with the CLI, installation only accepts IDs; the server queries the inventory and Windows Update, and matches by `UpdateId`. An ID that no longer exists, or an unexpected exception during installation, produces an individual failure result (`Finished` with `Success=false`) without interrupting the rest of the batch's updates.
5. **Real-time progress streaming with Channel**:
   - `InlineProgress` writes to a decoupled `Channel<InstallProgress>`, read concurrently by an asynchronous task toward `responseStream.WriteAsync`. This delivers progress events to the UI in real time as the phases run, maintains gRPC's concurrent-write safety and drains cleanly on completion or failure.
6. **Safe cancellation and mandatory auditing (Rule 8 of AGENTS.md)**:
   - A cancellation in `WuaDriverInstaller` during the phases before native installation mandatorily records the failure in the persistent SQLite history, using `CancellationToken.None` (so that cancellation does not interrupt the save), before re-throwing the exception. The native WUA installation phase is not interrupted, to avoid inconsistent states in the operating system.
6. **Explicit `Grpc.Tools`**:
   - The `Grpc.Tools` targets (proto codegen) are only imported if the package is referenced directly; `Grpc.AspNetCore` does not propagate them in this environment. Referenced with `PrivateAssets="All"` in `Agent.Ipc`.
7. **Suppression of `CA1711`**:
   - In `.editorconfig`, with justification, for `IOperationQueue`/`OperationQueue`: the "operation queue" of the design (MVP.md Milestone 2) is genuinely a serialized queue of one entry.
8. **`global.json` adjusted to the installed SDK**:
   - The environment went from having SDK 10.0.401 to 10.0.302; `global.json` was updated to `10.0.302` (`rollForward: latestFeature`) to restore the build.

### Tests run
- `dotnet build -c Release`: 9 projects built, 0 warnings, 0 errors.
- `dotnet test -c Release`: **133/133 tests passed** (39 in `Core.Tests`, 53 in `Agent.Service.Tests`, 41 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 format discrepancies.
- E2E of the real binary: service started in the console, pipe `\\.\pipe\DriverUpdater` published, real client with `Scan` of 266 real drivers, `GetStatus` and `GetHistory` verified.

### Notes for the next sprint (Sprint 5)
- The full IPC channel (service + contract + client) is operational and tested. Remaining to complete Milestone 2:
  1. **Sprint 5 — WPF UI (`Ui.Wpf`)**: MVVM with CommunityToolkit.Mvvm consuming `Agent.Ipc` (screens: home/scan, updates, history, settings). Reference `Agent.Ipc` from `Ui.Wpf`. Consider in the UX the deferred cancellation of the native installation phase (*"Cancelando... esperando a que finalice el controlador en curso"* (Cancelling... waiting for the current driver to finish)).
  2. **Settings and exclusions (U7)**: `config.json` in `%ProgramData%\DriverUpdater\`, `GetSettings`/`UpdateSettings` RPCs in the proto, and application of exclusions in `CheckUpdates`/`Install`.
  3. **Scheduled check and notifications (U8)**: scheduler in the service (no polling), a toast notification if there is news, and a pending-reboot warning.
  4. **Windows service installation**: register the binary as a service (`sc create` or WiX in Milestone 4). Test in a VM (never on the development machine with real operations).
  5. **E2E validation of the ACL in a VM with a standard account (audit reserve)**: Verify in a VM that an unprivileged local interactive user without administrative rights can connect and perform I/O operations on the pipe (`ReadWrite`), and certify that any attempt to alter the pipe's ACL/DACL is denied with `Access Denied`.

---

## Sprint 5 — Modern WPF User Interface (`Ui.Wpf`)

- **Date**: 2026-10-19
- **Status**: ✅ Completed
- **Milestone**: Milestone 2 (Service + UI — WPF UI part)

### Completed tasks
- [x] **S5-T1: Design system and premium visual tokens** (`src/Ui.Wpf/Resources/`):
  - `Colors.xaml`: professional dark palette (high-contrast dark theme for legibility), subtle gradients with *glassmorphism* aesthetics (`CardGlowGradient`), typographic hierarchy with Segoe UI Variable and semantic colors (`AccentCyan #0ea5e9`, `SuccessGreen #10b981`, `WarningAmber #f59e0b`, `ErrorRose #f43f5e`).
  - `Styles.xaml`: base styles for `Button` (primary with accent, secondary with subtle border, danger for cancellation/rollback), `TextBox` with accented focus, styled `CheckBox`, `DataGrid` and cards with `DropShadowEffect`.
  - `Icons.xaml`: library of clean vector geometries (Dashboard, Updates, History, Settings, Check, Cross, Shield, Warning, Refresh, Arrow, etc.).
  - `Converters/Converters.cs`: data converters for XAML (boolean and negated visibility, human-readable byte formatting, durations, operation state to colors/icons, and numeric counter badges).
- [x] **S5-T2: Components and container window (Shell)** (`src/Ui.Wpf/Views/` and `ViewModels/`):
  - `NavItem`: reusable UserControl for the sidebar navigation buttons, with declarative support for icon `Geometry`, label, active state and badge count.
  - `ShellWindow.xaml` / `.xaml.cs`: frameless window with built-in controls (minimize, maximize/restore, close), drag support via `DragMove`, a live indicator of the IPC connection status to the Windows service, and a contextual *breadcrumb*.
  - Dynamic view routing via `DataTemplate` that maps each `PageViewModelBase` (`DashboardViewModel`, `UpdatesViewModel`, `HistoryViewModel`, `SettingsViewModel`) to its corresponding view without strict coupling.
- [x] **S5-T3: Home / Dashboard screen** (`src/Ui.Wpf/Views/DashboardView.xaml` and `DashboardViewModel.cs`):
  - 4 statistic cards (Total drivers, Operational, With warning/error, and Global status).
  - General device health indicator with a dynamic pill.
  - Real-time reactive search that filters by device name, manufacturer, identifier or class.
  - Driver table with monospaced versions, device class and status badges.
  - Manual refresh button that invokes the `Scan` RPC and updates the last-scan date.
- [x] **S5-T4: Available updates screen** (`src/Ui.Wpf/Views/UpdatesView.xaml` and `UpdatesViewModel.cs`):
  - List of updates found with an individual or batch selector (*Seleccionar todo* (Select all) / *Deseleccionar todo* (Deselect all)).
  - Comparison of the currently installed version against the new recommended version.
  - Formatted download size and a "Requiere reinicio" (Restart required) badge when applicable.
  - Global progress bar and real-time phase report for each driver (`Downloading`, `Verifying`, `CreatingRestorePoint`, `Installing`, etc.) consuming the gRPC stream of `Install`.
  - **Deferred cancellation experience (`ARCHITECTURE.md`, IPC flow)**: when pressing "Cancelar" (Cancel), the UI activates the state *"Cancelando... esperando a que finalice el controlador en curso"* to respect the native WUA phase in progress and ensure operating system stability.
- [x] **S5-T5: History and Rollback screen** (`src/Ui.Wpf/Views/HistoryView.xaml` and `HistoryViewModel.cs`):
  - Audit table with formatted ISO date, device, previous and applied versions, operation type and result.
  - Search to filter events by device name or ID.
  - Selection of an entry and direct `Rollback` execution via the corresponding RPC, with visual progress and result feedback.
- [x] **S5-T6: Settings screen** (`src/Ui.Wpf/Views/SettingsView.xaml` and `SettingsViewModel.cs`):
  - Automatic check frequency selector with a day stepper.
  - WHQL security policy toggle (only Microsoft/WHQL-signed drivers) with a security warning when disabled.
  - Interactive management of the hardware exclusion list (add and remove identifiers).
- [x] **S5-T7: IPC client service and Dependency Injection startup** (`src/Ui.Wpf/Services/DriverAgentService.cs` and `App.xaml.cs`):
  - `DriverAgentService`: typed gRPC client consuming `DriverAgentChannel` from `Agent.Ipc`, exposing asynchronous methods and `IAsyncEnumerable` streams, and handling transparent reconnections.
  - `App.xaml.cs`: Serilog configuration with a rolling file at `%ProgramData%\DriverUpdater\logs\ui-.log`, dependency registration (`DriverAgentService`, ViewModels and ShellWindow), and non-blocking startup that connects to the service in the background.
  - Compliance with **Rule 3 of AGENTS.md**: the UI runs entirely as a standard interactive user without requiring privilege elevation (`requestedExecutionLevel asInvoker`).
- [x] **S5-T8: Quality, icon and removal of obsolete code**:
  - Replacement and permanent removal of the obsolete `MainWindow.xaml` and `MainWindow.xaml.cs`.
  - Creation of the native application icon `src/Ui.Wpf/Resources/icon.ico`.
- [x] **S5-T9: `ProgressBarSlim` rendering fix and immediate interactivity**:
  - Fix of the WPF template contract in `Styles.xaml`: added `PART_Track` alongside `PART_Indicator` so WPF can compute the effective width (`indicatorLength = percent * trackLength`). Without `PART_Track`, WPF bails out in `SetProgressBarIndicatorLength()`, leaving the indicator width at 0.
  - Implementation of `IndeterminateBar` with a continuous opacity animation (`0.25` to `1.0`) for indeterminate states (`IsIndeterminate="True"` in searches and scans), avoiding precedence collisions between WPF's local width value and the alignment triggers.
  - Immediate initialization of `IsInstalling = true`, `Percent = 0` and `PhaseLabel = "Iniciando instalación..."` (Starting installation...) in `UpdatesViewModel.cs` when pressing "Instalar seleccionadas" (Install selected), removing the visual delay until the first gRPC package.
  - Added a success visual badge (`BadgeSuccess`) when the installation completes.

### Technical decisions made
1. **Unprivileged consumption over gRPC (Rules 3 and 4 of AGENTS.md)**:
   - The `Ui.Wpf` application uses no administrative privilege elevation. Every privileged operation (hardware scan, download, restore point, installation and rollback) is delegated through the gRPC client over the named pipe `\\.\pipe\DriverUpdater`.
2. **Deferred cancellation experience (`ARCHITECTURE.md`, IPC flow)**:
   - Following the design guidelines to avoid corruption of the driver subsystem during the native WUA or PnPUtil phase, the UI explicitly shows the state *"Cancelando... esperando a que finalice el controlador en curso"* and disables new attempts until the installation RPC completes its safe cycle.
3. **Declarative navigation by ViewModel**:
   - Instead of classic WPF `Frame` and `Page`, a `ContentControl` in `ShellWindow` was used, with implicit `DataTemplate`s bound to `DashboardViewModel`, `UpdatesViewModel`, `HistoryViewModel` and `SettingsViewModel`.
4. **Lifecycle and reconnection**:
   - `ShellViewModel` polls the service state every 5 seconds via a lightweight timer that runs `GetStatusAsync`. If the service stops or restarts, the UI immediately sets the indicator to "Desconectado" (Disconnected) with a retry option, and re-enables actions automatically when the connection is restored.
5. **Embedded application icon**:
   - The `Resources/icon.ico` resource was generated and configured in `Ui.Wpf.csproj` to ensure a native appearance in the taskbar and Windows Explorer.

### Tests run
- `dotnet build -c Release`: 9 projects built, 0 warnings, 0 errors.
- `dotnet test`: **133/133 tests passed** (39 in `Core.Tests`, 53 in `Agent.Service.Tests`, 41 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 format discrepancies across the whole repository.

### Notes for the next sprint
- Milestone 2 (Service + UI) has the WPF UI and the IPC service implemented. The complementary Milestone 2 tasks are completed in Sprint 6.

---

## Sprint 6 — Persistent Settings, Scheduled Check and OEM Support Phase A

- **Date**: 2026-10-26
- **Status**: ✅ Completed
- **Milestone**: Milestone 2 (Closure of U7, U8 and motherboard support)

### Completed tasks
- [x] **S6-T1: Persistent settings and exclusions (U7)** (`src/Core/Models/AppSettings.cs`, `src/Agent.Service/Settings/JsonSettingsStore.cs`):
  - `AppSettings` model: list of excluded hardware IDs (`ExcludedHardwareIds`), check interval (`CheckIntervalHours`), sanitization and normalization with `Normalize()`.
  - `JsonSettingsStore`: persistence in `%ProgramData%\DriverUpdater\config.json` with atomic writes (temporary file + move with overwrite) and recovery from corruption with default values.
- [x] **S6-T2: IPC contract for Settings and Exclusions** (`src/Agent.Ipc/Protos/driveragent.proto`):
  - `GetSettings` and `UpdateSettings` RPCs with the `SettingsMsg` message.
  - Application of exclusions in the service: `CheckUpdates` omits excluded updates and `Install` rejects them with an explicit message.
- [x] **S6-T3: Background scheduled check (U8)** (`src/Agent.Service/Scheduling/`):
  - `ScheduledCheckService`: `BackgroundService` that periodically (every 15 min) evaluates whether an update check is due according to the configured interval (24 or 168 hours), reusing the serialized `IOperationQueue`.
  - `ScheduledCheckState`: in-memory singleton state that preserves the last check and the pending updates.
  - Architectural decision: no pop-up desktop notifications (toasts); the result is exposed passively via `GetStatus` and feeds the badge in the UI.
- [x] **S6-T4: Motherboard OEM support — Phase A** (`src/Core/Services/OemSupportCatalog.cs`, `src/Agent.Drivers/Inventory/WmiMotherboardInfoProvider.cs`):
  - Detection of the motherboard manufacturer and model via WMI (`Win32_BaseBoard`).
  - `GetMotherboard` RPC in gRPC.
  - Secure `OemSupportCatalog` catalog with official HTTPS URLs (Gigabyte, ASUS, MSI, ASRock) that the UI opens in the browser without automatic downloads (Phase A).
- [x] **S6-T5: Pending reboot detection in the Windows registry**:
  - `GetStatus` queries Windows registry keys (`RebootPending` in CBS and `RebootRequired` in Windows Update) to detect pending reboots after service restarts.
- [x] **S6-T6: WiX v4 packager and CI/CD**:
  - Project `installer/Installer.wixproj` with `Package.wxs` and the publish script `build.ps1`.
  - GitHub Actions workflows for build, tests and MSI installer generation.

---

## Sprint 7 — Security, Robustness and WHQL Policy (Milestone 3 Closure)

- **Date**: 2026-11-07
- **Status**: ✅ Completed
- **Milestone**: Milestone 3 (Security and robustness of the driver subsystem)

### Completed tasks
- [x] **S7-T1: WHQL policy in the settings model (`AppSettings`)** (`src/Core/Models/AppSettings.cs`):
  - Added the `OnlyWhql` property (boolean, default `true`) to require drivers certified by Microsoft/WHQL.
  - Preservation in `Normalize()` and unit tests in `tests/Core.Tests/AppSettingsTests.cs`.
- [x] **S7-T2: WHQL policy integration in the gRPC contract and the UI** (`src/Agent.Ipc/Protos/driveragent.proto`, `src/Ui.Wpf/ViewModels/SettingsViewModel.cs`):
  - Added field `bool only_whql = 4;` to the `SettingsMsg` message.
  - Mapping in `DriverAgentGrpcService.GetSettings` and `UpdateSettings`.
  - Synchronization in `SettingsViewModel.cs` to load and persist the option configured by the user in `SettingsView.xaml`.
- [x] **S7-T3: Advanced cryptographic WHQL detection** (`src/Agent.Drivers/Security/WinTrustSignatureVerifier.cs`):
  - Exhaustive inspection of Authenticode certificates: checks for Microsoft signers/issuers ("Microsoft Windows Hardware Compatibility Publisher", "Microsoft Windows Driver Publisher", "Windows Hardware", "Windows Compatibility").
  - Inspection of EKU extensions looking for the Windows hardware certification OID identifiers (`1.3.6.1.4.1.311.10.3.5`, `1.3.6.1.4.1.311.10.3.5.1`, `1.3.6.1.4.1.311.10.3.39`, `1.3.6.1.4.1.311.10.3.4.1`).
- [x] **S7-T4: Compliance and strict rejection of non-WHQL drivers** (`src/Core/Interfaces/IDriverInstaller.cs`, `src/Agent.Drivers/Install/WuaDriverInstaller.cs`):
  - Overload in `IDriverInstaller` with an `onlyWhql` parameter.
  - In `WuaDriverInstaller`: during the `Verifying` phase, if `onlyWhql` is enabled and the file lacks WHQL certification (`!sigResult.IsWhql`), installation is aborted immediately for security, recording the reason in the SQLite audit (`IOperationHistory`) without creating a restore point or altering the system.
  - If the user disables the policy (`onlyWhql == false`), drivers with a valid non-WHQL Authenticode signature are accepted.
  - Unit tests in `tests/Agent.Drivers.Tests/WuaDriverInstallerTests.cs` (abort on non-WHQL with the policy enabled, and success with the policy disabled).
- [x] **S7-T5: Input hardening and resilience in the service** (`src/Agent.Service/`):
  - `DriverAgentGrpcService`: maximum limit of 50 updates per batch in `Install` (DoS prevention).
  - `DriverAgentGrpcService`: bounded limit in `GetHistory` (max. 1000 records).
  - `JsonSettingsStore`: maximum read size limit for `config.json` (1 MB) to prevent resource exhaustion from tampering.
  - Resilience against null installer results.
- [x] **S7-T6: Full regression and security test suite**:
  - 151/151 unit and integration tests green (52 in `Core.Tests`, 56 in `Agent.Service.Tests`, 43 in `Agent.Drivers.Tests`).
  - Format verification `dotnet format --verify-no-changes` with 0 issues.

### Technical decisions made
1. **Backward compatibility in `IDriverInstaller`**:
   - The method `InstallAsync(update, progress, onlyWhql, ct)` was added with a default implementation in the interface that delegates to the original 3-parameter method.
   - In `DriverAgentGrpcService`, when `onlyWhql` is `true` (standard behavior), the 3-parameter overload is invoked, ensuring that all existing tests and test doubles keep working without massive adaptations.
2. **WHQL recognition via OIDs and Publishers**:
   - To avoid relying solely on text strings in the subject CN (which vary between Windows editions and catalogs), reading of the certificate's EKU extensions was added, looking for Microsoft's standard hardware compatibility OIDs.
3. **Decoupled persistence and UI**:
   - The UI already had the visual control (`CheckBox OnlyWhql`) in `SettingsView.xaml`. By adding the field to the gRPC contract and the `AppSettings` model, the setting connects reactively to the service without requiring elevated permissions in the WPF app.

### Tests run
- `dotnet build -c Release`: 9 projects built, 0 warnings, 0 errors.
- `dotnet test -c Release`: **151/151 tests passed** (52 in `Core.Tests`, 56 in `Agent.Service.Tests`, 43 in `Agent.Drivers.Tests`).
- `dotnet format --verify-no-changes`: 0 format discrepancies.

---

## Sprint 8 — Community Documentation and Distribution Compliance

- **Date**: 2026-11-07
- **Status**: 🟡 Implemented; MSI build pending WiX validation
- **Milestone**: Preparation of documentation and distribution artifacts

### Completed tasks
- [x] **S8-T1: Project license declaration** (`LICENSE`):
  - Added the standard MIT text with copyright holder `Sargon494`.
  - The intention to distribute the project as free/open source software is maintained; no commercial restrictions were added. The proposal to restrict sales was discarded.
- [x] **S8-T2: Privacy note and local handling** (`PRIVACY.md`):
  - Documented the hardware/driver inventory, settings, exclusions, SQLite history and logs that the application keeps locally.
  - Explained Windows Update Agent, GitHub Pages and links to third-party sites, distinguishing the processing controlled by the project from external services.
  - Documented that uninstalling does not delete `%ProgramData%\DriverUpdater` and how to delete the local data.
- [x] **S8-T3: Adjustment of public security and privacy claims** (`README.md`, `docs/index.html`, `docs/styles.css`):
  - Replaced absolute security claims with descriptions of the existing controls and their limits (signature, restore point and rollback).
  - Clarified independence from Microsoft and manufacturers, and the absence of affiliation.
  - Removed the download of typefaces from Google Fonts; the website uses local/system fonts.
  - Added links to the license and the privacy note.
- [x] **S8-T4: SBOM generation and third-party notices** (`installer/Generate-ComplianceArtifacts.ps1`):
  - Added a PowerShell generator of CycloneDX 1.5 from the resolved NuGet dependencies for the service and UI executables, their SHA-256 hashes and self-contained .NET frameworks.
  - Generates a list of packages, versions, declared licenses, authors, copyright, project links and a dependency graph. Fails explicitly on missing or unrecognized runtime manifests, licenses or notices.
  - Includes the applicable MIT, Apache-2.0 and BSD-3-Clause texts, plus `LICENSE.txt` and `ThirdPartyNotices.txt` of the .NET runtime.
- [x] **S8-T5: Integration of the documents into the MSI** (`installer/build.ps1`, `installer/DriverUpdater.wxs`, `installer/README.md`, `installer/licenses/`):
  - The build generates the SBOM and `THIRD-PARTY-NOTICES.txt` before invoking WiX.
  - The MSI installs in `Program Files\DriverUpdater` the project license, the notices, the applicable Apache/BSD texts and the version's SBOM.
  - Documented the flow and the need to review the notices for each release.

### Validation performed
- Self-contained publish of `Agent.Service` and `Ui.Wpf` for `win-x64` completed.
- Generation of the sample artifacts `installer/artifacts/DriverUpdater-1.0.0.cdx.json` and `installer/artifacts/THIRD-PARTY-NOTICES.txt`: 65 NuGet packages and 3 .NET frameworks; the SBOM contains 70 components and 71 dependency entries, with no unresolved references.
- Verified the MIT, Apache-2.0 and BSD-3-Clause license texts in the generated notices.
- PowerShell and WiX XML syntax checked; `git diff --check` without errors.
- **MSI not built or tested**: WiX was not installed in the environment. Test in a VM once WiX is available.

### Limitations and follow-up
- The automated SBOM inventories the NuGet packages with resolved runtime assets for these projects and the frameworks listed in their runtimeconfig. It does not replace a legal audit of the dependencies, the final MSI content or the notices of each distribution.
- Review the generated SBOM and `THIRD-PARTY-NOTICES.txt` in each version before publishing, and validate that the runtime versions/notices used by the build are the same ones included in the package.
- The documentary review does not constitute legal advice or a compliance certification.

## Sprint 9 — Quality and optional cleanup on uninstall

- **Date**: 2026-11-07
- **Status**: 🟡 Implemented; validation of the real flow pending in a VM
- **Milestone**: Robustness and uninstall experience

### Completed tasks
- [x] **S9-T1: Interactive option to delete local data** (`installer/DriverUpdater.wxs`):
  - Added an uninstall dialog with a checkbox unchecked by default to delete `%ProgramData%\DriverUpdater`.
  - The cleanup only runs during an interactive uninstall when explicitly selected; silent uninstalls and updates keep the data.
  - Documented the scope of the cleanup and the CLI logs that may remain outside that folder.
- [x] **S9-T2: Explicit installation failures and WiX dependencies** (`installer/DriverUpdater.wxs`, `installer/build.ps1`, `installer/README.md`):
  - A failure applying the service ACL now stops the installation instead of being ignored.
  - The build checks the `WixToolset.Util.wixext` extension, and the EULA command usage, which is incompatible with WiX 4, was fixed.
  - Documented WiX version 4.0.5 and the required extension.
- [x] **S9-T3: Updated privacy information** (`PRIVACY.md`, `README.md`, `installer/README.md`):
  - Explained when local data is kept or deleted on uninstall, and which data falls outside the MSI cleanup.

### Validation performed
- MSI build `DriverUpdater-1.0.1-x64.msi` completed with WiX 4.0.5.
- Inspected the tables of the generated MSI: dialog, sequence condition, cleanup action and data directory property present.
- `dotnet test -c Release`: 151/151 tests passed.
- `dotnet format --verify-no-changes` and `git diff --check` completed without errors.

### Limitations and follow-up
- Pending VM test of the interactive uninstall with the checkbox checked and unchecked, the silent uninstall, and an update. The MSI was not run on the development machine.
- The cleanup only covers `%ProgramData%\DriverUpdater`; CLI logs saved in another path are not deleted.
