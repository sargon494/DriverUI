<div align="center">

# DriverUpdater

**Safe, Cryptographically Verified & Official Windows 10/11 Driver Updates**

[![CI](https://github.com/Leonxlnx/DriverUI/actions/workflows/ci.yml/badge.svg)](https://github.com/Leonxlnx/DriverUI/actions/workflows/ci.yml)
[![Pages](https://github.com/Leonxlnx/DriverUI/actions/workflows/pages.yml/badge.svg)](https://github.com/Leonxlnx/DriverUI/actions/workflows/pages.yml)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![IPC](https://img.shields.io/badge/IPC-gRPC%20Named%20Pipes-00599C?logo=grpc&logoColor=white)](https://grpc.io/)
[![Security](https://img.shields.io/badge/Security-WHQL%20%26%20WinVerifyTrust-10b981?logo=shield&logoColor=white)](#security-principles)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br />

[**Interactive Live Web Simulator**](https://leonxlnx.github.io/DriverUI/) · [**Architecture Docs**](ARCHITECTURE.md) · [**Design Specs**](DESIGN.md) · [**Test Plan**](TESTING.md)

</div>

---

## 💡 Overview

Traditional third-party "driver updater" tools are notorious for bundling aggressive adware, pushing uncertified beta drivers, downloading from suspicious third-party mirrors, and frequently causing Blue Screens of Death (BSOD).

**DriverUpdater** solves this through a defensive, security-first architecture:
- **100% Official Sources**: Searches exclusively against the official **Windows Update Agent (WUA)** catalog. No third-party mirrors or proprietary web scrapers.
- **Cryptographic WinVerifyTrust Shield**: Every binary is validated using the Win32 `WinVerifyTrust` API with WHQL Enhanced Key Usage OIDs (`1.3.6.1.4.1.311.10.3.5`) prior to disk staging.
- **Mandatory System Restore Checkpoint**: If a System Restore Point cannot be created prior to installation, **the operation is immediately aborted**.
- **Zero-Privilege Desktop UI**: The WPF interface runs as a standard interactive user (`asInvoker`) with zero administrative elevation, communicating with a SYSTEM Windows Service via an ACL-restricted local named pipe (`\\.\pipe\DriverUpdater`).
- **Transactional Audit & Rollback**: Complete history preserved in local SQLite (`history.db`) with one-click rollback via `pnputil`.

---

## 🏗️ Architecture

```
┌──────────────────────────────────────┐     gRPC over Named Pipe     ┌──────────────────────────────────────────────┐
│  Ui.Wpf (Desktop GUI)                │ ◄──────────────────────────► │  Agent.Service (Windows Service)             │
│  - Standard User (asInvoker, 0 UAC)  │   \\.\pipe\DriverUpdater     │  - SYSTEM Account Worker Service             │
│  - Modern Glassmorphic Dark UI       │   HTTP/2 Cleartext           │  - OperationQueue (Single mutex per install) │
│  - Reactive Hardware Metrics         │   ACL: S-1-5-4 (Interactive) │  - WMI (Win32_PnPSignedDriver)               │
│  - Real-Time gRPC Streaming Reader   │                              │  - Windows Update Agent COM (Type='Driver')  │
│  - Safe Deferred Cancellation UX     │                              │  - WinVerifyTrust & WHQL OID Verification    │
└──────────────────┬───────────────────┘                              │  - SystemRestore WMI Checkpoint Service      │
                   │ uses                                             │  - SQLite Audit Ledger (%ProgramData%)       │
                   ▼                                                  └──────────────────────┬───────────────────────┘
            Agent.Ipc Contract                                                               │ uses
       (driveragent.proto + Channel)                                                         ▼
                                                                                   Agent.Drivers & Core
                                                                               (Implementations & Domain)
```

### Solution Projects

| Project | Target | Description |
|---|---|---|
| [`src/Core`](src/Core) | `net10.0` | Domain models (`InstalledDriver`, `DriverUpdate`, `AppSettings`), interfaces, version math (`VersionComparer`), and driver matching logic. **Zero Windows dependencies.** |
| [`src/Agent.Drivers`](src/Agent.Drivers) | `net10.0-windows` | Windows subsystems: WMI inventory, WUA COM interop, `WinVerifyTrust` P/Invoke, `WmiRestorePointService`, and SQLite persistence (`SqliteOperationHistory`). |
| [`src/Agent.Ipc`](src/Agent.Ipc) | `net10.0-windows` | Shared gRPC protobuf contract (`driveragent.proto`) and named pipe channel factory (`DriverAgentChannel`). |
| [`src/Agent.Service`](src/Agent.Service) | `net10.0-windows` | SYSTEM Worker Service, serialized `OperationQueue`, scheduled background check daemon, and named pipe security factory. |
| [`src/Ui.Wpf`](src/Ui.Wpf) | `net10.0-windows` | Desktop client in WPF MVVM (`CommunityToolkit.Mvvm`). Runs unprivileged (`asInvoker`). |
| [`src/Cli`](src/Cli) | `net10.0-windows` | Headless diagnostic command-line interface (`scan`, `updates`, `log`, `install`, `rollback`). |
| [`tests/`](tests/) | `net10.0` | Comprehensive xUnit, FluentAssertions, and NSubstitute test suites (**151 automated tests**). |
| [`installer/`](installer/) | WiX v4 | Standalone MSI installer definition with automated service registration and ACL bootstrapping. |

---

## 🔒 Security Principles (Mandatory Invariants)

1. **Mandatory Signature Verification**: No driver is installed without cryptographic Authenticode verification (`WinVerifyTrust`). By default, only drivers certified by Microsoft / WHQL are accepted.
2. **Mandatory Pre-Install Restore Point**: A Windows System Restore Point must be generated prior to installing any update batch. If checkpoint creation fails, installation is aborted.
3. **Unprivileged Presentation Layer**: The GUI **never** runs elevated. All privileged actions are mediated through the service.
4. **Restricted Pipe ACL**: The named pipe only permits connections from local interactive user sessions (SID `S-1-5-4`); network callers and unauthorized accounts are rejected at the kernel level.
5. **Safe Process Invocation**: System commands (`pnputil`) strictly use `ProcessStartInfo.ArgumentList` to guarantee command injection immunity.
6. **Immutable Audit Ledger**: Every transaction (success, failure, rollback) is permanently recorded in `%ProgramData%\DriverUpdater\history.db`.

---

## 🚀 Quick Start

### Prerequisites
- Windows 10 (22H2+) or Windows 11 (x64)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)

### 1. Build the Solution
```powershell
# Clone the repository
git clone https://github.com/Leonxlnx/DriverUI.git
cd DriverUI

# Restore and compile in Release mode
dotnet restore
dotnet build -c Release
```

### 2. Run the Automated Tests (151 Tests)
```powershell
dotnet test -c Release
```

### 3. Using the Diagnostic CLI
```powershell
# 1. Scan and list all installed hardware drivers
dotnet run --project src/Cli -- scan

# 2. Check for official updates from Windows Update
dotnet run --project src/Cli -- updates

# 3. View the SQLite audit log
dotnet run --project src/Cli -- log
```

### 4. Build the WiX v4 MSI Installer
```powershell
# Install WiX Toolset
dotnet tool install --global wix

# Build self-contained single-file win-x64 binaries and package MSI
powershell ./installer/build.ps1 -Version 1.0.0
```

---

## 🌐 Interactive Web Showcase & GitHub Pages

An interactive simulator demonstrating the complete **8-phase driver installation and rollback state machine** is hosted on GitHub Pages:

👉 **[https://leonxlnx.github.io/DriverUI/](https://leonxlnx.github.io/DriverUI/)**

The website is styled according to the design tenets of [`Leonxlnx/taste-skill`](https://github.com/Leonxlnx/taste-skill) and [`nextlevelbuilder/ui-ux-pro-max-skill`](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill), featuring a clean dark engineering aesthetic, responsive bento grids, and a live client/service pipe visualization.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

