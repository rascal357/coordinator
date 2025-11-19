# BatchProcessingBackgroundService テストケース

## 目的
`BatchProcessingBackgroundService`が正常に動作することを確認するためのテストケース集

## テスト対象メソッド
- `MarkBatchesAsProcessed`: DC_ActlのデータをもとにDC_BatchのIsProcessedを更新
- `DeleteCompletedBatches`: 最終ステップが完了したLotIdのレコードを削除

## テスト環境の準備

### 前提条件
1. SQLiteデータベース（coordinator.db）が存在する
2. アプリケーションが起動している
3. BatchProcessingBackgroundServiceが有効（appsettings.json）

### appsettings.json設定
```json
{
  "BatchProcessing": {
    "Enabled": true,
    "UpdateIntervalSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Coordinator.Services.BatchProcessingBackgroundService": "Debug"
    }
  }
}
```

---

## テストケース1: 単一ステップ・単一ロットの処理

### 目的
1ステップのみのバッチが処理され、IsProcessedが更新されて削除されることを確認

### テストデータ準備
```sql
-- DC_Batchにテストデータを挿入
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('TEST001', 1, 'CARR001', 'LOT001', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now'));

-- DC_Actlに処理中データを挿入
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT001', 'Type1', datetime('now'), 'CARR001', 25, 'PPID001', 'なし', 'LOC1');
```

### 期待される結果
1. **IsProcessed更新**: BatchId='TEST001', LotId='LOT001'のIsProcessedが0→1に更新される
2. **レコード削除**: Step2が存在しないため、レコードが削除される
3. **ログ出力**:
   - `Saved 1 batch updates to database`
   - `Deleted 1 completed batch records for 1 LotIds`
   - `- BatchId: TEST001, LotId: LOT001, Records: 1`

### 確認SQL
```sql
-- 更新前（IsProcessed=1のレコードが存在するか確認）
SELECT * FROM DC_Batch WHERE BatchId = 'TEST001' AND IsProcessed = 1;

-- 削除後（レコードが存在しないことを確認）
SELECT * FROM DC_Batch WHERE BatchId = 'TEST001';
```

---

## テストケース2: 複数ステップ・単一ロットの処理

### 目的
複数ステップのバッチで、Step1が処理されてもStep2が残ることを確認

### テストデータ準備
```sql
-- Step1とStep2のレコードを挿入
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('TEST002', 1, 'CARR002', 'LOT002', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('TEST002', 2, 'CARR002', 'LOT002', 25, 'Tech1', 'Furnace2', 'PPID002', 'なし', 0, datetime('now'));

-- DC_ActlにStep1の処理中データを挿入
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT002', 'Type1', datetime('now'), 'CARR002', 25, 'PPID001', 'Furnace2', 'LOC1');
```

### 期待される結果
1. **IsProcessed更新**: Step1のIsProcessedが0→1に更新される
2. **レコード保持**: Step2が存在するため、削除されない
3. **ログ出力**:
   - `Saved 1 batch updates to database`
   - 削除ログは出力されない

### 確認SQL
```sql
-- Step1が更新されていることを確認
SELECT * FROM DC_Batch WHERE BatchId = 'TEST002' AND Step = 1 AND IsProcessed = 1;

-- Step2が残っていることを確認
SELECT * FROM DC_Batch WHERE BatchId = 'TEST002' AND Step = 2 AND IsProcessed = 0;
```

---

## テストケース3: 複数ロット・異なる完了状態

### 目的
同じBatchIdで異なるLotIdが独立して処理されることを確認

### テストデータ準備
```sql
-- 同じBatchIdで異なるLotId
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
-- LOT003はStep1のみ（最終ステップ）
('TEST003', 1, 'CARR003', 'LOT003', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now')),
-- LOT004はStep1とStep2がある
('TEST003', 1, 'CARR004', 'LOT004', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('TEST003', 2, 'CARR004', 'LOT004', 25, 'Tech1', 'Furnace2', 'PPID002', 'なし', 0, datetime('now'));

-- DC_Actlに両方のLotIdを挿入
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT003', 'Type1', datetime('now'), 'CARR003', 25, 'PPID001', 'なし', 'LOC1'),
('Furnace1', 'LOT004', 'Type1', datetime('now'), 'CARR004', 25, 'PPID001', 'Furnace2', 'LOC1');
```

### 期待される結果
1. **IsProcessed更新**: 両方のStep1のIsProcessedが0→1に更新される
2. **LOT003削除**: Step2が存在しないため、LOT003のレコードが削除される
3. **LOT004保持**: Step2が存在するため、LOT004のレコードは残る
4. **ログ出力**:
   - `Saved 2 batch updates to database`
   - `Deleted 1 completed batch records for 1 LotIds`
   - `- BatchId: TEST003, LotId: LOT003, Records: 1`

