using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

namespace Coordinator.Pages;

public class WorkProgressModel : PageModel
{
    private readonly CoordinatorDbContext _context;

    public WorkProgressModel(CoordinatorDbContext context)
    {
        _context = context;
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
                    Line = eqp.Line
                };

                // Get actual processing data
                var actls = await _context.DcActls
                    .Where(a => a.EqpId == eqp.Name)
                    .OrderBy(a => a.TrackInTime)
                    .ToListAsync();

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

                // Mark batches as processed if their LotIds match actual processing
                await MarkBatchesAsProcessed(eqp.Name, timeGroups);

                // Get reserved batches (not processed), grouped by BatchId
                var reservedBatchIds = await _context.DcBatches
                    .Where(b => b.EqpId == eqp.Name && !b.IsProcessed)
                    .GroupBy(b => new { b.BatchId, b.CreatedAt })
                    .OrderBy(g => g.Key.CreatedAt)
                    .Take(3)
                    .Select(g => new { g.Key.BatchId, Batch = g.First() })
                    .ToListAsync();

                var reservedItems = new List<List<ProcessItem>>();
                foreach (var batchGroup in reservedBatchIds)
                {
                    var items = await CreateProcessItemsFromBatch(batchGroup.Batch);
                    reservedItems.Add(items);
                }

                if (reservedItems.Count > 0) progressViewModel.Reserved1 = reservedItems[0];
                if (reservedItems.Count > 1) progressViewModel.Reserved2 = reservedItems[1];
                if (reservedItems.Count > 2) progressViewModel.Reserved3 = reservedItems[2];

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
            var wipInfo = await _context.DcWips
                .Where(w => w.LotId == actl.LotId)
                .FirstOrDefaultAsync();

            items.Add(new ProcessItem
            {
                Carrier = wipInfo?.Carrier ?? "",
                Lot = actl.LotId,
                Qty = wipInfo?.Qty ?? 0,
                PPID = wipInfo?.TargetPPID ?? "",
                NextFurnace = "",
                Location = "",
                EndTime = actl.TrackInTime.ToString("yyyy/MM/dd HH:mm")
            });
        }

        return items;
    }

    private async Task<List<ProcessItem>> CreateProcessItemsFromBatch(DcBatch batch)
    {
        var items = new List<ProcessItem>();

        var batchMembers = await _context.DcBatchMembers
            .Where(bm => bm.BatchId == batch.BatchId)
            .ToListAsync();

        foreach (var member in batchMembers)
        {
            // Get next step's EqpId (current step + 1)
            var nextStep = await _context.DcBatches
                .Where(b => b.BatchId == batch.BatchId &&
                           b.CarrierId == member.CarrierId &&
                           b.Step == batch.Step + 1)
                .FirstOrDefaultAsync();

            items.Add(new ProcessItem
            {
                Carrier = member.CarrierId,
                Lot = member.LotId,
                Qty = member.Qty,
                PPID = batch.PPID,
                NextFurnace = nextStep?.EqpId ?? "なし",
                Location = "",
                EndTime = ""
            });
        }

        return items;
    }

    private async Task MarkBatchesAsProcessed(string eqpId, List<List<DcActl>> timeGroups)
    {
        if (!timeGroups.Any()) return;

        // Get all LotIds from actual processing
        var allLotIds = timeGroups.SelectMany(g => g.Select(a => a.LotId)).Distinct().ToList();

        // Get full batch members for this equipment
        var fullBatchMembers = await (from batch in _context.DcBatches
                                      join member in _context.DcBatchMembers
                                      on new { batch.BatchId, CarrierId = batch.CarrierId }
                                      equals new { member.BatchId, CarrierId = member.CarrierId }
                                      where batch.EqpId == eqpId
                                      select new { batch, member }).ToListAsync();

        // Check if any actual LotId exists in batch members
        foreach (var lotId in allLotIds)
        {
            var matchingBatch = fullBatchMembers.FirstOrDefault(fb => fb.member.LotId == lotId);

            if (matchingBatch != null)
            {
                // Mark all batches with the same BatchId as processed
                var batchesToUpdate = await _context.DcBatches
                    .Where(b => b.BatchId == matchingBatch.batch.BatchId)
                    .ToListAsync();

                foreach (var b in batchesToUpdate)
                {
                    b.IsProcessed = true;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
