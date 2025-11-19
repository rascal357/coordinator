# CreateBatchModel テストケース

## 目的
`CreateBatchModel`（Create Batch画面）が正常に動作することを確認するためのテストケース集

## テスト対象機能
- LotIdに基づくステップ情報の表示（DC_LotStepsから取得）
- PPID/EqpIdの選択肢表示と選択
- バッチID生成（タイムスタンプベース）
- NextEqpIdの自動設定（次ステップのEqpId）
- バリデーション（重複LotId、必須項目、同一EqpId重複）
- DC_BatchおよびDC_BatchMembersへの保存

## テスト環境の準備

### 前提条件
1. SQLiteデータベース（coordinator.db）が存在する
2. アプリケーションが起動している
3. DC_LotStepsテーブルにステップマスタが登録されている

### テストデータの準備
```sql
-- DC_LotStepsにテストデータを挿入
INSERT INTO DC_LotSteps (LotId, Step, EqpId, PPID)
VALUES
-- LOT001: Step1とStep2がある
('LOT001', 1, 'Furnace1', 'PPID001'),
('LOT001', 2, 'Furnace2', 'PPID002'),
-- LOT002: Step1のみ
('LOT002', 1, 'Furnace1', 'PPID001'),
-- LOT003: Step1からStep4まである
('LOT003', 1, 'Furnace1', 'PPID001'),
('LOT003', 2, 'Furnace2', 'PPID002'),
('LOT003', 3, 'Furnace3', 'PPID003'),
('LOT003', 4, 'Furnace4', 'PPID004'),
-- LOT004: 複数のPPID/EqpId選択肢がある
('LOT004', 1, 'Furnace1', 'PPID001'),
('LOT004', 1, 'Furnace1', 'PPID001A'),
('LOT004', 1, 'Furnace2', 'PPID002');

-- DC_Wipsにテストデータを挿入
INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
VALUES
(1, 'Tech1', 'CARR001', 'LOT001', 25, 'Part001', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001'),
(2, 'Tech1', 'CARR002', 'LOT002', 25, 'Part002', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001'),
(3, 'Tech2', 'CARR003', 'LOT003', 30, 'Part003', 'Stage1', 'Step1', 'Stage2', 'Step4', 'Furnace1', 'PPID001'),
(4, 'Tech1', 'CARR004', 'LOT004', 25, 'Part004', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001');
```

---

## テストケース1: 単一ロット・単一ステップのバッチ作成

### 目的
1つのロットで1ステップのみのバッチが正しく作成されることを確認

### テストシナリオ
1. WipLotList画面でLOT002を選択
2. Create Batchボタンをクリック
3. Create Batch画面が開く
4. **確認**: LOT002のStep1が表示される
5. **確認**: PPID001、Furnace1が自動選択されている
6. **確認**: NextEqpIdが「なし」（Step2が存在しないため）
7. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**:
   - BatchId: タイムスタンプ形式（例: 20251120123456789）
   - Step: 1
   - CarrierId: CARR002
   - LotId: LOT002
   - Qty: 25
   - Technology: Tech1
   - EqpId: Furnace1
   - PPID: PPID001
   - NextEqpId: なし
   - IsProcessed: 0
   - CreatedAt: 現在時刻

2. **画面遷移**: Work Progress画面にリダイレクト

### 確認SQL
```sql
SELECT * FROM DC_Batch WHERE LotId = 'LOT002' ORDER BY Step;
-- 結果: 1件のレコード
```

---

## テストケース2: 単一ロット・複数ステップのバッチ作成

### 目的
1つのロットで複数ステップのバッチが正しく作成され、NextEqpIdが設定されることを確認

### テストシナリオ
1. WipLotList画面でLOT001を選択
2. Create Batch画面が開く
3. **確認**: Step1とStep2が表示される
4. **確認**: Step1のNextEqpIdがFurnace2（Step2のEqpId）
5. **確認**: Step2のNextEqpIdが「なし」（Step3が存在しないため）
6. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**: 2件のレコード
   - Step1: EqpId=Furnace1, NextEqpId=Furnace2
   - Step2: EqpId=Furnace2, NextEqpId=なし
