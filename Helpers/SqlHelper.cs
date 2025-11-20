using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace Coordinator.Helpers;

/// <summary>
/// SQL クエリ作成用のヘルパークラス
/// </summary>
public static class SqlHelper
{
    /// <summary>
    /// IN句用のパラメータとプレースホルダーを生成します
    /// SQLインジェクション対策として、必ずパラメータ化されたクエリを使用します
    /// </summary>
    /// <typeparam name="T">値の型（string, int, etc.）</typeparam>
    /// <param name="paramPrefix">パラメータ名のプレフィックス（例: "eqp" → @eqp0, @eqp1...）</param>
    /// <param name="values">IN句に含める値のコレクション</param>
    /// <returns>プレースホルダー文字列とパラメータ配列のタプル</returns>
    /// <example>
    /// 使用例1: Entity Framework Core with FromSqlRaw
    /// <code>
    /// var eqpIds = new List&lt;string&gt; { "DVETC25", "DVETC26", "DVETC38" };
    /// var (placeholders, parameters) = SqlHelper.CreateInClause("eqp", eqpIds);
    ///
    /// var sql = $@"
    ///     SELECT * FROM DC_Actl
    ///     WHERE EqpId IN ({placeholders})
    ///     ORDER BY TrackInTime
    /// ";
    ///
    /// var results = await context.DcActls
    ///     .FromSqlRaw(sql, parameters)
    ///     .ToListAsync();
    /// </code>
    ///
    /// 使用例2: ADO.NET with DbCommand
    /// <code>
    /// var lotIds = new List&lt;string&gt; { "LOT001", "LOT002", "LOT003" };
    /// var (placeholders, parameters) = SqlHelper.CreateInClause("lot", lotIds);
    ///
    /// using (var command = context.Database.GetDbConnection().CreateCommand())
    /// {
    ///     command.CommandText = $@"
    ///         SELECT * FROM DC_Batch
    ///         WHERE LotId IN ({placeholders}) AND IsProcessed = 0
    ///     ";
    ///
    ///     // パラメータを追加
    ///     foreach (var param in parameters)
    ///     {
    ///         command.Parameters.Add(param);
    ///     }
    ///
    ///     await context.Database.OpenConnectionAsync();
    ///     using (var reader = await command.ExecuteReaderAsync())
    ///     {
    ///         // データ読み取り処理
    ///     }
    /// }
    /// </code>
    ///
    /// 使用例3: 複数のIN句を使用する場合
    /// <code>
    /// var eqpIds = new List&lt;string&gt; { "DVETC25", "DVETC26" };
    /// var lotTypes = new List&lt;string&gt; { "PS", "CS" };
    ///
    /// var (eqpPlaceholders, eqpParams) = SqlHelper.CreateInClause("eqp", eqpIds);
    /// var (typePlaceholders, typeParams) = SqlHelper.CreateInClause("type", lotTypes);
    ///
    /// // パラメータを結合
    /// var allParams = eqpParams.Concat(typeParams).ToArray();
    ///
    /// var sql = $@"
    ///     SELECT * FROM DC_Actl
    ///     WHERE EqpId IN ({eqpPlaceholders})
    ///       AND LotType IN ({typePlaceholders})
    /// ";
    ///
    /// var results = await context.DcActls
    ///     .FromSqlRaw(sql, allParams)
    ///     .ToListAsync();
    /// </code>
    ///
    /// 使用例4: 数値型のIN句
    /// <code>
    /// var ids = new List&lt;int&gt; { 1, 2, 3, 5, 8 };
    /// var (placeholders, parameters) = SqlHelper.CreateInClause("id", ids);
    ///
    /// var sql = $@"
    ///     SELECT * FROM DC_Eqps
    ///     WHERE Id IN ({placeholders})
    /// ";
    ///
    /// var equipments = await context.DcEqps
    ///     .FromSqlRaw(sql, parameters)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public static (string Placeholders, DbParameter[] Parameters) CreateInClause<T>(
        string paramPrefix,
        IEnumerable<T> values)
    {
        if (string.IsNullOrWhiteSpace(paramPrefix))
        {
            throw new ArgumentException("Parameter prefix cannot be null or whitespace.", nameof(paramPrefix));
        }

        var valueList = values?.ToList() ?? new List<T>();

        if (valueList.Count == 0)
        {
            // 空のリストの場合は、常にfalseになるIN句を返す
            // SQLite では IN () が構文エラーになるため
            return ("NULL", Array.Empty<DbParameter>());
        }

        var parameters = new DbParameter[valueList.Count];
        var placeholders = new string[valueList.Count];

        for (int i = 0; i < valueList.Count; i++)
        {
            var paramName = $"@{paramPrefix}{i}";
            placeholders[i] = paramName;
            parameters[i] = new SqliteParameter(paramName, valueList[i]);
        }

        return (string.Join(", ", placeholders), parameters);
    }

    /// <summary>
    /// IN句用のパラメータとプレースホルダーを生成します（汎用DbParameter版）
    /// データベースプロバイダーに依存しない実装
    /// </summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="paramPrefix">パラメータ名のプレフィックス</param>
    /// <param name="values">IN句に含める値のコレクション</param>
    /// <param name="createParameter">DbParameterを作成する関数</param>
    /// <returns>プレースホルダー文字列とパラメータ配列のタプル</returns>
    /// <example>
    /// Oracle Database など SQLite以外のデータベースを使用する場合
    /// <code>
    /// using Oracle.ManagedDataAccess.Client;
    ///
    /// var eqpIds = new List&lt;string&gt; { "DVETC25", "DVETC26" };
    /// var (placeholders, parameters) = SqlHelper.CreateInClause(
    ///     "eqp",
    ///     eqpIds,
    ///     (name, value) => new OracleParameter(name, value)
    /// );
    ///
    /// var sql = $@"
    ///     SELECT * FROM DC_ACTL
    ///     WHERE EQP_ID IN ({placeholders})
    /// ";
    /// </code>
    /// </example>
    public static (string Placeholders, DbParameter[] Parameters) CreateInClause<T>(
        string paramPrefix,
        IEnumerable<T> values,
        Func<string, T, DbParameter> createParameter)
    {
        if (string.IsNullOrWhiteSpace(paramPrefix))
        {
            throw new ArgumentException("Parameter prefix cannot be null or whitespace.", nameof(paramPrefix));
        }

        if (createParameter == null)
        {
            throw new ArgumentNullException(nameof(createParameter));
        }

        var valueList = values?.ToList() ?? new List<T>();

        if (valueList.Count == 0)
        {
            return ("NULL", Array.Empty<DbParameter>());
        }

        var parameters = new DbParameter[valueList.Count];
        var placeholders = new string[valueList.Count];

        for (int i = 0; i < valueList.Count; i++)
        {
            var paramName = $"@{paramPrefix}{i}";
            placeholders[i] = paramName;
            parameters[i] = createParameter(paramName, valueList[i]);
        }

        return (string.Join(", ", placeholders), parameters);
    }
}
