# 開発・保守ノート

最終更新: 2026-08-13

## アーキテクチャ

- `Services/BoothLibraryReader.cs`: BLM DBを読み取り専用で開き、アプリのモデルへ変換する境界層
- `Services/AssetClassifier.cs`: BOOTHカテゴリからアプリ分類を決定
- `Services/UserMetadataStore.cs`: 手動分類とAssets直下フラグを独自DBへ保存
- `Services/UnityImportTargetResolver.cs`: Unityプロジェクト内の導入先を決定
- `Models/LibraryItem.cs`: 一覧・詳細画面で使用する商品モデル
- `MainWindow.xaml(.cs)`: 一覧、分類タブ、検索、右スライド詳細パネル、編集ダイアログ

BLMのスキーマ変更へ対応するときは、`BoothLibraryReader`内だけで差異を吸収する方針です。BLM DBへの書き込みは禁止します。

## 自動分類規則

BOOTHの親分類・子分類を最優先かつ唯一の自動分類基準とします。商品名やタグによる推測は現在行いません。

| BOOTH分類 | アプリ分類 |
|---|---|
| 3Dモデル > 3Dキャラクター | アバター |
| 3Dモデル > 3D衣装 | 衣装 |
| 3Dモデル > 3D髪型 | 髪型 |
| 3Dモデル > 3D装飾品 | アクセサリー |
| 3Dモデル > 3D靴 | 衣装 |
| 3Dモデル > 3D小道具 | ギミック |
| 3Dモデル > 3Dテクスチャ | テクスチャ |
| 3Dモデル > 3D素材・マテリアル / 3Dマテリアル | マテリアル |
| 3Dモデル > 3Dツール・システム | ツール |
| 3Dモデル > 3Dモーション・アニメーション | アニメーション |
| 3Dモデル > 3D環境・ワールド | ワールド |
| 上記以外 | その他 |

BLMの旧データなどで親分類が取得できない場合に備え、実装では子分類名単独でも一致させます。手動保存済みの分類は自動分類より優先されます。

## Unity導入先規則

- 通常: `<Unityプロジェクト>/Assets/<アプリ分類>/`
- 「Assets直下に配置」がON: `<Unityプロジェクト>/Assets/`
- アバター: 常に`Assets`直下。フラグはON固定で変更不可

`UnityImportTargetResolver`でもアバター判定を行い、UI以外から呼ばれた場合にも直下配置を保証します。

## UI仕様

- 一覧カードは最小幅180。利用可能幅に入る最大列数を求め、余剰幅を全列へ均等配分して右端の空白を抑える
- BOOTHの正方形サムネイルに合わせ、画像領域の縦横をカード幅と連動
- 商品名は常に2行分（高さ38）を確保
- サムネイル左上に分類別の色付きバッジを表示
- カードクリックで右側から詳細パネルをスライド表示
- フォルダーとBOOTH URLを開く操作は詳細パネルに表示
- 分類と配置フラグは、詳細パネル右上の「編集する」から開く編集専用ダイアログで変更
- アバター商品の編集ダイアログではメイン呼称と識別タグも編集し、対応アバター欄は表示しない
- 詳細パネルではBOOTHタグを直接表示せず、アバター識別タグまたは対応アバターをグレーのチップで表示
- 詳細右上の「商品情報の再読み込み」はBLM情報更新、自動識別・対応判定更新、完全一致重複確認をまとめて実行
- 一覧上部に分類名のみのカスタムタブを表示（件数は表示しない）
- 選択中のタブは分類ラベルと同じ色の背景・下線で示す。ラジオボタンは使用しない
- タブの文字色と背景色は独自テンプレートで同時に切り替え、ホバー時は分類色の4px下線を表示
- ヘッダーはタイトルを上段に保ち、商品検索・対応アバター・ショップ・「フィルタをクリア」を固定幅の1行に配置する。対応アバターとショップは同じ幅にし、選択中の対応アバターはメイン呼称で表示する
- 一覧の再読み込みはカテゴリタブ行の並び替えセレクトボックス右側に配置する
- 検索欄の右に対応アバター選択を置き、選択したアバター本人と対応する衣装・髪型・テクスチャへ一覧を絞り込む。手動追加・除外と共通素体も反映する
- 対応アバターの右に検索付きショップフィルターを置く。詳細パネルのショップカードをクリックした場合は、商品検索・分類・対応アバターを解除してショップ条件だけを適用する
- 左側に84px幅のナビゲーションを置き、隙間のない「一覧」「設定」項目を表示
- 選択項目は落ち着かせたBOOTH系の赤`#D0575C`と白文字、未選択項目は透明背景と同色文字で表示
- BLMの未読`latestDownloadableAvailable`通知を商品種類IDから商品へ紐づけ、対象カード右上に「更新あり」バッジを表示する。これはBLM通知状態であり、配布ファイル日時の比較ではない
- 詳細パネルは460px幅。識別・対応タグはショップ名直下に濃いグレー背景・白文字の折り返しチップで全文表示
- 検索可能な識別タグは色を変えず、ホバー時の下線とリンクカーソルで示す。共通素体チップは非リンク
- 識別タグチップはアバター詳細だけに表示する。衣装・髪型・テクスチャの対応先はアバターの商品名ではなくメイン呼称で表示する
- 検索・分類後の結果をページングし、1ページ50件を標準とする
- 表示件数は50・100・200件から変更可能。検索・分類変更時は1ページ目へ戻る
- 商品名、ショップ名、BLMへの登録日、商品更新日、商品公開日を昇順・降順で並び替え可能
- 並び替えは現在の条件を表示する単一ボタンから開く2列メニューに統合する。左列はA-Z・新しい順、右列はZ-A・古い順とし、選択中の項目へチェックを表示する
- デフォルトは「BLMへの登録日・降順（新しい順）」

### 商品名クリーニングの調査メモ（2026-08-14）

元の商品名は変更せず表示時だけ整形する。BLM内の商品名を調査すると、補助表現は先頭以外にもあり、`【】`・`[]`のほか`〖〗`や左右が不一致な`[VRC Hair】`も存在するため、括弧の位置を問わず内容を判定して除去する。

