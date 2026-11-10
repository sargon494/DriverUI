# Technical decisions

This document records cross-cutting decisions and their reasons. It does not try to duplicate the component map or the implementation history.

- The current architecture and its flows are in [`ARCHITECTURE.md`](ARCHITECTURE.md).
- The scope and acceptance status are in [`MVP.md`](MVP.md).
- Tests and their evidence are in [`TESTING.md`](TESTING.md).
- The chronological details of each sprint are in [`SPRINT_LOG.md`](SPRINT_LOG.md). Historical notes are not current specifications.

## Current decisions

### D1. C#/.NET for the application

C#/.NET was chosen to share one language between the Windows service, the UI, and possible future components, and to use Windows APIs directly. A future backend is not part of the MVP and must not affect local operation.

### D2. Separation between the UI and the privileged service

The WPF UI runs as a standard user. The Windows service concentrates the operations that modify the system; local communication uses gRPC over a named pipe with an ACL. The UI will not be elevated to simplify flows. The implemented architecture is in `ARCHITECTURE.md`; validation procedures are in `TESTING.md`.

### D3. Windows Update as the MVP source

The MVP queries the Windows Update Agent. No packages are redistributed from an own catalog. OEM links are a help that the user opens in the browser; they do not imply downloading or installing from those manufacturers.

### D4. Installation policy

A valid signature is required, and WHQL is applied by default (`OnlyWhql = true`); the setting only allows disabling the WHQL requirement, not the signature validity check. Installation requires a restore point beforehand. These are design invariants, not a claim that every execution path applies them correctly: `ARCHITECTURE.md` records the limitation observed in the WUA adapter until it is resolved.

### D5. Periodic checks without desktop notifications

The configurable check (disabled, daily, or weekly) runs while the service is active. Its result is communicated through the Updates indicator inside the application. No toast notifications are shown and no user-level scheduled task is created.

### D6. Automated test suites and their scope

Automated tests are split into four projects with different purposes. The reasons are recorded in `TESTING.md`, section 4:

- `Core.Tests`: pure logic, no Windows dependencies.
- `Agent.Drivers.Tests`: implementations that touch Windows; some tests are integration tests marked with the `Integration` category.
- `Agent.Service.Tests`: gRPC over a real named pipe with doubles for the Windows-facing interfaces.
- `Integration.Tests`: real application components (settings file, SQLite history, operations queue) with no doubles.

End-to-end and performance tests are planned but not yet implemented (tracked in issue #18).

## Open decisions

- Evaluate additional driver sources only after reviewing licenses, provenance, and a manifest/hash/signature mechanism.
- Define whether the product will target home users or also businesses before designing accounts, licenses, or a backend.
- Decide the business model and whether there is a real need to migrate away from WPF.
- Choose a code-signing certificate before distributing signed binaries.

## How to update this record

Add new decisions here when they affect trust boundaries, compatibility, shared dependencies, or product behavior. Update `ARCHITECTURE.md` when the implementation changes and `MVP.md` when requirements or criteria change. `SPRINT_LOG.md` keeps the historical context; do not copy the current state there as a substitute for these documents.