2. **同じBatchId**: 両方のレコードが同じBatchIdを持つ
3. **同じCreatedAt**: 両方のレコードが同じCreatedAtを持つ

### 確認SQL
```sql
SELECT BatchId, Step, EqpId, NextEqpId, CreatedAt
FROM DC_Batch
WHERE LotId = 'LOT001'
ORDER BY Step;
-- 結果: 2件、同じBatchId、同じCreatedAt
```

---

## テストケース3: 複数ロット・異なるステップ数のバッチ作成

### 目的
複数のロットで異なるステップ数のバッチが正しく作成されることを確認

### テストシナリオ
1. WipLotList画面でLOT001（2ステップ）とLOT002（1ステップ）を選択
2. Create Batch画面が開く
3. **確認**:
   - LOT001: Step1, Step2が表示
   - LOT002: Step1のみが表示
4. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**: 3件のレコード
   - LOT001-Step1: NextEqpId=Furnace2
   - LOT001-Step2: NextEqpId=なし
   - LOT002-Step1: NextEqpId=なし
2. **同じBatchId**: すべてのレコードが同じBatchIdを持つ

### 確認SQL
```sql
SELECT BatchId, LotId, Step, NextEqpId
FROM DC_Batch
WHERE LotId IN ('LOT001', 'LOT002')
ORDER BY LotId, Step;
-- 結果: 3件、すべて同じBatchId
```

---

## テストケース4: 4ステップのバッチ作成

### 目的
最大4ステップのバッチが正しく作成されることを確認

### テストシナリオ
1. WipLotList画面でLOT003を選択
2. Create Batch画面が開く
3. **確認**: Step1~Step4がすべて表示される
4. **確認**: NextEqpIdの連鎖
   - Step1: NextEqpId=Furnace2
   - Step2: NextEqpId=Furnace3
   - Step3: NextEqpId=Furnace4
   - Step4: NextEqpId=なし
5. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**: 4件のレコード
2. **NextEqpIdの連鎖**: 各ステップのNextEqpIdが次のステップのEqpIdと一致

### 確認SQL
```sql
SELECT Step, EqpId, NextEqpId
FROM DC_Batch
WHERE LotId = 'LOT003'
ORDER BY Step;
-- 結果:
-- Step1: EqpId=Furnace1, NextEqpId=Furnace2
-- Step2: EqpId=Furnace2, NextEqpId=Furnace3
-- Step3: EqpId=Furnace3, NextEqpId=Furnace4
-- Step4: EqpId=Furnace4, NextEqpId=なし
```

---

## テストケース5: PPID/EqpIdの手動選択

### 目的
複数の選択肢がある場合、手動で選択できることを確認

### テストシナリオ
1. WipLotList画面でLOT004を選択
2. Create Batch画面が開く
3. **確認**: Step1にPPIDの選択肢が複数ある
   - PPID001（Furnace1）
   - PPID001A（Furnace1）
   - PPID002（Furnace2）
4. PPID001Aを選択
5. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**:
   - PPID: PPID001A
   - EqpId: Furnace1
2. **選択した値が保存**: デフォルトではなく選択した値が使用される

### 確認SQL
```sql
SELECT PPID, EqpId FROM DC_Batch WHERE LotId = 'LOT004';
-- 結果: PPID=PPID001A, EqpId=Furnace1
```

---

## テストケース6: 必須項目バリデーション

### 目的
PPIDまたはEqpIdが選択されていない場合、エラーが表示されることを確認

### テストシナリオ
1. WipLotList画面でLOT001を選択
2. Create Batch画面が開く
3. Step1のPPIDを「選択してください」に変更
4. バッチ作成ボタンをクリック

### 期待される結果
1. **エラーメッセージ**: 「LOT001 のStep 1のPPIDを選択してください」
2. **画面遷移なし**: Create Batch画面のまま
3. **データ保存なし**: DC_Batchにレコードが追加されない

### 確認方法
1. エラーメッセージが赤色で表示される
2. データベースを確認してレコードが追加されていないことを確認

---

## テストケース7: 重複LotIdバリデーション

### 目的
既にDC_Batchに存在するLotIdは追加できないことを確認