- 対応数: `【12アバター対応】`、`[21Avatars]`、`【複数アバター対応】`、`【全アバター対応】`
- 用途・環境: `【VRChat】`、`【VRChat想定】`、`【VRC 衣装】`、`【VRC Hair】`、`【3D衣装モデル】`
- 導入方式: `【PB】`、`【MA対応】`、`【MA設定済み】`、`【簡単導入】`
- 販促・状態: `【無料/Free】`、`【セール中】`、`【UPDATE】`
- 対応アバター: `【Milfy専用】`、`[Shinano]`など。登録済みアバター識別タグとの一致を条件にする

対応アバター表現は登録済みアバターのメイン呼称・識別タグとの一致で判定する。`【Nail&Ring】`や`【アイテクスチャ】`など商品内容そのものを表す可能性がある語は削除しない。

## 対応衣装・対応髪型・対応テクスチャと共通素体

衣装、髪型、アクセサリー、テクスチャ、マテリアル、ギミック、アニメーションが利用できる対象を、個別アバターだけでなく「共通素体」単位でも管理します。

- `Avatar`: 個別のアバター商品。BOOTH商品ID、名称を持つ
- `AvatarProfile.PrimaryIdentifier`: メイン呼称を1件保持（例: `ミルフィ`）
- `AvatarIdentifier`: サブ識別タグ。英語名、表記ゆれ、BOOTH商品IDなどを複数登録。完全なBOOTH URLは保存しない
- `avatar_shared_body_relations`: 登録済みアバター同士の共通素体関係。編集画面の複数選択で設定し、双方向の行として保存する

識別タグの照合は Unicode NFKC 正規化後に大文字小文字を無視して行う。共通素体は識別タグ内の文字列では推測せず、明示的な関係を推移的にたどる。
識別タグの直後に`対応`・`用`・`専用`が続くタグや説明文も一致とみなし、複数アバターをそれぞれ独立して検出する。
対応アバター編集は`ListView.SelectedItems`へ依存せず、各行の独立したチェック状態モデルから複数件を保存する。WinUIの表示前選択・仮想化による選択欠落を避けるためである。
自動照合はプロフィールごとに一致を1件選び、全プロフィールの走査を継続する。最初の一致で外側のループまで終了しないこと。
対応アバターはスクロール可能な複数選択フライアウトで編集する。`全アバター対応`設定の商品は、どのアバターで絞り込んでも表示する。
手動オーバーライドは自動判定との差分だけを保存する。自動候補を選択したままなら保存せず、自動候補を外した場合だけ`-1`、自動候補ではない項目を追加した場合だけ`1`とする。
詳細パネルの三点メニューにBOOTHデータ再取得、対応アバター個別設定リセット、ダウンロード重複削除を置き、それぞれの処理を分離する。
対応概念のない分類から対応対象の分類へ変更した場合は、既存の手動差分と全アバター対応をリセットし、その保存では自動判定だけを採用する。
カードの分類チップ直下に実効対応数を表示する。0件と全アバター対応では件数チップを表示しない。
三点メニュー内は余白のないフラットなアイコン＋テキスト行として表示する。
三点メニュー項目は通常時フラットにし、ホバー時は通常ボタンと同じ背景変化を表示する。
重複整理は各サフィックスグループでファイル更新日時が最も新しいフォルダーだけを残す。古いものをごみ箱へ移動した後、保持フォルダーの末尾`(1)`・`（1）`などを外して基準名へ変更する。
編集画面のUnity配置は「Unityインポート先」見出し、現在の配置先、`Assets直下に配置する`チェックで構成する。
詳細画面のショップ情報は正方形サムネイルとショップ名を角丸カード内に表示する。
詳細パネル幅は420pxとする。
一覧の対応アバターフィルターは選択ダイアログを開き、正方形サムネイルと一覧用商品名を横並びにした角丸行を縦方向へ並べ、1体を選択する。
アバター選択行の名称は一覧カードと同じ整形済み商品名を使い、最大2行で省略する。アイテム編集の複数選択フライアウトも同じ横長カードUIを使う。
一覧の対応アバターフィルターもモーダルではなくセレクトボックス型フライアウトを使う。単一選択・複数選択の両フライアウトに商品名とメイン呼称を対象とする候補内検索を設ける。
編集画面ではセレクトボックス風のフライアウトから複数選択し、詳細パネルでは「〇〇と共通素体」の検索リンクではないチップとして表示する。
- `ItemCompatibility`: 衣装・髪型・テクスチャ商品から、個別アバターまたは共通素体グループへの対応関係
- 対応関係には`自動検出`または`手動`の由来、検出根拠、確度を保持する
- 手動追加・手動削除は自動検出より優先する

### 自動検出対象

1. 商品説明内のアバターBOOTH商品URLまたは商品ID（最も強い根拠）
2. 商品説明内のアバター識別タグ
3. BOOTHタグ内の識別タグ、および「○○対応」「○○用」「○○専用」形式
4. BLMの`booth_item_variations.variation_name`に保存された商品種類・バリエーション名

BLM実データのスキーマでは`booth_item_variations`に`variation_name`と`order_id`が存在する。購入した種類を判定する余地はあるが、現行の読み取り処理は商品に紐づく全`variation_name`を連結しており、`order_id`を購入済み判定へ使っていない。フルパック／単体購入の機能へ利用する前に、`order_id`の有無・同一注文内の行構成と、BOOTH側でフルパック購入時に展開されるバリエーション行の実例を照合すること。

アバター商品を登録すると、商品名から日本語名・英語名・BOOTH商品IDを初期識別タグとして自動生成します。完全なBOOTH URLは保存せず、説明文中のURLは含まれる商品IDで照合します。ユーザーはアバター設定画面で識別タグを追加、修正、無効化できます。短すぎる名称や一般語は誤判定しやすいため自動確定に使いません。

検出結果には一致した文字列、検出場所（説明・タグ・バリエーション）、対象アバター、確度を保存します。単なる部分一致による誤判定を避けるため、自動結果は候補として提示し、確定状態と分離します。

共通素体グループへ登録されたアバター間では対応関係を伝播します。例としてミルフィとエクが同じグループなら、ミルフィ対応商品をエクでも対応として扱います。画面上では「直接対応」と「共通素体による対応」を区別して表示し、例外的に非対応となる商品を手動で除外できるようにします。

### 実装時の原則

- BOOTHへの追加アクセスは必須にせず、まずBLMに保存された説明、タグ、バリエーションを使う
- アバター識別タグと共通素体グループはアプリ独自DBに保存し、BLM DBへ書き込まない
- 自動判定の再実行で手動確定・手動除外を上書きしない
- アバター削除時も識別履歴と手動対応関係を不用意に消さない

