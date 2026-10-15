; Inno Setup script for Online Proctoring System Client
; Creates an all-in-one standalone installer for students

#ifndef BuildFlavor
#define BuildFlavor "production"
#endif
#if BuildFlavor == "development"
#define MyAppName "ExamGuardian DEV Client"
#define MyAppId "ExamGuardian.Development.Client"
#define MyAppDirectory "ExamGuardianDev"
#define MyInstallerName "ExamGuardian_Dev_Setup"
#else
#define MyAppName "Online Proctoring System Client"
#define MyAppId "{{8B7B2B64-94E5-4D56-A8F2-7A950BC71D0E}"
#define MyAppDirectory "OnlineProctoringSystem"
#define MyInstallerName "Online_Proctoring_System_Setup"
#endif
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Online Proctoring System"
#define MyAppURL "http://localhost/landing/"
#define MyAppExeName "Online_Proctoring_System_Client.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppDirectory}
DisableProgramGroupPage=yes
OutputDir={#ReleaseOutput}
OutputBaseFilename={#MyInstallerName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#DeployDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
