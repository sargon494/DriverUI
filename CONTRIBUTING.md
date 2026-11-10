# Contributing to DriverUpdater

Thank you for your interest in improving DriverUpdater. This project installs drivers on Windows, so contributions are reviewed with extra care. This guide explains how to get started safely.

Please read these first:

- [`AGENTS.md`](AGENTS.md): conventions, the mandatory security rules, and the working rules.
- [`ARCHITECTURE.md`](ARCHITECTURE.md): how the projects fit together.
- [`MVP.md`](MVP.md): the scope of the first version. Features outside that scope are discussed as issues first.
- [`SECURITY.md`](SECURITY.md): how to report vulnerabilities. **Do not report them in public issues.**

## Language

All documentation, code comments, commit messages, issues, and pull request descriptions are written in **English**. The text shown by the application may stay in Spanish until localization is implemented.

## Getting started

Requirements: Windows 10 or 11, the .NET 10 SDK, and Git.

```powershell
git clone https://github.com/sargon494/DriverUI.git
cd DriverUI
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes
```

To run only the tests that do not depend on Windows-specific components:

```powershell
dotnet test --filter "Category!=Integration"
```

The test projects and their rationale are described in [`TESTING.md`](TESTING.md), section 4.

## Safety rules for contributors

- **Never run installation code on your own computer.** Any operation that installs, removes, or rolls back a driver must be tested in a virtual machine with a restore point and a checkpoint. See [`TESTING.md`](TESTING.md).
- **Never bypass signature verification**, not even temporarily or behind an undocumented flag.
- **Do not add third-party drivers, binaries, or installers** to the repository.
- **Do not add telemetry or network calls** unless they are described in [`DESIGN.md`](DESIGN.md) first.
- **Do not include personal data** in your contributions, issues, or logs: hardware identifiers, serial numbers, usernames, and log files from your computer. See [`PRIVACY.md`](PRIVACY.md).

## Making a change

1. **Open an issue first** for anything larger than a small fix, so the approach can be agreed before you invest time.
2. **Create a branch** from `master` with a descriptive name, for example `fix/exclusion-reload` or `docs/install-guide`.
3. **Keep the change small and focused.** One logical change per pull request.
4. **Add or update tests.** A change to installation, rollback, or signature verification requires a test, or a written note explaining how it was verified manually.
5. **Update the documentation** that owns the topic: `MVP.md` for scope, `ARCHITECTURE.md` for implementation, `DESIGN.md` for lasting decisions, and `TESTING.md` for manual test procedures.
6. **Run the checks** listed above. CI must pass.
7. **Open a pull request** and fill in the template.

## Commit messages

Use short, imperative messages in English, for example `Add driver inventory service`. Explain *why* in the body when the reason is not obvious.

## Code style

- Nullable reference types enabled; zero warnings in `Release`. Warnings are errors in CI.
- `async`/`await` end to end; every asynchronous method accepts a `CancellationToken`.
- Dependency injection with `Microsoft.Extensions.DependencyInjection`; no static singletons.
- All Windows access (WMI, COM, `pnputil`, P/Invoke) is hidden behind interfaces defined in `Core`.
- Logging through `ILogger`. Do not use `Console.WriteLine` outside `Cli`.
- Names in code are in English. Field naming follows `.editorconfig` (`_camelCase` for private fields).

The formatting check (`dotnet format --verify-no-changes`) is part of CI.

## Review

Pull requests that touch installation, signature verification, the named pipe, or privilege boundaries receive a stricter review and may ask for a manual VM test. Be patient with those; they are the most sensitive parts of the project.

## Code of conduct

Participation is governed by the [Code of Conduct](CODE_OF_CONDUCT.md).
