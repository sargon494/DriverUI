# Security policy

DriverUpdater installs driver packages on Windows. A problem in this project can affect the stability of a computer or the integrity of its drivers, so please report security issues privately.

## Supported versions

Security fixes are provided for the **latest published release** only. Older releases are not patched; users should update to the latest version.

## Reporting a vulnerability

**Do not open a public issue, pull request, or discussion for a security problem.**

Report it privately using GitHub's **private vulnerability reporting** for this repository: open the **Security** tab and choose **Report a vulnerability**. If that option is not available, contact the maintainer through GitHub without including technical details in public.

Please include:

- A description of the problem and its impact (for example: bypassing signature verification, a privilege escalation through the service or its named pipe, or an unsafe rollback).
- The affected version or commit, and the Windows version used.
- Steps to reproduce, or a minimal proof of concept.
- Whether the issue is already public anywhere.

**Do not test on a computer you do not own or on a production machine.** Reproduce driver-installation issues only in a virtual machine, as described in `TESTING.md`. Do not include hardware identifiers, logs, or personal data in your report; see `PRIVACY.md`.

## What to expect

This is a community project maintained by volunteers. The maintainer will try to:

- Acknowledge the report and confirm whether it is in scope.
- Keep you informed of progress, and agree on a disclosure date when a fix is ready.
- Credit you in the release notes, if you wish.

Timelines are not guaranteed, but critical issues affecting signature verification, privilege boundaries, or the named-pipe access control are treated as the highest priority.

## Scope

In scope:

- The Windows service (`Agent.Service`), its named pipe, and its access control list (ACL).
- Driver signature verification (`WinVerifyTrust`) and the WHQL policy.
- Installation, restore point creation, and rollback logic.
- The installer (`installer/`) and the published release artifacts.

Out of scope:

- Vulnerabilities in Windows, Windows Update, or third-party drivers. Report those to Microsoft or the hardware vendor.
- Issues that require the attacker to already have administrator rights on the computer.
- Findings from automated scanners without a demonstrated impact.

## Design context

The security model is described in `AGENTS.md` (mandatory security rules) and `ARCHITECTURE.md` (section 7). Known limitations are documented there and in `DESIGN.md`; a report that repeats a documented limitation is still welcome if it shows a path to exploitation that is not yet described.