### 確認SQL
```sql
-- LOT003が削除されていることを確認
SELECT * FROM DC_Batch WHERE BatchId = 'TEST003' AND LotId = 'LOT003';
-- 結果: 0件

-- LOT004が残っていることを確認（Step1とStep2）
SELECT * FROM DC_Batch WHERE BatchId = 'TEST003' AND LotId = 'LOT004' ORDER BY Step;
-- 結果: 2件（Step1: IsProcessed=1, Step2: IsProcessed=0）
```

---

## テストケース4: 全ステップ完了後の削除

### 目的
全ステップが完了したら、すべてのレコードが削除されることを確認

### テストデータ準備
```sql
-- Step1とStep2のレコードを挿入
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('TEST004', 1, 'CARR005', 'LOT005', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('TEST004', 2, 'CARR005', 'LOT005', 25, 'Tech1', 'Furnace2', 'PPID002', 'なし', 0, datetime('now'));

-- Step1を処理中にする
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT005', 'Type1', datetime('now'), 'CARR005', 25, 'PPID001', 'Furnace2', 'LOC1');
```

### 実行手順
1. 30秒待機（Step1がIsProcessed=1に更新される）
2. Step2を処理中にする:
```sql
UPDATE DC_Batch SET IsProcessed = 0 WHERE BatchId = 'TEST004' AND Step = 1;
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace2', 'LOT005', 'Type1', datetime('now'), 'CARR005', 25, 'PPID002', 'なし', 'LOC2');
```
3. さらに30秒待機（Step2がIsProcessed=1に更新され、削除される）

### 期待される結果
1. **1回目の更新**: Step1のIsProcessed=1、削除されない
2. **2回目の更新**: Step2のIsProcessed=1、全レコード削除
3. **ログ出力**:
   - 1回目: `Saved 1 batch updates to database`
   - 2回目: `Saved 1 batch updates to database` + `Deleted 2 completed batch records for 1 LotIds`

### 確認SQL
```sql
-- すべてのレコードが削除されていることを確認
SELECT * FROM DC_Batch WHERE BatchId = 'TEST004';
-- 結果: 0件
```

---

## テストケース5: IsProcessed=1のレコードのスキップ

### 目的
既にIsProcessed=1のレコードは再度更新されないことを確認

### テストデータ準備
```sql
-- すでにIsProcessed=1のレコード
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt, ProcessedAt)
VALUES ('TEST005', 1, 'CARR006', 'LOT006', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 1, datetime('now', '-1 hour'), datetime('now', '-1 hour'));

-- DC_Actlに同じLotIdのデータを挿入
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT006', 'Type1', datetime('now'), 'CARR006', 25, 'PPID001', 'なし', 'LOC1');
```

### 期待される結果
1. **更新なし**: IsProcessed=1のレコードは更新されない
2. **削除なし**: レコードは削除されない
3. **ログ出力**:
   - 更新ログなし
   - 削除ログなし

### 確認SQL
```sql
-- ProcessedAtが変更されていないことを確認
SELECT ProcessedAt FROM DC_Batch WHERE BatchId = 'TEST005';
-- 結果: 1時間前のタイムスタンプのまま
```

---

## テストケース6: 複数装置での同時処理

### 目的
異なる装置で同時に処理が行われても正しく動作することを確認

### テストデータ準備
```sql
-- Furnace1とFurnace2で異なるLotを処理
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('TEST006A', 1, 'CARR007', 'LOT007', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now')),
('TEST006B', 1, 'CARR008', 'LOT008', 25, 'Tech1', 'Furnace2', 'PPID002', 'なし', 0, datetime('now'));

-- DC_Actlに両方の装置のデータを挿入
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT007', 'Type1', datetime('now'), 'CARR007', 25, 'PPID001', 'なし', 'LOC1'),
('Furnace2', 'LOT008', 'Type1', datetime('now'), 'CARR008', 25, 'PPID002', 'なし', 'LOC2');
```

### 期待される結果
1. **両方更新**: 両方のレコードのIsProcessedが1に更新される
2. **両方削除**: 両方のレコードが削除される
3. **ログ出力**:
   - `Saved 2 batch updates to database`
   - `Deleted 2 completed batch records for 2 LotIds`

---

## テストケース7: TrackInTimeが一致しない場合

### 目的
DC_ActlのTrackInTimeとDC_BatchのProcessedAtが一致することを確認

### テストデータ準備
```sql
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('TEST007', 1, 'CARR009', 'LOT009', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now'));

INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT009', 'Type1', datetime('now'), 'CARR009', 25, 'PPID001', 'なし', 'LOC1');
```

### 期待される結果
1. **ProcessedAt一致**: DC_BatchのProcessedAtがDC_ActlのTrackInTimeと同じ値になる

