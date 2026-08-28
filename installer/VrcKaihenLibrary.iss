#ifndef SourceDir
  #error SourceDir must be supplied with /DSourceDir=...
#endif
#ifndef AppVersion
  #error AppVersion must be supplied with /DAppVersion=...
#endif
#ifndef OutputDir
  #error OutputDir must be supplied with /DOutputDir=...
#endif

#define AppName "VRC改変ライブラリ（BOOTH Library Manager拡張）"
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
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; IconIndex: 0
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; IconIndex: 0; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加アイコン:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} を起動する"; Flags: nowait postinstall skipifsilent

[Code]
const
  DataAccessConsentVersion = 1;
  ConsentRegistryKey = 'Software\VrcKaihenLibrary';

var
  DataAccessConsentPage: TInputOptionWizardPage;

function HasRecordedDataAccessConsent: Boolean;
var
  RecordedVersion: Cardinal;
begin
  Result := RegQueryDWordValue(
    HKCU, ConsentRegistryKey, 'DataAccessConsentVersion', RecordedVersion)
    and (RecordedVersion >= DataAccessConsentVersion);
end;

function HasCommandLineDataAccessConsent: Boolean;
begin
  Result := CompareText(
    ExpandConstant('{param:DATAACCESSCONSENT|}'), 'accept') = 0;
end;

function IsDataAccessConsentAccepted: Boolean;
begin
  Result := HasRecordedDataAccessConsent
    or HasCommandLineDataAccessConsent
    or ((DataAccessConsentPage <> nil) and DataAccessConsentPage.Values[0]);
end;

procedure UpdateDataAccessConsentNextButton;
begin
  if (DataAccessConsentPage <> nil)
    and (WizardForm.CurPageID = DataAccessConsentPage.ID) then
    WizardForm.NextButton.Enabled := DataAccessConsentPage.Values[0];
end;

procedure DataAccessConsentCheckChanged(Sender: TObject);
begin
  UpdateDataAccessConsentNextButton;
end;

procedure InitializeWizard;
begin
  DataAccessConsentPage := CreateInputOptionPage(
    wpWelcome,
    'ローカルデータの参照について',
    'インストール前に内容を確認してください',
    '本アプリは、BOOTH Library Manager があなたのPC内に保存した商品情報' + #13#10 +
    '（保存場所：%APPDATA%\pm.booth.library-manager\data.db）から、商品名、ショップ名、' + #13#10 +
    '商品説明、タグ、購入・ダウンロード済みバリエーション、更新日時、商品保存先を' + #13#10 +
    '読み取り専用で参照します。元の商品情報の変更・置換・削除は行いません。' + #13#10#13#10 +
    '氏名、住所、メールアドレス、パスワード、Cookie、ブラウザ履歴は読み取りません。' + #13#10 +
    '独自サーバーへの送信、広告、テレメトリー、クラッシュ自動送信はありません。' + #13#10 +
    'サムネイルだけをBOOTH公式HTTPSドメインから取得する場合があります。' + #13#10#13#10 +
    '本アプリはBOOTHおよびBOOTH Library Managerの非公式ツールであり、' + #13#10 +
    '運営元による提供・保証・提携を受けていません。',
    False,
    False);
  DataAccessConsentPage.Add(
    '上記を確認し、PC内の商品情報を読み取り専用で参照することに同意します');
  DataAccessConsentPage.CheckListBox.OnClickCheck :=
    @DataAccessConsentCheckChanged;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = DataAccessConsentPage.ID then
    UpdateDataAccessConsentNextButton;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = DataAccessConsentPage.ID)
    and (HasRecordedDataAccessConsent or HasCommandLineDataAccessConsent);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = DataAccessConsentPage.ID)
    and not DataAccessConsentPage.Values[0] then
  begin
    MsgBox('内容を確認し、同意する場合だけインストールを続行できます。',
      mbInformation, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not IsDataAccessConsentAccepted then
    Result := 'PC内の商品情報を参照することへの同意が確認できないため、インストールを中止します。' + #13#10 +
      '無人インストールでは /DATAACCESSCONSENT=accept を明示してください。';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and IsDataAccessConsentAccepted then
    RegWriteDWordValue(HKCU, ConsentRegistryKey,
      'DataAccessConsentVersion', DataAccessConsentVersion);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, ConsentRegistryKey, 'DataAccessConsentVersion');
end;