### 現在の実装範囲

- アバター分類の商品からメイン呼称とサブ識別タグを初期生成
- アバター設定ダイアログではメイン呼称を識別タグ一覧の先頭へ編集可能なテキストボックスとして表示し、サブ識別タグは1件ごとのテキストボックスを＋／－で追加・削除する。メイン呼称も識別タグとして照合する
- 共通素体の選択は対応アバター選択と同じ、候補検索・サムネイル・商品名・チェックボックスを備えた複数選択UIを使用する
- 詳細パネルの対応アバターはメイン呼称だけを` / `区切りで表示する。検出元や手動設定などの根拠は小見出し横のリストアイコンへ集約し、ホバー時のツールチップで表示する
- 詳細パネル上部はショップリンクの右側に、アイコンを上・小さな文言を下へ置いた「フォルダー」「商品ページ」ボタンを配置する。Unity配置先は対応アバターの下へ控えめな補足表示として置く
- 詳細パネルでは保存フォルダー配下の`.unitypackage`を再帰検索し、件数付きExpanderへファイル名順で表示する。同名ファイルは更新日時の新しい順に並べ、重複名チップ、更新日時、保存フォルダーからの相対ディレクトリを表示する
- UnityパッケージのExpanderは初期状態で展開し、内容と各項目の余白を設けず、区切り線を使った横幅いっぱいのListViewとして表示する
- 詳細パネルは商品サムネイルを含む本文全体を1つのScrollViewerへ入れ、縦に長いUnityパッケージ一覧でも十分なスクロール範囲を確保する
- Unityパッケージカードのクリック時、Assets直下指定のアイテムは元ファイルをそのまま関連付け起動する。それ以外は`.unitypackage`内の各`pathname`の`Assets/`直後へ分類名を挿入した一時パッケージを、BLMライブラリ直下の隠し共通キャッシュ`.VrcKaihenManagerImportCache/<登録ID>/`へ生成し、`Assets/<分類>/`配下へインポートされる形で関連付け起動する。GUID・asset・asset.metaは変更しない
- パッケージアプリの`Environment.LocalApplicationData`は仮想化され、`ApplicationData.LocalCacheFolder`もUnityのバージョンや起動経路によって外部参照が安定しないため、Unityへ渡す生成物には使用しない。共通キャッシュは商品フォルダーの外に置き、商品内のUnityパッケージ一覧と重複判定へ混入させない
- `.unitypackage`のWindows関連付け先と起動中Editorのバージョンが異なると、関連付け起動した別Editorへ渡され、現在のプロジェクトではインポートが始まらない。実機では関連付けが`2022.3.48f1`、VRChatプロジェクトが`2022.3.22f1`となる事例を確認したため、関連付けは使用しない
- `-openfile`は起動済みEditorを前面化してもインポート要求を配送しないケースがあるため、最終的なインポート実行には使用しない。クリック時は本アプリの直後にある可視UnityウィンドウをZオーダーから探し、WMIのプロセスコマンドラインから`-projectPath`を取得する
- Unity Hub経由の起動ではオプションが`"-projectPath" "<path>"`のようにオプション名ごと引用符で囲まれるため、引用符あり・なしの両形式を解析する
- 対象プロジェクトへ`Assets/Editor/VrcKaihenManager/VrcKaihenManagerImportBridge.cs`を一度だけ配置する。要求するパッケージパスを`Library/VrcKaihenManager/import-request.txt`へ保存し、Editorブリッジが`AssetDatabase.ImportPackage(path, true)`をUnity内で実行する。初回はブリッジのコンパイル後に処理し、要求ファイルは処理開始時に削除する
- Unityプロジェクトが`AppData/Local`配下にある場合、パッケージ化されたデスクトップアプリの`System.IO`書き込みは仮想化され、UnityにはAsset Database通知だけ届いてコンパイル時に`CS2001: source file could not be found`となる。このためプロジェクト内のブリッジと要求ファイルは`Windows.Storage` API経由で実体へ書き込む
- ブリッジの初回コンパイルとアセンブリ再読み込み直後に`EditorApplication.update`内から直接`ImportPackage`を呼ぶと、Unityが画面を表示せず要求を無視する場合がある。要求読み取り後は`EditorApplication.delayCall`へ処理を移し、開始・完了・キャンセル・失敗イベントをUnityログへ記録する
- Unity Editorが見つからない場合、またはプロジェクトパスを取得できない場合はエラーを表示する。インポート確認画面はUnity側へ委ねる
- 配置変更で動作しない商品は「Assets直下に配置する」を使用する
- BLMへ後から追加したアバターは、起動時または一覧の「再読み込み」で同期する。同期後にアバター候補、対応数、自動判定を更新し、開いている編集画面には反映しないため再度開く
- `エクと共通素体`のようなサブ識別タグが、相手アバターのメイン呼称を参照して共通素体関係を作る
- 共通素体関係は片側だけの設定でも双方向として解釈し、連鎖した関係も展開
- 識別タグが1件でも保存済みなら、同期時に自動タグを追加・上書きしない
- 説明文、BOOTHタグ、商品種類から衣装・髪型・テクスチャの対応を検出
- 詳細パネルに直接検出と共通素体経由の根拠を表示
- 商品編集画面で自動候補を選択して手動確定、選択解除して手動除外、未検出アバターを選択して手動追加可能
- 手動確定・追加・除外は独自DBへ保存し、以後の自動検出より優先

## 未実装・次の候補

- Unityプロジェクトの登録・選択UI
- ZIP / unitypackage内の内容確認
- Unityプロジェクトを明示選択して直接インポートする処理（現状は配置先を書き換えた`.unitypackage`の関連付け起動）
- 導入前の競合検査とバックアップ
- 自動分類の再適用操作（手動分類を消す明示的な操作）

### 調査済み・未実装

- **BLMの購入バリエーション判定**: BLM実データの`booth_item_variations`には`variation_name`と`order_id`が存在するため、フルパック／アバター別商品の購入種類を判定できる可能性が高い。現行実装は全`variation_name`を連結するだけで、`order_id`による購入済み判定は未実装。実装前に、フルパック購入時にフルパック行だけが購入済みになるのか、内包する各バリエーション行にも同じ注文情報が付くのかを実データで検証する必要がある。
- 調査依頼で得たものの未実装となった仕様・判明事項は、今後もこの節へ「確認できた事実」「現在の制限」「実装前に必要な検証」を分けて追記する。

