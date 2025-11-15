# Coordinator テストドキュメント

## 概要

このディレクトリには、Coordinatorシステムの各画面の機能テストケースが含まれています。
テストフレームワークとしてxUnitを使用し、Entity Framework CoreのInMemoryデータベースを利用して単体テストを実行します。

## テストプロジェクト構成

```
Tests/
├── Coordinator.Tests/
│   ├── Pages/
│   │   ├── IndexPageTests.cs          # Dashboard画面のテスト
│   │   ├── WipLotListPageTests.cs     # WIP Lot List画面のテスト
│   │   ├── CreateBatchPageTests.cs    # Create Batch画面のテスト
│   │   └── WorkProgressPageTests.cs   # Work Progress画面のテスト
│   ├── Coordinator.Tests.csproj
│   └── ...
└── README.md
```

## 使用技術

- **テストフレームワーク**: xUnit 2.4.2以上
- **モックライブラリ**: Moq 4.20.x
- **データベース**: Entity Framework Core InMemory 8.0.0
- **統合テスト**: Microsoft.AspNetCore.Mvc.Testing 8.0.0

## テスト実行方法

### 基本的なテスト実行

#### 1. すべてのテストを実行

```bash
# プロジェクトルートから実行
cd /home/tomo/coordinator
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj
```

**実行結果例:**
```
Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 0.9527 Seconds
```

#### 2. ビルドしてからテストを実行

```bash
# ビルド
~/.dotnet/dotnet build Tests/Coordinator.Tests/Coordinator.Tests.csproj

# テスト実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build
```

### 特定のテストを実行

#### 3. 特定のテストクラスを実行

```bash
# Index (Dashboard) 画面のテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~IndexPageTests"

# WIP Lot List 画面のテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~WipLotListPageTests"

# Create Batch 画面のテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~CreateBatchPageTests"

# Work Progress 画面のテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~WorkProgressPageTests"
```

#### 4. 特定のテストメソッドを実行

```bash
# 日本語のテスト名で実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnGetAsync_装置一覧が正常に取得できること"

# 複数のテストを実行（OR条件）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnGetAsync_装置一覧が正常に取得できること|FullyQualifiedName~OnGetAsync_TYPEフィルタリングが正常に動作すること"
```

#### 5. カテゴリ別にテストを実行

```bash
# 名前に "フィルタリング" を含むテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~フィルタリング"

# 名前に "OnGetAsync" を含むテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnGetAsync"

# 名前に "OnPost" を含むテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnPost"
```

### テスト結果の表示オプション

#### 6. 詳細なログを表示

```bash
# 詳細なログ出力
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "console;verbosity=detailed"

# 通常のログ出力（デフォルト）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "console;verbosity=normal"

# 最小限のログ出力
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "console;verbosity=minimal"
```

#### 7. テスト結果をファイルに出力

```bash
# TRX形式で出力（Visual Studio Test Results）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "trx;LogFileName=test-results.trx"

# HTML形式で出力（要追加パッケージ）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "html;LogFileName=test-results.html"
```

### テスト実行時の便利なオプション

#### 8. 並列実行の制御

```bash
# 並列実行を無効化（デバッグ時に便利）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "console;verbosity=detailed" \
  -- xUnit.ParallelizeTestCollections=false
```

#### 9. カバレッジ収集

```bash
# コードカバレッジを収集（coverletが必要）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --collect:"XPlat Code Coverage"
```

### よく使うコマンド集

#### クイックテスト（ビルドスキップ）

```bash
# 前回のビルド結果を使用してテストのみ実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build --no-restore
```

#### 失敗したテストの詳細表示

```bash
# 失敗したテストの詳細なスタックトレースを表示
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --logger "console;verbosity=detailed" \
  --blame-crash
```

#### 画面別テスト実行のエイリアス

開発時によく使うコマンドは、シェルエイリアスとして設定すると便利です：

```bash
# .bashrc または .zshrc に追加
alias test-all='cd /home/tomo/coordinator && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj'
alias test-index='cd /home/tomo/coordinator && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~IndexPageTests"'
alias test-wip='cd /home/tomo/coordinator && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~WipLotListPageTests"'
alias test-batch='cd /home/tomo/coordinator && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~CreateBatchPageTests"'
alias test-progress='cd /home/tomo/coordinator && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~WorkProgressPageTests"'
```