### テストデータ準備
```sql
-- LOT005を既にDC_Batchに登録
INSERT INTO DC_Batch (BatchId, Step, CarrierId, LotId, Qty, Technology, EqpId, PPID, NextEqpId, IsProcessed, CreatedAt)
VALUES ('EXIST001', 1, 'CARR005', 'LOT005', 25, 'Tech1', 'Furnace1', 'PPID001', 'なし', 0, datetime('now'));

-- DC_LotStepsにLOT005を追加
INSERT INTO DC_LotSteps (LotId, Step, EqpId, PPID)
VALUES ('LOT005', 1, 'Furnace1', 'PPID001');

-- DC_WipsにLOT005を追加
INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
VALUES (5, 'Tech1', 'CARR005', 'LOT005', 25, 'Part005', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001');
```

### テストシナリオ
1. WipLotList画面でLOT005を選択
2. Create Batch画面が開く
3. バッチ作成ボタンをクリック

### 期待される結果
1. **エラーメッセージ**: 「LOT005は以下のバッチに含まれています。workProgress画面で確認してください。BatchId: EXIST001, LotId: LOT005, Steps: 1」
2. **画面遷移なし**: Create Batch画面のまま
3. **データ保存なし**: 新しいバッチは作成されない

---

## テストケース8: 同一EqpId重複バリデーション

### 目的
同じロットのStep1~4で同じEqpIdが重複選択された場合、エラーが表示されることを確認

### テストデータ準備
```sql
-- LOT006: Step1とStep2で同じ装置を選択できるように設定
INSERT INTO DC_LotSteps (LotId, Step, EqpId, PPID)
VALUES
('LOT006', 1, 'Furnace1', 'PPID001'),
('LOT006', 2, 'Furnace1', 'PPID001B');  -- 同じFurnace1

INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
VALUES (6, 'Tech1', 'CARR006', 'LOT006', 25, 'Part006', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001');
```

### テストシナリオ
1. WipLotList画面でLOT006を選択
2. Create Batch画面が開く
3. **確認**: Step1とStep2が表示される
4. Step1: Furnace1を選択（デフォルト）
5. Step2: Furnace1を選択
6. バッチ作成ボタンをクリック

### 期待される結果
1. **エラーメッセージ**: 「LOT006 のStep1~4の中で同じ装置（Furnace1）が重複して選択されています」
2. **画面遷移なし**: Create Batch画面のまま
3. **データ保存なし**: バッチは作成されない

---

## テストケース9: BatchId生成の一意性

### 目的
異なるタイミングで作成されたバッチが異なるBatchIdを持つことを確認

### テストシナリオ
1. WipLotList画面でLOT001を選択してバッチ作成
2. BatchId（例: 20251120123456789）を記録
3. 1秒待機
4. 再度WipLotList画面でLOT002を選択してバッチ作成
5. 新しいBatchId（例: 20251120123457890）を記録

### 期待される結果
1. **異なるBatchId**: 2つのバッチが異なるBatchIdを持つ
2. **タイムスタンプ形式**: BatchIdが「yyyyMMddHHmmssfff」形式

### 確認SQL
```sql
SELECT DISTINCT BatchId FROM DC_Batch ORDER BY CreatedAt DESC LIMIT 2;
-- 結果: 2つの異なるBatchId
```

---

## テストケース10: CreatedAtの一貫性

### 目的
同じバッチ内のすべてのレコードが同じCreatedAtを持つことを確認

### テストシナリオ
1. WipLotList画面でLOT003（4ステップ）を選択
2. バッチ作成ボタンをクリック

### 期待される結果
1. **同じCreatedAt**: 4つのレコードすべてが同じCreatedAtを持つ
2. **ミリ秒まで一致**: ミリ秒単位まで完全に一致

### 確認SQL
```sql
SELECT DISTINCT CreatedAt, COUNT(*) as RecordCount
FROM DC_Batch
WHERE LotId = 'LOT003'
GROUP BY CreatedAt;
-- 結果: 1行、RecordCount=4
```

---

## テストケース11: Carrier重複排除

### 目的
同じCarrierが複数回選択された場合でも、1回のみ処理されることを確認