## 重複ダウンロード整理

- BLMの商品フォルダー内で、同じ基底名に`(1)`、`(2)`、`（1）`等が付いた兄弟フォルダーを候補にする
- 相対ファイル名と全ファイル内容のSHA-256相当フィンガープリントが完全一致した場合のみ重複と判断
- 更新日時が最も新しいフォルダーだけを保持し、それ以外を削除候補にする。保持したフォルダーに`(1)`、`（1）`等のサフィックスがあれば基底名へ戻す
- 自動削除せず、詳細パネルで確認を受けた後にWindowsのごみ箱へ移動する
- 内容が異なる場合は更新版の可能性があるため削除しない

## 変更履歴

### 2026-08-14 Unityインポート障害の実機調査

- Windowsの`.unitypackage`関連付け先（Unity 2022.3.48f1）ではなく、前面のUnityプロセスからWMIで`-projectPath`を取得し、対象プロジェクト内のEditorブリッジ経由で`AssetDatabase.ImportPackage`を呼ぶ方式にした。
- 対象プロジェクトが`AppData/Local`配下の場合、パッケージ化アプリの`System.IO`書き込みは仮想化されるため、Editorブリッジと要求ファイルは`Windows.Storage`で実体へ書き込む。
- .NET `System.Formats.Tar.TarWriter`で再生成したgzip tarは、`tar.exe`では読めてもUnity 2022.3.22f1がインポート画面を開けず、`ImportPackageStarted`の後で停止した。
- 同じ展開内容をWindows標準のbsdtar（`tar.exe --format=ustar`）で再梱包するとUnityが正常に認識したため、配置先変更パッケージの生成処理をbsdtar方式へ変更した。
- 再生成前にアーカイブ内の絶対パスと`..`を拒否し、一時展開先は共通キャッシュ配下の固有ディレクトリに限定する。成功・失敗にかかわらず一時ファイルを削除する。
- キャッシュキーへ再梱包形式のバージョンを含め、旧TarWriter版の不正なキャッシュを再利用しない。
- 実機検証では「ボサボサショートヘア」のパッケージをアプリからクリックし、UnityのImport Unity Package画面が開くこと、および内部pathnameが`Assets/髪型/#MARIYURI/...`になっていることを確認した。検証時はImportを確定せずキャンセルした。
- 開発実行環境でWindows App SDK 2.3の動的依存関係初期化が`REGDB_E_CLASSNOTREG`になったため、`WindowsAppSDKSelfContained=true`としてランタイム同梱ビルドにした。

### 2026-08-14 Unityブリッジと再圧縮の改善

- Unity連携ブリッジの配置先を`Assets/Editor/VrcKaihenManager`から、埋め込みUPMパッケージ`Packages/com.vrckaihenmanager.import-bridge`へ変更した。商品アセットとツール用Editorコードを分離する。
- 次回のインポート要求時にUPMパッケージを作成・更新した後、旧`Assets/Editor/VrcKaihenManager`とそのmetaを削除する。`Assets/Editor`にほかの内容がある場合は残し、空になった場合だけフォルダーとmetaを削除する。
- 配置先変更用unitypackageのgzip再圧縮を`compression-level=1`へ変更し、圧縮率より生成速度を優先した。Unity互換性のためustar形式とbsdtarによる生成は維持する。
- 再圧縮設定をキャッシュキーのバージョンへ反映し、従来の標準圧縮キャッシュと混同しない。

### 2026-08-14 再圧縮を行わないUnityインポート方式の調査

- Unity 2022.3の公式`AssetDatabase.ImportPackage(string packagePath, bool interactive)`には、パッケージパスと確認画面の有無しか指定できず、インポート先を変更する引数やコールバックはない。
- `importPackageCompleted`は完了通知だけで、選択されたファイル一覧や保存先の置換機能を提供しない。
- 通常インポート後に`AssetDatabase.MoveAsset`で分類フォルダーへ移すことは可能だが、元の`Assets`直下で既存ファイルを先に上書きする危険、同一GUIDが既に分類フォルダーにある更新時の競合、移動後の再インポートが発生する。このため主要方式にはしない。
- 推奨方式は、元unitypackageを一度だけ展開し、GUIDディレクトリごとの`pathname`、`asset`、`asset.meta`を読み、`pathname`を`Assets/<分類>/...`へ変換してUnityプロジェクトへ直接配置した後、`AssetDatabase.Refresh`する独自インポーターである。再圧縮とUnity側での再展開が不要になる。
- 実データ「1_Material Pack ボサボサショートヘア」（88.8MB）を作業領域へ展開した実測は約0.31秒。154ファイル、40 pathname、37 asset、40 asset.meta、37 preview.png、展開後91.3MBだった。再圧縮方式より開始待ち時間を大幅に短縮できる見込み。
- 独自インポーターではUnity標準のImport Package画面を利用できないため、アプリ側にインポート内容確認UIが必要。各パスの選択、既存ファイル、同一GUID、上書き、パストラバーサルを事前検査する。
- フォルダー項目は`asset`を持たず`asset.meta`だけを持つ場合があるため、フォルダー作成とmeta配置にも対応する。`preview.png`はインポートには不要。
- 更新時はmeta内GUIDを読み、Unity側で`AssetDatabase.GUIDToAssetPath`を使って既存配置を確認する。同一GUIDが既に目的外の場所に存在する場合は自動上書きせず、更新先の選択または警告を行う。
- ファイル配置中はAsset Databaseの自動更新を抑止し、配置完了後に一度だけRefreshする。中断・失敗時に部分配置を残さないため、事前展開、競合検査、バックアップまたはロールバックを設ける。
- 公式資料: [AssetDatabase.ImportPackage](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.ImportPackage.html)、[AssetDatabase.MoveAsset](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.MoveAsset.html)、[AssetDatabase.Refresh](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.Refresh.html)、[Asset Database batching](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetDatabaseBatching.html)

### 2026-08-15 再圧縮なし直接インポートの試験実装

