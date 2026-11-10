# DriverUpdater architecture

This is a guide to the **implemented architecture**: components, dependencies, flows, and extension points. It is not the requirements register or the change history.

- `MVP.md` is the source of truth for scope and acceptance criteria.
- `DESIGN.md` keeps lasting technical decisions and open questions.
- `AGENTS.md` defines conventions and working rules.
- `TESTING.md` contains manual test procedures, the rationale for the automated test suites, and VM results.
- `SPRINT_LOG.md` is historical; its notes reflect the state at the close of each sprint.

## 1. What it is

An application for Windows 10/11 that searches for official driver updates (Windows Update) and installs them safely: signed drivers only, with a restore point beforehand and an operations log.

It is split into two processes that talk to each other:

- **Service** (`Agent.Service`): runs as a Windows service under the SYSTEM account. It is the only component that touches the system: WMI, Windows Update, `pnputil`, restore points, and signatures.
- **UI** (`Ui.Wpf`): a WPF application without administrator privileges. It only displays information and sends commands to the service.

Communication is **gRPC over a local named pipe** (`\\.\pipe\DriverUpdater`).

```
┌──────────────────────────┐   gRPC / named pipe    ┌──────────────────────────────┐
│ Ui.Wpf (user)            │ ─────────────────────▶ │ Agent.Service (SYSTEM)       │
│ MVVM, no privileges      │ ◀───────────────────── │ gRPC server + queue + tasks  │
└──────────────────────────┘                        └──────────────┬───────────────┘
        │ uses                                                    │ uses
        ▼                                                         ▼
  Agent.Ipc (.proto contract + client channel)           Agent.Drivers (WMI, WUA, pnputil,
                                                         signatures, restore points, SQLite)
                                                                  │
                                                                  ▼
                                                              Core (models and interfaces)
```

## 2. Projects

| Project | Responsibility | Depends on |
|---|---|---|
| `src/Core` | Models (`InstalledDriver`, `DriverUpdate`, `AppSettings`…), interfaces (`IDriverInventory`, `IUpdateSource`, `IDriverInstaller`, `ISettingsStore`…), and pure logic: `VersionComparer`, `DriverMatcher`, `OemSupportCatalog`. **No Windows dependencies.** | nothing |
| `src/Agent.Drivers` | Implementations that touch Windows: WMI inventory, Windows Update (COM), installation via WUA, restore points, signature verification (WinVerifyTrust), SQLite history. | `Core` |
| `src/Agent.Ipc` | gRPC contract (`Protos/driveragent.proto`) and `DriverAgentChannel`, the client channel factory over the named pipe. Shared by the service and the UI. | nothing |
| `src/Agent.Service` | gRPC server, operations queue, scheduled check, persistent settings, pipe ACL. | `Core`, `Agent.Drivers`, `Agent.Ipc` |
| `src/Ui.Wpf` | WPF interface in MVVM (CommunityToolkit.Mvvm). Client of the service. | `Core`, `Agent.Ipc` |
| `src/Cli` | Diagnostic tool without UI (`scan`, `updates`). Uses `Agent.Drivers` directly, without the service. | `Core`, `Agent.Drivers` |
| `tests/Core.Tests` | Unit tests for the pure logic. | `Core` |
| `tests/Agent.Drivers.Tests` | Tests for the implementations; integration tests are marked with `[Trait("Category","Integration")]`. | `Agent.Drivers` |
| `tests/Agent.Service.Tests` | Service tests: they start a real gRPC host over a pipe with doubles of the interfaces. | `Agent.Service` |
| `tests/Integration.Tests` | Component integration tests: real settings store, SQLite history, and operations queue, with no doubles. Rationale in `TESTING.md`, section 4. | `Core`, `Agent.Drivers`, `Agent.Service` |
| `installer/` | MSI installer (WiX v4), build script, and test guide. | — |

`Core` is the center: it knows nothing about Windows or gRPC, and everything else depends on it. This way the logic can be tested without a Windows machine.

## 3. Service (`Agent.Service`)

### Startup (`Program.cs`)

1. Configures Kestrel to listen on the named pipe `DriverUpdater`, with HTTP/2 and plain gRPC.
2. Applies the pipe ACL (`NamedPipeSecurityFactory`): only local interactive users can connect.
3. Registers the dependencies in DI:
   - `IOperationQueue` → `OperationQueue` (singleton).
   - `ISettingsStore` → `JsonSettingsStore`.
   - `ScheduledCheckState` (singleton) and `ScheduledCheckService` (background service).
   - The `Agent.Drivers` implementations through `AddDriverServices()`.
4. Maps `DriverAgentGrpcService`.

### Main components

