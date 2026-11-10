# VM test plan

Manual tests that cannot be run in CI: real installation, UI with a standard account, failures in the middle of an operation, and reboots. The automated tests (in `tests/`) run in CI; this document covers what remains. Section 4 explains why each automated test suite exists.

This document is the source of truth for manual validation procedures and their results. References: `installer/README.md` (build and install), `ARCHITECTURE.md` (current behavior), `MVP.md` (acceptance criteria).

## 0. Preparation (once per VM)

- A VM with Windows 10 (22H2) and another with Windows 11 (24H2). No personal data.
- System Restore enabled, or installation will fail when creating the restore point:
  ```powershell
  Enable-ComputerRestore -Drive "C:\"
  ```
- Two accounts: one **administrator** to install and one **standard** account to use the UI.
- Save a **clean checkpoint** after installing the MSI and before each scenario. Restore it between tests.
- Run the MSI with logging:
  ```powershell
  msiexec /i installer\artifacts\DriverUpdater-X.Y.Z-x64.msi /l*v install.log
  ```

## 1. What not to do

- **Do not uninstall network, storage, display, or chipset drivers in the VM.** If you lose the network or the disk, you will not be able to read the logs or restore. If you want to see updates, first check whether Windows Update offers any (the app itself shows this). A VM usually has few or no updates, and that is a result: record it.
- Never run these tests on the development computer (`AGENTS.md`).
- If you want a driver "with an update" to test installation, prepare it in the VM with a checkpoint and a non-critical device, never on a device the VM needs to boot.

## 2. Scenarios

For each one: note the MSI version, the checkpoint used, and the expected result versus the observed one. Attach `install.log` and the `logs\*.log` files from `%ProgramData%\DriverUpdater\`.

### 2.1 Startup and connection (standard account)

1. Install with the administrator account. Sign out.
2. Sign in with the standard account and open DriverUpdater.
3. **Expected:** "Connecting to the service" (Conectando con el servicio) for a few seconds, then "Service active" (Servicio activo). No error dialog appears.
4. Restart the VM. **Expected:** the service is **not** running (`sc query DriverUpdater` → `STOPPED`). Opening the UI starts it.

### 2.2 Network cut during a search

1. Open Updates and press "Search" (Buscar).
2. While it is searching, disable the adapter: `Disable-NetAdapter -Name "Ethernet" -Confirm:$false`.
3. **Expected:** the search ends with a readable error message. The UI does not close and does not show exceptions. The service keeps responding to `GetStatus` (the UI stays connected).
4. Re-enable the adapter and search again. **Expected:** the search works.

### 2.3 Invalid or missing signature

This cannot be tested with a real driver without risk. The automated test is the reference: a test `ISignatureVerifier` that rejects the signature must prevent installation and record the rejection. In the VM, check only that, when a real installation is rejected, the app shows the reason and the history records it.

- **Expected:** nothing is installed, the system is not modified, and the history records the operation as failed with the reason.

### 2.4 Reboot during installation

1. Create the checkpoint, choose an update, and press "Install" (Instalar).
2. When the progress bar advances (installation phase), restart the VM **from the host** (`Restart-VM -Force`), not from inside the VM.
3. On startup, open the UI with the standard account.
4. **Expected:**
   - The UI connects and no operation is in progress (the queue starts empty).
   - The history shows the operation as interrupted or failed, with its restore point.
   - If Windows asks for a reboot, the "Pending reboot" (Reinicio pendiente) notice appears.
5. Note which drivers were installed and check that the device works (Device Manager, no warning icon).

### 2.5 Pending reboot read from the registry

Test of the most recent feature (`GetStatus` reads the Windows flags). It does not require any update.

1. As administrator, create the flag Windows leaves when it asks for a reboot:
   ```powershell
   New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" -Force
   ```
2. Restart the service: `Restart-Service DriverUpdater`. Open the UI.
3. **Expected:** "Pending reboot" appears even though the service has just started.
4. Remove the flag:
   ```powershell
   Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
   ```
   Restart the service again. **Expected:** the notice disappears.

### 2.6 Exclusion (U7)

1. With an update in the list, press "Exclude" (Excluir).
2. **Expected:** it disappears from the list. Settings shows its hardware ID.
3. Press "Search" again. **Expected:** it does not come back.
4. Remove the exclusion in Settings, save, and search. **Expected:** it comes back.

### 2.7 Uninstallation

1. `msiexec /x` with the original MSI (or from Installed apps).
2. **Expected:** the service is stopped and removed (`sc query DriverUpdater` → does not exist), no files remain in `Program Files\DriverUpdater`, and `%ProgramData%\DriverUpdater` is kept (pending decision, see `installer/README.md`).

## 3. Result log

Record each run only in this table; `MVP.md` links to the criteria and does not duplicate VM results.

| Scenario | Windows | MSI | Result | Notes |
|---|---|---|---|---|
| 2.1 Startup | | | | |
| 2.2 Network cut | | | | |
| 2.3 Signature | | | | |
| 2.4 Reboot during installation | | | | |
| 2.5 Pending reboot | | | | |
| 2.6 Exclusion | | | | |
| 2.7 Uninstallation | | | | |

## 4. Automated test suites

This section explains **why** each automated test project exists, what it protects, and what it deliberately does not cover. The projects run in CI on `windows-latest` (`.github/workflows/ci.yml`).

| Project | What it tests | Why it exists |
|---|---|---|
| `tests/Core.Tests` | Pure logic: version comparison, hardware ID matching, models. | Fast feedback with no Windows dependency. Bugs in the matching rules are the most likely to cause a wrong driver to be offered, so they are tested exhaustively here. |
| `tests/Agent.Drivers.Tests` | Implementations that touch Windows: WMI inventory, WUA search, WinVerifyTrust, restore points, installer orchestration, SQLite history. | Verifies the adapters against real Windows behavior. Tests that need real Windows are marked `[Trait("Category","Integration")]`. Installer orchestration is tested with doubles so the safety rules (signature first, restore point mandatory, history always written) can be checked without installing anything. |
| `tests/Agent.Service.Tests` | The gRPC service over a real named pipe, with doubles for the Windows-facing interfaces. | Verifies that the RPCs, the operations queue, the pipe ACL, and the dependency injection wiring work together. Its harness (`AgentHarness`) exists because a missing service registration only fails at run time, so every new dependency must be added to it. |
| `tests/Integration.Tests` | Real components working together with no doubles: `JsonSettingsStore` writing a real file, `SqliteOperationHistory` on a real database, and `OperationQueue`. | Doubles hide the behavior that matters for persistence. These tests catch problems such as a broken atomic write, a corrupt settings file that must fall back to defaults, a history query returned in the wrong order, or a queue that does not serialize operations. Every test uses a temporary folder and is marked `[Trait("Category","Integration")]`. |

### Test scope and limits

- **Not covered by automated tests:** the real installation of a driver (manual, in a VM only, section 2), the user interface (manual, section 2), and failures such as network cuts or reboots during installation.
- **Planned, not yet implemented** (tracked in issue #18):
  - **End-to-end tests** that start the real service and exercise non-destructive calls (scan, status, settings) through the named pipe. They will never call the installation RPC.
  - **Performance tests** with time budgets for version comparison, hardware matching, and WMI parsing. They will start as a non-blocking CI job, because CI runner timing is not stable enough to fail builds yet.

### Running a subset

```powershell
# Everything that does not need Windows-specific components
dotnet test --filter "Category!=Integration"

# Only the component integration tests
dotnet test tests/Integration.Tests
```
