# SqlHelper 使用例

`SqlHelper`クラスは、SQLのIN句を安全にパラメータ化するためのヘルパークラスです。SQLインジェクション攻撃を防ぐため、必ずこのヘルパーを使用してIN句を構築してください。

## 基本的な使い方

### 例1: Entity Framework Core with FromSqlRaw

```csharp
using Coordinator.Helpers;
using Microsoft.EntityFrameworkCore;

// 複数の装置IDでフィルタリング
var eqpIds = new List<string> { "DVETC25", "DVETC26", "DVETC38" };

// IN句のプレースホルダーとパラメータを生成
var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", eqpIds);
// placeholders = "@eqp0, @eqp1, @eqp2"
// parameters = [SqliteParameter("@eqp0", "DVETC25"), ...]

// SQLクエリを構築
var sql = $@"
    SELECT * FROM DC_Actl
    WHERE EqpId IN ({placeholders})
    ORDER BY TrackInTime
";

// クエリを実行
var results = await context.DcActls
    .FromSqlRaw(sql, parameters)
    .ToListAsync();
```

### 例2: ADO.NET with DbCommand

```csharp
using Coordinator.Helpers;

var lotIds = new List<string> { "LOT001", "LOT002", "LOT003" };
var (placeholders, parameters) = SqlHelper.CreateInClause("lot", lotIds);

using (var command = context.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = $@"
        SELECT * FROM DC_Batch
        WHERE LotId IN ({placeholders}) AND IsProcessed = 0
    ";

    // パラメータを追加
    foreach (var param in parameters)
    {
        command.Parameters.Add(param);
    }

    await context.Database.OpenConnectionAsync();
    using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            // データ読み取り処理
            var batchId = reader.GetString(reader.GetOrdinal("BatchId"));
            var lotId = reader.GetString(reader.GetOrdinal("LotId"));
            // ...
        }
    }
}
```

## 応用例

### 例3: 複数のIN句を使用する場合

```csharp
using Coordinator.Helpers;

// 複数の条件でフィルタリング
var eqpIds = new List<string> { "DVETC25", "DVETC26" };
var lotTypes = new List<string> { "PS", "CS" };

// それぞれのIN句を生成
var (eqpPlaceholders, eqpParams) = SqlHelper.CreateInClause("eqp", eqpIds);
var (typePlaceholders, typeParams) = SqlHelper.CreateInClause("type", lotTypes);

// パラメータを結合
var allParams = eqpParams.Concat(typeParams).ToArray();

// SQLクエリ
var sql = $@"
    SELECT * FROM DC_Actl
    WHERE EqpId IN ({eqpPlaceholders})
      AND LotType IN ({typePlaceholders})
    ORDER BY TrackInTime DESC
";

var results = await context.DcActls
    .FromSqlRaw(sql, allParams)
    .ToListAsync();
```

### 例4: 数値型のIN句

```csharp
using Coordinator.Helpers;

// 整数型のリスト
var ids = new List<int> { 1, 2, 3, 5, 8 };
var (placeholders, parameters) = SqlHelper.CreateInClause("id", ids);

var sql = $@"
    SELECT * FROM DC_Eqps
    WHERE Id IN ({placeholders})
";

var equipments = await context.DcEqps
    .FromSqlRaw(sql, parameters)
    .ToListAsync();
```

### 例5: WorkProgressModel での実際の使用例

```csharp
using Coordinator.Helpers;

public async Task LoadProgressDataWithSql()
{
    // 特定の装置のみを取得したい場合
    var targetEqpIds = new List<string> { "DVETC25", "DVETC26", "DVETC38" };
    var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", targetEqpIds);

    var sql = $@"
        SELECT
            EqpId, LotId, LotType, TrackInTime,
            Carrier, Qty, PPID, Next, Location, EndTime
        FROM DC_Actl
        WHERE EqpId IN ({placeholders})
        ORDER BY TrackInTime
    ";

    var actls = await context.DcActls
        .FromSqlRaw(sql, parameters)
        .ToListAsync();

    // 取得したデータを処理
    var timeGroups = GroupByTimeWindow(actls, TimeSpan.FromMinutes(5));
    // ...
}
```

