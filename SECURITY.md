# Security Policy

## Supported releases

原則として最新のGitHub Releaseだけをサポートします。配布ファイルのバージョン、SHA-256、署名者、対応コミットをRelease本文で確認してください。

## Reporting a vulnerability

脆弱性の疑いがある場合は、再現手順、対象バージョン、影響範囲を添えてリポジトリ管理者へ報告してください。ユーザーの商品ファイル、購入情報、Unityプロジェクトを公開Issueへ添付しないでください。機密性が必要な場合は、GitHub Security AdvisoriesのPrivate vulnerability reportingを使用してください。

## Security boundaries

- BLM DBは読み取り専用で開きます。
- UnityPackageはリンク、特殊項目、危険なパス、過大な展開を拒否します。
- 外部サムネイルはHTTPSのBOOTH公式ドメインだけを自動取得します。
- 管理者権限、自動起動、常駐サービス、広告、テレメトリーを使用しません。

コード署名は配布工程の責任です。正式配布ではアプリ本体とインストーラーを信頼された証明書で署名し、タイムスタンプを付けてください。