使用例：
```bash
test-all      # すべてのテストを実行
test-index    # Index画面のテストのみ実行
test-wip      # WipLotList画面のテストのみ実行
```

## 各画面のテストケース

### 1. Index (Dashboard) ページ - IndexPageTests.cs

**テスト対象機能:**
- 装置一覧の表示
- TYPEフィルタリング
- LINEフィルタリング
- 複合フィルタリング

**テストケース:**

| テスト名 | テスト内容 | 期待結果 |
|---------|----------|---------|
| OnGetAsync_装置一覧が正常に取得できること | 装置一覧を取得 | TYPE別にグルーピングされた装置リストが取得できる |
| OnGetAsync_TYPEフィルタリングが正常に動作すること | 特定TYPEでフィルタ | 指定したTYPEの装置のみが表示される |
| OnGetAsync_LINEフィルタリングが正常に動作すること | 特定LINEでフィルタ | 指定したLINEの装置のみが表示される |
| OnGetAsync_TYPE_LINE複合フィルタリングが正常に動作すること | TYPEとLINEの両方でフィルタ | 両方の条件を満たす装置のみが表示される |
| OnGetAsync_フィルタなしの場合全装置が取得できること | フィルタなしで取得 | すべての装置が表示される |
| OnGetAsync_TypeとLineのリストが正しく取得できること | TypeとLineのリスト取得 | 重複なしのTYPE、LINEリストが取得できる |
| OnGetAsync_装置がTYPE別にグルーピングされていること | グルーピングの確認 | 同じTYPEの装置が同じグループに含まれる |
| OnGetAsync_データベースが空の場合でもエラーが発生しないこと | 空データベースでの動作 | エラーなく空リストが返される |

### 2. WipLotList ページ - WipLotListPageTests.cs

**テスト対象機能:**
- 指定装置のWIPロット一覧表示
- 登録済みキャリアの除外
- キャリア選択とCreateBatchへの遷移

**テストケース:**

| テスト名 | テスト内容 | 期待結果 |
|---------|----------|---------|
| OnGetAsync_指定装置のWIPロット一覧が取得できること | 装置を指定してWIP取得 | 該当装置のWIPロットが取得できる |
| OnGetAsync_登録済みキャリアが除外されること | バッチ登録済みキャリアの確認 | 登録済みキャリアは表示されない |
| OnGetAsync_優先度順にソートされていること | ソート順の確認 | Priority昇順でソートされている |
| OnGetAsync_装置名が指定されていない場合は空リストを返すこと | 装置名未指定時の動作 | 空リストが返される |
| OnGetAsync_存在しない装置名の場合は空リストを返すこと | 存在しない装置名の処理 | 空リストが返される |
| OnPost_キャリアが選択されていない場合は同じページにリダイレクトすること | 未選択時の動作 | 同じページにリダイレクト |
| OnPost_選択されたキャリアがCreateBatchに渡されること | キャリア選択処理 | CreateBatchページにLotIdが渡される |
| OnPost_複数キャリア選択時に正しく処理されること | 複数選択の処理 | すべての選択されたLotIdが渡される |
| OnPost_無効なインデックスが除外されること | 無効データの処理 | 無効なインデックスは除外される |

### 3. CreateBatch ページ - CreateBatchPageTests.cs

**テスト対象機能:**
- キャリアステップ情報の表示
- バッチ作成
- PPID/EqpId選択

**テストケース:**

| テスト名 | テスト内容 | 期待結果 |
|---------|----------|---------|
| OnGetAsync_キャリアステップ情報が正しく表示されること | ステップ情報の表示 | 1-4ステップの情報が正しく表示される |
| OnGetAsync_複数キャリアの処理が正しく行われること | 複数キャリア処理 | すべてのキャリア情報が表示される |
| OnGetAsync_重複LotIdが除外されること | 重複除外処理 | 重複したLotIdは1つのみ表示される |
| OnPostAsync_バッチが正常に作成されること | バッチ作成 | DC_BatchとDC_BatchMembersに登録される |
| OnPostAsync_同じBatchIdが全レコードに設定されること | BatchId統一 | 同じバッチ内のすべてのレコードが同じBatchIdを持つ |
| OnPostAsync_同じCreatedAtが全レコードに設定されること | CreatedAt統一 | 同じバッチ内のすべてのレコードが同じCreatedAtを持つ |
| OnPostAsync_BatchMemberが正しく作成されること | BatchMember作成 | LotId、Carrier、Qty、Technologyが正しく保存される |
| OnPostAsync_LotIdsが空の場合はIndexにリダイレクトすること | 空データ時の処理 | Indexページにリダイレクトされる |

