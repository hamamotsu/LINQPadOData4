# CLAUDE.md

このファイルは、Claude Code (claude.ai/code) がこのリポジトリで作業する際のガイダンスを提供します。

## プロジェクト概要

LINQPadOData4 は OData v4 サービス用の LINQPad 動的ドライバーです。OData エンドポイントに接続して `$metadata` を取得し、T4 テンプレートで厳密に型付けされた C# クラスを生成し、Roslyn でコンパイルして、LINQPad のエクスプローラーペインにスキーマを表示します。

元リポジトリ (meancrazy/LINQPadOData4) のフォーク。LINQPad 9 / .NET 10 / OData Client 8.x に対応。

## ビルド

```bash
dotnet build OData4.LINQPadDriver.sln
```

ビルド後のステップで、出力が `%LocalAppData%\LINQPad\Drivers\DataContext\NetCore\OData4.LINQPadDriver.Net10\` にコピーされ、LINQPad でのローカルテストが可能になります。

自動テストはありません。テストは LINQPad を通じて手動で行います。

## アーキテクチャ

**DynamicDriver.cs** — エントリポイント。`DynamicDataContextDriver`（LINQPad のドライバー API）を継承。接続ダイアログの表示、T4 によるコード生成、Roslyn によるコンパイル（`BuildAssembly`）、実行時の `DataServiceContext` 初期化（`IHttpClientFactory` + `SendingRequest2`）、リクエストのログ記録といったライフサイクル全体を統括します。

**ConnectionProperties.cs** — LINQPad の XML ストレージ（`IConnectionInfo.DriverData`）を基盤としたプロパティバッグ。URI、認証設定、プロキシ、カスタムヘッダー、ログフラグを保持します。`GetCredentials()`、`GetWebProxy()`、`GetCustomHeaders()`、`GetClientCertificate()` を提供します。

**Extensions.cs** — 静的ヘルパー。`GetModel()` は `HttpClient` で `$metadata` の CSDL を取得・解析して `IEdmModel` に変換します。`GetSchema()` は EDM モデルを LINQPad の `ExplorerItem` ツリー（エンティティセット、構造/ナビゲーションプロパティ、アクション）に変換します。

**Templates/ODataT4CodeGenerator.tt** — プリプロセス済み T4 テンプレート（Microsoft の OData v4 Client Code Generator ベース）。CSDL メタデータから `DataServiceContext` サブクラスとエンティティ型を生成します。`.cs` と `.ttinclude` ファイルは自動生成されるが、.NET 10 では T4 再生成環境が動作しないため `.cs` を直接編集しています。

**ConnectionDialog.xaml / CustomHeadersDialog.xaml** — 接続設定およびカスタム HTTP ヘッダー管理用の WPF ダイアログ。

## 主要な依存関係

- `LINQPad.Reference` 1.3.1 — LINQPad ドライバー拡張 API
- `Microsoft.OData.Client` 8.4.3 — OData プロトコルクライアントおよび `DataServiceContext`
- `Microsoft.CodeAnalysis.CSharp` 4.14.0 — 実行時アセンブリ生成用の Roslyn コンパイラ
- `Microsoft.Extensions.Http` 10.0.3 — `IHttpClientFactory` 用
- `Microsoft.VisualStudio.TextTemplating` — T4 テンプレートランタイム

## コードスタイル

- `.editorconfig` に準拠: タブでインデント、Allman 形式の波括弧、`var` を優先
- Windows 専用（WPF UI）
