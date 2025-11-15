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

        // Get all LotIds and EqpIds from actual processing
        var actlData = timeGroups
            .SelectMany(g => g.Select(a => new { a.LotId, a.EqpId }))
            .Distinct()
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
                    updatedCount++;
                }
                else if (matchingBatches.Count > 1)
                {
                    // If multiple records, mark the one with the smallest Step as processed
                    matchingBatches[0].IsProcessed = true;
                    updatedCount++;
                }
            }
        }

        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
        }

        return updatedCount;
    }
}