- アバターまたは「Assets直下へ配置」がONのアイテムは、従来どおり元unitypackageを`AssetDatabase.ImportPackage`へ渡してUnity標準画面を使用する。
- それ以外は元unitypackageを一時領域へ展開し、GUIDディレクトリの`pathname`を`Assets/<分類>/...`へ変換して、`asset`と`asset.meta`だけを直接インポート用ペイロードへ構成する。再圧縮は行わない。
- UPMブリッジは直接インポート要求を監視し、アセット件数と既存ファイル上書き件数をUnityの確認ダイアログに表示する。ユーザーが確定した場合だけプロジェクトへコピーする。
- metaのGUIDを`AssetDatabase.GUIDToAssetPath`と照合し、同一GUIDが予定配置先と異なる場所に存在する場合は安全のため中止する。警告は先頭8件と残件数だけを表示する。
- 配置中は`AssetDatabase.StartAssetEditing`で更新をまとめ、完了後に`StopAssetEditing`と`Refresh(ForceUpdate)`を一度実行する。
- 上書き対象は`Library/VrcKaihenManager/ImportBackups`へ一時退避し、例外時には新規ファイル削除と既存ファイル復元を試みる。完了後はバックアップと直接インポート用ペイロードを削除する。
- パストラバーサル、Assets外への出力、パッケージ内の同一配置先重複を拒否する。アプリからブリッジ要求を渡す前に失敗した場合も一時ペイロードを削除する。
- 実機では88.8MB・37アセットのパッケージで、再圧縮なし確認ダイアログが表示されることを確認した。検証時はキャンセルし、Unityプロジェクトへは配置していない。初回はUPMブリッジ更新によるコンパイル時間が加わる。
- 現時点ではUnity標準Import Package画面のようなファイル単位の選択UIは未実装。直接インポートはパッケージ内の全アセットが対象となる。

### 2026-08-15 Unity標準インポートへの復帰と前処理高速化

- 安全性とファイル単位選択を優先し、再圧縮なし直接インポートの実行経路およびブリッジコードを削除した。すべてのunitypackageをUnity標準の`AssetDatabase.ImportPackage(path, true)`へ渡す方式へ戻した。
- アバターおよび「Assets直下」は元パッケージをそのまま渡す。分類配置が必要なアイテムだけ、pathnameを書き換えた互換パッケージを事前生成する。
- 前処理の進行状況を詳細パネル下部のProgressBarとパーセント付きテキストで表示する。段階はキャッシュ確認、検査、展開、配置先変更、高速圧縮、キャッシュ保存、完了。
- パッケージ生成中の作業領域をBLMライブラリと同じDドライブから、`Path.GetTempPath()/VrcKaihenManager/UnityPackagePreparation`へ変更した。完成ファイルだけをBLM側の共通キャッシュへコピーする。
- アーカイブの安全確認はbsdtarの一覧取得結果に対して絶対パスと`..`を拒否し、展開にもWindows標準bsdtarを使用する。.NET TarReaderによる展開は実測で遅かったため使用しない。
- 再圧縮はUnity互換のgzip/ustarを維持しつつ`gzip:compression-level=0`とした。BOOTHのパッケージは画像・FBXなど既に圧縮しにくいデータが多く、圧縮率より生成速度を優先する。
- キャッシュへのコピーは`.copying-<GUID>`へ書いた後に同一ボリューム内でリネームし、途中終了した不完全ファイルを有効キャッシュとして扱わない。
- 「1_Material Pack ボサボサショートヘア」（88.8MB）の実機初回処理は、Dドライブ作業＋.NET展開版の約15.7秒から約3.9秒へ短縮した。完了後にUnity標準`Import Unity Package`画面が開くことを確認し、検証時はキャンセルした。
- 同じ元ファイル・更新日時・分類・生成方式の二回目以降は準備済みキャッシュを利用するため、再生成を行わない。

### 2026-08-15 Window shutdown crash guard

- Windows Application Error event 1000 showed an access violation (`0xc0000005`) in `Microsoft.UI.Xaml.dll` when the window was closed while asynchronous Unity-package preparation or bridge communication could still resume.
- `MainWindow` now marks its lifetime as closing from both `AppWindow.Closing` and `Window.Closed`.
- Unity import progress callbacks and continuations stop before accessing XAML controls once shutdown starts. Exception handlers also avoid writing status text after shutdown.
- Verification: x64 build completed with no warnings or errors; five normal-close tests at different points during startup all exited with code 0 and produced no new Application Error events.

### 2026-08-15 Download file browser

- The detail panel now scans every file below the item's BLM download directory once and shows a `ダウンロードファイル一覧` expander.
- Files are grouped in this fixed order: Unity packages, textures, editable image sources, 3D data, documents, and other files. Classification is extension-based in `MainWindow.ClassifyDownloadFile`.
- Selecting a group opens a common file-list dialog sorted by file name and then by newest timestamp. Duplicate base names retain the duplicate warning badge.
- Selecting a Unity package closes the dialog and uses the existing Unity standard import flow. Selecting any other file launches a new Explorer window with that file selected (`explorer.exe /n,/select`).
- The expander header has a compact button that opens the item's download folder.

### 2026-08-15 Download file browser layout revision

- The outer download-file expander was replaced with a permanently visible `ダウンロードファイル` section. Only individual file categories expand, with at most one category open at a time.
- Each category has its own accent color, a 5-pixel left accent line, and a matching icon color. Header/category spacing was removed so the first category begins immediately below the section header.
- The folder chip now reserves enough width for `フォルダーを開く`; the older folder action beside the shop link was removed.
- File actions are displayed inline below the selected category rather than in a `ContentDialog`. Unity packages still start the Unity import flow; other file types still open Explorer with the file selected.
- A case-insensitive `material` match in a Unity package file name displays a `マテリアル` badge before the file name.

### 2026-08-15 Inline file rows and avatar filter chips

- Expanded download-file rows now use a white background and inherit their category accent as a 5-pixel left line. File names use 14px semibold text, wrap to at most two lines, and no longer show the file timestamp.
- Avatar identifier chips now use a wrapping `ItemsWrapGrid` instead of a horizontal scroller. Only the primary identifier is interactive; aliases and shared-body labels are display-only.
- Clicking a primary avatar identifier no longer opens BOOTH search. It applies the application's compatible-avatar filter, clears text/shop/category filters, and updates the filter controls.
- Compatible avatars in the detail panel are rendered as the same wrapping chip UI. Each detected avatar chip applies the same in-app avatar filter, and the existing link-chip control underlines its text on pointer hover.

### 2026-08-15 Detail panel crash after chip wrapping change

