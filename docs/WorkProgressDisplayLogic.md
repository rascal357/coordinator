# WorkProgress画面の表示ロジック仕様書

## 概要

WorkProgress（作業進捗）画面では、各装置の処理状況を以下の5つのカテゴリーで表示します：

1. **In Process（処理中）**
2. **Waiting（処理待ち）**
3. **Reserved 1（予約1）**
4. **Reserved 2（予約2）**
5. **Reserved 3（予約3）**

---

## 1. In Process（処理中）とWaiting（処理待ち）の表示ロジック

### データソース
- **DC_Actl（実績テーブル）**

### 処理フロー

#### 1.1 実績データの取得
```csharp
var actls = await _context.DcActls
    .Where(a => a.EqpId == eqp.Name)
    .OrderBy(a => a.TrackInTime)  // TrackInTime（投入時刻）の昇順でソート
    .ToListAsync();
```

**説明:**
- 装置ごとにDC_Actlテーブルから実績データを取得
- **TrackInTime（投入時刻）の古い順**に並べる

#### 1.2 時間ウィンドウによるグループ化
```csharp
var timeGroups = GroupByTimeWindow(actls, TimeSpan.FromMinutes(5));
```

**GroupByTimeWindowメソッドの動作:**
```csharp
private List<List<DcActl>> GroupByTimeWindow(List<DcActl> actls, TimeSpan window)
{
    if (!actls.Any()) return new List<List<DcActl>>();

    var groups = new List<List<DcActl>>();
    var currentGroup = new List<DcActl> { actls[0] };
    var currentTime = actls[0].TrackInTime;

    for (int i = 1; i < actls.Count; i++)
    {
        var timeDiff = Math.Abs((actls[i].TrackInTime - currentTime).TotalMinutes);

        if (timeDiff <= window.TotalMinutes)  // ±5分以内なら同じグループ
        {
            currentGroup.Add(actls[i]);
        }
        else  // 5分を超えたら新しいグループ
        {
            groups.Add(currentGroup);
            currentGroup = new List<DcActl> { actls[i] };
            currentTime = actls[i].TrackInTime;
        }
    }

    if (currentGroup.Any())
    {
        groups.Add(currentGroup);
    }

    return groups;
}
```

**ロジック:**
- TrackInTimeが**±5分以内**のレコードを同じグループにまとめる
- 5分を超える時間差があると新しいグループを作成
- 結果として、投入時刻が近いロットがグループ化される

#### 1.3 In ProcessとWaitingへの割り当て
```csharp
// First group is in process, second group is waiting
if (timeGroups.Count > 0)
{
    progressViewModel.InProcess = await CreateProcessItemsFromActls(timeGroups[0]);
}

if (timeGroups.Count > 1)
{
    progressViewModel.Waiting = await CreateProcessItemsFromActls(timeGroups[1]);
}
```

**割り当てルール:**
- **1番目のグループ（最も古い投入時刻）** → **In Process（処理中）**
- **2番目のグループ（2番目に古い投入時刻）** → **Waiting（処理待ち）**
- 3番目以降のグループは表示されない

#### 1.4 ProcessItemの作成（In Process / Waiting）
```csharp
private async Task<List<ProcessItem>> CreateProcessItemsFromActls(List<DcActl> actls)
{
    var items = new List<ProcessItem>();

    foreach (var actl in actls)
    {
        items.Add(new ProcessItem
        {
            Carrier = actl.Carrier,
            Lot = actl.LotId,
            Qty = actl.Qty,
            PPID = actl.PPID,
            NextFurnace = actl.Next,
            Location = actl.Location,
            EndTime = actl.EndTime?.ToString("yyyy/MM/dd HH:mm") ?? ""
        });
    }

    return items;
}
```

**表示項目:**
- **Carrier**: DC_Actl.Carrier
- **Lot**: DC_Actl.LotId
- **Qty**: DC_Actl.Qty
- **PPID**: DC_Actl.PPID
- **Next**: DC_Actl.Next（次の装置）
- **Loc** (Location): DC_Actl.Location
- **End** (EndTime): DC_Actl.EndTime（終了予定時刻）

### 具体例

DC_Actlに以下のデータがある場合:

| EqpId   | LotId      | TrackInTime         | Carrier | Qty | PPID    |
|---------|------------|---------------------|---------|-----|---------|
| DVETC38 | SY79874.1  | 2025-01-15 09:00:00 | C22667  | 25  | GSIO3F4 |
| DVETC38 | SY79872.1  | 2025-01-15 09:02:00 | C22668  | 25  | GSIO3F4 |
| DVETC38 | SY79906.1  | 2025-01-15 09:04:00 | C22669  | 25  | GSIO3F4 |
| DVETC38 | SY78840.1  | 2025-01-15 12:00:00 | C22670  | 25  | GSIO3F5 |
| DVETC38 | SY79506.1  | 2025-01-15 12:03:00 | C22671  | 25  | GSIO3F5 |

