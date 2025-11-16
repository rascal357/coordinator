# WorkProgress画面 予約1~3のNext（NextFurnace）更新ロジック

## 概要

WorkProgress画面の「予約1」「予約2」「予約3」に表示される各Lotの**Next（NextFurnace）**列は、そのLotが次に処理される装置を示します。

このNextは、DC_Batchesテーブルの**Step + 1**のレコードから取得されます。つまり、現在のStepの次のStepで使用される装置が表示されます。

## データソース

### テーブル構造

**DC_Batches**:
| カラム | 型 | 説明 |
|--------|-----|------|
| BatchId | TEXT | バッチID（同じバッチ内で共通） |
| Step | INTEGER | 処理ステップ番号（1, 2, 3, 4） |
| CarrierId | TEXT | キャリアID |
| EqpId | TEXT | 装置ID |
| PPID | TEXT | プロセスレシピID |
| IsProcessed | BOOLEAN | 処理済みフラグ |
| CreatedAt | DATETIME | 作成日時 |

**DC_BatchMembers**:
| カラム | 型 | 説明 |
|--------|-----|------|
| BatchId | TEXT | バッチID |
| CarrierId | TEXT | キャリアID |
| LotId | TEXT | ロットID |
| Qty | INTEGER | 数量 |
| Technology | TEXT | テクノロジー |

## 処理フロー

### 1. LoadProgressData メソッド（予約1~3の取得）

```
装置ごとにループ
  ↓
装置のDC_Batchesを取得（IsProcessed=false）
  ↓
BatchIdとCreatedAtでグルーピング
  ↓
CreatedAt昇順でソート
  ↓
最大3件を取得
  ↓
各バッチに対してCreateProcessItemsFromBatch呼び出し
  ↓
予約1、予約2、予約3に設定
```

**コード**:
```csharp
// Get reserved batches from in-memory dictionary
var equipmentBatches = batchesByEquipment.ContainsKey(eqp.Name)
    ? batchesByEquipment[eqp.Name]
    : new List<DcBatch>();

var reservedBatchIds = equipmentBatches
    .GroupBy(b => new { b.BatchId, b.CreatedAt })
    .OrderBy(g => g.Key.CreatedAt)
    .Take(3)
    .Select(g => new { g.Key.BatchId, Batch = g.First() })
    .ToList();

var reservedItems = new List<List<ProcessItem>>();
foreach (var batchGroup in reservedBatchIds)
{
    var items = await CreateProcessItemsFromBatch(batchGroup.Batch, eqp.Name);
    reservedItems.Add(items);
}
```

### 2. CreateProcessItemsFromBatch メソッド（Next取得）

```
入力: DcBatchレコード、装置ID
  ↓
装置IDとBatchIdでDC_Batchesを検索してCarrierIdを取得
  ↓
DC_BatchMembersをBatchIdとCarrierIdで検索
  ↓
各BatchMemberに対してループ
  ↓
DC_Batchesで次のStepを検索
  条件: BatchId = 同じ
        CarrierId = 同じ
        Step = 現在のStep + 1
  ↓
見つかった場合: NextFurnace = 次のStepのEqpId
見つからない場合: NextFurnace = "なし"
  ↓
ProcessItemリストを返す
```

**コード**:
```csharp
private async Task<List<ProcessItem>> CreateProcessItemsFromBatch(DcBatch batch, string eqpId)
{
    var items = new List<ProcessItem>();

    // Get CarrierIds that match the BatchId and EqpId
    var carrierIdsForEqp = await _context.DcBatches
        .Where(b => b.BatchId == batch.BatchId && b.EqpId == eqpId)
        .Select(b => b.CarrierId)
        .Distinct()
        .ToListAsync();

    // Get batch members only for carriers that use this equipment
    var batchMembers = await _context.DcBatchMembers
        .Where(bm => bm.BatchId == batch.BatchId && carrierIdsForEqp.Contains(bm.CarrierId))
        .ToListAsync();

    foreach (var member in batchMembers)
    {
        // Get next step's EqpId (current step + 1)
        var nextStep = await _context.DcBatches
            .Where(b => b.BatchId == batch.BatchId &&
                       b.CarrierId == member.CarrierId &&
                       b.Step == batch.Step + 1)
            .FirstOrDefaultAsync();

        items.Add(new ProcessItem
        {
            Carrier = member.CarrierId,
            Lot = member.LotId,
            Qty = member.Qty,
            PPID = batch.PPID,
            NextFurnace = nextStep?.EqpId ?? "なし",
            Location = "",
            EndTime = ""
        });
    }

    return items;
}
```

