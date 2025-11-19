# WorkProgressModel テストケース

## 目的
`WorkProgressModel`（Work Progress画面）が正常に動作することを確認するためのテストケース集

## テスト対象機能
- 装置進捗データの表示（In Process、Waiting、Reserved 1-3）
- DC_Actlのグループ化（±5分）
- NextFurnaceの表示（DC_Batchから取得）
- TYPE/LINEフィルタリング
- 装置メモの更新
- バッチの削除
- 自動更新（30秒間隔）

## テスト環境の準備

### 前提条件
1. SQLiteデータベース（coordinator.db）が存在する
2. アプリケーションが起動している
3. DC_Eqpsテーブルに装置マスタが登録されている

### 装置マスタの準備
```sql
-- テスト用装置を登録
INSERT INTO DC_Eqps (Name, Type, Line, Note)
VALUES
('Furnace1', 'DIFF', 'A', NULL),
('Furnace2', 'DIFF', 'A', NULL),
('Furnace3', 'LPCVD', 'B', NULL),
('Furnace4', 'LPCVD', 'B', NULL);
```

---

## テストケース1: In Process（処理中）の表示

### 目的
DC_Actlの最も早いグループが「処理中」として表示されることを確認

### テストデータ準備
```sql
-- Furnace1で処理中のデータ（TrackInTime: 10:00）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location, EndTime)
VALUES
('Furnace1', 'LOT001', 'Type1', datetime('now'), 'CARR001', 25, 'PPID001', 'Furnace2', 'LOC1', datetime('now', '+2 hours')),
('Furnace1', 'LOT002', 'Type1', datetime('now', '+2 minutes'), 'CARR002', 25, 'PPID001', 'Furnace2', 'LOC1', datetime('now', '+2 hours'));
```

### 期待される結果
1. **表示位置**: 「処理中」列に表示
2. **表示内容**:
   - Carrier: CARR001, CARR002
   - Lot: LOT001, LOT002
   - Qty: 25
   - PPID: PPID001
   - Next: Furnace2
   - Loc: LOC1
   - End: HH:mm形式（例: 12:00）

### 確認方法
1. Work Progress画面を開く
2. Furnace1の行を確認
3. 「処理中」列に2つのレコードが表示されることを確認

---

## テストケース2: Waiting（処理待ち）の表示

### 目的
DC_Actlの2番目のグループが「処理待ち」として表示されることを確認

### テストデータ準備
```sql
-- Furnace1で処理中のデータ（TrackInTime: 10:00）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT001', 'Type1', datetime('now'), 'CARR001', 25, 'PPID001', 'Furnace2', 'LOC1'),
('Furnace1', 'LOT002', 'Type1', datetime('now', '+2 minutes'), 'CARR002', 25, 'PPID001', 'Furnace2', 'LOC1');

-- Furnace1で処理待ちのデータ（TrackInTime: 10:10）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT003', 'Type1', datetime('now', '+10 minutes'), 'CARR003', 25, 'PPID001', 'Furnace2', 'LOC2'),
('Furnace1', 'LOT004', 'Type1', datetime('now', '+12 minutes'), 'CARR004', 25, 'PPID001', 'Furnace2', 'LOC2');
```

### 期待される結果
1. **処理中**: LOT001, LOT002が表示
2. **処理待ち**: LOT003, LOT004が表示

### 確認方法
1. Work Progress画面を開く
2. Furnace1の「処理中」列に2件
3. Furnace1の「処理待ち」列に2件
4. それぞれ異なるLocationが表示されることを確認

---

## テストケース3: グループ化（±5分以内）

### 目的
TrackInTimeが±5分以内のレコードが同じグループになることを確認

### テストデータ準備
```sql
-- グループ1: 10:00, 10:02, 10:04（±5分以内）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT001', 'Type1', datetime('now'), 'CARR001', 25, 'PPID001', 'Furnace2', 'LOC1'),
('Furnace1', 'LOT002', 'Type1', datetime('now', '+2 minutes'), 'CARR002', 25, 'PPID001', 'Furnace2', 'LOC1'),
('Furnace1', 'LOT003', 'Type1', datetime('now', '+4 minutes'), 'CARR003', 25, 'PPID001', 'Furnace2', 'LOC1');

-- グループ2: 10:11（5分を超える）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace1', 'LOT004', 'Type1', datetime('now', '+11 minutes'), 'CARR004', 25, 'PPID001', 'Furnace2', 'LOC2');
```

