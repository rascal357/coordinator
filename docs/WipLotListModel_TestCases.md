# WipLotListModel テストケース

## 目的
`WipLotListModel`（WIP Lot List画面）が正常に動作することを確認するためのテストケース集

## テスト対象機能
- 装置指定によるWIPリストの表示（DC_Wipsから取得）
- ソート機能（Priority、LotId、Carrier等）
- 選択機能（チェックボックス）
- Carrier重複チェック
- 最低1件選択チェック
- CreateBatch画面へのデータ引き渡し（TempData）

## テスト環境の準備

### 前提条件
1. SQLiteデータベース（coordinator.db）が存在する
2. アプリケーションが起動している
3. DC_Wipsテーブルにデータが登録されている

### テストデータの準備
```sql
-- DC_Wipsにテストデータを挿入
INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
VALUES
-- Furnace1向けのWIP
(1, 'Tech1', 'CARR001', 'LOT001', 25, 'Part001', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001'),
(2, 'Tech1', 'CARR002', 'LOT002', 30, 'Part002', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001'),
(3, 'Tech2', 'CARR003', 'LOT003', 20, 'Part003', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID002'),
(4, 'Tech1', 'CARR004', 'LOT004', 25, 'Part004', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001'),
-- Furnace2向けのWIP
(5, 'Tech1', 'CARR005', 'LOT005', 25, 'Part005', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace2', 'PPID003'),
(6, 'Tech2', 'CARR006', 'LOT006', 30, 'Part006', 'Stage1', 'Step1', 'Stage2', 'Step2', 'Furnace2', 'PPID004'),
-- 重複Carrier（テスト用）
(7, 'Tech1', 'CARR001', 'LOT007', 25, 'Part007', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001');
```

---

## テストケース1: 装置指定によるWIPリスト表示

### 目的
指定した装置（TargetEqpId）のWIPのみが表示されることを確認

### テストシナリオ
1. Dashboard画面でFurnace1をクリック
2. WipLotList画面が開く（EqpName=Furnace1）

### 期待される結果
1. **表示されるWIP**: Furnace1向けのWIPのみ（LOT001, LOT002, LOT003, LOT004, LOT007）
2. **表示されないWIP**: Furnace2向けのWIP（LOT005, LOT006）
3. **カラム表示**:
   - Priority
   - Technology
   - Carrier
   - LotId
   - Qty
   - PartName
   - CurrentStage
   - CurrentStep
   - TargetStage
   - TargetStep
   - TargetPPID

### 確認SQL
```sql
SELECT * FROM DC_Wips WHERE TargetEqpId = 'Furnace1' ORDER BY Priority;
-- 結果: 5件（LOT001, LOT002, LOT003, LOT004, LOT007）
```

---

## テストケース2: ソート機能（Priority昇順）

### 目的
Priorityでソートできることを確認（デフォルト）

### テストシナリオ
1. WipLotList画面を開く（EqpName=Furnace1）
2. **確認**: デフォルトでPriority昇順にソートされている

### 期待される結果
1. **表示順序**:
   - 1位: LOT001 (Priority=1)
   - 2位: LOT002 (Priority=2)
   - 3位: LOT003 (Priority=3)
   - 4位: LOT004 (Priority=4)
   - 5位: LOT007 (Priority=7)

---

## テストケース3: ソート機能（LotId）

### 目的
LotIdでソートできることを確認

### テストシナリオ
1. WipLotList画面を開く
2. LotIdカラムのヘッダーをクリック

### 期待される結果
1. **1回目クリック**: LotId昇順（LOT001, LOT002, LOT003, LOT004, LOT007）
2. **2回目クリック**: LotId降順（LOT007, LOT004, LOT003, LOT002, LOT001）

---

## テストケース4: ソート機能（Carrier）

### 目的
Carrierでソートできることを確認

### テストシナリオ
1. WipLotList画面を開く
2. Carrierカラムのヘッダーをクリック

### 期待される結果
1. **昇順**: CARR001（2件）, CARR002, CARR003, CARR004
2. **重複Carrier**: CARR001のLOT001とLOT007が連続して表示される

---

## テストケース5: 単一ロット選択