## Next取得ロジックの詳細

### 検索条件

**DC_Batchesから次のStepを検索**:
```csharp
var nextStep = await _context.DcBatches
    .Where(b => b.BatchId == batch.BatchId &&         // 同じバッチ
               b.CarrierId == member.CarrierId &&     // 同じキャリア
               b.Step == batch.Step + 1)              // 次のStep
    .FirstOrDefaultAsync();
```

### 設定値

```csharp
NextFurnace = nextStep?.EqpId ?? "なし"
```

- **nextStepが見つかった場合**: 次のStepのEqpIdを設定
- **nextStepが見つからない場合**: "なし"を設定

### 見つからないケース

1. **最終Step**: batch.Stepが最後のStepの場合、Step + 1は存在しない
2. **途中でStepが欠落**: Step 1, 3 と登録されている場合、Step 2が存在しない
3. **データ不整合**: 何らかの理由でStep + 1のレコードが削除された

## データフロー例

### 前提条件

**DC_Batches**:
| Id | BatchId | Step | CarrierId | EqpId | PPID | IsProcessed | CreatedAt |
|----|---------|------|-----------|-------|------|-------------|-----------|
| 1 | BATCH001 | 1 | C22667 | DVETC25 | PPID1 | false | 2023-11-15 10:00 |
| 2 | BATCH001 | 2 | C22667 | DVETC26 | PPID2 | false | 2023-11-15 10:00 |
| 3 | BATCH001 | 3 | C22667 | DVETC27 | PPID3 | false | 2023-11-15 10:00 |
| 4 | BATCH001 | 1 | C22668 | DVETC25 | PPID1 | false | 2023-11-15 10:00 |
| 5 | BATCH001 | 2 | C22668 | DVETC26 | PPID2 | false | 2023-11-15 10:00 |

**DC_BatchMembers**:
| Id | BatchId | CarrierId | LotId | Qty | Technology |
|----|---------|-----------|-------|-----|------------|
| 1 | BATCH001 | C22667 | SY79874.1 | 25 | T6-MV |
| 2 | BATCH001 | C22668 | SY79872.1 | 25 | T6-MV |

### 処理実行: DVETC25装置の予約1を表示

#### Step 1: LoadProgressData

1. DVETC25のDC_Batchesを取得（IsProcessed=false）
   - Id=1 (BatchId=BATCH001, Step=1, CarrierId=C22667)
   - Id=4 (BatchId=BATCH001, Step=1, CarrierId=C22668)

2. BatchIdとCreatedAtでグルーピング
   - BATCH001 (CreatedAt: 2023-11-15 10:00)

3. 最大3件取得
   - BATCH001のみ（1件）

4. CreateProcessItemsFromBatch呼び出し
   - 引数: batch=Id:1, eqpId="DVETC25"

#### Step 2: CreateProcessItemsFromBatch

1. **DVETC25とBATCH001のCarrierIdを取得**
   ```sql
   SELECT DISTINCT CarrierId
   FROM DC_Batches
   WHERE BatchId = 'BATCH001' AND EqpId = 'DVETC25'
   ```
   結果: C22667, C22668

2. **DC_BatchMembersを取得**
   ```sql
   SELECT *
   FROM DC_BatchMembers
   WHERE BatchId = 'BATCH001'
     AND CarrierId IN ('C22667', 'C22668')
   ```
   結果:
   - Id=1 (CarrierId=C22667, LotId=SY79874.1, Qty=25)
   - Id=2 (CarrierId=C22668, LotId=SY79872.1, Qty=25)