### 例6: 動的な条件でのバッチ検索

```csharp
using Coordinator.Helpers;

public async Task<List<DcBatch>> GetBatchesByCarriers(List<string> carrierIds)
{
    if (carrierIds == null || !carrierIds.Any())
    {
        return new List<DcBatch>();
    }

    var (placeholders, parameters) = SqlHelper.CreateInClause("carrier", carrierIds);

    var sql = $@"
        SELECT * FROM DC_Batch
        WHERE CarrierId IN ({placeholders})
          AND IsProcessed = 0
        ORDER BY CreatedAt
    ";

    return await context.DcBatches
        .FromSqlRaw(sql, parameters)
        .ToListAsync();
}
```

## エッジケースの処理

### 空のリストの場合

```csharp
var emptyList = new List<string>();
var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", emptyList);
// placeholders = "NULL"
// parameters = []

// この場合、IN句は常にfalseになります
var sql = $@"
    SELECT * FROM DC_Actl
    WHERE EqpId IN ({placeholders})
";
// 結果: 0件が返される
```

### nullの場合

```csharp
List<string>? nullList = null;
var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", nullList);
// placeholders = "NULL"
// parameters = []
// 安全に処理されます
```

## ⚠️ 注意事項

### ❌ 絶対にやってはいけないこと

```csharp
// 危険！SQLインジェクションのリスク
var eqpIds = new List<string> { "DVETC25", "DVETC26" };
var inClause = string.Join("', '", eqpIds);
var sql = $"SELECT * FROM DC_Actl WHERE EqpId IN ('{inClause}')";
context.DcActls.FromSqlRaw(sql); // 危険！
```

### ✅ 正しい方法

```csharp
// 安全！パラメータ化されたクエリ
var eqpIds = new List<string> { "DVETC25", "DVETC26" };
var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", eqpIds);
var sql = $"SELECT * FROM DC_Actl WHERE EqpId IN ({placeholders})";
context.DcActls.FromSqlRaw(sql, parameters); // 安全！
```

## LINQとの比較

### LINQ を使用した場合（推奨）

```csharp
// 最もシンプルで安全
var eqpIds = new List<string> { "DVETC25", "DVETC26", "DVETC38" };
var results = await context.DcActls
    .Where(a => eqpIds.Contains(a.EqpId))
    .ToListAsync();
```

### SqlHelper を使用する場合

- 複雑なSQLクエリが必要な場合
- パフォーマンスの最適化が必要な場合
- 既存のSQLクエリを移植する場合
- JOINやサブクエリなど、LINQで表現しにくい場合

## Oracle Database など他のDBへの対応

将来的にOracle Databaseなどに移行する場合:

```csharp
using Oracle.ManagedDataAccess.Client;
using Coordinator.Helpers;

var eqpIds = new List<string> { "DVETC25", "DVETC26" };

// Oracle用のパラメータを作成
var (placeholders, parameters) = SqlHelper.CreateInClause(
    "eqp",
    eqpIds,
    (name, value) => new OracleParameter(name, value)
);

var sql = $@"
    SELECT * FROM DC_ACTL
    WHERE EQP_ID IN ({placeholders})
";

// Oracle用のコンテキストで実行
var results = await oracleContext.DcActls
    .FromSqlRaw(sql, parameters)
    .ToListAsync();
```

## まとめ

- **基本**: `SqlHelper.CreateInClause("prefix", values)` でIN句を生成
- **安全**: SQLインジェクション対策として必ずパラメータ化
- **推奨**: 可能な限りLINQ（`Contains`）を使用
- **用途**: 複雑なクエリやパフォーマンス最適化が必要な場合に使用
