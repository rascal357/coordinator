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

        // Get all equipment
        var equipments = await context.DcEqps.ToListAsync(stoppingToken);

        int totalUpdated = 0;

        foreach (var eqp in equipments)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                int updated = await ProcessEquipmentBatches(context, eqp.Name, stoppingToken);
                totalUpdated += updated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing batches for equipment {EquipmentName}", eqp.Name);
            }
        }

        if (totalUpdated > 0)
        {
            _logger.LogInformation("Updated {Count} batch records to IsProcessed=true", totalUpdated);
        }
        else
        {
            _logger.LogDebug("No batch records needed updating");
        }
    }

    private async Task<int> ProcessEquipmentBatches(CoordinatorDbContext context, string eqpName, CancellationToken stoppingToken)
    {
        // Get actual processing data
        var actls = await context.DcActls
            .Where(a => a.EqpId == eqpName)
            .OrderBy(a => a.TrackInTime)
            .ToListAsync(stoppingToken);

        // TODO: 将来的にSQLクエリで取得する場合
        // 以下のようなSQLクエリでDC_Actlと同じ項目を取得し、DcActlモデルにマッピングする
        /*
        var sql = @"
            SELECT
                EqpId, LotId, LotType, TrackInTime, Carrier, Qty, PPID, Next, Location, EndTime
            FROM [外部データソース]
            WHERE EqpId = @eqpName
            ORDER BY TrackInTime
        ";

        using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = sql;
            command.Parameters.Add(new SqliteParameter("@eqpName", eqpName));

            // Set command timeout if needed for long-running queries
            // command.CommandTimeout = 300; // seconds

            await context.Database.OpenConnectionAsync(stoppingToken);
            using (var reader = await command.ExecuteReaderAsync(stoppingToken))
            {
                actls = new List<DcActl>();
                while (await reader.ReadAsync(stoppingToken))
                {
                    // Check for cancellation in loop for long-running queries
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    actls.Add(new DcActl
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

        if (!actls.Any())
            return 0;

        // Group actls by TrackInTime within ±5 minutes
        var timeGroups = GroupByTimeWindow(actls, TimeSpan.FromMinutes(5));

        // Mark batches as processed
        var (updatedCount, updatedBatches) = await MarkBatchesAsProcessed(context, eqpName, timeGroups, stoppingToken);

        // Delete completed batches
        await DeleteCompletedBatches(context, updatedBatches, stoppingToken);

        return updatedCount;
    }

    private List<List<DcActl>> GroupByTimeWindow(List<DcActl> actls, TimeSpan window)
    {
        if (!actls.Any()) return new List<List<DcActl>>();

        var groups = new List<List<DcActl>>();
        var currentGroup = new List<DcActl> { actls[0] };
        var currentTime = actls[0].TrackInTime;

        for (int i = 1; i < actls.Count; i++)
        {
            var timeDiff = Math.Abs((actls[i].TrackInTime - currentTime).TotalMinutes);

            if (timeDiff <= window.TotalMinutes)
            {
                currentGroup.Add(actls[i]);
            }
            else
            {
                groups.Add(currentGroup);
                currentGroup = new List<DcActl> { actls[i] };
                currentTime = actls[i].TrackInTime;
            }
        }

        if (currentGroup.Any())
        {
            groups.Add(currentGroup);
        }

        return groups;
    }

    private async Task<(int updatedCount, List<DcBatch> updatedBatches)> MarkBatchesAsProcessed(CoordinatorDbContext context, string eqpId, List<List<DcActl>> timeGroups, CancellationToken stoppingToken)
    {
        if (!timeGroups.Any()) return (0, new List<DcBatch>());

        int updatedCount = 0;
        var updatedBatches = new List<DcBatch>(); // Track updated batches

        // Get all LotIds, EqpIds, and TrackInTime from actual processing
        var actlData = timeGroups
            .SelectMany(g => g.Select(a => new { a.LotId, a.EqpId, a.TrackInTime }))
            .ToList();

        foreach (var actl in actlData)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Find matching batches by LotId and EqpId
            var matchingBatches = await context.DcBatches
                .Where(b => b.LotId == actl.LotId &&
                           b.EqpId == actl.EqpId &&
                           b.IsProcessed == 0) // Only update if not already processed
                .OrderBy(b => b.Step)
                .ToListAsync(stoppingToken);

            if (matchingBatches.Count == 1)
            {
                // If only one record, mark it as processed
                matchingBatches[0].IsProcessed = 1;
                matchingBatches[0].ProcessedAt = actl.TrackInTime;
                context.Update(matchingBatches[0]); // Explicitly mark as modified
                updatedBatches.Add(matchingBatches[0]);
                updatedCount++;
            }
            else if (matchingBatches.Count > 1)
            {
                // If multiple records, mark the one with the smallest Step as processed
                matchingBatches[0].IsProcessed = 1;
                matchingBatches[0].ProcessedAt = actl.TrackInTime;
                context.Update(matchingBatches[0]); // Explicitly mark as modified
                updatedBatches.Add(matchingBatches[0]);
                updatedCount++;
            }
        }

        // Save changes for updates first
        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Saved {Count} batch updates to database", updatedCount);
        }

        // Log updated batches for maintenance
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
                    _logger.LogInformation("  [Step {Step}] Carrier: {CarrierId}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}, ProcessedAt: {ProcessedAt}",
                        batch.Step,
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

        return (updatedCount, updatedBatches);
    }

    private async Task DeleteCompletedBatches(CoordinatorDbContext context, List<DcBatch> updatedBatches, CancellationToken stoppingToken)
    {
        // Check if updated batches are the last step and delete if needed (per LotId)
        var batchesToDelete = new List<DcBatch>();

        foreach (var batch in updatedBatches)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                // Check if there is a next step for this specific LotId
                var nextStepExists = await context.DcBatches
                    .Where(b => b.BatchId == batch.BatchId &&
                               b.LotId == batch.LotId &&
                               b.Step == batch.Step + 1)
                    .AnyAsync(stoppingToken);

                _logger.LogDebug("Checked next step for BatchId: {BatchId}, LotId: {LotId}, Step: {Step}, NextStepExists: {NextStepExists}",
                    batch.BatchId, batch.LotId, batch.Step, nextStepExists);

                // If no next step exists, this is the last step for this LotId
                if (!nextStepExists)
                {
                    // Delete all records for this BatchId and LotId
                    var recordsToDelete = await context.DcBatches
                        .Where(b => b.BatchId == batch.BatchId && b.LotId == batch.LotId)
                        .ToListAsync(stoppingToken);

                    batchesToDelete.AddRange(recordsToDelete);

                    _logger.LogDebug("Marked {Count} records for deletion: BatchId: {BatchId}, LotId: {LotId}",
                        recordsToDelete.Count, batch.BatchId, batch.LotId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking/deleting next step for BatchId: {BatchId}, LotId: {LotId}, Step: {Step}",
                    batch.BatchId, batch.LotId, batch.Step);
            }
        }

        // Delete completed batches (per LotId)
        if (batchesToDelete.Any())
        {
            context.DcBatches.RemoveRange(batchesToDelete);

            // Save changes for deletions
            await context.SaveChangesAsync(stoppingToken);

            // Group by BatchId and LotId for logging
            var deletedGroups = batchesToDelete
                .GroupBy(b => new { b.BatchId, b.LotId })
                .Select(g => new { g.Key.BatchId, g.Key.LotId, Count = g.Count() })
                .ToList();

            _logger.LogInformation("Deleted {Count} completed batch records for {Groups} LotIds",
                batchesToDelete.Count,
                deletedGroups.Count);

            foreach (var group in deletedGroups)
            {
                _logger.LogInformation("  - BatchId: {BatchId}, LotId: {LotId}, Records: {Count}",
                    group.BatchId, group.LotId, group.Count);
            }
        }
    }
}
