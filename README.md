# Coordinator System


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

## Oracleへの移行手順

現在、システムはSQLiteを使用していますが、将来的にOracleデータベースへの移行が可能です。
コードベース内の各SQLite使用箇所には、Oracle用のコメントが準備されています。

### 移行ステップ

#### 1. NuGetパッケージの変更

`Coordinator.csproj` ファイルを編集：

```xml
<!-- SQLite用パッケージをコメントアウト -->
<!-- <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" /> -->

<!-- Oracle用パッケージのコメントを解除 -->
<PackageReference Include="Oracle.EntityFrameworkCore" Version="8.23.50" />
```

#### 2. 接続文字列の変更

`appsettings.json` ファイルを編集：

```json
{
  "ConnectionStrings": {
    // SQLite用をコメントアウト
    // "DefaultConnection": "Data Source=coordinator.db"

    // Oracle用の接続文字列を設定
    "DefaultConnection": "User Id=your_username;Password=your_password;Data Source=your_host:1521/your_service_name"
  }
}
```

#### 3. DbContext設定の変更

`Program.cs` の以下の箇所を変更：

```csharp
// SQLiteをコメントアウト
// builder.Services.AddDbContext<CoordinatorDbContext>(options =>
//     options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Oracleのコメントを解除
builder.Services.AddDbContext<CoordinatorDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));
```

#### 4. SQLite固有のSQL構文の変更

`Program.cs` のカラム存在チェックとALTER TABLE文を変更：

```csharp
// SQLite用をコメントアウト
// var columnExists = context.Database.SqlQueryRaw<int>(
//     "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Eqps') WHERE name = 'Note'")
//     .AsEnumerable()
//     .FirstOrDefault();

// Oracle用のコメントを解除
var columnExists = context.Database.SqlQueryRaw<int>(
    "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_EQPS' AND COLUMN_NAME = 'NOTE'")
    .AsEnumerable()
    .FirstOrDefault();

if (columnExists == 0)
{
    // SQLiteをコメントアウト
    // context.Database.ExecuteSqlRaw("ALTER TABLE DC_Eqps ADD COLUMN Note TEXT");

    // Oracleのコメントを解除
    context.Database.ExecuteSqlRaw("ALTER TABLE DC_EQPS ADD NOTE NVARCHAR2(2000)");
}
```

#### 5. モデルクラスの確認（必要に応じて）

`Models/DcEqp.cs` など、主キーの自動採番設定が必要な場合：

```csharp
[Key]
[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
public int Id { get; set; }
```

#### 6. データベースの作成とマイグレーション

Oracleデータベースに接続できることを確認し、以下のコマンドでデータベースを作成：

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

または、`EnsureCreated()`を使用している場合は、そのまま実行：

```bash
dotnet run
```

### Oracle移行時の注意点

1. **テーブル名とカラム名**: Oracleでは大文字で保存されます
2. **識別子の長さ制限**: Oracle 12.1以前は30文字、12.2以降は128文字まで
3. **データ型のマッピング**:
   - `string` → `NVARCHAR2(n)`
   - `DateTime` → `TIMESTAMP`
   - `bool` → `NUMBER(1)` (0=false, 1=true)
4. **シーケンス**: 主キーの自動採番にはシーケンスとトリガーが必要な場合があります
5. **インデックス名**: 長さ制限に注意し、必要に応じて明示的に指定します

詳細は各ファイルのコメントを参照してください。