### 4. WorkProgress ページ - WorkProgressPageTests.cs

**テスト対象機能:**
- 装置別進捗状況の表示
- In Process/Waiting/Reserved表示
- バッチ削除
- Note更新

**テストケース:**

| テスト名 | テスト内容 | 期待結果 |
|---------|----------|---------|
| OnGetAsync_進捗データが正常に取得できること | 進捗データ取得 | 装置別の進捗データが取得できる |
| OnGetAsync_TYPE別にグルーピングされていること | グルーピング確認 | TYPE別に装置がグルーピングされている |
| OnGetAsync_TYPEフィルタリングが正常に動作すること | TYPEフィルタ | 指定TYPEのみが表示される |
| OnGetAsync_LINEフィルタリングが正常に動作すること | LINEフィルタ | 指定LINEのみが表示される |
| OnGetAsync_InProcessとWaitingが正しく分類されること | 状態分類 | TrackInTimeで正しく分類される |
| OnGetAsync_Reservedが正しく表示されること | Reserved表示 | 未処理バッチが表示される |
| OnGetAsync_Reserved順序がCreatedAt昇順であること | Reserved順序 | 古いバッチから順に表示される |
| OnPostUpdateNoteAsync_Note更新が正常に動作すること | Note更新 | 装置のNoteが更新される |
| OnPostUpdateNoteAsync_装置名が空の場合はエラーを返すこと | 入力検証 | エラーメッセージが返される |
| OnPostDeleteBatchAsync_バッチ削除が正常に動作すること | バッチ削除 | バッチと関連データが削除される |
| OnPostDeleteBatchAsync_BatchIdが空の場合はエラーを返すこと | 入力検証 | エラーメッセージが返される |
| OnGetResetProcessedAsync_IsProcessedフラグがリセットされること | フラグリセット | すべてのバッチがIsProcessed=falseになる |
| OnGetAsync_TypesとLinesが正しく取得できること | フィルタリスト取得 | TypeとLineのリストが取得できる |
| OnGetRefreshAsync_進捗データの更新が正常に動作すること | データ更新 | 最新の進捗データが取得できる |

## テストデータ

各テストではInMemoryデータベースを使用し、テストごとに独立したデータベースインスタンスを作成します。
これにより、テスト間の依存関係を排除し、テストの信頼性を確保しています。

### サンプルデータ構造

各テストクラスの`GetInMemoryDbContext()`メソッドで、以下のようなサンプルデータを作成しています：

- **装置 (DcEqps)**: 複数TYPE、複数LINEの装置
- **WIP (DcWips)**: 優先度、キャリア、ロット情報
- **キャリアステップ (DcCarrierSteps)**: 1-4ステップの装置とレシピ情報
- **バッチ (DcBatches, DcBatchMembers)**: バッチID、ステップ、処理状態
- **実績 (DcActls)**: TrackInTimeによるグルーピング用データ

## カバレッジ目標

- **機能カバレッジ**: 各画面の主要機能を100%カバー
- **条件分岐カバレッジ**: フィルタリング、データ検証など主要な条件分岐をカバー
- **異常系テスト**: 空データ、無効データに対する処理を確認

## テストのベストプラクティス

1. **AAA パターン**: Arrange (準備) → Act (実行) → Assert (検証) の順序でテストを記述
2. **独立性**: 各テストは独立して実行可能
3. **明確な命名**: テスト名は「対象メソッド_テスト内容_期待結果」の形式
4. **InMemoryデータベース**: 各テストで独立したデータベースインスタンスを使用
5. **日本語テスト名**: テスト内容を明確に理解できるよう日本語で記述

## トラブルシューティング