- **`DriverAgentGrpcService`**: implements all the RPCs. It translates gRPC messages to `Core` models and back (`GrpcMappers`). Every operation that touches the system goes through the queue.
- **`OperationQueue`**: serializes operations with a semaphore. A search and an installation never overlap. It exposes the state (`OperationState`): operation in progress, percentage, phase, last scan, and operations waiting in the queue.
- **`ScheduledCheckService`**: every 15 minutes it checks whether a check is due. Based on `CheckIntervalHours` (0, 24, or 168) it searches for updates through the **same queue** as the UI, and stores the result in `ScheduledCheckState` (memory). It does not start with Windows: it only runs while the service is running.
- **`JsonSettingsStore`**: reads and writes `%ProgramData%\DriverUpdater\config.json`. Writes are atomic (temporary file + replacement). If the file is corrupt, it uses default values. It normalizes and validates before saving (`AppSettings.Normalize`).

### Exposed RPCs

| RPC | What it does |
|---|---|
| `Scan` | Inventory of installed drivers (WMI). |
| `CheckUpdates` | Stream with the available updates. Filters out the ones the user excluded. |
| `Install` | Progress stream for the installation. Rejects excluded updates. Creates a restore point first. |
| `Rollback` | Reverts an operation recorded in the history. |
| `GetHistory` | Operations log (SQLite). |
| `GetStatus` | Queue state, last scan, pending reboot (service memory or a Windows flag in the registry), and the result of the last scheduled check. |
| `GetSettings` / `UpdateSettings` | Reads and saves the persistent settings. |
| `GetMotherboard` | Motherboard manufacturer and model (`Win32_BaseBoard`). |

## 4. User interface (`Ui.Wpf`)

### MVVM structure

- **`App.xaml.cs`**: startup and DI. Registers `DriverAgentService` and the ViewModels as singletons.
- **`ShellWindow`** + **`ShellViewModel`**: main window. Navigation between pages (`AppPage`), connection state (`IsConnected`, `IsConnecting`), and polling of the service every 5 seconds. If there is no connection, it shows the retry screen.
- **Pages**: `DashboardViewModel`/`DashboardView` (inventory grouped by class), `UpdatesViewModel`/`UpdatesView` (search, select, install, exclude), `HistoryViewModel`/`HistoryView`, `SettingsViewModel`/`SettingsView`.
- **`DriverAgentService`**: the UI's only gateway to the service. It wraps the gRPC client, manages the channel, and controls the Windows service (`ServiceController`) to start it on demand.

### Connection flow

1. `ShellViewModel.InitializeAsync` → `ConnectOrStartAsync`.
2. It tries to connect. If that fails, it starts the service with `ServiceController` and retries for up to 5 seconds (the service shows as "Running" before its pipe is ready).
3. If it connects, it loads the active page and starts polling.

The service **does not** start with Windows: the installer registers it in manual mode (`Start="demand"`) and grants interactive users permission to start and stop it (`sc sdset`).

### Styles and resources

- `Resources/Colors.xaml`: palette (color and typography tokens).
- `Resources/Styles.xaml`: styles for buttons, cards, expanders, and animations. Includes `Icons.xaml` through `MergedDictionaries`.
- `Resources/Icons.xaml`: SVG geometries as `Geometry`.

## 5. Key data flows

### Search for updates (Updates page)

```
UpdatesViewModel.CheckUpdatesAsync
  → DriverAgentService.CheckUpdatesAsync (gRPC stream)
    → DriverAgentGrpcService.CheckUpdates
      → OperationQueue.EnqueueAsync
        → IDriverInventory.GetInstalledDriversAsync   (WMI)
        → IUpdateSource.FindUpdatesAsync               (WUA)
        → exclusion filter (AppSettings)
  ← DriverUpdateMsg for each update
```

### Install

```
UpdatesViewModel.InstallSelectedAsync
  → Install (progress stream)
    → OperationQueue
      → IDriverInstaller (WuaDriverInstaller), which:
          · tries to verify the received paths with ISignatureVerifier / WinVerifyTrust
          · creates a restore point per driver and aborts if it fails
          · installs via WUA
      → IOperationHistory.RecordAsync (SQLite)
  ← InstallProgressMsg (phase, percentage, result)
```

### Exclude an update

```
UpdatesViewModel.ExcludeAsync
  → GetSettings → adds the hardware ID → UpdateSettings
  → the list is updated; later searches no longer show it
```

### Scheduled check and in-app indicator

```
ScheduledCheckService (every 15 min)
  → if due: EnqueueAsync(search for updates)
  → ScheduledCheckState.Record(time, pending count)
GetStatus returns that data
ShellViewModel.PollStatusAsync → UpdatesBadge (only for a new check)
```

The scheduled check shows no desktop notifications. It only updates the indicator on the Updates tab.

### Installation and verification

The installer downloads through WUA, verifies the paths returned by the executor, and only then creates a restore point and invokes the native installation. `WuaDriverInstaller` aborts and records a failure if the successful download does not include verifiable files. Because `DefaultWuaExecutor` still returns an empty list of paths, real installation through that adapter fails safely until the way to obtain and verify the files is resolved. Do not claim that each package is actually verified, and do not enable installation through this path until this gap is closed.

This case is pending isolated validation; see `TESTING.md` for the restrictions on VM testing. Product policy still requires a valid signature and, by default, WHQL (`OnlyWhql = true`).

### Motherboard support (phase A)

