using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Coordinator.Data;
using Coordinator.Models;

namespace Coordinator.Services;

public class BatchProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BatchProcessingBackgroundService> _logger;
    private readonly IOptionsMonitor<BatchProcessingOptions> _options;

    public BatchProcessingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BatchProcessingBackgroundService> logger,
        IOptionsMonitor<BatchProcessingOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        _logger.LogInformation("BatchProcessingBackgroundService started. Enabled: {Enabled}, Update interval: {Interval} seconds",
            options.Enabled, options.UpdateIntervalSeconds);

        // Wait a bit before starting the first update to allow the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Get current options (will reflect any changes from appsettings.json)
            options = _options.CurrentValue;

            // Check if batch processing is enabled
            if (!options.Enabled)
            {
                _logger.LogDebug("Batch processing is disabled. Waiting...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                await UpdateBatchProcessingStatus(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating batch processing status");
            }

            // Wait for the next update interval (using current value from options)
            await Task.Delay(TimeSpan.FromSeconds(options.UpdateIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("BatchProcessingBackgroundService stopped");
    }

    private async Task UpdateBatchProcessingStatus(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoordinatorDbContext>();

        _logger.LogDebug("Starting batch processing status update");

        // Get all equipment IDs from DcEqps
        var equipmentIds = await context.DcEqps
            .Select(e => e.Name)
            .ToListAsync(stoppingToken);

        // Cache all DcActls and DcBatches at the beginning (like WorkProgressModel.LoadProgressData)
        // Only include actls for equipment that exists in DcEqps
        var allActls = await context.DcActls
            .Where(a => equipmentIds.Contains(a.EqpId))
            .ToListAsync(stoppingToken);

        var allBatches = await context.DcBatches
            .Where(b => b.IsProcessed == 0)
            .ToListAsync(stoppingToken);

        // TODO: 将来的にSQLクエリで取得する場合
        // 以下のようなSQLクエリでDC_Actlと同じ項目を取得し、DcActlモデルにマッピングする
        /*
        var sql = @"
            SELECT
                EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location, EndTime
            FROM [外部データソース]
            ORDER BY EqpId, TrackInTime
        ";

        using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = sql;

            // Set command timeout if needed for long-running queries
            // command.CommandTimeout = 300; // seconds

            await context.Database.OpenConnectionAsync(stoppingToken);
            using (var reader = await command.ExecuteReaderAsync(stoppingToken))
            {
                allActls = new List<DcActl>();
                while (await reader.ReadAsync(stoppingToken))
                {
                    // Check for cancellation in loop for long-running queries
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    allActls.Add(new DcActl
                    {
                        EqpId = reader.GetString(0),
                        LotId = reader.GetString(1),
                        LotType = reader.GetString(2),
                        TrackInTime = reader.GetDateTime(3),
                        Carrier = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Qty = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                        PPID = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Next = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        Location = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        EndTime = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                    });
                }
            }
        }
        */

        // Group DcActls by equipment (EqpId)
        var actlsByEquipment = allActls
            .Where(a => a.EqpId != null)
            .GroupBy(a => a.EqpId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group DcBatches by equipment (EqpId) for quick lookup
        var batchesByEquipment = allBatches
            .GroupBy(b => b.EqpId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int totalUpdated = 0;

        // Process IsProcessed updates for each equipment
        foreach (var eqpId in actlsByEquipment.Keys)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                var actls = actlsByEquipment[eqpId];
                var batches = batchesByEquipment.ContainsKey(eqpId)
                    ? batchesByEquipment[eqpId]
                    : new List<DcBatch>();

                int updated = await UpdateIsProcessed(context, eqpId, actls, batches, stoppingToken);
                totalUpdated += updated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing batches for equipment {EquipmentName}", eqpId);
            }
        }

        if (totalUpdated > 0)
        {
            _logger.LogInformation("Updated {Count} batch records to IsProcessed=1", totalUpdated);
        }
        else
        {
            _logger.LogDebug("No batch records needed updating");
        }

        // After all equipment processing is complete, delete completed batches
        await DeleteCompletedBatches(context, stoppingToken);
    }

    private async Task<int> UpdateIsProcessed(
        CoordinatorDbContext context,
        string eqpId,
        List<DcActl> actls,
        List<DcBatch> batches,
        CancellationToken stoppingToken)
    {
        if (!actls.Any() || !batches.Any())
            return 0;

        int updatedCount = 0;
        var updatedBatches = new List<DcBatch>();

        // For each LotId in actls, find matching batches and update IsProcessed
        foreach (var actl in actls)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Find batches with matching EqpId, LotId, and IsProcessed=0
            var matchingBatches = batches
                .Where(b => b.LotId == actl.LotId && b.IsProcessed == 0)
                .ToList();

            if (matchingBatches.Any())
            {
                foreach (var batch in matchingBatches)
                {
                    batch.IsProcessed = 1;
                    batch.ProcessedAt = actl.TrackInTime;
                    context.Update(batch);
                    updatedBatches.Add(batch);
                    updatedCount++;
                }
            }
        }

        // Save changes
        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Equipment {EqpId}: Updated {Count} batch records to IsProcessed=1", eqpId, updatedCount);

            // Log updated batches
            if (updatedBatches.Any())
            {
                _logger.LogInformation("=== Batches Marked as Processed ===");
                _logger.LogInformation("Equipment: {EqpId}, Updated Count: {Count}", eqpId, updatedBatches.Count);
                _logger.LogInformation("");

                // Group by BatchId for better readability
                var groupedBatches = updatedBatches
                    .GroupBy(b => b.BatchId)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedBatches)
                {
                    _logger.LogInformation("--- BatchId: {BatchId} ---", group.Key);
                    foreach (var batch in group.OrderBy(b => b.CarrierId).ThenBy(b => b.Step))
                    {
                        _logger.LogInformation("  [Step {Step}] LotId: {LotId}, Carrier: {CarrierId}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}, ProcessedAt: {ProcessedAt}",
                            batch.Step,
                            batch.LotId,
                            batch.CarrierId,
                            batch.EqpId,
                            batch.PPID,
                            batch.NextEqpId,
                            batch.ProcessedAt?.ToString("yyyy/MM/dd HH:mm:ss"));
                    }
                    _logger.LogInformation("");
                }
                _logger.LogInformation("====================================");
            }
        }

        return updatedCount;
    }

    private async Task DeleteCompletedBatches(CoordinatorDbContext context, CancellationToken stoppingToken)
    {
        // Re-cache DcBatches after updates
        var allBatches = await context.DcBatches.ToListAsync(stoppingToken);

        // Group by BatchId
        var batchGroups = allBatches
            .GroupBy(b => b.BatchId)
            .ToList();

        var batchesToDelete = new List<DcBatch>();

        // Check if all records in each BatchId group have IsProcessed=1
        foreach (var group in batchGroups)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var batchList = group.ToList();

            // If all records have IsProcessed=1, delete all records for this BatchId
            if (batchList.All(b => b.IsProcessed == 1))
            {
                batchesToDelete.AddRange(batchList);
            }
        }

        // Delete completed batches
        if (batchesToDelete.Any())
        {
            context.DcBatches.RemoveRange(batchesToDelete);
            await context.SaveChangesAsync(stoppingToken);

            // Group by BatchId for logging
            var deletedGroups = batchesToDelete
                .GroupBy(b => b.BatchId)
                .Select(g => new { BatchId = g.Key, Count = g.Count() })
                .ToList();

            _logger.LogInformation("Deleted {Count} completed batch records for {Groups} BatchIds",
                batchesToDelete.Count,
                deletedGroups.Count);

            foreach (var group in deletedGroups)
            {
                _logger.LogInformation("  - BatchId: {BatchId}, Records: {Count}",
                    group.BatchId, group.Count);
            }
        }
    }
}
