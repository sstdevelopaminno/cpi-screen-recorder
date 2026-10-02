#define MyAppName "CPI Screen Recorder"
#define MyAppVersion "0.3.0"
#define MyAppPublisher "CUTTING POINT INNOVATION CO., LTD."
#define MyAppExeName "CPI.ScreenRecorder.exe"

[Setup]
AppId={{91F6C376-9BDA-4B8C-BB01-6A1FBE2F1034}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\CPI Screen Recorder
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=CPI-Screen-Recorder-Setup
SetupIconFile=..\src\CpiScreenRecorder\Resources\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\CpiScreenRecorder\Resources\AppIcon.ico"; DestDir: "{app}"; DestName: "AppIcon.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "สร้างไอคอนบน Desktop"; GroupDescription: "ตัวเลือกเพิ่มเติม:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "เปิด {#MyAppName}"; Flags: nowait postinstall skipifsilent