### テストデータ準備
```sql
-- 同じCarrierを持つ複数のLotId
INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
VALUES
(7, 'Tech1', 'CARR999', 'LOT007', 25, 'Part007', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001'),
(8, 'Tech1', 'CARR999', 'LOT008', 25, 'Part008', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001');

INSERT INTO DC_LotSteps (LotId, Step, EqpId, PPID)
VALUES
('LOT007', 1, 'Furnace1', 'PPID001'),
('LOT008', 1, 'Furnace1', 'PPID001');
```

### テストシナリオ
1. WipLotList画面でLOT007とLOT008を選択
2. **確認**: 両方とも同じCarrier（CARR999）
3. Create Batch画面が開く
4. バッチ作成ボタンをクリック

### 期待される結果
1. **重複排除**: CARR999は1回のみ処理される
2. **DC_Batch**: 2件のレコード（LOT007とLOT008で各1件）

### 注意
現在の実装では、LotIdベースで処理されるため、同じCarrierでも異なるLotIdなら両方とも処理されます。

---

## テストケース12: WipDataの引き継ぎ

### 目的
WipLotListから渡されたWipDataがCreate Batch画面で正しく使用されることを確認

### テストシナリオ
1. WipLotList画面でLOT001を選択
2. TempDataにWipDataが保存される
3. Create Batch画面が開く
4. **確認**: Carrier、Qty、Technologyが表示される
5. バッチ作成ボタンをクリック

### 期待される結果
1. **DC_Batch**:
   - CarrierId: CARR001（WipDataから取得）
   - Qty: 25（WipDataから取得）
   - Technology: Tech1（WipDataから取得）

### 確認SQL
```sql
SELECT CarrierId, Qty, Technology FROM DC_Batch WHERE LotId = 'LOT001';
-- 結果: CarrierId=CARR001, Qty=25, Technology=Tech1
```

---

## テストケース13: Recipe情報の取得（API）

### 目的
EqpIdとPPIDを選択した際に、Recipe情報が取得できることを確認

### テストシナリオ
1. Create Batch画面を開く
2. PPIDドロップダウンを変更
3. **確認**: JavaScriptでAPIが呼ばれる（`/CreateBatch?handler=RecipeInfo&eqpId=...&ppid=...`）
4. **確認**: Recipe情報（OkNg、SpecialNotes等）が表示される

### 期待される結果
1. **APIレスポンス**: JSON形式でRecipe情報が返される
```json
{
  "eqpId": "Furnace1",
  "ppid": "PPID001",
  "okNg": "OK",
  "specialNotes": "なし",
  "trenchDummy": "必要",
  "dmType": "AAA",
  "twType": "BBB",
  "posA": "○",
  "posB": "ー",
  "posC": "ー",
  "posD": "○",
  "posE": "○",
  "posF": "ー"
}
```

### 確認方法
1. ブラウザの開発者ツール（F12）を開く
2. Networkタブを確認
3. RecipeInfo APIが呼ばれていることを確認

---

## テストケース14: ログ出力

### 目的
バッチ作成時に適切なログが出力されることを確認

### テストシナリオ
1. WipLotList画面でLOT003（4ステップ）を選択
2. バッチ作成ボタンをクリック
3. アプリケーションログを確認

### 期待されるログ
```
=== Batch Created Successfully ===
BatchId: 20251120123456789
Created At: 2025/11/20 12:34:56.789

--- DC_Batch Records (4) ---
  [Step 1] LotId: LOT003, Carrier: CARR003, Qty: 30, Technology: Tech2, EqpId: Furnace1, PPID: PPID001, NextEqpId: Furnace2
  [Step 2] LotId: LOT003, Carrier: CARR003, Qty: 30, Technology: Tech2, EqpId: Furnace2, PPID: PPID002, NextEqpId: Furnace3
  [Step 3] LotId: LOT003, Carrier: CARR003, Qty: 30, Technology: Tech2, EqpId: Furnace3, PPID: PPID003, NextEqpId: Furnace4
  [Step 4] LotId: LOT003, Carrier: CARR003, Qty: 30, Technology: Tech2, EqpId: Furnace4, PPID: PPID004, NextEqpId: なし
=====================================
```

