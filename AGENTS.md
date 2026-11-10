# AGENTS.md

Guide for AI agents (and people) working in this repository.

## Project

**DriverUpdater**: a Windows 10/11 application that detects and installs driver updates automatically and safely (signed drivers only, with a restore point beforehand and rollback).

## Documentation and source of truth

- `MVP.md`: scope, user stories, acceptance criteria, and milestone status.
- `ARCHITECTURE.md`: implemented architecture, projects, flows, and extension points.
- `DESIGN.md`: lasting technical decisions and open design questions; it is not an implementation diary.
- `TESTING.md`: manual VM validation procedures, the rationale for the automated test suites, and VM results.
- `SPRINT_LOG.md`: chronological history. Its notes describe the state on the date of each sprint; they do not override the current documentation.
- `PRIVACY.md`: public information about local data and external services.

If two documents disagree, use the document that owns the topic listed above and update the related references. Before working on a task, read `MVP.md` if it affects scope and `ARCHITECTURE.md` to understand the current behavior.

## Documentation language

- **All project documentation is written in English.** This repository is open source and must be understandable to any contributor. This covers the Markdown files listed above, the installer guide, and new documentation written in code comments or XML docs.
- Do not add new documentation in Spanish. If you find Spanish documentation, translate it when you touch that file.
- Strings shown to the user by the application may stay in Spanish until localization is implemented (see `MVP.md`, which excludes localization from the MVP).
- Commit messages and pull request descriptions are written in English.

## Stack

| Layer | Technology |
|---|---|
| Language | C# 14 / .NET 10 (LTS) |
| Agent service | Worker Service (Windows service, SYSTEM account) |
| Windows access | WMI (`System.Management`), Windows Update Agent (COM), `pnputil`, WinVerifyTrust (P/Invoke) |
| UI ↔ service IPC | gRPC over named pipes (Kestrel's native transport) |
| UI | WPF (MVVM with CommunityToolkit.Mvvm) |
| Backend (later phase) | ASP.NET Core + EF Core + PostgreSQL |
| Tests | xUnit, FluentAssertions, NSubstitute |
| Installer | WiX (MSI) |

## Repository structure

```
DriverUpdater/
├─ src/
│  ├─ Core/            # models, versions, hardware ID matching (no Windows dependencies)
│  ├─ Agent.Drivers/   # WMI, WUA, pnputil, signatures, restore points
│  ├─ Agent.Service/   # Worker Service and gRPC server
│  ├─ Agent.Ipc/       # gRPC contract (proto) and client channel factory over the named pipe
│  ├─ Ui.Wpf/          # user interface
│  ├─ Cli/             # diagnostic tool without UI
├─ tests/
├─ installer/
├─ AGENTS.md
├─ ARCHITECTURE.md
├─ DESIGN.md
├─ MVP.md
├─ TESTING.md
├─ SPRINT_LOG.md
└─ PRIVACY.md
```

## Commands

```powershell
dotnet restore
dotnet build -c Release
dotnet test
dotnet run --project src/Cli -- scan        # list installed drivers
dotnet run --project src/Cli -- updates     # check for updates
dotnet format --verify-no-changes           # check formatting
```

Operations that install drivers require an administrator console and **must be tested in a virtual machine**, never on the main development machine.

## Code conventions

- Nullable reference types enabled; zero warnings in `Release`.
- `async/await` end to end; every asynchronous method accepts a `CancellationToken`.
- Dependency injection with `Microsoft.Extensions.DependencyInjection`; no static singletons.
- All Windows access (WMI, COM, pnputil, P/Invoke) is hidden behind interfaces defined in `Core` so it can be simulated in tests.
- Logging with `ILogger` (Serilog as the sink). Never use `Console.WriteLine` outside `Cli`.
- Names in code are in English.
- Small commits with imperative messages (`Add driver inventory service`).

## Security rules (mandatory)

1. **Never install a driver without verifying its digital signature** (WinVerifyTrust). By default, only WHQL / Microsoft-signed drivers.
2. **Always create a restore point** before installing. If it fails, abort the installation.
3. The UI **must never** run with elevated privileges. Everything privileged goes through the service.
4. The service only accepts connections from the local UI (named pipe ACL restricted to interactive users).
5. Do not run commands built by concatenating external input; use `ProcessStartInfo.ArgumentList`.
6. Every download is verified with a hash (SHA-256) and a signature before it runs.
7. Do not store secrets in the repository. Use `dotnet user-secrets` or environment variables.
8. Every installation leaves a record in the operations log (what, when, previous version, result).

These rules state requirements; they do not certify that every current flow already meets them. The limitation of the WUA adapter in file verification is described in `ARCHITECTURE.md` and must be resolved before claiming that each package is verified.

## Testing

- Pure logic (`Core`): unit tests, no Windows dependencies.
- `Agent.Drivers`: integration tests marked with `[Trait("Category","Integration")]`, which only run on Windows and in a VM.
- A change that touches installation, rollback, or signature verification **requires a test**, or a note explaining how it was verified manually.
- The rationale for each automated test project is documented in `TESTING.md`, section 4.

## What NOT to do

- Do not add third-party drivers, binaries, or installers to the repository.
- Do not disable signature verification, not even "temporarily" or behind an undocumented flag.
- Do not introduce telemetry or network calls that are not described in `DESIGN.md`.
- Do not change the architecture (for example, running the UI as administrator) without updating `DESIGN.md`.
- Do not run installation code against the developer's real machine.

## How to work on a task

1. Read `MVP.md` for scope and `ARCHITECTURE.md` for the current implementation.
2. Make small, focused changes; record in `DESIGN.md` only lasting technical decisions.
3. Build, run the tests, and run `dotnet format` before considering the task finished.
4. Summarize what was done, what was tested, and what remains pending.
