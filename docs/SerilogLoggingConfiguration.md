# Serilog ロギング設定ガイド

## 概要

Coordinatorアプリケーションでは、構造化ロギングライブラリ「Serilog」を使用してログを管理しています。
Serilogは、日付ごとのファイル分割、ログレベル管理、構造化されたログ出力などの高度な機能を提供します。

## ログ出力先

ログは以下の2箇所に同時出力されます：

### 1. コンソール出力
- アプリケーション実行中のターミナル/コマンドプロンプトに表示
- リアルタイムでログを確認可能
- デバッグや開発時に便利

### 2. ファイル出力
- **出力先**: `Logs/` フォルダ
- **ファイル名形式**: `log-yyyyMMdd.txt`
  - 例: `log-20250116.txt` (2025年1月16日のログ)
- **日次ローリング**: 日付が変わると自動的に新しいファイルを作成
- **保持期間**: 過去30日分のログファイルを保持（それ以前は自動削除）

## ログフォーマット

ファイルに出力されるログは以下の形式です：

```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}
```

**出力例:**
```
2025-01-16 12:34:56.789 [INF] Starting Coordinator application
2025-01-16 12:35:00.456 [INF] === Batch Created Successfully ===
2025-01-16 12:35:00.457 [INF] BatchId: 20250116123456789
2025-01-16 12:35:30.790 [ERR] Error occurred while updating batch processing status
```

**フォーマット要素:**
- **Timestamp**: 日時（ミリ秒まで表示）
- **Level**: ログレベル（3文字略語）
  - `INF`: Information
  - `WRN`: Warning
  - `ERR`: Error
  - `FTL`: Fatal
  - `DBG`: Debug
- **Message**: ログメッセージ本文
- **Exception**: 例外が発生した場合のスタックトレース

## ログレベル設定

### appsettings.json での設定

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "System": "Warning"
    }
  }
}
```

### ログレベルの説明

| レベル | 説明 | 使用例 |
|--------|------|--------|
| **Debug** | デバッグ情報 | 変数の値、処理フロー確認 |
| **Information** | 一般的な情報 | バッチ作成、処理完了 |
| **Warning** | 警告 | 非推奨機能の使用、遅延 |
| **Error** | エラー | 例外発生、処理失敗 |
| **Fatal** | 致命的エラー | アプリケーション停止 |

### レベルのオーバーライド

- **Default**: `Information` - アプリケーション全体のデフォルトレベル
- **Override**: 特定のネームスペースのレベルを上書き
  - `Microsoft.*`: ASP.NET Coreのフレームワークログは Warning 以上のみ
  - `System.*`: システムライブラリのログは Warning 以上のみ

これにより、重要なアプリケーションログは詳細に記録しつつ、フレームワークの詳細ログは抑制できます。

## Program.cs での設定

### Serilog初期化

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

**設定パラメータ:**
- `path`: ログファイルのパス（`log-.txt` は `log-yyyyMMdd.txt` に展開される）
- `rollingInterval`: ローリング間隔（`RollingInterval.Day` = 日次）
- `retainedFileCountLimit`: 保持するファイル数（30日分）
- `outputTemplate`: ログのフォーマットテンプレート

### アプリケーションへの統合

```csharp
builder.Host.UseSerilog();
```

この1行でSerilogをASP.NET Coreのロギングシステムに統合します。

### クリーンアップ

```csharp
finally
{
    Log.Information("Shutting down Coordinator application");
    Log.CloseAndFlush();
}
```

アプリケーション終了時にバッファをフラッシュし、すべてのログを確実に書き込みます。

## 実装されているログ出力

### 1. CreateBatch画面（Pages/CreateBatch.cshtml.cs）

**バッチ作成時のログ:**
```csharp
_logger.LogInformation("=== Batch Created Successfully ===");
_logger.LogInformation("BatchId: {BatchId}", batchId);
_logger.LogInformation("Created At: {CreatedAt}", createdAt);
_logger.LogInformation("--- DC_Batch Records ({Count}) ---", count);
_logger.LogInformation("  [Step {Step}] Carrier: {CarrierId}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}",
    step, carrier, eqpId, ppid, nextEqpId);
```

**ログ出力例:**
```
2025-01-16 12:35:00.456 [INF] === Batch Created Successfully ===
2025-01-16 12:35:00.457 [INF] BatchId: 20250116123456789
2025-01-16 12:35:00.458 [INF] Created At: 2025/01/16 12:34:56.789
2025-01-16 12:35:00.459 [INF]
2025-01-16 12:35:00.460 [INF] --- DC_Batch Records (8) ---
2025-01-16 12:35:00.461 [INF]   [Step 1] Carrier: C22667, EqpId: DVETC25, PPID: PPID1, NextEqpId: DVETC26
```

### 2. BatchProcessingBackgroundService（Services/BatchProcessingBackgroundService.cs）

**バッチ処理状態更新時のログ:**
```csharp
_logger.LogInformation("=== Batches Marked as Processed ===");
_logger.LogInformation("Equipment: {EqpId}, Updated Count: {Count}", eqpId, count);
_logger.LogInformation("--- BatchId: {BatchId} ---", batchId);
_logger.LogInformation("  [Step {Step}] Carrier: {CarrierId}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}, ProcessedAt: {ProcessedAt}",
    step, carrier, eqpId, ppid, nextEqpId, processedAt);
