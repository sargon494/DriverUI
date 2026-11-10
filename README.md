<div align="center">

# DriverUpdater

**Finds Windows driver updates and applies safety controls on Windows 10 and 11**

[![CI](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/ci.yml)
[![GitHub Pages](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml/badge.svg)](https://github.com/sargon494/DriverUI/actions/workflows/pages.yml)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20(64--bit)-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![WHQL security](https://img.shields.io/badge/Signature-WHQL%20%2F%20Microsoft-10b981?logo=shield&logoColor=white)](#-security-controls)
[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br />

### 🌐 [Try the Interactive Simulator on GitHub Pages](https://sargon494.github.io/DriverUI/)

</div>

---

## 💡 Why DriverUpdater?

**DriverUpdater** is a free, open-source community project designed as an alternative to commercial driver-update applications. It does not unlock, modify, or bypass the licenses of other programs: it is a standalone application. In this version it searches for updates through the Windows Update Agent (WUA); it does not maintain its own catalog or download server. Installation performs signature checks and requires a restore point beforehand, depending on the configuration and the state of the system.

---

## 🛡️ Security controls

| Guarantee | How it protects you |
|---|---|
| **1. Signature check** | The service checks signatures with `WinVerifyTrust`. The WHQL policy is enabled by default, but it can be changed in Settings. A valid signature does not guarantee that a driver is safe, compatible, or free of defects. |
| **2. Restore point** | Before installing, a restore point is requested. If it cannot be created, the installation flow is aborted. The availability and effectiveness of System Restore depend on Windows and its configuration. |
| **3. Rollback** | The application offers an operation to revert a recorded installation. There is no guarantee that a rollback restores the previous state on every machine. |
| **4. Privilege separation** | The UI runs as a standard user and asks the Windows service, which has elevated privileges, to perform operations. This reduces the UI's exposure, but does not remove all risks. |
| **5. Local history** | The SQLite history and the logs are stored locally in `%ProgramData%\DriverUpdater`. They are not deleted automatically when you uninstall; see [Privacy](PRIVACY.md) for the data and how to delete it. |

These controls reduce some risks, but installing drivers can cause failures or loss of functionality. Make backups and review each update before installing it.

---

## 🔎 Data and connections

The application has no backend of its own, no accounts, and no analytics for the project. See the [privacy note](PRIVACY.md) for what is stored locally and how Windows Update and third-party sites may process data.

---

## 🖥️ How the application works

The application is designed to be intuitive and clear for anyone. The labels in this mock-up are translated; the application currently shows its interface in Spanish.

```
┌────────────────────────────────────────────────────────────────────────┐
│  DriverUpdater — Control Panel                                         │
├───────────────┬────────────────────────────────────────────────────────┤
│ 🏠 Home       │  Health status: [ 185 Devices OK  ✓ ]                  │
│ 🔄 Updates    │                                                        │
│ 📜 History    │  Motherboard: Gigabyte B760M DS3H DDR4                 │
│ ⚙️ Settings   │  [ Link to the OEM's official driver support page ]    │
└───────────────┴────────────────────────────────────────────────────────┘
```

### 1. Home (dashboard)
- Shows the overall status of your computer's components (graphics card, network, sound, chipset, storage).
- Detects your motherboard manufacturer and model (ASUS, Gigabyte, MSI, ASRock) and provides a safe button to open the manufacturer's **official support page** in your browser.

### 2. Updates
- When you press **Search for updates**, it asks Windows Update which official drivers have a newer version for your exact hardware.
- It shows a clear comparison: **Installed version → Recommended new version**, and the download size.
- You can select the drivers you want to update and press **Install selected**. A real-time progress bar shows the phases (`Downloading` → `Verifying signature` → `Creating restore point` → `Installing`).
- If you never want to update a component (for example, a dedicated sound card or a special controller), use the **Exclude** button.

### 3. History
- Shows a detailed audit of each driver installed or rolled back: date, device, previous version, and result.
- If an update causes problems, select the row and press **Roll back driver** to immediately return to the previous version.

### 4. Settings
- **Automatic check**: choose whether the background service checks for new drivers (disabled, every 24 hours, or every week).
- **WHQL policy**: enabled by default to require official Microsoft certificates.
- **Exclusion list**: review or remove the hardware identifiers you have decided not to update.

---

## 📥 Installation

### System requirements
- **Windows 10 (version 22H2 or later)** or **Windows 11** (64-bit).
- System protection enabled on the system drive (`C:\`) so restore points can be created.

### Downloading the installer
1. Go to the [**Releases on GitHub**](https://github.com/sargon494/DriverUI/releases) section.
2. Download the `.msi` installer (for example: `DriverUpdater-1.0.0-x64.msi`).
3. Run the installer as administrator once. It registers the Windows service and creates the shortcut in your Start menu.
4. Open **DriverUpdater** from the Start menu.

---

## ⌨️ Command-line tool (CLI)

For support technicians, system administrators, or advanced users who prefer the console without the graphical interface:

```powershell
# 1. Scan and inventory all installed drivers with manufacturer and version
dotnet run --project src/Cli -- scan

# 2. Check for official updates in Windows Update
dotnet run --project src/Cli -- updates

# 3. View the audit log and previous installations
dotnet run --project src/Cli -- log

# 4. Install a specific driver by its identifier
dotnet run --project src/Cli -- install <UpdateId>

# 5. Roll back an operation by its history ID
dotnet run --project src/Cli -- rollback <OperationId>
```

---

## ❓ Frequently asked questions (FAQ)

#### Why does DriverUpdater find fewer updates than other commercial programs?
Because DriverUpdater **only offers official drivers published and certified by Microsoft** for your exact hardware model. Other commercial programs index beta versions from forums, drivers from similar computers, or uncertified files that put system stability at risk.

#### Does the program use resources in the background?
Almost none. The idle service uses less than **50 MB of RAM** and practically **0% CPU**. It does not run continuous scans in a loop, and it does not show intrusive pop-ups or advertising notifications.

#### What happens if the power goes out or an installation is cancelled halfway?
DriverUpdater follows a safe cancellation policy:
- If you cancel during download or verification, it stops immediately.
- If the driver has already started installing at a low level in Windows, it waits for that file to finish copying atomically, so the Windows driver subsystem is not left corrupted.
- In addition, you always have the **System Restore point** created beforehand to go back.

---

## 🛠️ For developers

### Technology stack
- **Language**: C# 14 / .NET 10 (LTS).
- **Windows service**: Worker Service under the SYSTEM account with Kestrel and the native named-pipe transport.
- **IPC (inter-process communication)**: gRPC over a local named pipe (`\\.\pipe\DriverUpdater`) with strict access control limited to interactive users (`S-1-5-4`).
- **Windows access**: WMI (`System.Management`), COM (`Microsoft.Update.Session`), native `WinVerifyTrust` (P/Invoke), and `pnputil`.
- **User interface**: WPF with a clean MVVM architecture using `CommunityToolkit.Mvvm`.
- **Tests**: 151 automated tests with xUnit, FluentAssertions, and NSubstitute. The test suites and their rationale are described in [`TESTING.md`](TESTING.md#4-automated-test-suites).
- **Installer**: WiX Toolset v4 (MSI).

### Build and test locally
```powershell
# Clone the repository
git clone https://github.com/sargon494/DriverUI.git
cd DriverUI

# Restore packages and build in Release mode
dotnet restore
dotnet build -c Release

# Run the full suite of 151 unit and integration tests
dotnet test -c Release

# Build the MSI installer with WiX v4
powershell ./installer/build.ps1 -Version 1.0.0
```

### Additional technical documentation
- 🗺️ [`ARCHITECTURE.md`](ARCHITECTURE.md): implemented architecture, flows, and extension points.
- 📐 [`DESIGN.md`](DESIGN.md): current technical decisions and open questions.
- 🎯 [`MVP.md`](MVP.md): scope, user stories, and acceptance criteria.
- 🧪 [`TESTING.md`](TESTING.md): manual test procedures, the rationale for the automated test suites, and VM results.
- 📋 [`SPRINT_LOG.md`](SPRINT_LOG.md): chronological history; it does not replace the current state.
- 🛡️ [`AGENTS.md`](AGENTS.md): conventions, security rules, and the documentation map.

---

## 📄 License

The code of this project is available under the [MIT license](LICENSE). Public installers may include third-party dependencies and components with their own licenses and notices. Before publishing each compiled version, generate and review an inventory/SBOM of the dependencies and their license notices, including any runtime components that are packaged.

## ⚠️ Disclaimer

The software is provided "as is", under the MIT license. There is no guarantee that an update is suitable for a specific computer, or that a restore point or rollback will always allow you to recover the previous state. You decide what to install and accept the risks of driver changes. Do not use it on critical machines without validation and adequate backups.

DriverUpdater is an independent community project and is not affiliated with, sponsored by, or endorsed by Microsoft or by device manufacturers. Windows, Microsoft, and third-party trademarks belong to their respective owners.