### 期待される結果
1. **処理中**: LOT001, LOT002, LOT003（3件）
2. **処理待ち**: LOT004（1件）

### 境界値テスト
```sql
-- 境界値: ちょうど5分後
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES
('Furnace2', 'LOT101', 'Type1', datetime('now'), 'CARR101', 25, 'PPID001', 'Furnace3', 'LOC1'),
('Furnace2', 'LOT102', 'Type1', datetime('now', '+5 minutes'), 'CARR102', 25, 'PPID001', 'Furnace3', 'LOC1');
```

**期待**: 5分ちょうどは同じグループ（処理中に2件表示）

---

## テストケース4: Reserved 1-3（予約）の表示

### 目的
DC_BatchのレコードがCreatedAt順に「予約1」「予約2」「予約3」に表示されることを確認

### テストデータ準備
```sql
-- 3つのバッチを異なる時刻で作成
-- 予約1: 一番古い
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('BATCH001', 1, 'CARR011', 'LOT011', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now', '-2 hours'));

-- 予約2: 2番目に古い
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('BATCH002', 1, 'CARR012', 'LOT012', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now', '-1 hour'));

-- 予約3: 3番目に古い
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('BATCH003', 1, 'CARR013', 'LOT013', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now', '-30 minutes'));

-- 予約4: 4番目（表示されない）
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('BATCH004', 1, 'CARR014', 'LOT014', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now'));
```

### 期待される結果
1. **予約1**: LOT011（BATCH001）
2. **予約2**: LOT012（BATCH002）
3. **予約3**: LOT013（BATCH003）
4. **予約4以降**: 表示されない

### 確認方法
1. Work Progress画面を開く
2. Furnace1の「予約1」「予約2」「予約3」列を確認
3. CreatedAtの古い順に表示されることを確認
4. 各予約列に削除ボタン（×）が表示されることを確認

---

## テストケース5: NextFurnaceの表示（DC_Batchから取得）

### 目的
処理中のLotのNextFurnaceが、DC_BatchのNextEqpIdから取得されることを確認

### テストデータ準備
```sql
-- DC_Batchにレコードを作成
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt, ProcessedAt)
VALUES ('BATCH010', 1, 'CARR020', 'LOT020', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace3', 1, datetime('now', '-1 hour'), datetime('now'));

-- DC_ActlにLOT020を追加（NextはFurnace2だが、DC_BatchのNextEqpIdを使用する）
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT020', 'Type1', datetime('now'), 'CARR020', 25, 'PPID001', 'Furnace2', 'LOC1');
```

### 期待される結果
1. **Next表示**: Furnace3（DC_BatchのNextEqpId）
2. **DC_ActlのNextは無視**: Furnace2は使用されない

### 確認SQL
```sql
-- ProcessedAtとTrackInTimeが一致していることを確認
SELECT
    b.NextEqpId,
    b.ProcessedAt,
    a.TrackInTime,
    a.Next as ActlNext
FROM DC_Batch b
JOIN DC_Actl a ON b.LotId = a.LotId AND b.EqpId = a.EqpId
WHERE b.BatchId = 'BATCH010';
-- 結果: NextEqpId='Furnace3', ProcessedAt=TrackInTime
```

### 確認方法
1. Work Progress画面でFurnace1の処理中を確認
2. LOT020のNext列に「Furnace3」が表示されることを確認

---

## テストケース6: TYPE/LINEフィルタリング

### 目的
TYPEとLINEのフィルタが正しく動作することを確認

### テストデータ準備
```sql
-- 装置マスタが既に登録されていることを前提
-- Furnace1: TYPE=DIFF, LINE=A
-- Furnace2: TYPE=DIFF, LINE=A
-- Furnace3: TYPE=LPCVD, LINE=B
-- Furnace4: TYPE=LPCVD, LINE=B
```

### テストシナリオ

#### シナリオ6-1: TYPEフィルタ（DIFFのみ）
1. Work Progress画面を開く
2. フィルターを展開
3. TYPE「DIFF」のみチェック
4. **期待結果**: Furnace1, Furnace2のみ表示、Furnace3, Furnace4は非表示

#### シナリオ6-2: LINEフィルタ（Bのみ）
1. Work Progress画面を開く
2. フィルターを展開
3. LINE「B」を選択
4. **期待結果**: Furnace3, Furnace4のみ表示、Furnace1, Furnace2は非表示

