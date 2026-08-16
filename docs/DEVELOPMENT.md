# 開発・引き継ぎガイド

最終確認日: 2026-08-16

基準コミット: `f7faa0b`（`main` / `origin/main`）

この文書は、過去の Codex セッションを参照できない状態でも改修を再開できるように、現行コードから復元した仕様と判断事項をまとめたものです。着手前に `git status --short` と直近のコミットを確認し、この文書よりコードが新しい場合はコードを優先してください。

## 現在の状態

- GitHub: `https://github.com/usa-mishin/VrcKaihenLibrary.git`
- ソリューション: `VrcKaihenLibrary.slnx`
- アプリ: .NET 8 / WinUI 3 / Windows App SDK 2.3
- 対応プラットフォーム: x86 / x64 / ARM64
- 2026-08-16 に製品名、プロジェクト、名前空間、MSIX identity を `VrcKaihenManager` から `VrcKaihenLibrary` へ変更済み
- Debug x64 のビルドと、署名済み MSIX 1.0.0.0 のインストールを確認済み
- Release/MSIX は WinUI XAML activation のクラッシュを避けるため trimming を無効化済み
- 現在の `main` に未コミット変更はない状態から、この文書整備を開始

## 最初に実行する確認

```powershell
git status --short
git log -5 --oneline --decorate
dotnet build VrcKaihenLibrary.slnx -c Debug -p:Platform=x64
```

実データを使う場合は、BLM DB が `%APPDATA%\pm.booth.library-manager\data.db` にあることを確認します。ユーザーの DB や商品ファイルを削除・更新する診断は行わないでください。

## コード構成

- `VrcKaihenLibrary/MainWindow.xaml(.cs)`: 一覧、検索、フィルター、詳細パネル、編集ダイアログ、Unity インポートを統括する UI
- `Models/LibraryItem.cs`: 一覧・詳細表示用モデル。商品名短縮、バッジ表示、取得種別などの表示ロジックも保持
- `Models/AvatarProfile.cs`: 登録アバター、識別名、互換性判定結果
- `Models/AssetCategories.cs`: アプリ内分類の定義
- `Services/BoothLibraryReader.cs`: BLM DB を読み取り専用で開き、BLM スキーマをアプリモデルへ変換する境界
- `Services/UserMetadataStore.cs`: 本アプリ専用 SQLite DB の作成、移行、設定保存
- `Services/AssetClassifier.cs`: BOOTH カテゴリからアプリ内分類を決定
- `Services/AvatarCompatibilityService.cs`: 識別名、手動上書き、共通素体を使った対応判定
- `Services/PurchasedPackClassifier.cs`: BLM variation から取得種別を判定
- `Services/DuplicateDownloadService.cs`: ダウンロード済みファイルの重複を扱う
- `Services/UnityImportTargetResolver.cs`: カテゴリと設定から Unity の配置先を決定
- `Services/UnityPackageImportService.cs`: `.unitypackage` を検査し、必要なら pathname を書き換えて再生成・キャッシュ
- `Services/UnityEditorBridgeService.cs`: 起動中 Unity Editor の検出とインポート要求の受け渡し
- `scripts/`: 開発証明書、MSIX ビルド・インストール、App Installer 生成

## 守るべき設計上の前提

### データ安全性

- BLM DB は必ず `Mode=ReadOnly` で開く。BLM のスキーマ変更対応は原則 `BoothLibraryReader` 内に閉じ込める。
- 手動分類、対応アバター、カテゴリ別インポート先、アプリ設定は `%LOCALAPPDATA%\VrcKaihenLibrary\library.db` に保存する。
- 旧 `%LOCALAPPDATA%\VrcKaihenManager\library.db` は、新 DB が存在しない場合だけ SQLite backup API でコピーする。旧 DB は削除しない。
- ローカルの BLM DB、ユーザー商品、生成済みキャッシュをテストの後処理で消さない。

### 分類と Unity 配置

- 自動分類は BOOTH の親カテゴリ・子カテゴリを最優先にし、商品名やタグからの推測は行わない。
- 通常は `<Unity project>/Assets/<カテゴリ別フォルダー>/` に導入する。
- 「Assets 直下」は `<Unity project>/Assets/` を意味する。
- アバター商品は常に Assets 直下へ導入し、ユーザーが変更できない。
- `.unitypackage` の pathname 書き換え時は archive entry の安全性を先に検査し、path traversal を許可しない。
- 再生成物は BLM ライブラリ直下の隠しフォルダー `.VrcKaihenLibraryImportCache` にキャッシュする。旧キャッシュ名は可能なら移動して引き継ぐ。

### 対応アバター

