; Build with scripts\build-inno-installer.ps1, or publish with -PublishOnly
; and then open this file in the Inno Setup Compiler IDE.
#ifndef ProjectRoot
  #define ProjectRoot AddBackslash(SourcePath) + ".."
#endif
#ifndef PublishDir
  #define PublishDir ProjectRoot + "\artifacts\inno\publish\win-x64"
#endif
#ifndef AppExeName
  #define ConfigSearch FindFirst(PublishDir + "\*.runtimeconfig.json", 0)
  #if !ConfigSearch
    #error "No published application found. Run scripts\build-inno-installer.ps1 -PublishOnly first."
  #endif
  #define AppExeName RemoveFileExt(RemoveFileExt(FindGetFileName(ConfigSearch))) + ".exe"
  #if FindNext(ConfigSearch)
    #error "Multiple applications found in PublishDir. Publish to a clean directory."
  #endif
  #expr FindClose(ConfigSearch)
#endif
#if !FileExists(PublishDir + "\" + AppExeName)
  #error "The application executable is missing from PublishDir."
#endif
#ifndef AppVersion
  #define AppVersion "1.0.3.3"
#endif
#if !FileExists(PublishDir + "\coreclr.dll") || !FileExists(PublishDir + "\PresentationFramework.dll")
  #error "PublishDir must contain a self-contained Windows desktop build."
#endif
#if !FileExists(PublishDir + "\LICENSE.txt") || !FileExists(PublishDir + "\DOTNET-LICENSE.txt") || !FileExists(PublishDir + "\DOTNET-THIRD-PARTY-NOTICES.txt")
  #error "Required license files are missing. Use scripts\build-inno-installer.ps1 to publish."
#endif

[Setup]
; Keep this ID stable across Inno releases. It is separate from the MSI identity.
AppId={{F80CDDC2-462A-4DC9-83E9-88FC36BDC2EC}
AppName=Map Lab (Beta)
AppVersion={#AppVersion}-beta
VersionInfoVersion={#AppVersion}
AppPublisher=Brian Diffenbaugh
AppCopyright=Copyright (c) 2026 Brian Diffenbaugh
DefaultDirName={autopf}\Map Lab
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
LicenseFile={#ProjectRoot}\LICENSE.txt
SetupIconFile={#ProjectRoot}\Assets\MapLab.ico
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#ProjectRoot}\artifacts\inno\installer
OutputBaseFilename=MapLab-{#AppVersion}-beta-win-x64-setup
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenuicon"; Description: "Add Map Lab to the Start menu"; GroupDescription: "Shortcuts:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Map Lab\Map Lab"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: startmenuicon
Name: "{autodesktop}\Map Lab"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Map Lab"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function MsiEnumRelatedProducts(UpgradeCode: string; Reserved, Index: Cardinal;
  ProductCode: string): Cardinal;
  external 'MsiEnumRelatedProductsW@msi.dll stdcall';

function InitializeSetup: Boolean;
var
  ProductCode: string;
  Status: Cardinal;
begin
  SetLength(ProductCode, 39);
  Status := MsiEnumRelatedProducts('{7C642D73-845B-4DB7-89A4-F37FE4114870}',
    0, 0, ProductCode);
  Result := Status = 259; { ERROR_NO_MORE_ITEMS }
  if Status = 0 then
    SuppressibleMsgBox('An MSI-installed copy of Map Lab is already registered. ' +
      'Uninstall that copy through Windows Installed apps before switching to ' +
      'this Inno Setup installer. Back up your maps first.', mbError, MB_OK, IDOK)
  else if not Result then
    SuppressibleMsgBox('Setup could not check for an existing Map Lab MSI ' +
      'installation (Windows Installer error ' + IntToStr(Status) + '). ' +
      'Resolve that error before continuing.', mbError, MB_OK, IDOK);
end;
