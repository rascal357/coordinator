# BatchProcessingBackgroundService 処理ロジック

## 概要

BatchProcessingBackgroundServiceは、バックグラウンドで定期的に実行され、DC_Batchテーブルの`IsProcessed`フラグを自動的に更新するサービスです。

実際に装置で処理中または処理待ちのLotがDC_Actlに存在する場合、そのLotに対応するDC_Batchレコードを`IsProcessed=true`に設定します。これにより、WorkProgress画面の「予約1～3」表示から処理中・処理待ちのバッチが自動的に除外されます。

## サービスの起動と設定

### 設定項目

- **更新間隔**: `appsettings.json`の`BatchProcessing:UpdateIntervalSeconds`で設定（デフォルト: 30秒）
- **初回実行遅延**: アプリケーション起動後5秒待機してから最初の更新を実行

### サービスライフサイクル

1. アプリケーション起動時に自動的に開始
2. 5秒待機（アプリケーションの完全起動を待つ）
3. 30秒間隔で定期的に`UpdateBatchProcessingStatus`を実行
4. アプリケーション停止時に自動的に終了

## CancellationToken（stoppingToken）について

### 概要

`stoppingToken`は**キャンセルトークン（CancellationToken）**で、バックグラウンドサービスを安全に停止するための仕組みです。

**役割**: アプリケーションがシャットダウンする際に、実行中の非同期処理を中断するためのシグナル

### BatchProcessingBackgroundServiceでの使用例

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)  // ← 停止要求をチェック
    {
        await UpdateBatchProcessingStatus(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);  // ← 停止可能な待機
    }
}
```

### 具体的な動作

1. **通常時**: `stoppingToken.IsCancellationRequested` = `false`
   - ループが継続
   - 30秒ごとに処理を実行

2. **アプリケーション停止時（Ctrl+C など）**:
   - `stoppingToken.IsCancellationRequested` = `true`に変化
   - ループが終了
   - 処理が中断される

### なぜ必要か

**停止要求を無視すると問題が発生**:

```csharp
// ❌ 悪い例（停止トークンなし）
while (true)
{
    await UpdateBatchProcessingStatus(CancellationToken.None);
    await Task.Delay(TimeSpan.FromSeconds(30));
}
// → アプリケーション停止時に無限ループが終了しない
```

```csharp
// ✅ 良い例（停止トークンあり）
while (!stoppingToken.IsCancellationRequested)
{
    await UpdateBatchProcessingStatus(stoppingToken);
    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
}
// → アプリケーション停止時にすぐにループを抜ける
```

### データベースクエリでの使用

```csharp
var equipments = await context.DcEqps.ToListAsync(stoppingToken);
```

- データベースクエリ実行中に停止要求が来たら、クエリを中断
- 30秒かかる重いクエリでも、停止要求が来たらすぐに中断できる

### ループ内でのチェック

```csharp
foreach (var eqp in equipments)
{
    if (stoppingToken.IsCancellationRequested)
        break;  // ← ループ途中でも停止

    await ProcessEquipmentBatches(context, eqp.Name, stoppingToken);
}
```

- 装置が100個ある場合、途中で停止要求が来ても即座に中断
- 全装置の処理完了を待たずに停止可能

### まとめ

| 項目 | 説明 |
|------|------|
| **名前** | stoppingToken（CancellationToken型） |
| **目的** | バックグラウンドサービスの安全な停止 |
| **チェック方法** | `stoppingToken.IsCancellationRequested` |
| **渡す場所** | 長時間実行される非同期メソッド（ToListAsync、Task.Delayなど） |
| **効果** | アプリケーション停止時に処理を即座に中断 |

**つまり**: アプリケーションが「停止してください」と伝えるための通知システムです。

## 処理フロー

### 1. ExecuteAsync メソッド（メインループ）

```
起動
  ↓
5秒待機
  ↓
ループ開始
  ↓
UpdateBatchProcessingStatus 実行
  ↓
30秒待機
  ↓