3. **各BatchMemberに対して次のStepを検索**

   **Member 1 (C22667, SY79874.1)**:
   ```sql
   SELECT *
   FROM DC_Batches
   WHERE BatchId = 'BATCH001'
     AND CarrierId = 'C22667'
     AND Step = 2  -- batch.Step (1) + 1
   ```
   結果: Id=2 (EqpId=DVETC26)

   **NextFurnace**: "DVETC26"

   **Member 2 (C22668, SY79872.1)**:
   ```sql
   SELECT *
   FROM DC_Batches
   WHERE BatchId = 'BATCH001'
     AND CarrierId = 'C22668'
     AND Step = 2  -- batch.Step (1) + 1
   ```
   結果: Id=5 (EqpId=DVETC26)

   **NextFurnace**: "DVETC26"

#### Step 3: 予約1に設定

**Reserved1**:
| Carrier | Lot | Qty | PPID | Next | Loc | End |
|---------|-----|-----|------|------|-----|-----|
| C22667 | SY79874.1 | 25 | PPID1 | DVETC26 | | |
| C22668 | SY79872.1 | 25 | PPID1 | DVETC26 | | |

## エッジケース

### ケース1: 最終Stepの場合

**DC_Batches**:
| Id | BatchId | Step | CarrierId | EqpId | IsProcessed |
|----|---------|------|-----------|-------|-------------|
| 1 | BATCH001 | 3 | C22667 | DVETC27 | false |

- **現在のStep**: 3
- **次のStep**: 4を検索
- **結果**: 見つからない
- **NextFurnace**: "なし"

### ケース2: 途中のStepが欠落

**DC_Batches**:
| Id | BatchId | Step | CarrierId | EqpId | IsProcessed |
|----|---------|------|-----------|-------|-------------|
| 1 | BATCH001 | 1 | C22667 | DVETC25 | false |
| 2 | BATCH001 | 3 | C22667 | DVETC27 | false |

- **現在のStep**: 1
- **次のStep**: 2を検索
- **結果**: 見つからない（Step 2が欠落）
- **NextFurnace**: "なし"

### ケース3: 複数のCarrierで異なるNext

**DC_Batches**:
| Id | BatchId | Step | CarrierId | EqpId | IsProcessed |
|----|---------|------|-----------|-------|-------------|
| 1 | BATCH001 | 1 | C22667 | DVETC25 | false |
| 2 | BATCH001 | 2 | C22667 | DVETC26 | false |
| 3 | BATCH001 | 1 | C22668 | DVETC25 | false |
| 4 | BATCH001 | 2 | C22668 | DVETC27 | false |

**DVETC25の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | DVETC26 |
| C22668 | SY79872.1 | DVETC27 |

- C22667とC22668は同じ装置（DVETC25）で処理されるが、次の装置が異なる

### ケース4: 同じCarrierで複数のLot

**DC_BatchMembers**:
| Id | BatchId | CarrierId | LotId |
|----|---------|-----------|-------|
| 1 | BATCH001 | C22667 | SY79874.1 |
| 2 | BATCH001 | C22667 | SY79872.1 |

**DC_Batches**:
| Id | BatchId | Step | CarrierId | EqpId |
|----|---------|------|-----------|-------|
| 1 | BATCH001 | 1 | C22667 | DVETC25 |
| 2 | BATCH001 | 2 | C22667 | DVETC26 |

**DVETC25の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | DVETC26 |
| C22667 | SY79872.1 | DVETC26 |

- 同じCarrierに複数のLotがある場合、すべて同じNext（DVETC26）を持つ

## 処理ステップの例

### 4ステップ処理の例

**CreateBatch画面で4ステップのバッチを作成**:

**DC_Batches**:
| BatchId | Step | CarrierId | EqpId | PPID |
|---------|------|-----------|-------|------|
| BATCH001 | 1 | C22667 | DVETC25 | PPID1 |
| BATCH001 | 2 | C22667 | DVETC26 | PPID2 |
| BATCH001 | 3 | C22667 | DVETC27 | PPID3 |
| BATCH001 | 4 | C22667 | DVETC28 | PPID4 |