```
DashboardViewModel → GetMotherboard → Win32_BaseBoard
OemSupportCatalog.Find(manufacturer, model) → official HTTPS search for the model
The user chooses the product/revision on the OEM site and opens its downloads.
The application only opens the browser; it does not download or install from that page.
```

## 6. Storage

| Data | Where | Who writes it |
|---|---|---|
| Settings | `%ProgramData%\DriverUpdater\config.json` | Service (`JsonSettingsStore`) |
| Operations history | `%ProgramData%\DriverUpdater\history.db` (SQLite) | Service |
| Service logs | `%ProgramData%\DriverUpdater\logs\service-*.log` | Service (Serilog) |
| UI logs | `%ProgramData%\DriverUpdater\logs\ui-*.log` | UI (Serilog) |

The UI never writes to `%ProgramData%`: it asks the service to make the changes.

## 7. Security (summary)

The working rules are in `AGENTS.md`; `DESIGN.md` keeps the security decisions. The ones that define the architecture:

- Privilege lives in the service. The UI never runs elevated.
- The pipe only accepts local users. The ACL is set when the pipe is created.
- Policy requires a valid signature and, by default, WHQL. See the limitation of the verification flow described above: the current WUA adapter does not return file paths.
- There is always a restore point before installing. If it cannot be created, nothing is installed.
- Support searches are generated only for known manufacturers and use HTTPS on their official domains. The WMI model is encoded as a search parameter; the motherboard revision is not assumed.
- The product queries Windows Update through WUA; the OEM support button opens an HTTPS model search on the official site in the browser. There is no project backend and no project telemetry.

## 8. How to extend

### Add a new RPC

1. Define the message and the `rpc` in `src/Agent.Ipc/Protos/driveragent.proto`.
2. Implement the method in `DriverAgentGrpcService` (if it touches the system, run it through `OperationQueue`).
3. If it needs domain logic, add the interface in `Core/Interfaces` and the implementation in `Agent.Drivers`.
4. Add the wrapper method in `Ui.Wpf/Services/DriverAgentService.cs`.
5. Add tests in `tests/Agent.Service.Tests` (using the harness, see §9).

### Add a page to the UI

1. Create `ViewModels/XxxViewModel.cs` inheriting from `PageViewModelBase` and register it in `App.xaml.cs`.
2. Create `Views/XxxView.xaml` (UserControl) and its `DataTemplate` in `ShellWindow.xaml`.
3. Add the value to `AppPage` and the `NavItem` in the sidebar.

### Add an update source

Implement `IUpdateSource` in `Agent.Drivers` and register it in `ServiceCollectionExtensions`. OEM sources (phase B) are pending a license review (open decisions in `DESIGN.md`).

## 9. Tests

- **`Core.Tests`**: pure logic, fast, no Windows.
- **`Integration.Tests`**: real components (settings file, SQLite history, operations queue) working together, with no doubles. Its purpose and the reasons for each test are documented in `TESTING.md`, section 4.
- **`Agent.Service.Tests`**: `AgentHarness` starts a real gRPC host over a pipe with the `Agent.Drivers` doubles (NSubstitute). **Every new service dependency must be registered in both harnesses** (`AgentHarness` and the pipe ACL one), or every RPC fails with `Exception was thrown by handler`. The settings and motherboard doubles are in `SettingsStubs`.
- **`Agent.Drivers.Tests`**: integration tests marked with `[Trait("Category","Integration")]`.
- **UI**: has no automated tests. It is tested in a VM with a standard account.

To run only the tests that do not depend on Windows-specific components, filter out the integration category: `dotnet test --filter "Category!=Integration"`.

CI (`.github/workflows/ci.yml`) builds and runs the tests on `windows-latest`. If a test fails, each failed test appears as an annotation on the run.

## 10. Build, installer, and release

- `installer/build.ps1 -Version X.Y.Z`: publishes the service and the UI as self-contained single files (`win-x64`) and generates the MSI with WiX.
- `IncludeNativeLibrariesForSelfExtract=true` is required for the service: without it, SQLite cannot find its native DLL.
- CI: the `build-installer` job generates the MSI on each push to `master`. `release.yml` creates the release when a `v*` tag is pushed.

## 11. Known pitfalls

- **XAML**: `StaticResource` only sees the keys of its own dictionary and of its `MergedDictionaries`. `Storyboard` durations inside styles must be literals (`0:0:0.15`), not `DynamicResource`: it breaks the sealing of the styles when the window loads.
- **Naming**: `.editorconfig` requires `_camelCase` for all private fields, including `static readonly` ones. Warnings count as errors in CI.
- **Service ACL**: `sc sdset` replaces the whole ACL. If you only grant permissions to interactive users, also keep the status query right (`CCLCSWRPWPDTLOCRRC`), or the UI will not be able to read the status.
- **Windows Update**: the same driver can appear several times in `Win32_PnPSignedDriver` (once per device instance). This is not an error.
- **Restore points**: if System Restore is disabled, installation fails. In the VM: `Enable-ComputerRestore -Drive "C:\"`.