ループ継続（停止要求まで）
```

**エラーハンドリング**:
- UpdateBatchProcessingStatus内で例外が発生してもサービスは停止せず、エラーログを出力して次の実行を継続

### 2. UpdateBatchProcessingStatus メソッド（全装置の更新）

```
全装置を取得（DC_Eqps）
  ↓
装置ごとにループ
  ↓
ProcessEquipmentBatches 実行
  ↓
更新件数を集計
  ↓
ログ出力（更新あり: INFO、更新なし: DEBUG）
```

**処理内容**:
1. `DC_Eqps`テーブルから全装置を取得
2. 各装置に対して`ProcessEquipmentBatches`を呼び出し
3. 全装置の更新件数を集計してログに出力

**エラーハンドリング**:
- 特定の装置でエラーが発生しても他の装置の処理は継続
- エラーはWARNINGレベルでログ出力

### 3. ProcessEquipmentBatches メソッド（装置ごとの処理）

```
装置のDC_Actlレコードを取得
  ↓
TrackInTime昇順でソート
  ↓
GroupByTimeWindow でグルーピング（±5分）
  ↓
MarkBatchesAsProcessed でバッチ更新
  ↓
更新件数を返す
```

**処理内容**:
1. 指定装置の`DC_Actl`レコードを取得（`EqpId`でフィルタ）
2. `TrackInTime`昇順でソート
3. `GroupByTimeWindow`メソッドで±5分の時間窓でグルーピング
4. `MarkBatchesAsProcessed`メソッドでバッチを`IsProcessed=true`に更新

**条件**:
- `DC_Actl`にレコードが存在しない場合は0を返して終了

### 4. GroupByTimeWindow メソッド（時間窓グルーピング）

**アルゴリズム**:

```
入力: DcActlのリスト、時間窓（5分）
  ↓
最初のレコードで新しいグループを開始
currentTime = 最初のTrackInTime
  ↓
2番目以降のレコードをループ
  ↓
現在のレコードとcurrentTimeの差を計算
  ↓
差が5分以内?
  YES → 現在のグループに追加
  NO  → 現在のグループを確定
        新しいグループを開始
        currentTime = 現在のTrackInTime
  ↓
最後のグループを追加
  ↓
出力: グループのリスト
```

**例**:

| レコード | TrackInTime | 前のレコードとの差 | グループ |
|---------|-------------|------------------|----------|
| 1 | 10:00 | - | Group1 |
| 2 | 10:02 | 2分 | Group1 |
| 3 | 10:04 | 2分 | Group1 |
| 4 | 10:15 | 11分 | Group2 |
| 5 | 10:17 | 2分 | Group2 |

**注意点**:
- WorkProgress.cshtml.csの`GroupByTimeWindow`メソッドと同じロジック
- 時間差は絶対値（`Math.Abs`）で計算
- 各グループ内のレコードは`TrackInTime`でソート済み

### 5. MarkBatchesAsProcessed メソッド（バッチ更新）

**処理フロー**:

```
timeGroupsから全LotId/EqpIdを抽出
  ↓
重複を除外
  ↓
各LotIdに対してループ
  ↓
DC_BatchMembersをLotIdで検索
  ↓
各BatchMemberに対して
  ↓
DC_BatchesをBatchId/CarrierId/EqpIdで検索
  ↓
IsProcessed=falseのレコードのみ取得
  ↓
Step昇順でソート
  ↓
レコード数によって処理分岐
  - 1件: そのレコードをIsProcessed=true
  - 複数件: Step最小のレコードをIsProcessed=true
  ↓
更新件数をカウント
  ↓
SaveChangesAsync
  ↓