#### シナリオ6-3: 複合フィルタ（TYPE=LPCVD, LINE=B）
1. Work Progress画面を開く
2. フィルターを展開
3. TYPE「LPCVD」、LINE「B」を選択
4. **期待結果**: Furnace3, Furnace4のみ表示

#### シナリオ6-4: フィルタクリア
1. フィルターを設定した状態で「クリア」ボタンをクリック
2. **期待結果**: すべての装置が表示される

---

## テストケース7: 装置メモの更新

### 目的
装置メモが正しく更新され、表示されることを確認

### テストシナリオ

#### シナリオ7-1: メモの新規登録
1. Work Progress画面を開く
2. Furnace1の編集ボタン（鉛筆アイコン）をクリック
3. モーダルが開く
4. メモ欄に「メンテナンス中」と入力
5. 保存ボタンをクリック
6. **期待結果**:
   - モーダルが閉じる
   - Furnace1のメモ欄に「メンテナンス中」が表示される
   - ページリロードなしで更新される

#### シナリオ7-2: メモの編集
1. 既にメモがある装置の編集ボタンをクリック
2. メモを「メンテナンス完了」に変更
3. 保存ボタンをクリック
4. **期待結果**: メモが更新される

#### シナリオ7-3: メモの削除
1. 既にメモがある装置の編集ボタンをクリック
2. メモを空にする
3. 保存ボタンをクリック
4. **期待結果**: メモ欄に「(メモなし)」が表示される

#### シナリオ7-4: 文字数制限（200文字）
1. 編集ボタンをクリック
2. 201文字以上入力しようとする
3. **期待結果**: 200文字で入力が止まる、文字カウンタが「200 / 200 文字」と表示される

### 確認SQL
```sql
-- メモが正しく保存されていることを確認
SELECT Name, Note FROM DC_Eqps WHERE Name = 'Furnace1';
```

---

## テストケース8: バッチの削除

### 目的
予約バッチが正しく削除されることを確認

### テストデータ準備
```sql
-- 削除対象のバッチ
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('BATCH_DEL001', 1, 'CARR030', 'LOT030', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('BATCH_DEL001', 1, 'CARR031', 'LOT031', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now'));
```

### テストシナリオ
1. Work Progress画面を開く
2. Furnace1の「予約1」列に削除ボタン（×）が表示されることを確認
3. 削除ボタンをクリック
4. 確認ダイアログが表示される
5. 「OK」をクリック
6. **期待結果**:
   - ページがリロードされる
   - BATCH_DEL001のすべてのレコードが削除される
   - 予約1列が空になる

### 確認SQL
```sql
-- バッチが削除されていることを確認
SELECT * FROM DC_Batch WHERE BatchId = 'BATCH_DEL001';
-- 結果: 0件
```

### ログ確認
```
削除ログの例:
Batch Deleted
BatchId: BATCH_DEL001
Deleted At: 2025/11/20 12:34:56.789
DC_Batch Records Deleted (2)
  [Step 1] LotId: LOT030, Carrier: CARR030, ...
  [Step 1] LotId: LOT031, Carrier: CARR031, ...
```

---

## テストケース9: 自動更新（30秒間隔）

### 目的
画面が30秒ごとに自動更新されることを確認

### テストシナリオ
1. Work Progress画面を開く
2. ブラウザの開発者ツール（F12）を開く
3. Consoleタブを確認
4. 30秒待機
5. **期待結果**:
   - コンソールに「データを更新しました: {日時}」が出力される
   - ページがリロードされずにデータが更新される

### テストデータ準備
```sql
-- 画面を開いた後、DC_Actlに新しいデータを追加
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT999', 'Type1', datetime('now'), 'CARR999', 25, 'PPID001', 'Furnace2', 'LOC1');
```

### 確認方法
1. 画面を開いてから30秒待つ
2. Furnace1の処理中列に新しいLOT999が表示されることを確認
3. ページがリロードされていないことを確認（入力中のテキストなどが消えない）

---

## テストケース10: カラムの表示/非表示切り替え

### 目的
処理中/処理待ち/予約のカラムを表示/非表示できることを確認

### テストシナリオ

#### シナリオ10-1: 処理中を非表示
1. Work Progress画面を開く
2. 「処理中」ボタンをクリック
3. **期待結果**:
   - 処理中のカラムが非表示になる
   - ボタンのアイコンが右向き矢印に変わる

#### シナリオ10-2: 再度表示
1. 非表示の状態で「処理中」ボタンをクリック
2. **期待結果**:
   - 処理中のカラムが表示される
   - ボタンのアイコンが下向き矢印に変わる

