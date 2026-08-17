#ifndef SourceDir
  #error SourceDir must be supplied with /DSourceDir=...
#endif
#ifndef AppVersion
  #error AppVersion must be supplied with /DAppVersion=...
#endif
#ifndef OutputDir
  #error OutputDir must be supplied with /DOutputDir=...
#endif

#define AppName "VRC改変ライブラリ"
#define AppExeName "VrcKaihenLibrary.exe"

[Setup]
AppId={{A781C8A8-2F4B-49FD-AAB5-5CBCA56B40D0}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=usa-mishin
DefaultDirName={localappdata}\Programs\VrcKaihenLibrary
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=VrcKaihenLibrary-{#AppVersion}-x64-setup
SetupIconFile={#SourceDir}\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany=usa-mishin
VersionInfoDescription={#AppName} セットアップ
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加アイコン:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} を起動する"; Flags: nowait postinstall skipifsilent
