# Installer (MSI · WiX 4.0.5)

Status: the workflow installs WiX 4.0.5 and the Util 4.0.5 extension. The MSI build for this revision must be confirmed in CI and tested on a Windows 10/11 VM before distribution. WiX 7 is not compatible with this configuration and requires separately accepting its OSMF EULA.

## What it installs

| Component | Where | How |
|---|---|---|
| Service `Agent.Service` | `Program Files\DriverUpdater\Agent.Service.exe` | Windows service `DriverUpdater`, `LocalSystem` account, **on-demand start** (it does not run when the computer turns on). The UI starts it when opened; the installer grants interactive users permission to start and stop the service, but not to configure it |
| UI `Ui.Wpf` | `Program Files\DriverUpdater\Ui.Wpf.exe` | Start menu shortcut; runs without elevation |

When uninstalling, the service is stopped and removed before the files are deleted.

## Build

```powershell
dotnet tool install --global wix --version 4.0.5
wix extension add -g WixToolset.Util.wixext/4.0.5
.\installer\build.ps1 -Version 1.0.0      # produces installer\artifacts\DriverUpdater-1.0.0-x64.msi
```

The build also generates `DriverUpdater-<version>.cdx.json` (CycloneDX 1.5 SBOM) and `THIRD-PARTY-NOTICES.txt` in `installer\artifacts`. Both, together with the project's MIT license and the Apache-2.0 / BSD-3-Clause texts where applicable, are included in `Program Files\DriverUpdater` inside the MSI.
The SBOM is derived from the NuGet dependencies resolved for the executables and from the .NET frameworks included. The NuGet notices identify the packages and their declared licenses; review the notices and licenses of each package before publishing a release.

## Test (VM only)

```powershell
msiexec /i installer\artifacts\DriverUpdater-1.0.0-x64.msi /l*v install.log
```

Check: after opening the UI, it starts the `DriverUpdater` service and connects ("Service active").
When uninstalling interactively, an option unchecked by default appears to delete `%ProgramData%\DriverUpdater` (history, settings, and logs). If it is not checked, the data is kept; a silent uninstall also keeps it. Version updates never show the option and never delete the data. Test both outcomes, and also confirm that the service is stopped and removed and that the files in `Program Files` are deleted.

## Pending

- Review the `RPWP` permission decision for `IU` (start/stop the service without elevation) with the security team.
- Graphical wizard: add the `WixToolset.UI` extension (currently the installation is silent by default).
- Code signing of the MSI and the `.exe` files (`signtool`), a Milestone 4 requirement.
- Review the SBOM and notices generated for each version before every release.
- The optional cleanup in the uninstaller covers `%ProgramData%\DriverUpdater`; CLI logs saved in the directory the CLI is run from are outside that folder.
- Automatic updates (self-update) are outside this MSI.
