# Coordinator System

半導体製造工程における炉処理(Furnace Processing)の進捗管理、バッチ作成、WIP(Work In Progress) 情報の可視化を行うシステムです。

## 技術スタック

- **言語**: C#
- **フレームワーク**: ASP.NET Core Razor Pages
- **データベース**: SQLite
- **ORM**: Entity Framework Core

## 機能

### 1. Dashboard画面
- 装置の稼働状況を一覧表示
- TYPE、LINEでフィルタリング可能
- 装置名をクリックしてWIP Lot List画面へ遷移

### 2. WIP Lot List画面
- 指定装置のWIPロット一覧表示
- キャリアを選択してバッチ作成へ
- 重複キャリアの自動排除

### 3. Create Batch画面
- 複数キャリアに対して最大4ステップのレシピ、装置情報を表示
- バッチIDを生成してバッチを作成
- DC_BatchおよびDC_BatchMembersテーブルに保存

### 4. Work Progress画面
- 炉処理進捗ダッシュボード
- 処理中、処理待ち、予約1-3の状態を表示
- TYPE、LINEでフィルタリング可能

## セットアップ

### 前提条件

- .NET 8.0 SDK以上がインストールされていること

### インストールと実行

1. 依存関係の復元
```bash
dotnet restore
```

2. アプリケーションの実行
```bash
dotnet run
```

3. ブラウザで以下のURLにアクセス
```
https://localhost:5001
または
http://localhost:5000
```

## データベース

SQLiteデータベースは初回起動時に自動的に作成されます。

### テーブル構成

- **DC_Eqps**: 装置情報
- **DC_Wips**: WIP情報
- **DC_CarrierSteps**: キャリアステップ情報（最大4ステップ）
- **DC_Batch**: バッチ情報
- **DC_BatchMembers**: バッチメンバー詳細
- **DC_Actl**: 実績処理データ

初回起動時にサンプルデータが自動的に投入されます。

## プロジェクト構成

```
Coordinator/
├── Data/
│   ├── CoordinatorDbContext.cs    # EF Core DbContext
│   └── DbInitializer.cs           # サンプルデータ初期化
├── Models/
│   ├── DcEqp.cs                   # 装置エンティティ
│   ├── DcWip.cs                   # WIPエンティティ
│   ├── DcCarrierStep.cs           # キャリアステップエンティティ
│   ├── DcBatch.cs                 # バッチエンティティ
│   ├── DcBatchMember.cs           # バッチメンバーエンティティ
│   ├── DcActl.cs                  # 実績エンティティ
│   ├── CarrierStepViewModel.cs    # Create Batch用ViewModel
│   └── WorkProgressViewModel.cs   # Work Progress用ViewModel
├── Pages/
│   ├── Index.cshtml               # Dashboard画面
│   ├── WipLotList.cshtml          # WIP Lot List画面
│   ├── CreateBatch.cshtml         # Create Batch画面
│   ├── WorkProgress.cshtml        # Work Progress画面
│   └── Shared/
│       └── _Layout.cshtml         # 共通レイアウト
├── wwwroot/
│   ├── css/                       # CSSファイル
│   └── js/                        # JavaScriptファイル
├── Program.cs                     # アプリケーションエントリポイント
├── Coordinator.csproj             # プロジェクトファイル
└── appsettings.json               # 設定ファイル
```

## 開発

### データベースの再作成

データベースファイル（coordinator.db）を削除して、アプリケーションを再起動すると、新しいデータベースとサンプルデータが作成されます。

```bash
rm coordinator.db
dotnet run
```