- Opening an item could terminate the app inside native XAML before a managed exception was recorded. The regression was isolated to populating a variable-width `ItemsWrapGrid` hosted by a plain `ItemsControl` when the detail panel opened.
- Identifier and compatible-avatar chips now use `RichTextBlock` inline UI containers. This preserves variable-width wrapping, hover underline, and in-app filter clicks without relying on the unsupported/unstable virtualizing panel combination.
- Verification: x64 build completed with no warnings or errors. UI Automation launched the packaged executable, selected a library card, found the `アイテム詳細` panel, and confirmed the process remained alive.

### 2026-08-15 Detail panel item-dependent freeze

- The first verification checked process survival but did not detect an item-dependent UI stall. Download files were being appended one by one to six observable category collections on the UI thread, producing one layout notification per file.
- Category file lists are now built as immutable batches and assigned once. Opening a detail panel produces only six category notifications regardless of the number of downloaded files.
- Expanded file lists now have a 360px height limit and their own scrollbar. This restores `ListView` virtualization inside the detail panel's outer `ScrollViewer`, preventing every file row from being created at once for large products.
- Verification: x64 build completed with no warnings or errors. Automated detail opening across multiple cards remained responsive after the change.

### 2026-08-15 Visual Studio break when opening detail panel

- The `RichTextBlock`/`InlineUIContainer` workaround still allowed item-dependent XAML exceptions and could make the debugger open `MainWindow.xaml.cs` instead of showing the panel.
- Tag layout now uses the project-owned non-virtualizing `Controls/SimpleWrapPanel.cs` as an `ItemsControl.ItemsPanel`. It performs only measure/arrange wrapping and avoids both `ItemsWrapGrid` virtualization and interactive controls embedded in text layout.
- `ItemsGrid_ItemClick` now has an exception boundary. An unexpected item-specific managed exception is reported in the status text and debug output rather than escaping the event handler.
- A clean x64 rebuild completed with no warnings or errors. UI Automation opened all 12 visible item cards consecutively; the process remained alive and responsive after every detail-panel update.

### 2026-08-15 Download file brush COM exception

- Visual Studio showed an unhandled `System.Runtime.InteropServices.COMException` at `DownloadFileEntry.AccentBrush`.
- `FindDownloadFiles` runs on a worker thread. The `AccentBrush` property initializer created a WinUI `SolidColorBrush` while each result was constructed on that worker, violating WinUI object thread affinity.
- `DownloadFileEntry.AccentBrush` is now nullable and has no UI-object initializer. The existing category assembly step assigns the category brush only after `await Task.Run(...)` has resumed on the UI thread.
- Verification: x64 build completed with no warnings or errors. The packaged app opened an item and remained alive/responsive with the detail panel visible after waiting 10 seconds for download-file scanning to complete.

### 2026-08-15 Detail links, scrollbar spacing, and category import settings

- Identifier chips use a template without pointer visual states, so hovering only underlines the text and no longer changes the chip color.
- Compatible avatars are displayed as slash-separated primary-name links instead of chips. Each avatar remains individually clickable and applies the in-app compatible-avatar filter; hover underline is provided by `LinkChipBorder`.
- The detail `ScrollViewer` reserves 12px on its right and fixed thumbnail/shop widths were reduced from 372px to 360px, keeping the vertical scrollbar outside content and download rows.
- Added persistent SQLite table `category_import_settings(category, folder_name, import_to_assets_root, updated_at)` and a settings-page editor for every asset category.
- Default settings use the category name as the Assets child folder. Avatar and World default to Assets root; Avatar remains locked to root, while World and other categories can be changed in Settings.
- Category-root settings take precedence over an item's root flag. An item can still opt into root when its category is folder-based. Folder names reject empty/invalid/reserved values before saving.
- `UnityPackageImportService.PrepareForImport` now receives the resolved folder name (or null for Assets root), includes it in the cache key, and rewrites unitypackage pathnames to the configured folder.
- x64 integration build completed with no warnings or errors. GUI automation was not run in this turn because the active execution policy rejected GUI escalation; no user settings were mutated during verification.

### 2026-08-15 Edit dialog scrolling and download row hierarchy

- The item-edit dialog content is wrapped in a vertically scrolling region with a 620px maximum height and right-side scrollbar clearance. ContentDialog primary/close buttons remain outside that region, so large avatar identifier sets can be edited through the final row without losing Save/Cancel.
- Expanded download-file row accent lines were reduced from 5px to 3px; category header accents remain 5px to preserve hierarchy.
- The standalone detail placement label was removed from the visible metadata area. The Unity package category now has a second, smaller line showing the resolved destination (`Assets root` or the configured category folder); other categories remain single-line.
- Saving category import settings refreshes the open detail panel's download categories so the Unity package destination subtitle changes immediately.
- x64 build completed with no warnings or errors.

### 2026-08-15 Product-title cleanup re-audit (proposal, not implemented)

- Re-audited all 258 current item names in the BLM database after additional downloads. No cleanup behavior was changed in this audit.
- Existing cleanup already removes recognized avatar identifiers in `【】`/`[]`/`〖〗`, bracketed avatar counts such as `【15アバター対応】`, generic labels such as `【VRChat】`, `【VRC Hair】`, `【PB】`, `【MA対応】`, and simple `SALE`/`セール中` decorations.
- High-confidence additions proposed for a later implementation:
  - Recognize `〈...〉` and `《...》` when their content is an already-approved auxiliary pattern. Current examples include `〈19アバター対応〉`, `〈21アバター対応〉`, and `《SALE》`.
  - Remove avatar-count compatibility text even when it is outside brackets, including full-width digits, optional `+α`, English `Avatar(s)`, and surrounding separators. Examples: `１６アバター対応`, `34アバター対応`, `20 Avatars`, `16Avatar対応`, and `8アバター+α対応`.
  - Extend promotional cleanup to percentage/prefix forms such as `50%SALE`, `SALE２０％OFF`, the observed typo `SALE５０％OF`, `SUMMER SALE`, and `サマーセール中`, while preserving the actual title after the promotion.
  - Treat the following bracket contents as auxiliary type/compatibility labels: `3Dモデル`, `VRChat向け衣装モデル`, `VRC向けしっぽアクセサリー`, `VRC想定`, `lilToon対応`, `アイテクスチャ`, and `アクセサリー`.
  - Support parenthesized auxiliary forms only when the whole content matches an approved pattern, for example `(17 Avatar)`, `（13アバター対応）`, `（Modular Avatar対応）`, and `(VRC 3Dアイテム)`.
