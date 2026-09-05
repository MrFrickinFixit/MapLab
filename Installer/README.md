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
artifacts\installer\MapLab-1.0.3.3-beta-win-x64.msi
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

## Inno Setup EXE (alternative to MSI)

`Installer\MapLab.iss` targets Inno Setup 6.3 or newer. Build a self-contained
Windows x64 installer with:

```powershell
.\scripts\build-inno-installer.ps1
# Optional version and compiler-path overrides:
.\scripts\build-inno-installer.ps1 -Version 1.0.3.3 -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

The helper reads the version and executable name from `MapLab.csproj`, so beta
executable names do not need to be duplicated in the installer. It refreshes only
`artifacts\inno\publish\win-x64`, includes the existing Map Lab license and .NET
notices, and writes `artifacts\inno\installer\MapLab-<version>-beta-win-x64-setup.exe`.
The WiX MSI sources and output are left alone.

For the Inno Setup Compiler IDE, first run:

```powershell
.\scripts\build-inno-installer.ps1 -PublishOnly
```

Then open `Installer\MapLab.iss` and select **Compile**. Without command-line
defines, the script detects the executable from the published runtime config and
defaults to version `1.0.3.3`. The installed application version is displayed as
`1.0.3.3-beta` while Windows file-version metadata remains numeric.

On a first interactive install, setup prompts for **all users** (administrator
rights) or **just for you**, presents the license and destination folder, and
offers independent Start menu and desktop shortcut checkboxes, both initially on.
Folder and shortcut locations follow the selected scope. Subsequent Inno runs
retain the previous scope and choices. `/ALLUSERS` and `/CURRENTUSER` select scope
for unattended deployments; `/TASKS="startmenuicon,desktopicon"` selects shortcuts
and `/TASKS=""` selects neither.

Inno and MSI are alternative distribution formats, not interchangeable upgrades.
The EXE blocks installation when Windows Installer reports a related Map Lab MSI.
Uninstall the old format before switching, and back up maps first. Keep the Inno
`AppId` stable for future Inno upgrades. Switching user scope also requires
uninstalling the prior installation. This build does not configure code signing;
the generated EXE is unsigned. Compilation does not install Map Lab.