**各装置での予約表示**:

**DVETC25（Step 1）の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | DVETC26 |

- 現在Step=1、次のStep=2のEqpId（DVETC26）を表示

**DVETC26（Step 2）の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | DVETC27 |

- 現在Step=2、次のStep=3のEqpId（DVETC27）を表示

**DVETC27（Step 3）の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | DVETC28 |

- 現在Step=3、次のStep=4のEqpId（DVETC28）を表示

**DVETC28（Step 4）の予約1**:
| Carrier | Lot | Next |
|---------|-----|------|
| C22667 | SY79874.1 | なし |

- 現在Step=4、次のStep=5は存在しない → "なし"を表示

## BatchProcessingBackgroundServiceとの連携

### IsProcessedフラグの影響

BatchProcessingBackgroundServiceがDC_Batch.IsProcessedをtrueに更新すると、その装置の予約表示から除外されます。

**例**:

**更新前（DVETC25の予約1に表示）**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | false ← |
| BATCH001 | 2 | C22667 | DVETC26 | false |

**LotがDVETC25で処理開始 → BatchProcessingBackgroundServiceが実行**

**更新後（DVETC25の予約1から除外）**:
| BatchId | Step | CarrierId | EqpId | IsProcessed |
|---------|------|-----------|-------|-------------|
| BATCH001 | 1 | C22667 | DVETC25 | true ← |
| BATCH001 | 2 | C22667 | DVETC26 | false |

**DVETC26の予約1に表示されるようになる**:
- Step 2がまだIsProcessed=falseなので、DVETC26の予約に表示
- Next = DVETC27（Step 3のEqpId）

## パフォーマンス考慮事項

### 現在の実装

**CreateProcessItemsFromBatch内のクエリ**:

1. CarrierIds取得: 1クエリ（装置+BatchIdでフィルタ）
2. BatchMembers取得: 1クエリ（BatchId+CarrierIdsでフィルタ）
3. 次のStep取得: **BatchMember数 × 1クエリ**

**例**: 1つの予約に10個のBatchMemberがある場合
- クエリ数: 2 + 10 = 12クエリ

### 改善の余地（将来の最適化）

**一括取得パターン**:

```csharp
// 全BatchMembersの次のStepを一括取得
var memberCarrierIds = batchMembers.Select(m => m.CarrierId).ToList();
var nextSteps = await _context.DcBatches
    .Where(b => b.BatchId == batch.BatchId &&
               memberCarrierIds.Contains(b.CarrierId) &&
               b.Step == batch.Step + 1)
    .ToListAsync();

// Dictionary化
var nextStepDict = nextSteps.ToDictionary(ns => ns.CarrierId);

foreach (var member in batchMembers)
{
    var nextEqpId = nextStepDict.ContainsKey(member.CarrierId)
        ? nextStepDict[member.CarrierId].EqpId
        : "なし";

    items.Add(new ProcessItem
    {
        NextFurnace = nextEqpId,
        // ... 他のプロパティ
    });
}
```

これにより、クエリ数を **3クエリ** に削減可能（BatchMember数に関わらず）。

## まとめ

**予約1~3のNext（NextFurnace）の更新ロジック**:

1. **データソース**: DC_Batchesテーブルの次のStep（Step + 1）
2. **検索条件**: 同じBatchId、同じCarrierId、Step = 現在のStep + 1
3. **設定値**:
   - 見つかった場合: 次のStepのEqpId
   - 見つからない場合: "なし"
4. **ユースケース**:
   - 通常: 次の処理装置を表示
   - 最終Step: "なし"を表示
   - データ欠落: "なし"を表示
5. **BatchProcessingBackgroundServiceとの連携**:
   - Step 1がIsProcessed=trueになると予約1から除外
   - Step 2が次の予約として表示される
   - Nextは自動的にStep 3のEqpIdを示す

これにより、オペレーターは各Lotが次にどの装置で処理されるかを一目で把握できます。