- 識別名は Unicode NFKC 正規化後、大文字小文字と全角半角を吸収して照合する。
- アバターごとにメイン呼称と複数の識別名を持てる。完全な BOOTH URL は識別名として保存しない。
- 手動の追加・除外は自動判定との差分として保存する。
- 共通素体による間接一致はフィルターと件数には含めるが、詳細の直接対応一覧には表示しない。
- 「単体購入」の自動対応判定では、購入済み variation 名だけを根拠にする。未購入 variation、説明、タグを混ぜると別アバター向け商品を誤検出する。

### BLM variation と取得種別

- `booth_item_variations.order_id IS NOT NULL` を購入済み variation の判断材料にする。
- 対応カテゴリのみ `フルパック`、`単体購入`、`無料/ギフト` を表示する。
- 明示的な full-pack 表現があれば `フルパック`。なければ購入済み variation に登録アバター識別名があれば `単体購入`。それ以外は `フルパック` 扱い。
- variation 行があるのに全 `order_id` が null の場合、BLM のローカル DB だけでは無料とギフトを区別できないため `無料/ギフト` と表示する。
- 調査時点の 281 商品中、177 商品だけが `order_id` と非空の `variation_name` を両方持つ。91 商品は購入行があっても variation 名が null、12 商品は全 order_id が null、1 商品は variation 行なし。欠損値から購入 variation を推測しない。

### UI の現行仕様

- カードは正方形サムネイルを基準に可変幅で並べ、商品名は2行、ショップ名は1行。
- 取得種別、対応アバター件数、更新ありバッジはサムネイル右上に縦積み。専用メタデータ行へ移す変更は取り消し済み。
- 詳細は右側のスライドパネル。タグ、リンク、ダウンロードファイル、取得 variation、対応アバターを表示する。
- 検索、カテゴリ、対応アバター、ショップ、取得種別のフィルターは合成され、変更時は1ページ目へ戻る。
- 標準ページサイズは50件で、50・100・200件を選べる。
- 商品名の「スマート短縮」は設定で無効化できる。短縮規則を広げる場合、アバター名や商品固有名を消さないサンプル確認が必要。

## 名前変更後に残している互換処理

- 旧アプリ DB から新 DB への初回コピー
- Unity の旧 UPM bridge `com.vrckaihenmanager.import-bridge` の除去
- `.VrcKaihenManagerImportCache` から `.VrcKaihenLibraryImportCache` への移動

これらは既存ユーザーの移行用です。単なる旧名の検索結果として削除しないでください。MSIX identity は `usa-mishin.VrcKaihenLibrary` で、旧 identity とは Windows 上で別アプリとして扱われます。

## MSIX と配布

- 開発用の自己署名証明書は、テスト PC の Local Machine `TrustedPeople` に信頼登録が必要で、管理者 PowerShell を使う。
- `scripts/Build-Msix.ps1` が署名済みパッケージを `artifacts/msix` に生成する。
- `scripts/New-AppInstaller.ps1` は公開 HTTPS URL が決まった後の自動更新用。
- 正式配布では自己署名証明書を使わず、信頼されたコード署名サービスまたは証明書を使う。

## 検証方針

変更範囲に応じ、最低限次を確認します。

1. `dotnet build VrcKaihenLibrary.slnx -c Debug -p:Platform=x64`
2. BLM DB を読む変更では、DB が読み取り専用であることと欠損値を許容すること
3. UI 変更では、一覧、詳細パネル、編集ダイアログ、スクロール、ウィンドウ終了を実機確認
4. Unity 導入変更では、通常配置、Assets 直下、アバター、キャッシュ再利用、悪意ある archive path の拒否を確認
5. 配布変更では Release/MSIX を別途ビルドし、インストール済みアプリを起動確認

自動テストプロジェクトはまだありません。純粋ロジック（分類、タイトル短縮、取得種別、互換判定）を変更する際は、回帰を防ぐためテストプロジェクト追加を優先候補にしてください。

## 次に着手する際の候補

明示的な未完実装や `TODO` は現行ソースにありません。次の依頼が来るまでは仕様変更を推測して実装しないでください。保守性の観点では、以下が有力です。

- `MainWindow.xaml.cs` に集中している UI ロジックの分離
- 分類、商品名短縮、取得種別、対応判定の単体テスト追加
- BLM スキーマ差分を検証する fixture または読み取り契約テスト
- 本番署名・公開 URL 決定後の MSIX 配布フロー完成

## ドキュメント更新ルール

仕様、永続化形式、互換処理、ビルド方法、配布方法を変えたコミットでは、この文書も同じ変更で更新してください。調査メモを追記し続けるのではなく、「現在の正しい状態」が読めるように既存節を更新します。重要な過去経緯は Git 履歴に残します。