### 目的
1つのロットを選択してCreateBatchに渡せることを確認

### テストシナリオ
1. WipLotList画面を開く（EqpName=Furnace1）
2. LOT001のチェックボックスをチェック
3. Create Batchボタンをクリック

### 期待される結果
1. **画面遷移**: CreateBatch画面にリダイレクト
2. **URLパラメータ**: `?LotIds=LOT001&EqpName=Furnace1`
3. **TempData**:
   - SelectedWipData: LOT001のWipDataが含まれる
   - Carrier: CARR001
   - Qty: 25
   - Technology: Tech1

---

## テストケース6: 複数ロット選択

### 目的
複数のロットを選択してCreateBatchに渡せることを確認

### テストシナリオ
1. WipLotList画面を開く
2. LOT001, LOT002, LOT003のチェックボックスをチェック
3. Create Batchボタンをクリック

### 期待される結果
1. **画面遷移**: CreateBatch画面にリダイレクト
2. **URLパラメータ**: `?LotIds=LOT001,LOT002,LOT003&EqpName=Furnace1`
3. **TempData**: 3つのWipDataが含まれる

---

## テストケース7: 選択なしバリデーション

### 目的
1つもロットを選択していない場合、エラーが表示されることを確認

### テストシナリオ
1. WipLotList画面を開く
2. どのチェックボックスもチェックしない
3. Create Batchボタンをクリック

### 期待される結果
1. **エラーメッセージ**: 「少なくとも1つのロットを選択してください」
2. **画面遷移なし**: WipLotList画面のまま
3. **TempData**: データが保存されない

---

## テストケース8: Carrier重複チェック（警告）

### 目的
同じCarrierを持つロットを選択した場合、警告が表示されることを確認

### テストシナリオ
1. WipLotList画面を開く
2. LOT001とLOT007を選択（両方ともCARR001）
3. Create Batchボタンをクリック

### 期待される結果（2つのパターン）

#### パターンA: エラーで停止
1. **エラーメッセージ**: 「重複するCarrier（CARR001）が選択されています：LOT001, LOT007」
2. **画面遷移なし**: WipLotList画面のまま

#### パターンB: 警告のみ（処理継続）
1. **警告メッセージ**: 「CARR001が重複しています」
2. **画面遷移**: CreateBatch画面に進む
3. **重複排除**: CreateBatch側で重複を自動排除

### 確認方法
現在の仕様を確認して、どちらのパターンが実装されているか確認

---

## テストケース9: チェックボックスの状態保持

### 目的
ソート後もチェックボックスの状態が保持されることを確認

### テストシナリオ
1. WipLotList画面を開く
2. LOT001とLOT003をチェック
3. LotIdカラムでソート
4. **確認**: LOT001とLOT003のチェック状態が保持されている

### 期待される結果
1. **チェック維持**: ソート後もチェックされたまま
2. **JavaScriptによる制御**: ページリロードなしでソートとチェック状態保持

---

## テストケース10: EqpNameパラメータなし

### 目的
EqpNameパラメータがない場合の動作を確認

### テストシナリオ
1. URLを直接入力: `/WipLotList`（EqpNameパラメータなし）

### 期待される結果
1. **画面表示**: すべてのWIPが表示される（装置フィルタなし）
2. **または**: エラーメッセージが表示される
3. **または**: Indexページにリダイレクト

### 確認方法
現在の実装を確認して、どの動作が正しいか確認

---

## テストケース11: TempDataの引き渡し

### 目的
選択されたWIPデータがTempData経由でCreateBatchに正しく渡されることを確認

### テストシナリオ
1. WipLotList画面でLOT001, LOT002を選択
2. Create Batchボタンをクリック
3. CreateBatch画面で開発者ツール（F12）を開く
4. ブラウザのストレージを確認

### 期待される結果
1. **TempData**: SelectedWipDataキーに以下のJSON形式でデータが保存される
```json
[
  {
    "lotId": "LOT001",
    "carrier": "CARR001",
    "qty": 25,
    "technology": "Tech1",
    "state": "現在の状態",
    "next1": "次工程1",
    "next2": "次工程2",
    "next3": "次工程3"
  },
  {
    "lotId": "LOT002",
    "carrier": "CARR002",
    "qty": 30,
    "technology": "Tech1",
    "state": "現在の状態",
    "next1": "次工程1",
    "next2": "次工程2",
    "next3": "次工程3"
  }
]
```