**グループ化結果:**
- **グループ1 (±5分以内)**: SY79874.1, SY79872.1, SY79906.1 → **In Process**
- **グループ2 (±5分以内)**: SY78840.1, SY79506.1 → **Waiting**

---

## 2. Reserved 1~3（予約1~3）の表示ロジック

### データソース
- **DC_Batches（バッチテーブル）**
- **DC_BatchMembers（バッチメンバーテーブル）**

### 処理フロー

#### 2.1 未処理バッチの取得
```csharp
var reservedBatchIds = await _context.DcBatches
    .Where(b => b.EqpId == eqp.Name && !b.IsProcessed)  // 装置IDが一致 かつ 未処理
    .GroupBy(b => new { b.BatchId, b.CreatedAt })       // BatchIdとCreatedAtでグループ化
    .OrderBy(g => g.Key.CreatedAt)                      // CreatedAt昇順（古い順）
    .Take(3)                                            // 最大3件まで取得
    .Select(g => new { g.Key.BatchId, Batch = g.First() })
    .ToListAsync();
```

**ロジック:**
- **フィルタ条件1**: `b.EqpId == eqp.Name` → 該当装置のバッチのみ
- **フィルタ条件2**: `!b.IsProcessed` → 未処理（処理中でない）バッチのみ
- **グループ化**: `BatchId` と `CreatedAt` でグループ化（同じBatchIdのレコードをまとめる）
- **ソート**: `CreatedAt`（バッチ作成日時）の**昇順**（古い順）
- **件数制限**: **最大3件**まで取得

#### 2.2 ProcessItemの作成（Reserved）
```csharp
var reservedItems = new List<List<ProcessItem>>();
foreach (var batchGroup in reservedBatchIds)
{
    var items = await CreateProcessItemsFromBatch(batchGroup.Batch, eqp.Name);
    reservedItems.Add(items);
}
```

#### 2.3 Reserved1~3への割り当て
```csharp
if (reservedItems.Count > 0)
{
    progressViewModel.Reserved1 = reservedItems[0];
    progressViewModel.Reserved1BatchId = reservedBatchIds[0].BatchId;
}
if (reservedItems.Count > 1)
{
    progressViewModel.Reserved2 = reservedItems[1];
    progressViewModel.Reserved2BatchId = reservedBatchIds[1].BatchId;
}
if (reservedItems.Count > 2)
{
    progressViewModel.Reserved3 = reservedItems[2];
    progressViewModel.Reserved3BatchId = reservedBatchIds[2].BatchId;
}
```

**割り当てルール:**
- **1番目のバッチ（最も古いCreatedAt）** → **Reserved 1**
- **2番目のバッチ** → **Reserved 2**
- **3番目のバッチ** → **Reserved 3**

#### 2.4 CreateProcessItemsFromBatchメソッドの詳細
```csharp
private async Task<List<ProcessItem>> CreateProcessItemsFromBatch(DcBatch batch, string eqpId)
{
    var items = new List<ProcessItem>();

    // ステップ1: 該当装置で処理されるキャリアIDを取得
    var carrierIdsForEqp = await _context.DcBatches
        .Where(b => b.BatchId == batch.BatchId && b.EqpId == eqpId)
        .Select(b => b.CarrierId)
        .Distinct()
        .ToListAsync();

    // ステップ2: バッチメンバー情報を取得（該当キャリアのみ）
    var batchMembers = await _context.DcBatchMembers
        .Where(bm => bm.BatchId == batch.BatchId && carrierIdsForEqp.Contains(bm.CarrierId))
        .ToListAsync();

    // ステップ3: 各メンバーのProcessItemを作成
    foreach (var member in batchMembers)
    {
        // 次のステップの装置IDを取得 (現在のStep + 1)
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
            NextFurnace = nextStep?.EqpId ?? "なし",  // 次ステップの装置ID
            Location = "",
            EndTime = ""
        });
    }

    return items;
}
```

**処理の詳細:**

1. **キャリアIDの抽出**:
   - DC_Batchesから該当バッチ・該当装置のキャリアIDを取得
   - 1つのバッチに複数のキャリアが含まれる場合がある

2. **バッチメンバー取得**:
   - DC_BatchMembersから該当キャリアのロット情報を取得
   - Carrier、LotId、Qty、Technologyなどの詳細情報を含む

3. **次工程の装置取得**:
   - DC_Batchesから`Step + 1`のレコードを検索
   - 次工程の装置ID（EqpId）を取得
   - 存在しない場合は「なし」

4. **ProcessItem作成**:
   - **Carrier**: DC_BatchMembers.CarrierId
   - **Lot**: DC_BatchMembers.LotId
   - **Qty**: DC_BatchMembers.Qty
   - **PPID**: DC_Batches.PPID（現在のステップのPPID）
   - **NextFurnace**: 次ステップのEqpId
   - **Location**: 空（予約状態では未設定）
   - **EndTime**: 空（予約状態では未設定）

### 具体例

DC_Batchesに以下のデータがある場合:

