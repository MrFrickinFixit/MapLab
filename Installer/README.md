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

The setup defaults to an all-users installation under Program Files, but the
wizard also offers a just-for-me installation under the current user's local app
data. Open **Advanced** during setup to choose the scope, destination, and whether
to create the Start menu and Desktop shortcuts. Both shortcuts are enabled by
default and can be selected independently.

The installed application folder includes the Map Lab freeware license and the
Microsoft .NET license and third-party notices. The MSI supports upgrades and can
be removed from Windows **Installed apps**.