- Medium-confidence candidates that should be implemented only as exact allow-list entries, not broad keyword rules: `無料版あり`, `無料有/＋Free sample`, `全118種`, `全6種`, `50種`, `23types`, and `10Color＋10`. These are supplementary information, but counts can sometimes distinguish a product variation.
- Do not remove arbitrary bracketed text. Current product names use brackets for genuine names/series, including `【♡CatDoll♡】`, `【⟡Simple Elegant Dress⟡】`, `【🎀MilkyRibbonMaid🎀】`, `【O₂ Series】`, and `【Sweet Drop Braids Hair】`.
- Also avoid broad removal of words such as `Hair`, `Texture`, `Material`, `FullPack`, `Bundle`, `Animation`, `ギミック`, or `専用`; these frequently form part of the actual product name. Avatar-specific bracket removal should continue to rely on registered avatar identifiers.
- Implementation note: run cleanup as bounded tokens/segments and normalize whitespace/separators afterward. Do not globally delete substrings such as `SALE` or `対応`, because that can leave fragments (`20%OFF`) or damage genuine names.

### 2026-08-15 Product-title cleanup expansion and file-row hover

- Implemented the high-confidence title cleanup findings from the 258-item BLM audit. Auxiliary matching now supports `〈〉`/`《》`, approved parenthesized labels, unbracketed avatar-count compatibility text, and percentage/season variants of sale decorations.
- Added exact allow-list cleanup for the reviewed supplementary labels (`無料版あり`, `無料有/＋Free sample`, `全118種`, `全6種`, `50種`, `23types`, and `10Color＋10`). Arbitrary bracket text remains untouched so product/series names are preserved.
- Expanded exact type-label cleanup for `3Dモデル`, VRChat/VRC clothing and accessory labels, `VRC想定`, `lilToon対応`, `アイテクスチャ`, and `アクセサリー`.
- Download-file rows now transition from white to a subtle gray on hover and a slightly darker gray while pressed. The category-colored left accent remains unchanged.

### 2026-08-15 Smart title shortening setting and file-row separators

- The automatic title cleanup feature is now named `商品名スマート短縮機能` in the UI and project documentation.
- Added a persistent application-wide toggle to Settings. It defaults to enabled when no saved value exists; disabling it immediately restores the original BLM product names without modifying source data.
- Toggle changes immediately refresh item cards, detail titles, filtering, and avatar/shop option display names. The value is stored in the local `application_settings` table.
- Restored a one-pixel separator below every expanded download-file row while retaining the hover and pressed background transitions.

### 2026-08-15 Unity package badge layout

- Moved Unity-package badges into a dedicated row above the file name, increasing the row height only when at least one badge is visible.
- Badge order is fixed as `マテリアル` followed by `同一名あり`; both appear side by side when applicable. The file name keeps its existing two-line limit below them.

### 2026-08-15 Compatible-avatar display and shared-body inference

- Compatible-avatar links in the detail panel render the slash as a separate element with explicit left and right margins, guaranteeing visible ` / ` spacing between separately clickable avatar names. The separator is collapsed before the first name.
- Matches inferred only through a shared-body relationship (`ThroughBaseBody`) are no longer listed in the detail panel and no longer appear in its evidence tooltip. Direct automatic matches and manual additions remain visible.
- Shared-body-only matches remain part of the effective compatibility set used by avatar filtering and compatibility counts, so selecting that related avatar still shows the item as compatible.

### 2026-08-15 Downloaded BOOTH product variations

- Verified against BLM data that BOOTH item `6744059` has one `booth_item_variations` row with a non-null `order_id`: `【Milfy】 ミルフィ` (order `61879556`). A non-null `order_id` is therefore used as the purchased/downloaded variation criterion.
- `BoothLibraryReader` now reads ordered purchased variation names separately from the existing all-variation text used by compatibility detection.
- Added a `DL商品` action immediately left of `商品ページ` in the detail panel. It opens a modal listing the purchased variation names recorded by BLM; when none can be identified, the modal explains that no purchased variation was found.

### 2026-08-15 Purchased pack-type detection

- Audited 177 BLM items with purchased variation rows (`order_id IS NOT NULL`); six products contained multiple purchased rows. Avatar-specific examples include `【Milfy】 ミルフィ`, `愛莉 - Airi`, and `Milfy×Eku`. Full-pack examples commonly use `FullPack`, `Full Set`, `ALL`, `Complete set`, `フルパック`, or `フルセット`.
- Added pack-type detection only for categories that support compatible avatars: clothing, hair, accessories, textures, materials, gimmicks, and animations. Avatars, tools, shaders, worlds, and other categories never show a pack badge.
- Classification precedence is: an explicit full-pack expression in any purchased variation means `フルパック`; otherwise a registered avatar name/alias in any purchased variation means `単体購入`; otherwise it is treated as `フルパック`, matching the requested fallback rule.
- Numeric-only identifiers, version tokens, platform terms, and generic `3Dモデル` identifiers are excluded from name matching to reduce false avatar-specific classifications.
- Item cards show the resulting `フルパック` or `単体購入` chip at the thumbnail's upper right. Items without BLM purchased-variation data show neither chip. The existing update badge is stacked below it when both are visible.

### 2026-08-15 Missing downloaded-variation investigation and pack-chip colors

- Re-audited all registered BLM items after users reported missing `DL商品` values. Of 281 registered items, 177 have at least one row containing both a non-null `order_id` and a non-empty `variation_name`; 104 cannot produce the current downloaded-product list.
- Missing-data breakdown: 91 items have a purchased variation row and valid `order_id`, but BLM stores `variation_name` as null; 12 have variation rows but every `order_id` is null; one has no `booth_item_variations` row at all. There are currently no registered user items lacking a BOOTH item ID in this dataset.
- The 12 null-order cases include free products. Some have one variation, while others expose many names (for example 9 or 13) with no order marker, so the database does not identify which one was downloaded. Treating every null-order variation as purchased would create false results for multi-variation products.
- For the 91 null-name cases, the exact purchased variation cannot be recovered from the current BLM database fields. The BOOTH item name could be shown as a fallback, but it would not prove which variation was purchased; this fallback is intentionally not implemented pending a separate product decision.
- Pack chips now use the muted BOOTH red (`#D0575C`). `フルパック` uses a red background with white text; `単体購入` uses a white background with a red border and red text.