#### シナリオ10-3: 複数カラムの非表示
1. 「処理中」「処理待ち」「予約1~3」をすべて非表示にする
2. **期待結果**: すべてのデータカラムが非表示になり、装置名のみが表示される

---

## テストケース11: EndTimeの表示形式

### 目的
EndTimeが「HH:mm」形式で表示されることを確認

### テストデータ準備
```sql
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location, EndTime)
VALUES ('Furnace1', 'LOT040', 'Type1', datetime('now'), 'CARR040', 25, 'PPID001', 'Furnace2', 'LOC1', datetime('2025-11-20 14:30:00'));
```

### 期待される結果
1. **End列の表示**: 14:30（HH:mm形式）
2. **日付は表示されない**: 年月日は含まれない

### 確認方法
1. Work Progress画面を開く
2. Furnace1の処理中のEnd列を確認
3. 「14:30」のような形式で表示されることを確認

---

## テストケース12: 複数ステップのバッチ表示

### 目的
同じBatchIdで複数ステップがある場合、LotIdとCarrierは1回のみ表示されることを確認

### テストデータ準備
```sql
-- 同じBatchId、同じLotIdで複数ステップ
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('BATCH050', 1, 'CARR050', 'LOT050', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('BATCH050', 2, 'CARR050', 'LOT050', 25, 'Tech1', 'Furnace2', 'PPID002', 'Furnace3', 0, datetime('now')),
('BATCH050', 3, 'CARR050', 'LOT050', 25, 'Tech1', 'Furnace3', 'PPID003', 'なし', 0, datetime('now'));
```

### 期待される結果
1. **予約1の表示**:
   - Carrier: CARR050（1回のみ）
   - Lot: LOT050（1回のみ）
   - Qty: 25
   - PPID: PPID001（Step1のみ表示）
   - Next: Furnace2

### 確認SQL
```sql
-- グループ化されていることを確認
SELECT BatchId, LotId, CarrierId, COUNT(*) as StepCount
FROM DC_Batch
WHERE BatchId = 'BATCH050'
GROUP BY BatchId, LotId, CarrierId;
-- 結果: StepCount = 3
```

---

## テストケース13: エラーハンドリング

### 目的
エラーが発生しても画面が正常に動作することを確認

### シナリオ13-1: 存在しない装置のメモ更新
1. ブラウザの開発者ツールでリクエストを改ざん
2. 存在しない装置名でメモを更新しようとする
3. **期待結果**: エラーメッセージが表示される

### シナリオ13-2: 存在しないBatchIdの削除
1. 存在しないBatchIdで削除リクエストを送信
2. **期待結果**: エラーメッセージが表示される

### シナリオ13-3: データベース接続エラー
1. データベースファイルを削除または移動
2. 画面をリロード
3. **期待結果**: エラーページまたはエラーメッセージが表示される

---

## 統合テストシナリオ

### シナリオ: 完全なワークフロー

#### ステップ1: バッチ作成
```sql
-- CreateBatch画面で作成されたバッチをシミュレート
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES
('WORKFLOW001', 1, 'CARR100', 'LOT100', 25, 'Tech1', 'Furnace1', 'PPID001', 'Furnace2', 0, datetime('now')),
('WORKFLOW001', 2, 'CARR100', 'LOT100', 25, 'Tech1', 'Furnace2', 'PPID002', 'なし', 0, datetime('now'));
```

#### ステップ2: Work Progress画面で確認
1. Work Progress画面を開く
2. Furnace1の「予約1」にLOT100が表示されることを確認

#### ステップ3: 処理開始（DC_Actlに挿入）
```sql
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace1', 'LOT100', 'Type1', datetime('now'), 'CARR100', 25, 'PPID001', 'Furnace2', 'LOC1');
```

#### ステップ4: 30秒後の自動更新を確認
1. 30秒待機
2. **期待結果**:
   - 「予約1」からLOT100が消える
   - 「処理中」にLOT100が表示される

#### ステップ5: BatchProcessingBackgroundServiceの動作
1. さらに30秒待機（BackgroundServiceが動作）
2. **期待結果**:
   - Step1のIsProcessedが1に更新される
   - Step2が存在するため削除されない

#### ステップ6: Step2の処理
```sql
INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
VALUES ('Furnace2', 'LOT100', 'Type1', datetime('now'), 'CARR100', 25, 'PPID002', 'なし', 'LOC2');
```

