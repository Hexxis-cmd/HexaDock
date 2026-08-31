[Setup]
AppId={{B9E970C4-6406-4F57-8C79-15687ED6CB9C}
AppName=HexaDock
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\HexaDock
DefaultGroupName=HexaDock
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\HexaDock.ico
UninstallDisplayIcon={app}\HexaDock.exe
OutputDir=..\..\Release
OutputBaseFilename=HexaDock-1.0.0-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\..\Release\HexaDock-win-x64\HexaDock.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\HexaDock"; Filename: "{app}\HexaDock.exe"; Tasks: desktopicon
Name: "{group}\HexaDock"; Filename: "{app}\HexaDock.exe"

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop launcher"; Flags: checkedonce

[Run]
Filename: "{app}\HexaDock.exe"; Description: "Launch HexaDock"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\HexaDock.exe"; Parameters: "--restore-icons"; Flags: runhidden skipifdoesntexist; RunOnceId: "RestoreDesktopIcons"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
