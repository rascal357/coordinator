using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;
// TODO: 将来的にSQLクエリで取得する場合は以下のusing文を追加
// using Microsoft.Data.Sqlite;
// using System.Data;

namespace Coordinator.Pages;

public class WorkProgressModel : PageModel
{
    private readonly CoordinatorDbContext _context;
    private readonly ILogger<WorkProgressModel> _logger;

    public WorkProgressModel(CoordinatorDbContext context, ILogger<WorkProgressModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Dictionary<string, List<EquipmentProgressViewModel>> EquipmentsByType { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<string> Lines { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string>? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LineFilter { get; set; }

    public async Task OnGetAsync()
    {
        await LoadProgressData();
    }

    public async Task<IActionResult> OnGetRefreshAsync()
    {
        await LoadProgressData();
        return new JsonResult(new { success = true, data = EquipmentsByType });
    }

    public async Task<IActionResult> OnPostUpdateNoteAsync([FromForm] string eqpName, [FromForm] string note)
    {
        if (string.IsNullOrEmpty(eqpName))
        {
            return new JsonResult(new { success = false, message = "Equipment name is required" });
        }

        try
        {
            var equipment = await _context.DcEqps
                .Where(e => e.Name == eqpName)
                .FirstOrDefaultAsync();

            if (equipment == null)
            {
                return new JsonResult(new { success = false, message = "Equipment not found" });
            }

            equipment.Note = string.IsNullOrWhiteSpace(note) ? null : note;
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Note updated successfully" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"Error updating note: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostDeleteBatchAsync(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
        {
            return new JsonResult(new { success = false, message = "BatchId is required" });
        }

        try
        {
            // Get batch data before deletion for logging
            var batches = await _context.DcBatches
                .Where(b => b.BatchId == batchId)
                .OrderBy(b => b.LotId)
                .ThenBy(b => b.Step)
                .ToListAsync();

            // Log batch deletion information for maintenance
            LogBatchDeletion(batchId, batches);

            // Delete from DC_Batches
            _context.DcBatches.RemoveRange(batches);

            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Batch deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting batch: {BatchId}", batchId);
            return new JsonResult(new { success = false, message = $"Error deleting batch: {ex.Message}" });
        }
    }

    private void LogBatchDeletion(string batchId, List<DcBatch> batches)
    {
        try
        {
            _logger.LogWarning("=== Batch Deleted ===");
            _logger.LogWarning("BatchId: {BatchId}", batchId);
            _logger.LogWarning("Deleted At: {DeletedAt}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"));
            _logger.LogWarning("");

            // Log DC_Batch records
            _logger.LogWarning("--- DC_Batch Records Deleted ({Count}) ---", batches.Count);
            foreach (var batch in batches)
            {
                _logger.LogWarning("  [Step {Step}] LotId: {LotId}, Carrier: {CarrierId}, Qty: {Qty}, Technology: {Technology}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}, IsProcessed: {IsProcessed}, ProcessedAt: {ProcessedAt}",
                    batch.Step,
                    batch.LotId,
                    batch.CarrierId,
                    batch.Qty,
                    batch.Technology,
                    batch.EqpId,
                    batch.PPID,
                    batch.NextEqpId,
                    batch.IsProcessed,
                    batch.ProcessedAt?.ToString("yyyy/MM/dd HH:mm:ss"));
            }
            _logger.LogWarning("===========================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging batch deletion for BatchId: {BatchId}", batchId);
        }
    }

    public async Task<IActionResult> OnGetResetProcessedAsync()
    {
        try
        {
            var allBatches = await _context.DcBatches.ToListAsync();
            foreach (var batch in allBatches)
            {
                batch.IsProcessed = 0;
            }
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = $"Reset {allBatches.Count} batches" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    private async Task LoadProgressData()
    {
        var query = _context.DcEqps.AsQueryable();

        if (TypeFilter != null && TypeFilter.Any())
        {
            query = query.Where(e => TypeFilter.Contains(e.Type));
        }

        if (!string.IsNullOrEmpty(LineFilter))
        {
            query = query.Where(e => e.Line == LineFilter);
        }

        var equipments = await query.OrderBy(e => e.Type).ThenBy(e => e.Name).ToListAsync();

        // Get distinct types and lines for filters
        Types = await _context.DcEqps.Select(e => e.Type).Distinct().OrderBy(t => t).ToListAsync();
        Lines = await _context.DcEqps.Select(e => e.Line).Distinct().OrderBy(l => l).ToListAsync();

        // Optimization: Fetch all equipment data at once to avoid N+1 queries
        var equipmentIds = equipments.Select(e => e.Name).ToList();

        // Get all DcActls for all equipments in one query
        var allActls = await _context.DcActls
            .Where(a => equipmentIds.Contains(a.EqpId))
            .OrderBy(a => a.EqpId)
            .ThenBy(a => a.TrackInTime)
            .ToListAsync();

        // Group by equipment ID in memory
        var actlsByEquipment = allActls
            .GroupBy(a => a.EqpId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.TrackInTime).ToList());

        // Get all batches for all equipments in one query
        var allBatches = await _context.DcBatches
            .Where(b => equipmentIds.Contains(b.EqpId) && b.IsProcessed == 0)
            .ToListAsync();

        // Group batches by equipment ID in memory
        var batchesByEquipment = allBatches
            .GroupBy(b => b.EqpId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group equipments by type
        var groupedEquipments = equipments.GroupBy(e => e.Type);

        foreach (var group in groupedEquipments)
        {
            var equipmentList = new List<EquipmentProgressViewModel>();

            foreach (var eqp in group)
            {
                var progressViewModel = new EquipmentProgressViewModel
                {
                    EqpName = eqp.Name,
                    Line = eqp.Line,
                    Note = eqp.Note
                };

                // Get actual processing data from in-memory dictionary
                var actls = actlsByEquipment.ContainsKey(eqp.Name)
                    ? actlsByEquipment[eqp.Name]
                    : new List<DcActl>();

                // TODO: 将来的にSQLクエリで取得する場合
                // 以下のようなSQLクエリでDC_Actlと同じ項目を取得し、DcActlモデルにマッピングする
                /*
                var sql = @"
                    SELECT
                        EqpId, LotId, LotType, TrackInTime
                    FROM [外部データソース]
                    WHERE EqpId = @eqpName
                    ORDER BY TrackInTime
                ";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqliteParameter("@eqpName", eqp.Name));

                    await _context.Database.OpenConnectionAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        actls = new List<DcActl>();
                        while (await reader.ReadAsync())
                        {
                            actls.Add(new DcActl
                            {
                                EqpId = reader.GetString(0),
                                LotId = reader.GetString(1),
                                LotType = reader.GetString(2),
                                TrackInTime = reader.GetDateTime(3)
                            });
                        }
                    }
                }
                */

                // Group actls by TrackInTime within ±5 minutes
                var timeGroups = GroupByTimeWindow(actls, TimeSpan.FromMinutes(5));

                // First group is in process, second group is waiting
                if (timeGroups.Count > 0)
                {
                    progressViewModel.InProcess = await CreateProcessItemsFromActls(timeGroups[0]);
                }

                if (timeGroups.Count > 1)
                {
                    progressViewModel.Waiting = await CreateProcessItemsFromActls(timeGroups[1]);
                }

                // Note: Batch processing status is now updated by BatchProcessingBackgroundService
                // No need to update IsProcessed here to avoid duplicate processing

                // Get reserved batches from in-memory dictionary
                var equipmentBatches = batchesByEquipment.ContainsKey(eqp.Name)
                    ? batchesByEquipment[eqp.Name]
                    : new List<DcBatch>();

                var reservedBatchIds = equipmentBatches
                    .GroupBy(b => new { b.BatchId, b.CreatedAt })
                    .OrderBy(g => g.Key.CreatedAt)
                    .Take(3)
                    .Select(g => new { g.Key.BatchId, Batch = g.First() })
                    .ToList();

                var reservedItems = new List<List<ProcessItem>>();
                foreach (var batchGroup in reservedBatchIds)
                {
                    var items = await CreateProcessItemsFromBatch(batchGroup.Batch, eqp.Name);
                    reservedItems.Add(items);
                }

                if (reservedItems.Count > 0)
                {
                    progressViewModel.Reserved1 = reservedItems[0];
                    progressViewModel.Reserved1BatchId = reservedBatchIds[0].BatchId;
                }
                if (reservedItems.Count > 1)
                {
                    progressViewModel.Reserved2 = reservedItems[1];
                    progressViewModel.Reserved2BatchId = reservedBatchIds[1].BatchId;
                }
                if (reservedItems.Count > 2)
                {
                    progressViewModel.Reserved3 = reservedItems[2];
                    progressViewModel.Reserved3BatchId = reservedBatchIds[2].BatchId;
                }

                equipmentList.Add(progressViewModel);
            }

            EquipmentsByType[group.Key] = equipmentList;
        }
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

    private async Task<List<ProcessItem>> CreateProcessItemsFromActls(List<DcActl> actls)
    {
        var items = new List<ProcessItem>();

        foreach (var actl in actls)
        {
            string nextFurnace = actl.Next;

            // Get the latest ProcessedAt record from DC_Batch for this equipment
            var latestBatch = await _context.DcBatches
                .Where(b => b.EqpId == actl.EqpId && b.ProcessedAt != null)
                .OrderByDescending(b => b.ProcessedAt)
                .FirstOrDefaultAsync();

            // If the latest batch's ProcessedAt matches actl's TrackInTime
            if (latestBatch != null && latestBatch.ProcessedAt == actl.TrackInTime)
            {
                // Use NextEqpId from the latest batch
                nextFurnace = latestBatch.NextEqpId;
            }

            items.Add(new ProcessItem
            {
                Carrier = actl.Carrier,
                Lot = actl.LotId,
                Qty = actl.Qty,
                PPID = actl.PPID,
                NextFurnace = nextFurnace,
                Location = actl.Location,
                EndTime = actl.EndTime?.ToString("yyyy/MM/dd HH:mm") ?? ""
            });
        }

        return items;
    }

    private async Task<List<ProcessItem>> CreateProcessItemsFromBatch(DcBatch batch, string eqpId)
    {
        var items = new List<ProcessItem>();

        // Get batches that match the BatchId and EqpId
        var batches = await _context.DcBatches
            .Where(b => b.BatchId == batch.BatchId && b.EqpId == eqpId)
            .GroupBy(b => new { b.LotId, b.CarrierId, b.Qty, b.Technology })
            .Select(g => g.First())
            .ToListAsync();

        foreach (var batchItem in batches)
        {
            items.Add(new ProcessItem
            {
                Carrier = batchItem.CarrierId,
                Lot = batchItem.LotId ?? "",
                Qty = batchItem.Qty,
                PPID = batchItem.PPID,
                NextFurnace = batchItem.NextEqpId,
                Location = "",
                EndTime = ""
            });
        }

        return items;
    }

    private async Task MarkBatchesAsProcessed(string eqpId, List<List<DcActl>> timeGroups)
    {
        if (!timeGroups.Any()) return;

        // Get all LotIds and EqpIds from actual processing
        var actlData = timeGroups
            .SelectMany(g => g.Select(a => new { a.LotId, a.EqpId }))
            .Distinct()
            .ToList();

        foreach (var actl in actlData)
        {
            // Find matching batches by LotId and EqpId
            var matchingBatches = await _context.DcBatches
                .Where(b => b.LotId == actl.LotId && b.EqpId == actl.EqpId)
                .OrderBy(b => b.Step)
                .ToListAsync();

            if (matchingBatches.Count == 1)
            {
                // If only one record, mark it as processed
                matchingBatches[0].IsProcessed = 1;
            }
            else if (matchingBatches.Count > 1)
            {
                // If multiple records, mark the one with the smallest Step as processed
                matchingBatches[0].IsProcessed = 1;
            }
        }

        await _context.SaveChangesAsync();
    }
}
