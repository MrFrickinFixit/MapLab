# Map Lab Windows installer

The MSI uses the WiX Advanced setup wizard. It includes license acceptance,
installation-scope and destination-folder selection, optional shortcuts,
installation progress and completion pages, and modify/repair/remove maintenance
options when the package is run after installation.

Run the following command from the repository root:

```powershell
.\scripts\build-installer.ps1
```

The script publishes a self-contained 64-bit Windows build and creates:

```text
artifacts\installer\MapLab-1.0.1-win-x64.msi
```

To assign a new installer version:

```powershell
.\scripts\build-installer.ps1 -Version 1.1.0
```

Setup now walks through the installation choices instead of offering a one-click
path that skips them. The wizard prompts for an all-users or just-for-me install,
shows the destination (and allows changing the all-users Program Files location),
then presents the Start menu and Desktop shortcuts as independent optional
features. Both shortcuts are enabled by default.

The installed application folder includes the Map Lab freeware license and the
Microsoft .NET license and third-party notices. The MSI supports upgrades and can
be removed from Windows **Installed apps**.
