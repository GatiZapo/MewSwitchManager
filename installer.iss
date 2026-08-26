[Setup]
AppId={{C5D6E5C7-5B9E-4A6B-9A32-1E7C4E6D2A91}
AppName=MewNX
AppVersion=0.4.0
AppVerName=MewNX 0.4.0
AppPublisher=MewNX Project
DefaultDirName={autopf}\MewNX
DefaultGroupName=MewNX
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
UninstallDisplayIcon={app}\MewNX.exe
VersionInfoDescription=Advanced Nintendo Switch Toolkit
VersionInfoProductName=MewNX
VersionInfoCompany=MewNX Project
VersionInfoVersion=0.4.0.0
CreateUninstallRegKey=yes

[Files]
Source: "dist\MewNX.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "dist\SHA256SUMS.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MewNX"; Filename: "{app}\MewNX.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MewNX"; Filename: "{app}\MewNX.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Run]
Filename: "{app}\MewNX.exe"; Description: "Iniciar MewNX"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
