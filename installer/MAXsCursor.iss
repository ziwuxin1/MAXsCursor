; Inno Setup script for MAXs Cursor
; Compile with:
;   "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\MAXsCursor.iss
;
; Produces installer\MAXsCursor-Setup-v1.0.exe that installs per-user (no admin
; prompt needed) and registers an uninstaller under Windows "Apps & Features".

#define AppName      "MAXs Cursor"
#define AppVersion   "1.1"
#define AppPublisher "ziwuxin1"
#define AppURL       "https://github.com/ziwuxin1/MAXsCursor"
#define AppExeName   "MAXsCursor.exe"

[Setup]
AppId={{E7A3C9D2-5B4F-4E8A-9B1C-3F6D2A8E1D0B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion=1.1.0.0

; Per-user install so the wizard never raises a UAC prompt.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={userpf}\MAXsCursor
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Output
OutputDir=.
OutputBaseFilename=MAXsCursor-Setup-v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Branding
SetupIconFile=..\src\MAXsCursor\Assets\cursor.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} v{#AppVersion}

; Supported OS
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start MAXs Cursor when I sign in"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\MAXsCursor.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Best-effort cleanup: kill any running instance before the uninstaller deletes files.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden; RunOnceId: "KillApp"