更新件数を返す
```

**詳細ロジック**:

1. **timeGroupsからLotId/EqpIdを抽出**
   ```csharp
   var actlData = timeGroups
       .SelectMany(g => g.Select(a => new { a.LotId, a.EqpId }))
       .Distinct()
       .ToList();
   ```
   - 全てのグループから全てのDcActlレコードを平坦化
   - LotIdとEqpIdのペアを抽出
   - 重複を除外

2. **DC_BatchMembersを検索**
   ```csharp
   var matchingMembers = await context.DcBatchMembers
       .Where(m => m.LotId == actl.LotId)
       .ToListAsync(stoppingToken);
   ```
   - LotIdでDC_BatchMembersを検索
   - 1つのLotIdに対して複数のBatchMemberが存在する可能性あり

3. **DC_Batchesを検索**
   ```csharp
   var matchingBatches = await context.DcBatches
       .Where(b => b.BatchId == member.BatchId &&
                  b.CarrierId == member.CarrierId &&
                  b.EqpId == actl.EqpId &&
                  !b.IsProcessed)
       .OrderBy(b => b.Step)
       .ToListAsync(stoppingToken);
   ```
   - BatchId、CarrierId、EqpIdで検索
   - **IsProcessed=falseのレコードのみ**取得（重複更新を防止）
   - Step昇順でソート

4. **IsProcessedフラグ更新**
   - **1件の場合**: そのレコードを`IsProcessed=true`に設定
   - **複数件の場合**: Step最小のレコード（先頭）を`IsProcessed=true`に設定
   - 0件の場合: 何もしない（既にIsProcessed=trueの場合）

5. **データベース保存**
   - 更新があった場合のみ`SaveChangesAsync`を実行

## データフロー例

### 前提条件

**DC_Actl（実際の処理データ）**:
| EqpId | LotId | TrackInTime |
|-------|-------|-------------|
| DVETC25 | SY79874.1 | 10:00 |
| DVETC25 | SY79872.1 | 10:02 |

**DC_BatchMembers**:
| BatchId | CarrierId | LotId |
|---------|-----------|-------|
| BATCH001 | C22667 | SY79874.1 |

**DC_Batches（更新前）**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | false |
| BATCH001 | 2 | C22667 | DVETC39 | false |

### 処理実行

1. **装置DVETC25の処理**
   - DC_ActlからEqpId='DVETC25'のレコードを取得
   - 2件のレコードがTrackInTime 2分差で存在 → 同じグループ

2. **LotId='SY79874.1'の処理**
   - DC_BatchMembersでLotId='SY79874.1'を検索 → BATCH001/C22667が見つかる
   - DC_BatchesでBatchId='BATCH001', CarrierId='C22667', EqpId='DVETC25'を検索
   - Step=1のレコードが見つかる（IsProcessed=false）
   - Step=1のレコードを`IsProcessed=true`に更新

3. **LotId='SY79872.1'の処理**
   - DC_BatchMembersでLotId='SY79872.1'を検索 → 見つからない
   - 何もしない

### 更新後

**DC_Batches（更新後）**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | **true** ← 更新 |
| BATCH001 | 2 | C22667 | DVETC39 | false |

### WorkProgress画面への影響

- **更新前**: BATCH001のStep1とStep2が両方`IsProcessed=false`なので「予約1」に表示される可能性あり
- **更新後**: BATCH001のStep1が`IsProcessed=true`なので「予約」表示から除外される
- Step2はまだ`IsProcessed=false`なので、DVETC39装置の「予約」に表示される

## WorkProgressとの連携

### WorkProgress.cshtml.cs LoadProgressDataメソッドとの関係

**WorkProgressの予約表示ロジック**:
```csharp
var allBatches = await _context.DcBatches
    .Where(b => equipmentIds.Contains(b.EqpId) && !b.IsProcessed)
    .ToListAsync();
