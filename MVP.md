# MVP.md

Scope of the first usable version of **DriverUpdater**.

## 1. MVP goal

Demonstrate that driver updates can be **detected and installed safely** on Windows 10/11 using **only Windows Update** as the source, with a background service and a simple interface.

The MVP **does not need a backend**.

## 2. Scope

### Included
- Inventory of installed drivers.
- Driver update search through the Windows Update Agent.
- Installation of one or several drivers.
- Digital signature verification before installing; WHQL is required by default, and the user can disable only that requirement.
- A restore point before installing each driver.
- Rollback of the last update.
- Operations history and log.
- Windows service + WPF UI without privileges.
- Optional scheduled check (daily/weekly) with a passive indicator inside the application; no desktop notifications.
- Diagnostic CLI.

### Excluded (later phases)
- Backend, accounts, licenses, telemetry.
- OEM or component-manufacturer sources (NVIDIA, AMD, Intel).
- Fleet management / web console.
- Self-update of the application.
- Localization into several languages (the MVP is in Spanish).
- Toast notifications or user-defined scheduled tasks.

## 3. User stories

| ID | As a user, I want… | So that… |
|---|---|---|
| U1 | to see the list of installed drivers with version and date | I know the state of my computer |
| U2 | to press "Search for updates" and see which ones exist | I can decide what to install |
| U3 | to install a specific driver or all pending ones | I keep the computer up to date |
| U4 | a restore point to be created before installing | I can go back if something fails |
| U5 | to undo the last update | I can recover a driver that causes problems |
| U6 | to see a history of what was installed and when | I have traceability |
| U7 | to exclude drivers I do not want to update | I avoid changes to sensitive hardware |
| U8 | the app to check for updates by itself | I do not have to remember |

## 4. Acceptance criteria

- **U1**: the inventory shows the name, manufacturer, version, date, and status of each device; it takes less than 30 s.
- **U2**: the search lists only driver-type updates, with current → new version.
- **U3**: installation shows progress, reports success or error per driver, and warns if a reboot is needed.
- **U4**: if the restore point cannot be created, installation **does not continue** and the user is informed.
- **U5**: after the rollback, the driver version returns to the previous one and this is recorded.
- **U6**: the history persists after the computer restarts.
- **U7**: an excluded driver does not appear in updates and is not installed in batch.
- **U8**: if enabled, the check runs every 24 or 168 hours while the service is active; a new result is shown once in the Updates indicator. There is no toast and no user-defined scheduled task.
- **Security**: a package with an invalid or missing signature is rejected and recorded.
- **Performance**: the idle service uses less than 50 MB of RAM.

## 5. Milestones and tasks

### Milestone 0: Prototypes (1–2 weeks)
- [x] Create the .NET 10 solution and the `Core`, `Agent.Drivers`, and `Cli` projects.
- [x] List drivers with `Win32_PnPSignedDriver`.
- [x] Search for drivers with WUA (`Type='Driver'`) and show the results.
- [ ] Install a test driver in a VM (reserved for VM testing).
- [x] Go / no-go decision: does the source give useful results?

### Milestone 1: Core + CLI (3–4 weeks)
- [x] Models and interfaces in `Core`; version comparison with tests.
- [x] `IDriverInventory` and `IUpdateSource` (WUA).
- [x] `ISignatureVerifier` (WinVerifyTrust).
- [x] `IRestorePointService`.
- [x] `IDriverInstaller` with installation, recording, and rollback.
- [x] History in SQLite.
- [x] CLI commands: `scan`, `updates`, `install`, `rollback`, `log`.

### Milestone 2: Service + UI (3–4 weeks)
- [x] Worker Service running as a Windows service.
- [x] gRPC server over a named pipe with restricted ACL.
- [x] Operations queue (one installation at a time) and cancellation.
- [x] WPF UI: home, update list, history, settings.
- [x] Exclusions and scheduled check.
- [x] Scheduled check indicator and pending-reboot notice inside the app; no desktop notifications.

### Milestone 3: Security and robustness (2–3 weeks)
- [x] Valid signature required; WHQL required by default, with an advanced option to accept valid non-WHQL signatures.
- [ ] Tests on Windows 10 and 11 VMs and non-destructive tests on 2–3 real computers (documented in `TESTING.md`). Real driver installations are tested only in VMs.
- [ ] Failure tests (network cut, invalid signature, reboot during installation) (documented in `TESTING.md`).
- [x] Review of the pipe ACL and input validation.

### Milestone 4: Installer and beta (2 weeks)
- [x] MSI with WiX that registers the service and the UI.
- [ ] Code signing of the installer and binaries.
- [ ] Closed beta with a few users and feedback collection.

## 6. Definition of "done" for the MVP

- All acceptance criteria met.
- Tested on Windows 10 and 11 in VMs; any real installation test is done only in a VM, never on the main development computer.
- No warnings in `Release`; unit tests green.
- A signed installer that installs and uninstalls without leaving leftovers.
- Minimal user documentation (install, scan, update, undo).

## 7. MVP risks

| Risk | Impact | Mitigation |
|---|---|---|
| Windows Update offers few useful drivers | High | Validate in Milestone 0; if poor, bring OEM sources forward |
| A driver causes a blue screen | High | Restore point, rollback, one-by-one installation, VM tests |
| False positives from antivirus software | Medium | Code signing, signed installer |
| COM/WUA complexity in .NET | Medium | Early prototype; isolate behind `IUpdateSource` |
| Lack of hardware variety for testing | Medium | VMs with snapshots and a beta with volunteers |

## 8. After the MVP

1. Backend with its own catalog and lists of problematic drivers.
2. OEM and manufacturer sources (after license review).
3. Self-update and stable/beta channels.
4. Accounts, licenses, and optional telemetry.
5. Enterprise version with a management console.

Manual validation status is recorded in `TESTING.md`; the implementation history is in `SPRINT_LOG.md`.