```

**ログ出力例:**
```
2025-01-16 12:35:30.789 [INF] === Batches Marked as Processed ===
2025-01-16 12:35:30.790 [INF] Equipment: DVETC38, Updated Count: 2
2025-01-16 12:35:30.791 [INF]
2025-01-16 12:35:30.792 [INF] --- BatchId: 20250116123456789 ---
2025-01-16 12:35:30.793 [INF]   [Step 1] Carrier: C22667, EqpId: DVETC38, PPID: PPID1, NextEqpId: DVETC39, ProcessedAt: 2025/01/16 12:34:56
```

**バッチ削除時のログ:**
```csharp
_logger.LogInformation("Deleted {Count} completed batches: {BatchIds}", count, batchIds);
```

**エラー時のログ:**
```csharp
_logger.LogError(ex, "Error occurred while updating batch processing status");
_logger.LogWarning(ex, "Error processing batches for equipment {EquipmentName}", eqpName);
```

## カスタマイズ方法

### ログレベルの変更

開発環境でより詳細なログが必要な場合、`appsettings.Development.json` を作成：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Information"
      }
    }
  }
}
```

### 保持期間の変更

ログファイルを90日間保持したい場合：

```csharp
.WriteTo.File(
    path: "Logs/log-.txt",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 90,  // 30から90に変更
    outputTemplate: "...")
```

### ファイルサイズ制限

ファイルサイズで分割したい場合：

```csharp
.WriteTo.File(
    path: "Logs/log-.txt",
    rollingInterval: RollingInterval.Day,
    fileSizeLimitBytes: 10_485_760,  // 10MB
    rollOnFileSizeLimit: true,
    retainedFileCountLimit: 30,
    outputTemplate: "...")
```

### JSON形式での出力

構造化ログをJSON形式で保存したい場合：

```csharp
using Serilog.Formatting.Compact;

.WriteTo.File(
    new CompactJsonFormatter(),
    path: "Logs/log-.json",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30)
```

## トラブルシューティング

### ログファイルが作成されない

**原因1**: Logsフォルダへの書き込み権限がない
- **解決策**: アプリケーション実行ユーザーに書き込み権限を付与

**原因2**: パスが間違っている
- **解決策**: Program.csの `path:` パラメータを確認

### ログが出力されない

**原因**: ログレベルが高すぎる
- **解決策**: appsettings.jsonの `MinimumLevel` を `Debug` に変更して確認

### 古いログファイルが削除されない

**原因**: `retainedFileCountLimit` が設定されていない
- **解決策**: Program.csで `retainedFileCountLimit: 30` を確認

### ログファイルが大きくなりすぎる

**解決策1**: `retainedFileCountLimit` を減らす（例: 7日分）
**解決策2**: ログレベルを上げる（`Information` → `Warning`）
**解決策3**: フレームワークログを抑制（Override設定を確認）

## ログの確認方法

### リアルタイムでログを確認

```bash
# アプリケーション実行
~/.dotnet/dotnet run

# 別のターミナルでログファイルを監視
tail -f Logs/log-$(date +%Y%m%d).txt
```

### 特定の文字列を検索

```bash
# "Error"を含む行を検索
grep "Error" Logs/log-20250116.txt

# "BatchId"を含む行とその前後3行を表示
grep -C 3 "BatchId" Logs/log-20250116.txt
```

### 日付範囲で検索

```bash
# 12:30～12:40の間のログを表示
awk '/2025-01-16 12:3[0-9]/' Logs/log-20250116.txt
```

## 参考リンク

- [Serilog 公式ドキュメント](https://serilog.net/)
- [Serilog.AspNetCore GitHub](https://github.com/serilog/serilog-aspnetcore)
- [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)

## まとめ

Serilogを使用することで、以下のメリットがあります：

- **構造化ログ**: 検索・分析が容易
- **日次ローリング**: 日付ごとに整理されたログファイル
- **自動クリーンアップ**: 古いログファイルの自動削除
- **柔軟な設定**: レベル、フォーマット、出力先を自由にカスタマイズ可能
- **高性能**: 非同期書き込みによる低オーバーヘッド

保守・運用時には、`Logs/` フォルダ内のログファイルを確認することで、アプリケーションの動作状況やエラーを詳細に追跡できます。
