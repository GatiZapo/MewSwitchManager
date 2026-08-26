#define AppName "MewNX"
#define AppVersion "0.4.0"
#define AppPublisher "MewNX Project"
#define AppExeName "MewNX.exe"

[Setup]
AppId={{C5D6E5C7-5B9E-4A6B-9A32-1E7C4E6D2A91}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\MewNX
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=installer-output
OutputBaseFilename=MewNX-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=assets\MewNX.ico
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoDescription=Advanced Nintendo Switch Toolkit
VersionInfoProductName=MewNX
VersionInfoCompany={#AppPublisher}
VersionInfoVersion={#AppVersion}.0
CreateUninstallRegKey=yes

[Files]
Source: "dist\MewNX.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "dist\SHA256SUMS.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MewNX"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\MewNX"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar MewNX"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