### 確認SQL
```sql
-- ProcessedAtとTrackInTimeが一致することを確認
SELECT
    b.ProcessedAt as BatchProcessedAt,
    a.TrackInTime as ActlTrackInTime,
    b.ProcessedAt = a.TrackInTime as IsMatching
FROM DC_Batch b
JOIN DC_Actl a ON b.LotId = a.LotId AND b.EqpId = a.EqpId
WHERE b.BatchId = 'TEST007';
-- 結果: IsMatching = 1（一致）
```

---

## テストケース8: エラーハンドリング

### 目的
データベースエラーが発生しても処理が継続されることを確認

### テストデータ準備
```sql
-- 正常なレコード
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('TEST008', 1, 'CARR010', 'LOT010', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now'));

-- LotIdがNULLの異常レコード（実際には制約で防がれるが、テスト用）
-- このレコードは挿入できない場合、スキップ

INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT010', 'Type1', datetime('now'), 'CARR010', 25, 'PPID001', 'なし', 'LOC1');
```

### 期待される結果
1. **エラーログ**: 異常なレコードに対してエラーログが出力される
2. **処理継続**: 正常なレコードは処理される
3. **部分的成功**: エラーがあっても他のレコードは正常に処理される

---

## ログ確認方法

### コンソールログ
アプリケーション実行時のコンソール出力を確認

### ファイルログ
appsettings.jsonでファイルログを設定している場合、ログファイルを確認

### 重要なログメッセージ
- `Saved {Count} batch updates to database` - 更新成功
- `Deleted {Count} completed batch records for {Groups} LotIds` - 削除成功
- `ERROR:` - エラー発生
- `Checked next step for BatchId:` - デバッグログ（LogLevel=Debug時）

---

## クリーンアップ

テスト後、テストデータを削除：
```sql
DELETE FROM DC_Batch WHERE BatchId LIKE 'TEST%';
DELETE FROM DC_Actl WHERE LotId LIKE 'LOT%';
```

---

## 自動テストスクリプト（Python例）

```python
import sqlite3
import time
from datetime import datetime

def run_test_case_1():
    """テストケース1: 単一ステップ・単一ロットの処理"""
    conn = sqlite3.connect('coordinator.db')
    cursor = conn.cursor()

    # テストデータ挿入
    batch_id = f'TEST{int(time.time())}'
    now = datetime.now().isoformat()

    cursor.execute("""
        INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
        VALUES (?, 1, 'CARR001', 'LOT001', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, ?)
    """, (batch_id, now))

    cursor.execute("""
        INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
        VALUES ('Furnace1', 'LOT001', 'Type1', ?, 'CARR001', 25, 'PPID001', 'なし', 'LOC1')
    """, (now,))

    conn.commit()

    print(f"テストデータ挿入完了: BatchId={batch_id}")
    print("30秒待機中...")
    time.sleep(30)

    # 結果確認
    cursor.execute("SELECT * FROM DC_Batch WHERE BatchId = ?", (batch_id,))
    result = cursor.fetchall()

    if len(result) == 0:
        print("✓ テスト成功: レコードが削除されました")
    else:
        print(f"✗ テスト失敗: {len(result)}件のレコードが残っています")

    conn.close()

if __name__ == '__main__':
    run_test_case_1()
```

---

## テスト実行チェックリスト

- [ ] appsettings.jsonでBatchProcessing.Enabled = true
- [ ] アプリケーションが起動している
- [ ] ログレベルがDebugに設定されている（詳細ログ確認時）
- [ ] テストケース1実行
- [ ] テストケース2実行
- [ ] テストケース3実行
- [ ] テストケース4実行
- [ ] テストケース5実行
- [ ] テストケース6実行
- [ ] テストケース7実行
- [ ] テストケース8実行
- [ ] ログで結果確認
- [ ] データベースで結果確認
- [ ] テストデータのクリーンアップ

---

## 想定される問題と対処法

### 問題1: IsProcessedが更新されない
**原因**:
- DC_ActlのLotIdとEqpIdがDC_Batchと一致していない
- IsProcessedが既に1になっている

**対処**:
- データの一致を確認
- IsProcessed=0のレコードがあることを確認

### 問題2: レコードが削除されない
**原因**:
- 次のステップが存在する
- DeleteCompletedBatchesでエラーが発生している

**対処**:
- 次のステップの有無を確認
- エラーログを確認

### 問題3: 別のLotIdのレコードも削除される
**原因**:
- 旧実装のバグ（BatchId単位で削除していた）

**対処**:
- 最新のコード（LotId単位の削除）になっていることを確認

---

## まとめ

このテストケース集を使用して、`BatchProcessingBackgroundService`の各機能が正常に動作することを確認してください。すべてのテストケースが成功すれば、サービスは本番環境で使用可能です。