### テストが失敗する場合

1. **データベース状態の確認**: InMemoryデータベースのデータが正しく初期化されているか確認
2. **モックの設定確認**: TempData、HttpContextなどのモックが適切に設定されているか確認
3. **非同期処理**: async/awaitが正しく使用されているか確認

### 新しいテストを追加する場合

1. 対応する画面のテストクラスに新しいテストメソッドを追加
2. `[Fact]` 属性を付与
3. AAAパターンに従ってテストを記述
4. 日本語で明確なテスト名を付ける

## CI/CD統合

テストはCI/CDパイプラインに統合することができます。

### GitHub Actions

`.github/workflows/test.yml`:

```yaml
name: Run Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run tests
      run: dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: test-results
        path: '**/test-results.trx'
```

### GitLab CI/CD

`.gitlab-ci.yml`:

```yaml
stages:
  - test

test:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:8.0
  script:
    - dotnet restore
    - dotnet build --no-restore
    - dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"
  artifacts:
    when: always
    paths:
      - '**/test-results.trx'
    reports:
      junit: '**/test-results.trx'
```

### Jenkins Pipeline

`Jenkinsfile`:

```groovy
pipeline {
    agent any

    stages {
        stage('Restore') {
            steps {
                sh 'dotnet restore'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build --no-restore'
            }
        }

        stage('Test') {
            steps {
                sh 'dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"'
            }
        }
    }

    post {
        always {
            archiveArtifacts artifacts: '**/test-results.trx', allowEmptyArchive: true
        }
    }
}
```

### ローカルでのCI/CD環境シミュレーション

CI/CD環境と同じ手順でテストを実行：

```bash
# 依存関係の復元
~/.dotnet/dotnet restore

# ビルド
~/.dotnet/dotnet build --no-restore

# テスト実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --no-build \
  --verbosity normal \
  --logger "trx;LogFileName=test-results.trx"
```

## クイックリファレンス

### 最もよく使うコマンド

| 操作 | コマンド |
|------|---------|
| すべてのテストを実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj` |
| Index画面のテストのみ実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~IndexPageTests"` |
| WipLotList画面のテストのみ実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~WipLotListPageTests"` |
| CreateBatch画面のテストのみ実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~CreateBatchPageTests"` |
| WorkProgress画面のテストのみ実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~WorkProgressPageTests"` |
| 詳細ログで実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --logger "console;verbosity=detailed"` |
| ビルドしてからテスト実行 | `~/.dotnet/dotnet build Tests/Coordinator.Tests/Coordinator.Tests.csproj && ~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --no-build` |
| 特定のテストメソッドのみ実行 | `~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj --filter "FullyQualifiedName~OnGetAsync_装置一覧が正常に取得できること"` |

### テスト実行のワークフロー例

#### 開発中のテスト実行

```bash
# 1. コードを修正

# 2. 関連する画面のテストのみ実行（高速）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~IndexPageTests"

# 3. すべてのテストを実行（最終確認）
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj
```

#### テストが失敗した場合のデバッグ

```bash
# 1. 失敗したテストのみを詳細ログで実行
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnGetAsync_装置一覧が正常に取得できること" \
  --logger "console;verbosity=detailed"

# 2. 並列実行を無効化してデバッグ
~/.dotnet/dotnet test Tests/Coordinator.Tests/Coordinator.Tests.csproj \
  --filter "FullyQualifiedName~OnGetAsync_装置一覧が正常に取得できること" \
  --logger "console;verbosity=detailed" \
  -- xUnit.ParallelizeTestCollections=false
```

### テスト結果の例

**成功時:**
```
Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 0.9527 Seconds
```

**失敗時:**
```
Test Run Failed.
Total tests: 39
     Passed: 38
     Failed: 1
 Total time: 1.2 Seconds

Failed   Coordinator.Tests.Pages.IndexPageTests.OnGetAsync_装置一覧が正常に取得できること
Error Message:
   Assert.Equal() Failure
   Expected: 4
   Actual:   3
```

## 参考資料

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [EF Core Testing](https://docs.microsoft.com/en-us/ef/core/testing/)
- [ASP.NET Core Testing](https://docs.microsoft.com/en-us/aspnet/core/test/)
- [dotnet test コマンド](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