---

## エラーシナリオ

### シナリオE1: LotIdパラメータなし
1. URLを直接入力: `/CreateBatch`（LotIdsパラメータなし）
2. **期待結果**: Indexページにリダイレクト

### シナリオE2: 存在しないLotId
1. URLを入力: `/CreateBatch?LotIds=NOTEXIST001`
2. **期待結果**: ステップ情報が表示されず、空のフォームが表示される

### シナリオE3: DC_LotStepsにデータなし
1. DC_LotStepsにレコードがないLotIdを渡す
2. **期待結果**: ステップが表示されず、バッチ作成できない

---

## パフォーマンステスト

### テストケース: 大量ロットのバッチ作成

#### テストデータ準備
```sql
-- 100個のLotIdを準備
DO $$
BEGIN
  FOR i IN 1..100 LOOP
    INSERT INTO DC_LotSteps (LotId, Step, EqpId, PPID)
    VALUES ('LOTPERF' || i, 1, 'Furnace1', 'PPID001');

    INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
    VALUES (i, 'Tech1', 'CARRPERF' || i, 'LOTPERF' || i, 25, 'PartPerf', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace1', 'PPID001');
  END LOOP;
END $$;
```

#### 期待される結果
1. **表示速度**: 画面表示が5秒以内
2. **保存速度**: バッチ作成が10秒以内
3. **メモリ使用**: アプリケーションがクラッシュしない

---

## 統合テストシナリオ

### シナリオ: WipLotListからCreateBatchへの完全フロー

#### ステップ1: WipLotList画面
1. Dashboard画面でFurnace1をクリック
2. WipLotList画面が開く
3. LOT001, LOT002, LOT003を選択
4. Create Batchボタンをクリック

#### ステップ2: CreateBatch画面
1. 3つのロットのステップ情報が表示される
2. 各ステップのPPID/EqpIdを確認
3. バッチ作成ボタンをクリック

#### ステップ3: WorkProgress画面
1. Work Progress画面にリダイレクトされる
2. Furnace1の「予約1」列に新しいバッチが表示される
3. 3つのLotIdが表示される

---

## テスト実行チェックリスト

### 基本機能
- [ ] ステップ情報の表示（DC_LotStepsから取得）
- [ ] PPID/EqpIdの選択肢表示
- [ ] デフォルト値の自動選択
- [ ] NextEqpIdの自動設定
- [ ] BatchId生成（タイムスタンプ）
- [ ] DC_Batchへの保存

### バリデーション
- [ ] 必須項目チェック（PPID、EqpId）
- [ ] 重複LotIdチェック
- [ ] 同一EqpId重複チェック

### データ整合性
- [ ] 同じBatchIdの一貫性
- [ ] 同じCreatedAtの一貫性
- [ ] NextEqpIdの連鎖
- [ ] WipDataの引き継ぎ

### エラーハンドリング
- [ ] LotIdパラメータなし
- [ ] 存在しないLotId
- [ ] DC_LotStepsにデータなし

### ログ
- [ ] バッチ作成成功ログ
- [ ] エラーログ

---

## クリーンアップ

テスト後、テストデータを削除：
```sql
DELETE FROM DC_Batch WHERE BatchId LIKE 'EXIST%' OR LotId LIKE 'LOT%';
DELETE FROM DC_LotSteps WHERE LotId LIKE 'LOT%';
DELETE FROM DC_Wips WHERE LotId LIKE 'LOT%';
```

---

## まとめ

このテストケース集を使用して、`CreateBatchModel`（Create Batch画面）の各機能が正常に動作することを確認してください。特に以下の重要な機能に注意：

1. **NextEqpIdの連鎖**: 各ステップのNextEqpIdが次のステップのEqpIdと一致する
2. **バリデーション**: 重複LotId、必須項目、同一EqpId重複が正しくチェックされる
3. **BatchId生成**: タイムスタンプベースで一意のBatchIdが生成される
4. **CreatedAtの一貫性**: 同じバッチ内のすべてのレコードが同じCreatedAtを持つ

すべてのテストケースが成功すれば、Create Batch画面は本番環境で使用可能です。
