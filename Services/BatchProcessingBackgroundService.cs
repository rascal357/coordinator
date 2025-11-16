using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

namespace Coordinator.Services;

public class BatchProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BatchProcessingBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _updateIntervalSeconds;

    public BatchProcessingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BatchProcessingBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _configuration = configuration;
        _updateIntervalSeconds = _configuration.GetValue<int>("BatchProcessing:UpdateIntervalSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchProcessingBackgroundService started. Update interval: {Interval} seconds", _updateIntervalSeconds);

        // Wait a bit before starting the first update to allow the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateBatchProcessingStatus(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating batch processing status");
            }

            // Wait for the next update interval
            await Task.Delay(TimeSpan.FromSeconds(_updateIntervalSeconds), stoppingToken);
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
                        EndTime = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
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
        return await MarkBatchesAsProcessed(context, eqpName, timeGroups, stoppingToken);
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

    private async Task<int> MarkBatchesAsProcessed(CoordinatorDbContext context, string eqpId, List<List<DcActl>> timeGroups, CancellationToken stoppingToken)
    {
        if (!timeGroups.Any()) return 0;

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

            // Find matching batch members by LotId
            var matchingMembers = await context.DcBatchMembers
                .Where(m => m.LotId == actl.LotId)
                .ToListAsync(stoppingToken);

            foreach (var member in matchingMembers)
            {
                // Join DC_BatchMembers and DC_Batch by BatchId and CarrierId
                // Filter by LotId, EqpId, and BatchId
                var matchingBatches = await context.DcBatches
                    .Where(b => b.BatchId == member.BatchId &&
                               b.CarrierId == member.CarrierId &&
                               b.EqpId == actl.EqpId &&
                               !b.IsProcessed) // Only update if not already processed
                    .OrderBy(b => b.Step)
                    .ToListAsync(stoppingToken);

                if (matchingBatches.Count == 1)
                {
                    // If only one record, mark it as processed
                    matchingBatches[0].IsProcessed = true;
                    matchingBatches[0].ProcessedAt = actl.TrackInTime;
                    updatedBatches.Add(matchingBatches[0]);
                    updatedCount++;
                }
                else if (matchingBatches.Count > 1)
                {
                    // If multiple records, mark the one with the smallest Step as processed
                    matchingBatches[0].IsProcessed = true;
                    matchingBatches[0].ProcessedAt = actl.TrackInTime;
                    updatedBatches.Add(matchingBatches[0]);
                    updatedCount++;
                }
            }
        }

        // Check if updated batches are the last step and delete if needed
        var batchIdsToDelete = new HashSet<string>();

        foreach (var batch in updatedBatches)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Check if there is a next step for this batch
            var nextStepExists = await context.DcBatches
                .Where(b => b.BatchId == batch.BatchId &&
                           b.CarrierId == batch.CarrierId &&
                           b.Step == batch.Step + 1)
                .AnyAsync(stoppingToken);

            // If no next step exists, this is the last step
            if (!nextStepExists)
            {
                batchIdsToDelete.Add(batch.BatchId);
            }
        }

        // Delete batches and batch members for completed BatchIds
        if (batchIdsToDelete.Any())
        {
            // Delete from DC_BatchMembers
            var batchMembersToDelete = await context.DcBatchMembers
                .Where(bm => batchIdsToDelete.Contains(bm.BatchId))
                .ToListAsync(stoppingToken);
            context.DcBatchMembers.RemoveRange(batchMembersToDelete);

            // Delete from DC_Batches
            var batchesToDelete = await context.DcBatches
                .Where(b => batchIdsToDelete.Contains(b.BatchId))
                .ToListAsync(stoppingToken);
            context.DcBatches.RemoveRange(batchesToDelete);

            _logger.LogInformation("Deleted {Count} completed batches: {BatchIds}",
                batchIdsToDelete.Count, string.Join(", ", batchIdsToDelete));
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

        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
        }

        return updatedCount;
    }
}
