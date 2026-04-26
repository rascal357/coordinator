using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

// ---------------------------------------------------------------------------
// BatchProcessorCli
//   バッチ処理ステータス更新を1回実行して終了するコンソールアプリ。
//   Windows タスクスケジューラから定期的に呼び出すことを想定。
//
//   使用方法:
//     BatchProcessorCli.exe
//     BatchProcessorCli.exe --db "C:\path\to\coordinator.db"
//
//   終了コード:
//     0 ... 正常終了
//     1 ... エラー終了
// ---------------------------------------------------------------------------

var exeDir = AppContext.BaseDirectory;

// ---- 設定読み込み ----------------------------------------------------------
var config = new ConfigurationBuilder()
    .SetBasePath(exeDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// --db オプションでDBパスを上書き可能
string connectionString;
var dbArg = config["db"];
if (!string.IsNullOrEmpty(dbArg))
{
    connectionString = $"Data Source={Path.GetFullPath(dbArg)}";
}
else
{
    var raw = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection が設定されていません。");

    // 相対パスを exe ディレクトリ基準で解決
    if (raw.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    {
        var dataSource = raw["Data Source=".Length..].Trim();
        if (!Path.IsPathRooted(dataSource))
            raw = $"Data Source={Path.GetFullPath(Path.Combine(exeDir, dataSource))}";
    }
    connectionString = raw;
}

// ---- ログ設定 -------------------------------------------------------------
var logFilePath = config["Logging:File:Path"] ?? "../Logs/batch-processor-.log";
if (!Path.IsPathRooted(logFilePath))
    logFilePath = Path.GetFullPath(Path.Combine(exeDir, logFilePath));

Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
var logFile = logFilePath.Replace("-.", $"-{DateTime.Now:yyyyMMdd}.");

using var loggerFactory = LoggerFactory.Create(lb =>
{
    lb.AddSimpleConsole(o =>
    {
        o.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
        o.SingleLine = true;
    });
    lb.AddProvider(new FileLoggerProvider(logFile));
    lb.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger("BatchProcessorCli");

// ---- メイン処理 -----------------------------------------------------------
logger.LogInformation("BatchProcessorCli 開始 (DB: {Cs})", connectionString);

try
{
    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync();

    await RunBatchProcessing(conn, logger);

    logger.LogInformation("BatchProcessorCli 正常終了");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "BatchProcessorCli 異常終了");
    return 1;
}

// ---------------------------------------------------------------------------
// バッチ処理ロジック
// ---------------------------------------------------------------------------
static async Task RunBatchProcessing(SqliteConnection conn, ILogger logger)
{
    logger.LogInformation("バッチ処理ステータス更新を開始します");

    // 管理対象の装置名一覧を取得
    var equipmentIds = await QueryListAsync(conn,
        "SELECT NAME FROM DC_Eqps",
        r => r.GetString(0));

    logger.LogInformation("管理対象装置数: {Count}", equipmentIds.Count);

    if (equipmentIds.Count == 0)
    {
        logger.LogWarning("DC_Eqps にデータがありません。処理をスキップします。");
        return;
    }

    // DC_Actl: 管理対象装置のレコードを取得
    var inClause = string.Join(",", equipmentIds.Select((_, i) => $"@eqp{i}"));
    var allActls = await QueryListAsync(conn,
        $"SELECT EqpId, LotId, TrackInTime FROM DC_Actl WHERE EqpId IN ({inClause})",
        r => new ActlRow(
            EqpId:       r.IsDBNull(0) ? null : r.GetString(0),
            LotId:       r.IsDBNull(1) ? null : r.GetString(1),
            TrackInTime: r.IsDBNull(2) ? (DateTime?)null : DateTime.Parse(r.GetString(2))
        ),
        cmd => { for (int i = 0; i < equipmentIds.Count; i++) cmd.Parameters.AddWithValue($"@eqp{i}", equipmentIds[i]); });

    // DC_Batch: 未処理レコードを取得
    var allBatches = await QueryListAsync(conn,
        "SELECT Id, BatchId, LotId, EqpId FROM DC_Batch WHERE IsProcessed = 0",
        r => new BatchRow(
            Id:      r.GetInt32(0),
            BatchId: r.GetString(1),
            LotId:   r.IsDBNull(2) ? null : r.GetString(2),
            EqpId:   r.GetString(3)
        ));

    logger.LogInformation("取得件数 - DC_Actl: {A}, DC_Batch(未処理): {B}",
        allActls.Count, allBatches.Count);

    // 装置ごとにグループ化
    var actlsByEqp   = allActls.Where(a => a.EqpId != null).GroupBy(a => a.EqpId!).ToDictionary(g => g.Key, g => g.ToList());
    var batchesByEqp = allBatches.GroupBy(b => b.EqpId).ToDictionary(g => g.Key, g => g.ToList());

    int totalUpdated = 0;

    using var txn = conn.BeginTransaction();
    try
    {
        // 装置ごとに IsProcessed を更新
        foreach (var eqpId in actlsByEqp.Keys)
        {
            var actls   = actlsByEqp[eqpId];
            var batches = batchesByEqp.TryGetValue(eqpId, out var b) ? b : new List<BatchRow>();

            int updated = await UpdateIsProcessed(conn, txn, eqpId, actls, batches, logger);
            totalUpdated += updated;
        }

        // 全ステップが処理済みになったバッチを削除
        int deleted = await DeleteCompletedBatches(conn, txn, logger);

        txn.Commit();

        if (totalUpdated > 0)
            logger.LogInformation("{Count} 件のバッチレコードを IsProcessed=1 に更新しました", totalUpdated);
        else
            logger.LogInformation("更新対象のバッチレコードはありませんでした");

        if (deleted > 0)
            logger.LogInformation("{Count} 件の完了バッチレコードを削除しました", deleted);
    }
    catch
    {
        txn.Rollback();
        throw;
    }
}

static async Task<int> UpdateIsProcessed(
    SqliteConnection conn, SqliteTransaction txn,
    string eqpId, List<ActlRow> actls, List<BatchRow> batches,
    ILogger logger)
{
    if (actls.Count == 0 || batches.Count == 0)
        return 0;

    // LotId をキーにしたBatchのルックアップ
    var batchByLot = batches
        .Where(b => b.LotId != null)
        .GroupBy(b => b.LotId!)
        .ToDictionary(g => g.Key, g => g.ToList());

    int updatedCount = 0;

    using var cmd = conn.CreateCommand();
    cmd.Transaction = txn;
    cmd.CommandText = "UPDATE DC_Batch SET IsProcessed = 1, ProcessedAt = @pt WHERE Id = @id";
    cmd.Parameters.Add("@pt", SqliteType.Text);
    cmd.Parameters.Add("@id", SqliteType.Integer);

    foreach (var actl in actls)
    {
        if (actl.LotId == null || !batchByLot.TryGetValue(actl.LotId, out var matched))
            continue;

        foreach (var batch in matched)
        {
            cmd.Parameters["@pt"].Value = actl.TrackInTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value;
            cmd.Parameters["@id"].Value = batch.Id;
            await cmd.ExecuteNonQueryAsync();
            updatedCount++;

            logger.LogInformation(
                "  IsProcessed=1: BatchId={BatchId}, Id={Id}, LotId={LotId}, EqpId={EqpId}",
                batch.BatchId, batch.Id, batch.LotId, batch.EqpId);
        }
    }

    return updatedCount;
}

static async Task<int> DeleteCompletedBatches(
    SqliteConnection conn, SqliteTransaction txn, ILogger logger)
{
    // 全レコード取得してグループ判定（件数が多い場合はサブクエリに変更可）
    var all = await QueryListAsync(conn,
        "SELECT Id, BatchId, IsProcessed FROM DC_Batch",
        r => (Id: r.GetInt32(0), BatchId: r.GetString(1), IsProcessed: r.GetInt32(2)));

    var completedIds = all
        .GroupBy(r => r.BatchId)
        .Where(g => g.All(r => r.IsProcessed == 1))
        .SelectMany(g => g.Select(r => r.Id))
        .ToList();

    if (completedIds.Count == 0)
        return 0;

    // SQLite はバインドパラメータ数に上限があるため分割削除
    const int chunkSize = 500;
    int totalDeleted = 0;
    for (int i = 0; i < completedIds.Count; i += chunkSize)
    {
        var chunk = completedIds.Skip(i).Take(chunkSize).ToList();
        var placeholders = string.Join(",", chunk.Select((_, j) => $"@d{j}"));
        using var cmd = conn.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = $"DELETE FROM DC_Batch WHERE Id IN ({placeholders})";
        for (int j = 0; j < chunk.Count; j++)
            cmd.Parameters.AddWithValue($"@d{j}", chunk[j]);
        totalDeleted += await cmd.ExecuteNonQueryAsync();
    }

    logger.LogInformation("完了バッチ削除: {Count} 件", totalDeleted);
    return totalDeleted;
}

// 汎用クエリヘルパー
static async Task<List<T>> QueryListAsync<T>(
    SqliteConnection conn, string sql,
    Func<SqliteDataReader, T> map,
    Action<SqliteCommand>? parameterizer = null)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    parameterizer?.Invoke(cmd);
    var list = new List<T>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        list.Add(map(reader));
    return list;
}

// ---------------------------------------------------------------------------
// モデル
// ---------------------------------------------------------------------------
record ActlRow(string? EqpId, string? LotId, DateTime? TrackInTime);
record BatchRow(int Id, string BatchId, string? LotId, string EqpId);

// ---------------------------------------------------------------------------
// ファイルロガー
// ---------------------------------------------------------------------------
public sealed class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly StreamWriter _writer = new(filePath, append: true) { AutoFlush = true };
    public ILogger CreateLogger(string categoryName) => new FileLogger(_writer, categoryName);
    public void Dispose() => _writer.Dispose();
}

public sealed class FileLogger(StreamWriter writer, string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var level = logLevel switch
        {
            LogLevel.Information => "INFO ",
            LogLevel.Warning     => "WARN ",
            LogLevel.Error       => "ERROR",
            LogLevel.Critical    => "CRIT ",
            _                    => logLevel.ToString()[..4].ToUpper()
        };
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {formatter(state, exception)}";
        if (exception != null)
            line += Environment.NewLine + exception;
        lock (writer)
            writer.WriteLine(line);
    }
}