```

- `IsProcessed=false`のバッチのみ取得
- BatchProcessingBackgroundServiceが`IsProcessed=true`に更新すると、自動的に予約表示から除外される

### 処理タイミング

1. **CreateBatch画面**: バッチ作成時に`IsProcessed=false`で登録
2. **BatchProcessingBackgroundService**: 30秒ごとにDC_Actlをチェックして`IsProcessed=true`に更新
3. **WorkProgress画面**: リロード時に`IsProcessed=false`のバッチのみ表示

## 複数Step処理の例

### シナリオ

1つのBatchIdに複数のStepがある場合:

**DC_Batches**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | false |
| BATCH001 | 2 | C22667 | DVETC26 | false |
| BATCH001 | 3 | C22667 | DVETC27 | false |

**DC_Actl**:
| EqpId | LotId | TrackInTime |
|-------|-------|-------------|
| DVETC25 | SY79874.1 | 10:00 |

### 処理結果

1. DVETC25でLotId='SY79874.1'が処理中
2. BATCH001のStep1（EqpId=DVETC25）のみ`IsProcessed=true`に更新
3. Step2とStep3は`IsProcessed=false`のまま（まだ処理されていない）

**更新後**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | **true** |
| BATCH001 | 2 | C22667 | DVETC26 | false |
| BATCH001 | 3 | C22667 | DVETC27 | false |

## 同じBatchId/CarrierId/EqpIdで複数レコードがある場合

### シナリオ（通常は発生しないが、データ不整合の場合）

**DC_Batches**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | false |
| BATCH001 | 2 | C22667 | DVETC25 | false |

両方とも同じ装置（DVETC25）で同じCarrier（C22667）の場合:

### 処理結果

1. `OrderBy(b => b.Step)`でソート → Step1, Step2の順
2. **Step最小（Step=1）のレコードのみ**`IsProcessed=true`に更新
3. Step=2は`IsProcessed=false`のまま

**更新後**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | **true** |
| BATCH001 | 2 | C22667 | DVETC25 | false |

## ログ出力

### INFOレベル

- **起動時**: `BatchProcessingBackgroundService started. Update interval: 30 seconds`
- **更新あり**: `Updated {Count} batch records to IsProcessed=true`
- **停止時**: `BatchProcessingBackgroundService stopped`

### DEBUGレベル

- **更新開始**: `Starting batch processing status update`
- **更新なし**: `No batch records needed updating`

### WARNINGレベル

- **装置処理エラー**: `Error processing batches for equipment {EquipmentName}`

### ERRORレベル

- **全体処理エラー**: `Error occurred while updating batch processing status`

## パフォーマンス考慮事項

### データベースクエリ数

装置数をN、Lot数をMとした場合:

1. **全装置取得**: 1クエリ
2. **装置ごとのDC_Actl取得**: Nクエリ
3. **LotごとのDC_BatchMembers取得**: M×Nクエリ（最悪ケース）
4. **BatchMemberごとのDC_Batches取得**: M×Nクエリ（最悪ケース）

**最悪ケース**: 1 + N + 2MN クエリ

### 改善の余地（将来の最適化）

現在は装置ごとに個別クエリを実行していますが、以下の最適化が可能:

1. **全装置のDC_Actlを一括取得**
   ```csharp
   var allActls = await context.DcActls
       .Where(a => equipmentIds.Contains(a.EqpId))
       .ToListAsync();
   ```

2. **全LotIdのDC_BatchMembersを一括取得**
   ```csharp
   var allMembers = await context.DcBatchMembers
       .Where(m => lotIds.Contains(m.LotId))
       .ToListAsync();
   ```

3. **全BatchIdのDC_Batchesを一括取得**
   ```csharp
   var allBatches = await context.DcBatches
       .Where(b => batchIds.Contains(b.BatchId) && !b.IsProcessed)
       .ToListAsync();
   ```

これにより、クエリ数を **3クエリ** に削減可能。

## まとめ

BatchProcessingBackgroundServiceの主要な役割:

1. **自動化**: 30秒ごとに自動的にバッチ処理状態を更新
2. **分離**: WorkProgress画面の表示ロジックと処理ロジックを分離
3. **正確性**: 実際の処理状況（DC_Actl）に基づいて予約状態（DC_Batch.IsProcessed）を更新
4. **段階的処理**: 複数Stepがある場合、現在のStepのみをIsProcessed=trueに更新
5. **冗長性**: エラーが発生しても次の実行で再試行される

これにより、WorkProgress画面は常に最新の予約状態を表示でき、処理中・処理待ちのバッチが「予約1～3」に誤って表示されることを防ぎます。