### 確認方法
CreateBatch画面でCarrier、Qty、Technologyが正しく表示されることを確認

---

## テストケース12: 全選択/全解除機能（オプション）

### 目的
全選択/全解除ボタンが正しく動作することを確認（実装されている場合）

### テストシナリオ
1. WipLotList画面を開く
2. 「全選択」ボタンをクリック
3. **確認**: すべてのチェックボックスがチェックされる
4. 「全解除」ボタンをクリック
5. **確認**: すべてのチェックボックスが解除される

### 期待される結果
1. **全選択**: すべてのWIPが選択される
2. **全解除**: すべてのチェックが外れる

---

## テストケース13: ページネーション（将来機能）

### 目的
大量のWIPがある場合、ページネーションが動作することを確認

### テストデータ準備
```sql
-- 100件のWIPを挿入
DO $$
BEGIN
  FOR i IN 1..100 LOOP
    INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
    VALUES (i, 'Tech1', 'CARR' || i, 'LOT' || i, 25, 'Part' || i, 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001');
  END LOOP;
END $$;
```

### 期待される結果
1. **1ページ**: 20件表示（または設定した件数）
2. **ページ移動**: 次ページ/前ページボタンで移動可能
3. **選択維持**: ページを移動しても選択状態が保持される

---

## エラーシナリオ

### シナリオE1: 存在しない装置
1. URLを入力: `/WipLotList?EqpName=NOTEXIST`
2. **期待結果**: WIPが0件表示、またはエラーメッセージ

### シナリオE2: データベース接続エラー
1. データベースファイルを削除または移動
2. WipLotList画面を開く
3. **期待結果**: エラーページまたはエラーメッセージ

### シナリオE3: 無効なLotId選択
1. ブラウザの開発者ツールでフォームデータを改ざん
2. 存在しないLotIdを送信
3. **期待結果**: エラーメッセージまたは無視される

---

## パフォーマンステスト

### テストケース: 大量WIPの表示

#### テストデータ準備
```sql
-- 1000件のWIPを挿入
DO $$
BEGIN
  FOR i IN 1..1000 LOOP
    INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID)
    VALUES (i, 'Tech1', 'CARR' || i, 'LOTPERF' || i, 25, 'PartPerf', 'Stage1', 'Step1', 'Stage2', 'Step1', 'Furnace1', 'PPID001');
  END LOOP;
END $$;
```

#### 期待される結果
1. **表示速度**: 3秒以内にページが表示される
2. **ソート速度**: 1秒以内にソートが完了する
3. **選択速度**: チェックボックスの操作が遅延なく動作する

---

## 統合テストシナリオ

### シナリオ: DashboardからCreateBatchまでの完全フロー

#### ステップ1: Dashboard画面
1. Dashboard画面を開く
2. TYPE「DIFF」、LINE「A」でフィルタ
3. Furnace1をクリック

#### ステップ2: WipLotList画面
1. Furnace1のWIPリストが表示される
2. Priority順にソートされている
3. LOT001, LOT002, LOT003を選択
4. Create Batchボタンをクリック

#### ステップ3: CreateBatch画面
1. 3つのロットのステップ情報が表示される
2. Carrier、Qty、Technologyが正しく表示される
3. バッチ作成ボタンをクリック

#### ステップ4: WorkProgress画面
1. Work Progress画面にリダイレクトされる
2. 新しいバッチが「予約1」に表示される

---

## UIテスト

### テストケース: レスポンシブデザイン

#### テストシナリオ
1. デスクトップブラウザでWipLotList画面を開く
2. ブラウザウィンドウを縮小
3. タブレットサイズ（768px）で確認
4. スマートフォンサイズ（375px）で確認

#### 期待される結果
1. **デスクトップ**: すべてのカラムが表示される
2. **タブレット**: 重要なカラムのみ表示、横スクロール可能
3. **スマートフォン**: カード形式で表示、縦スクロール

---

## アクセシビリティテスト

### テストケース: キーボード操作