### 2026-08-15 Missing-variation and free-download badges

- BLM variation-state flags are now read separately from purchased variation names: whether any variation row exists and whether any row has a non-null order ID.
- Compatible-asset categories with no variation row, or with a paid order row whose `variation_name` is null/empty, are classified as `フルパック` instead of receiving no badge.
- When variation rows exist but none has an order ID, the registered item is classified as `無料/ギフト`. This covers BLM's indistinguishable free and gift-download representation and takes precedence over full-pack/avatar-name detection.
- `無料/ギフト` uses the same white background, BOOTH-red border, and BOOTH-red text treatment as `単体購入`; `フルパック` remains the filled BOOTH-red variant.

### 2026-08-15 Gift-download investigation and badge placement

- Inspected every BLM table definition and notification payload associated with null-order variations. BLM stores no gift/present/acquisition-type field or separate gift/order table; both free downloads and gifted downloads appear as `booth_item_variations.order_id IS NULL`.
- Notification payloads contain variation IDs, downloadable filenames, update availability, or download failures, but no free-versus-gift marker. Therefore the two acquisition paths cannot be reliably distinguished using the current local BLM database alone.
- Null-order acquisitions are displayed as `無料/ギフト`, avoiding a distinction that BLM does not provide.
- On item cards, the compatible-avatar count chip appears immediately below the `フルパック`/`単体購入`/`無料/ギフト` chip. The update badge follows below it.
- The same acquisition/pack chip is now displayed immediately before the `対応アバター` subheading in the detail panel, using the same fill, border, and foreground colors as the card chip.

### 2026-08-15 Avatar-specific purchase compatibility restriction

- Renamed acquisition labels from `単体` to `単体購入` and from `無料DL` to `無料/ギフト`.
- For `単体購入`, automatic compatible-avatar detection now uses only the purchased BLM variation names (`DL商品`) as evidence. Description text, BOOTH tags, and unpurchased variation names are intentionally excluded so avatars from other available packs are not automatically selected.
- Manual additions/exclusions, all-avatar overrides, and shared-body propagation still apply after this restricted automatic result. Full-pack and free/gift items continue using the normal description/tag/all-variation detection.
- The detail acquisition badge, `対応アバター` heading, and evidence-list icon now share a 26px-high, vertically centered header row.

### 2026-08-15 Acquisition-type filtering

- Added a fixed-width acquisition filter to the main search row with `すべての取得形態`, `フルパック`, `単体購入`, and `無料/ギフト` options.
- The filter compares against the computed purchased-pack type, composes with text/category/avatar/shop filters, resets paging to page one, and is cleared by the existing `フィルタをクリア` action.

### 2026-08-15 Card acquisition metadata row

- Moved the acquisition-type and compatible-avatar-count chips off the thumbnail overlay into a dedicated single horizontal row immediately below the square thumbnail.
- The update badge remains overlaid at the thumbnail's upper right, while the category badge remains at the upper left.
- Increased computed card height by 29px for the metadata row without reducing the existing two-line product-name and one-line shop-name area.

### 2026-08-15 Card acquisition metadata row rollback

- Reverted the immediately preceding card metadata-row change at user request. Acquisition type and compatible-avatar count are again stacked at the thumbnail's upper right, above the update badge.
- Removed the dedicated row below the thumbnail and restored the compact `CardWidth + 76` card height.

### 2026-08-13

- BLM読み取り専用連携と商品一覧を実装
- 独自分類、手動編集、Assets直下フラグを実装
- カード詳細ダイアログ、BOOTHリンク、分類色バッジを実装
- BOOTHカテゴリ優先の分類規則へ変更し、ワールド・その他を追加
- 分類タブ、カード幅縮小、アバターのAssets直下固定を実装
- 分類タブを分類名のみの表示へ変更
- カードを正方形サムネイル対応の縦長表示へ変更し、分類タブの外観をカスタマイズ
- 大量アイテム向けのページングと表示件数切替を追加
- 詳細表示を右スライドパネルへ変更し、編集専用ダイアログと分離
- 選択タブの色を分類ラベル色へ統一し、検索欄をヘッダーへ移動
- 詳細パネルをレイアウト内の右列に配置し、開閉時にメイン領域をリフロー
- 詳細パネル表示不具合を修正し、右列幅を明示確保してからスライドする方式へ変更
- 5項目の昇降順ソートを追加
- 対応アバターと共通素体のデータモデル方針を確定
- サフィックス付き重複フォルダーの完全一致検出と、ごみ箱への整理機能を追加
- デフォルトの並び順をBLM登録日の新しい順へ変更
- アバター識別タグ、共通素体グループ、対応アバター自動検出の初期実装を追加
- 対応アバター候補の手動確定・除外・追加を実装
- メイン呼称と複数サブ識別タグを根幹とする識別方式へ変更。共通素体は登録済みアバターの複数選択で設定
- 識別タグから完全なBOOTH URLを廃止し、既存の自動生成URLを移行時に除去
- アバター設定を各アバター商品の編集画面へ統合し、詳細タグをチップ表示へ変更
- 商品単位の再読み込み導線へ情報更新・自動判定・重複確認を統合
- 左ナビゲーション、可変幅カード、詳細パネル拡幅、タグチップの視認性改善を実装
- GridViewItemの既定最小幅・余白を除去してカード列の右側余白を修正し、長いタグを縦型チップで全文表示
- 空の設定ページと左メニュー切替を追加。カード下部を72pxへ圧縮し、タグを丸い横並びチップへ変更
- RowDefinitionへのサイズバインドを廃止し、サムネイル要素をカード幅と直接連動して正方形を保証
- 左メニューを専用テンプレート化し、ホバー時の標準黒色上書きを無効化
### 2026-08-16 Git repository setup

- Connected the workspace to `https://github.com/usa-mishin/VrcKaihenManager.git` as the `origin` remote.
- The GitHub repository was empty when connected, so there is no existing remote history to merge.
- Added a root `.gitignore` for Visual Studio/.NET outputs, user-specific IDE files, local diagnostics, Codex investigation directories, and the temporary `SchemaInspector` utility.
- Use the Visual Studio-bundled Git executable until a standalone Git installation is added to `PATH`.