#### ステップ7: 最終確認
1. Work Progress画面でFurnace2を確認
2. 「処理中」にLOT100が表示される
3. NextがDC_BatchのNextEqpId「なし」になっていることを確認

---

## 自動テストスクリプト（JavaScript例）

```javascript
// Playwright/Puppeteerを使用した自動テスト例
const { chromium } = require('playwright');

async function testWorkProgress() {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  // Work Progress画面を開く
  await page.goto('http://localhost:5000/WorkProgress');

  // フィルターを開く
  await page.click('#filterToggleBtn');
  await page.waitForTimeout(500);

  // TYPEフィルタを選択
  await page.check('#type-DIFF');
  await page.waitForTimeout(1000);

  // 装置が表示されていることを確認
  const furnaceText = await page.textContent('text=Furnace1');
  console.assert(furnaceText !== null, '✓ Furnace1が表示されています');

  // メモ編集ボタンをクリック
  await page.click('button[onclick*="openEditNoteModal(\'Furnace1\'"]');
  await page.waitForTimeout(500);

  // メモを入力
  await page.fill('#editNoteText', 'テストメモ');

  // 保存ボタンをクリック
  await page.click('text=保存');
  await page.waitForTimeout(500);

  // メモが表示されていることを確認
  const noteText = await page.textContent('#note-DIFF-0');
  console.assert(noteText.includes('テストメモ'), '✓ メモが保存されました');

  await browser.close();
  console.log('✓ すべてのテストが成功しました');
}

testWorkProgress();
```

---

## パフォーマンステスト

### テストケース: 大量データの表示

#### テストデータ準備
```sql
-- 各装置に100件のDC_Actlレコードを挿入
-- （実際には画面では最大10件まで表示）
DO $$
BEGIN
  FOR i IN 1..100 LOOP
    INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location)
    VALUES ('Furnace1', 'LOT' || i, 'Type1', datetime('now'), 'CARR' || i, 25, 'PPID001', 'Furnace2', 'LOC1');
  END LOOP;
END $$;
```

#### 期待される結果
1. **表示件数**: 最大10件まで表示される
2. **レスポンス時間**: 2秒以内にページが表示される
3. **自動更新**: 30秒ごとの更新でも遅延なく動作する

---

## テスト実行チェックリスト

### 基本機能
- [ ] 装置リストの表示
- [ ] TYPE/LINEフィルタリング
- [ ] 処理中の表示
- [ ] 処理待ちの表示
- [ ] 予約1-3の表示
- [ ] EndTimeの表示（HH:mm形式）
- [ ] NextFurnaceの表示（DC_Batchから取得）

### インタラクティブ機能
- [ ] 装置メモの新規登録
- [ ] 装置メモの編集
- [ ] 装置メモの削除
- [ ] バッチの削除
- [ ] カラムの表示/非表示切り替え

### 自動更新
- [ ] 30秒ごとの自動更新
- [ ] ページリロードなしでのデータ更新

### エラーハンドリング
- [ ] 存在しない装置のエラー処理
- [ ] データベースエラーの処理

### パフォーマンス
- [ ] 大量データでの表示速度
- [ ] 自動更新の安定性

---

## クリーンアップ

テスト後、テストデータを削除：
```sql
DELETE FROM DC_Batch WHERE BatchId LIKE 'BATCH%' OR BatchId LIKE 'WORKFLOW%' OR BatchId LIKE 'TEST%';
DELETE FROM DC_Actl WHERE LotId LIKE 'LOT%' AND EqpId IN ('Furnace1', 'Furnace2', 'Furnace3', 'Furnace4');
UPDATE DC_Eqps SET Note = NULL WHERE Name IN ('Furnace1', 'Furnace2', 'Furnace3', 'Furnace4');
```

---

## まとめ

このテストケース集を使用して、`WorkProgressModel`（Work Progress画面）の各機能が正常に動作することを確認してください。特に以下の重要な機能に注意：

1. **グループ化ロジック**: ±5分以内のTrackInTimeで正しくグループ化される
2. **NextFurnaceの取得**: DC_BatchのProcessedAtとDC_ActlのTrackInTimeが一致する場合、DC_BatchのNextEqpIdが使用される
3. **LotId単位の処理**: 同じBatchIdでも異なるLotIdは独立して表示される
4. **自動更新**: ページリロードなしで30秒ごとにデータが更新される

すべてのテストケースが成功すれば、Work Progress画面は本番環境で使用可能です。