#### テストシナリオ
1. Tabキーでフォーカス移動
2. Spaceキーでチェックボックスを操作
3. Enterキーでボタンをクリック

#### 期待される結果
1. **フォーカス順序**: 論理的な順序でフォーカスが移動
2. **キーボード操作**: マウスなしですべての操作が可能
3. **フォーカス表示**: フォーカスされている要素が視覚的に明確

---

## テスト実行チェックリスト

### 基本機能
- [ ] 装置指定によるWIPリスト表示
- [ ] Priority順のデフォルトソート
- [ ] カラムヘッダーによるソート
- [ ] チェックボックスによる選択
- [ ] Create Batchボタン

### バリデーション
- [ ] 選択なしエラー
- [ ] Carrier重複チェック

### データ引き渡し
- [ ] TempDataへのWipData保存
- [ ] CreateBatch画面での受け取り

### ソート機能
- [ ] Priority昇順/降順
- [ ] LotId昇順/降順
- [ ] Carrier昇順/降順
- [ ] Technology昇順/降順

### UI/UX
- [ ] チェック状態の保持（ソート後）
- [ ] レスポンシブデザイン
- [ ] キーボード操作

### エラーハンドリング
- [ ] EqpNameパラメータなし
- [ ] 存在しない装置
- [ ] データベースエラー

---

## 自動テストスクリプト（Playwright例）

```javascript
const { test, expect } = require('@playwright/test');

test('WipLotList: 複数ロット選択とバッチ作成', async ({ page }) => {
  // WipLotList画面を開く
  await page.goto('http://localhost:5000/WipLotList?EqpName=Furnace1');

  // LOT001とLOT002を選択
  await page.check('input[value="LOT001"]');
  await page.check('input[value="LOT002"]');

  // Create Batchボタンをクリック
  await page.click('button:has-text("Create Batch")');

  // CreateBatch画面に遷移することを確認
  await expect(page).toHaveURL(/\/CreateBatch/);

  // URLパラメータを確認
  const url = page.url();
  expect(url).toContain('LotIds=LOT001,LOT002');
  expect(url).toContain('EqpName=Furnace1');

  console.log('✓ テスト成功: 複数ロット選択とバッチ作成');
});

test('WipLotList: 選択なしエラー', async ({ page }) => {
  await page.goto('http://localhost:5000/WipLotList?EqpName=Furnace1');

  // どのチェックボックスもチェックしない
  // Create Batchボタンをクリック
  await page.click('button:has-text("Create Batch")');

  // エラーメッセージが表示されることを確認
  const errorMessage = await page.textContent('.alert-danger, .error-message');
  expect(errorMessage).toContain('少なくとも1つのロットを選択してください');

  console.log('✓ テスト成功: 選択なしエラー');
});

test('WipLotList: ソート機能', async ({ page }) => {
  await page.goto('http://localhost:5000/WipLotList?EqpName=Furnace1');

  // LotIdカラムのヘッダーをクリック
  await page.click('th:has-text("LotId")');

  // 1秒待機（ソート完了を待つ）
  await page.waitForTimeout(1000);

  // 最初の行のLotIdを取得
  const firstLotId = await page.textContent('tbody tr:first-child td:nth-child(4)'); // LotIdは4列目と仮定

  // LOT001が最初に表示されることを確認（昇順）
  expect(firstLotId).toContain('LOT001');

  console.log('✓ テスト成功: ソート機能');
});
```

---

## クリーンアップ

テスト後、テストデータを削除：
```sql
DELETE FROM DC_Wips WHERE LotId LIKE 'LOT%' OR LotId LIKE 'LOTPERF%';
```

---

## まとめ

このテストケース集を使用して、`WipLotListModel`（WIP Lot List画面）の各機能が正常に動作することを確認してください。特に以下の重要な機能に注意：

1. **装置フィルタリング**: TargetEqpIdで正しくフィルタリングされる
2. **ソート機能**: 各カラムでソートが正しく動作する
3. **バリデーション**: 選択なし、Carrier重複が正しくチェックされる
4. **データ引き渡し**: TempData経由でCreateBatchに正しくデータが渡される

すべてのテストケースが成功すれば、WIP Lot List画面は本番環境で使用可能です。
