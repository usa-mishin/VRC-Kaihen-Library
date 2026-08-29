# <img width="30" height="30" alt="icon" src="https://github.com/user-attachments/assets/7a952dd8-4cb3-4dda-bf4a-42be636960ff" /> VRC改変ライブラリ（BOOTH Library Manager拡張）（VrcKaihenLibrary）

BOOTH Library Manager（BLM）が管理する VRChat 向け商品を整理し、Unity プロジェクトへの導入を支援する非公式の Windows デスクトップアプリです。BOOTHおよびBOOTH Library Managerの運営元による提供・保証・提携を受けた製品ではありません。

## 主な機能

- BLM の SQLite データベースと商品保存先を読み取り専用で参照
- サムネイル付きの商品一覧、検索、分類、並べ替え、ページング
- 対応アバター、共通素体、取得種別（フルパック・単体購入・無料/ギフト）の管理
- 商品ごとの分類と Unity の配置先設定をアプリ専用 DB に保存
- ダウンロードファイルの確認と `.unitypackage` の Unity Editor へのインポート
- 商品フォルダーと BOOTH 商品ページを詳細画面から表示

## 必要環境

- Windows 10 19041 以降
- .NET 8 SDK
- Visual Studio 2022（WinUI 3 の開発時）
- BOOTH Library Manager（実データを使った動作確認時）

## ビルド

```powershell
dotnet build VrcKaihenLibrary.slnx -c Debug -p:Platform=x64
```

起動プロジェクトは `VrcKaihenLibrary/VrcKaihenLibrary.csproj` です。

## データの保存場所

- BLM DB: `%APPDATA%\pm.booth.library-manager\data.db`
- 本アプリ DB: `%LOCALAPPDATA%\VrcKaihenLibrary\library.db`

BLM DB には書き込みません。分類、対応アバター、配置設定などは本アプリ専用 DB に保存します。旧名 `VrcKaihenManager` の DB があり、新 DB がまだない場合は初回起動時にバックアップコピーして移行します。

## プライバシーと安全性

- 広告、利用状況解析、クラッシュ自動送信、独自サーバーへのデータ送信はありません。
- BLM DB は読み取り専用で開き、設定だけを本アプリ専用DBへ保存します。
- サムネイルの自動通信先は HTTPS のBOOTH公式ドメインに制限します。
- UnityPackageは安全な作業領域へ展開し、リンク、絶対パス、パストラバーサル、特殊項目、過大な項目数・展開容量を拒否します。
- Unityプロジェクトの変更と重複整理は、ユーザーが該当操作を選んだ場合だけ実行します。

詳しくはアプリ左下の「ポリシー」、[プライバシーポリシー](docs/PRIVACY.md)、[セキュリティ方針](SECURITY.md)を参照してください。本アプリはBOOTHおよびBOOTH Library Managerの非公式ツールです。

BLM連携の取得項目、通信境界、同意導線、検証証跡、運営元への確認事項は[BOOTH Library Manager 連携監査](docs/BOOTH-INTEGRATION-AUDIT.md)にまとめています。

## MSIX の開発用ビルド

```powershell
$thumbprint = .\scripts\New-DevelopmentCertificate.ps1
.\scripts\Build-Msix.ps1 -Version 1.0.0.0 -Platform x64 -CertificateThumbprint $thumbprint
```

自己署名証明書を使うテスト PC では、管理者 PowerShell から証明書を一度信頼させます。

```powershell
.\scripts\Trust-DevelopmentCertificate.ps1 -CertificateThumbprint <Thumbprint>
.\scripts\Install-Msix.ps1 -PackagePath <生成したMSIXのパス>
```

本番配布には信頼された証明書によるコード署名が必要です。

他PCへ自己署名版を渡す手順は [docs/INSTALL-OTHER-PC.md](docs/INSTALL-OTHER-PC.md) を参照してください。

## EXE インストーラー

証明書の事前登録が不要な、自己完結型の非パッケージ版も生成できます。ビルドには Inno Setup 6 が必要です。

```powershell
winget install --id JRSoftware.InnoSetup -e
.\scripts\Build-ExeInstaller.ps1 -Version 1.0.6.0 -Platform x64
```

生成物は `artifacts/installer` に保存されます。更新時は同じインストーラーをより大きな版番号で生成し、既存環境で実行すると上書き更新されます。詳細は [docs/EXE-DISTRIBUTION.md](docs/EXE-DISTRIBUTION.md) を参照してください。

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/). リリース用インストーラーはGitHub Actionsでタグのソースから再現ビルドし、署名リクエストを手動承認します。詳細は [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md) を参照してください。

申請用のプロジェクト情報は [SignPath application draft](docs/SIGNPATH-APPLICATION.md) にまとめています。

## 開発を再開する方へ

公開前に個人情報・秘密情報・ローカル設定を確認する場合は、[オープンソース公開前チェック](docs/OPEN-SOURCE-CHECKLIST.md) も参照してください。

## ライセンス

本プロジェクトは [MIT License](LICENSE) で提供します。利用しているライブラリについては [Third-party notices](THIRD-PARTY-NOTICES.md) を参照してください。

最初に [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) を読んでください。現在の構成、壊してはいけない前提、確認済みの制約、検証手順をまとめています。
