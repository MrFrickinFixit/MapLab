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

The MSI installs Map Lab under Program Files, adds Start menu and Desktop shortcuts, supports upgrades, and can be removed from Windows **Installed apps**.
