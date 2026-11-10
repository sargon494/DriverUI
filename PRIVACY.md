# Privacy and data handling

Last updated: November 7, 2026

This note describes the known behavior of the community version of DriverUpdater in this repository. It is not legal advice and does not replace the privacy information of the third-party services mentioned.

## Windows application

The application has no accounts, no backend of its own, and no analytics or telemetry service of the project. The project does not receive the data the application reads from the computer.

To work, the application may access locally:

- Device and driver inventory (including names, manufacturer, versions, device identifiers, and hardware).
- Settings and device exclusions that the user saves.
- Operations history: device, hardware identifier, update title, versions, date, result, and, if it occurs, the error message.
- Technical service logs, which may include error details.

Settings, history, and logs are stored on the computer, under `%ProgramData%\DriverUpdater`. The application does not send them to the maintainer. When you uninstall interactively, the installer offers a checkbox, unchecked by default, to delete this folder. If it is left unchecked or a silent uninstall is used, the data is kept and can be deleted manually. Version updates do not delete the folder. The CLI tool may also create logs in a `logs` folder relative to the directory it is run from; those logs are outside the MSI cleanup.

When searching for or installing drivers, the application uses the Windows Update Agent (WUA) of Windows. Communication with Windows Update is performed by Windows and its components; it may involve technical information needed to search for, download, or install updates. The project does not control and cannot exhaustively describe the data Windows transmits, its retention periods, or its recipients. Refer to Microsoft's and Windows Update's documentation and privacy settings. If the periodic check is enabled, the service may start these searches at the configured interval while it is running.

At the user's request, the application may also open manufacturers' support pages in the browser. Those sites are governed by their own policies.

## Project website

The demonstration page is served through GitHub Pages. The site includes no project analytics service, and its typefaces are served from the site itself. GitHub may process connection and usage data under its own terms and privacy policy. Links to GitHub, Microsoft, and other sites lead to third-party services.

## Choice and control

Inventory and saved data are kept on the computer. You can stop using the application and delete the local data by following the steps above. Do not publish hardware identifiers, logs, or data from your computer in issues or public forums.

## Controller and contact

This is a free community project published by Sargon494. The code and installers may be publicly available on GitHub. No legal entity or private contact channel has been designated for formal privacy requests. For general questions about the code, use the repository's GitHub channels and do not include personal information or hardware identifiers in a public issue.

Before the installers are offered publicly, the maintainer must confirm the appropriate contact details, review the legal requirements that apply in the territories the project targets, and check this note with qualified advice where appropriate. This note does not by itself claim to comply with all legal obligations.