| BatchId      | EqpId   | CarrierId | Step | PPID    | IsProcessed | CreatedAt           |
|--------------|---------|-----------|------|---------|-------------|---------------------|
| 20231115001  | DVETC25 | C22667    | 1    | PPID1   | false       | 2025-01-15 08:00:00 |
| 20231115002  | DVETC25 | C22668    | 1    | PPID1   | false       | 2025-01-15 09:00:00 |
| 20231115003  | DVETC25 | C22669    | 1    | PPID2   | false       | 2025-01-15 10:00:00 |
| 20231115004  | DVETC25 | C22670    | 1    | PPID2   | false       | 2025-01-15 11:00:00 |

**DVETC25装置の予約表示:**
- **Reserved 1**: BatchId=20231115001 (CreatedAt: 08:00) ← 最も古い
- **Reserved 2**: BatchId=20231115002 (CreatedAt: 09:00) ← 2番目に古い
- **Reserved 3**: BatchId=20231115003 (CreatedAt: 10:00) ← 3番目に古い
- BatchId=20231115004は4番目なので表示されない

---

## 3. 表示項目の比較

| 表示項目 | In Process / Waiting | Reserved 1~3 |
|----------|---------------------|--------------|
| **Carrier** | DC_Actl.Carrier | DC_BatchMembers.CarrierId |
| **Lot** | DC_Actl.LotId | DC_BatchMembers.LotId |
| **Qty** | DC_Actl.Qty | DC_BatchMembers.Qty |
| **PPID** | DC_Actl.PPID | DC_Batches.PPID |
| **Next** | DC_Actl.Next | 次ステップのDC_Batches.EqpId |
| **Loc** | DC_Actl.Location | 空（未設定） |
| **End** | DC_Actl.EndTime | 空（未設定） |

---

## 4. 重要なポイント

### In Process / Waiting
✅ **5分ウィンドウ**: 同じバッチや連続投入されたロットをまとめるための時間差
✅ **TrackInTime昇順**: 古い順にソートして時系列で判断
✅ **最大2グループ**: In Processが1つ、Waitingが1つ
✅ **DC_Actlから直接取得**: すべての情報がDC_Actlテーブルに格納されている

### Reserved 1~3
✅ **IsProcessed=false**: 未処理のバッチのみ表示（処理中・完了したバッチは除外）
✅ **CreatedAt昇順**: バッチ作成日時の古い順（先に作成されたバッチが優先）
✅ **最大3件**: Reserved 1, 2, 3の3枠まで表示
✅ **装置ごとに独立**: 各装置ごとに予約1~3を取得
✅ **BatchIdでグループ化**: 同じBatchIdの複数ステップをまとめて1つのバッチとして扱う
✅ **次工程情報**: NextFurnace（次の炉）情報も表示

---

## 5. データフロー図

```
[DC_Actl]
    ↓
装置でフィルタ & TrackInTime昇順ソート
    ↓
5分ウィンドウでグループ化
    ↓
├─ グループ1 → In Process
└─ グループ2 → Waiting

[DC_Batches] + [DC_BatchMembers]
    ↓
装置 & IsProcessed=false でフィルタ
    ↓
CreatedAt昇順ソート & 最大3件取得
    ↓
├─ 1番目 → Reserved 1
├─ 2番目 → Reserved 2
└─ 3番目 → Reserved 3
```

---

## 6. 実装ファイル

- **ページモデル**: `Pages/WorkProgress.cshtml.cs`
- **メソッド**:
  - `LoadProgressData()`: メインの処理ロジック
  - `GroupByTimeWindow()`: 時間ウィンドウでのグループ化
  - `CreateProcessItemsFromActls()`: In Process/Waiting用のProcessItem作成
  - `CreateProcessItemsFromBatch()`: Reserved用のProcessItem作成

---

## 7. データベーステーブル構造

### DC_Actl（実績テーブル）
| カラム名 | 型 | 説明 |
|----------|-----|------|
| EqpId | string | 装置ID |
| LotId | string | ロットID |
| LotType | string | ロットタイプ |
| TrackInTime | DateTime | 投入時刻 |
| Carrier | string | キャリアID |
| Qty | int | 数量 |
| PPID | string | プロセスレシピID |
| Next | string | 次の装置 |
| Location | string | ロケーション |
| EndTime | DateTime? | 終了予定時刻 |

### DC_Batches（バッチテーブル）
| カラム名 | 型 | 説明 |
|----------|-----|------|
| BatchId | string | バッチID |
| Step | int | ステップ番号 |
| CarrierId | string | キャリアID |
| EqpId | string | 装置ID |
| PPID | string | プロセスレシピID |
| IsProcessed | bool | 処理済みフラグ |
| CreatedAt | DateTime | 作成日時 |

### DC_BatchMembers（バッチメンバーテーブル）
| カラム名 | 型 | 説明 |
|----------|-----|------|
| BatchId | string | バッチID |
| CarrierId | string | キャリアID |
| LotId | string | ロットID |
| Qty | int | 数量 |
| Technology | string | テクノロジー |

---

**最終更新日**: 2025-11-15
**バージョン**: 1.0
