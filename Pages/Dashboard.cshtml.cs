using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

namespace Coordinator.Pages;

public class DashboardModel : PageModel
{
    private readonly CoordinatorDbContext _context;
    private readonly ILogger<DashboardModel> _logger;
    private readonly IConfiguration _configuration;

    public DashboardModel(CoordinatorDbContext context, ILogger<DashboardModel> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public Dictionary<string, List<EquipmentProgressViewModel>> EquipmentsByType { get; set; } = new();
    public Dictionary<string, EquipmentProgressViewModel> EquipmentByName { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<string> Lines { get; set; } = new();
    public HashSet<string> YuuTypes { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string>? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LineFilter { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["AutoRefreshIntervalSeconds"] = _configuration.GetValue<int>("Dashboard:AutoRefreshIntervalSeconds", 30);
        await LoadProgressData();
        var yuuList = await _context.DcEqpTypes.Where(t => t.Yuu == 1).Select(t => t.Type).ToListAsync();
        YuuTypes = new HashSet<string>(yuuList);
    }

    public async Task<IActionResult> OnGetRefreshAsync()
    {
        await LoadProgressData();
        var yuuList = await _context.DcEqpTypes.Where(t => t.Yuu == 1).Select(t => t.Type).ToListAsync();
        YuuTypes = new HashSet<string>(yuuList);
        return new JsonResult(new { success = true, data = EquipmentsByType, yuuTypes = YuuTypes });
    }

    public async Task<IActionResult> OnPostUpdateNoteAsync([FromForm] string eqpName, [FromForm] string note)
    {
        if (string.IsNullOrEmpty(eqpName))
            return new JsonResult(new { success = false, message = "Equipment name is required" });
        try
        {
            var equipment = await _context.DcEqps.Where(e => e.Name == eqpName).FirstOrDefaultAsync();
            if (equipment == null)
                return new JsonResult(new { success = false, message = "Equipment not found" });
            equipment.Note = string.IsNullOrWhiteSpace(note) ? null : note;
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostUpdateScheduleAsync([FromForm] string eqpName, [FromForm] string schedule)
    {
        if (string.IsNullOrEmpty(eqpName))
            return new JsonResult(new { success = false, message = "Equipment name is required" });
        try
        {
            var equipment = await _context.DcEqps.Where(e => e.Name == eqpName).FirstOrDefaultAsync();
            if (equipment == null)
                return new JsonResult(new { success = false, message = "Equipment not found" });
            equipment.Schedule = string.IsNullOrWhiteSpace(schedule) ? null : schedule;
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostToggleYuuAsync([FromForm] string typeName, [FromForm] int yuu)
    {
        if (string.IsNullOrEmpty(typeName))
            return new JsonResult(new { success = false, message = "Type name is required" });

        try
        {
            var record = await _context.DcEqpTypes.FindAsync(typeName);
            if (record == null)
            {
                _context.DcEqpTypes.Add(new DcEqpType { Type = typeName, Yuu = yuu });
            }
            else
            {
                record.Yuu = yuu;
            }
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
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

        Types = await _context.DcEqps.Select(e => e.Type).Distinct().OrderBy(t => t).ToListAsync();
        Lines = await _context.DcEqps.Select(e => e.Line).Distinct().OrderBy(l => l).ToListAsync();

        var equipmentIds = equipments.Select(e => e.Name).ToList();

        var allActls = await _context.DcActls
            .Where(a => equipmentIds.Contains(a.EqpId))
            .OrderBy(a => a.EqpId)
            .ThenBy(a => a.TrackInTime)
            .ToListAsync();

        var actlsByEquipment = allActls
            .Where(a => a.EqpId != null)
            .GroupBy(a => a.EqpId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.TrackInTime).ToList());

        var allBatches = await _context.DcBatches
            .Where(b => equipmentIds.Contains(b.EqpId) && b.IsProcessed == 0)
            .ToListAsync();

        var batchesByEquipment = allBatches
            .GroupBy(b => b.EqpId)
            .ToDictionary(g => g.Key, g => g.ToList());

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
                    Note = eqp.Note,
                    Schedule = eqp.Schedule
                };

                var actls = actlsByEquipment.ContainsKey(eqp.Name)
                    ? actlsByEquipment[eqp.Name]
                    : new List<DcActl>();

                var timeGroups = GroupByTimeWindow(actls, TimeSpan.FromMinutes(5));

                if (timeGroups.Count > 0)
                {
                    progressViewModel.InProcess = await CreateProcessItemsFromActls(timeGroups[0]);
                }

                if (timeGroups.Count > 1)
                {
                    progressViewModel.Waiting = await CreateProcessItemsFromActls(timeGroups[1]);
                }

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

        EquipmentByName = EquipmentsByType.Values
            .SelectMany(v => v)
            .ToDictionary(e => e.EqpName, e => e);
    }

    private List<List<DcActl>> GroupByTimeWindow(List<DcActl> actls, TimeSpan window)
    {
        if (!actls.Any()) return new List<List<DcActl>>();

        var groups = new List<List<DcActl>>();
        var currentGroup = new List<DcActl> { actls[0] };
        var currentTime = actls[0].TrackInTime ?? DateTime.MinValue;

        for (int i = 1; i < actls.Count; i++)
        {
            var trackInTime = actls[i].TrackInTime ?? DateTime.MinValue;
            var timeDiff = Math.Abs((trackInTime - currentTime).TotalMinutes);

            if (timeDiff <= window.TotalMinutes)
            {
                currentGroup.Add(actls[i]);
            }
            else
            {
                groups.Add(currentGroup);
                currentGroup = new List<DcActl> { actls[i] };
                currentTime = trackInTime;
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

        var lotIds = actls.Select(a => a.LotId).Where(l => l != null).Distinct().ToList();
        var wipList = await _context.DcWips.Where(w => lotIds.Contains(w.LotId)).ToListAsync();
        var wipPriorities = new Dictionary<string, int>();
        foreach (var wip in wipList)
            wipPriorities.TryAdd(wip.LotId, wip.Priority);

        foreach (var actl in actls)
        {
            string? nextFurnace = actl.Next;

            var latestBatch = await _context.DcBatches
                .Where(b => b.EqpId == actl.EqpId && b.LotId == actl.LotId && b.ProcessedAt == actl.TrackInTime)
                .FirstOrDefaultAsync();

            if (latestBatch != null)
            {
                nextFurnace = latestBatch.NextEqpId;
            }

            items.Add(new ProcessItem
            {
                Carrier = actl.Carrier ?? string.Empty,
                Lot = actl.LotId ?? string.Empty,
                Qty = actl.Qty ?? 0,
                Priority = actl.LotId != null && wipPriorities.TryGetValue(actl.LotId, out var p) ? p : 0,
                PPID = actl.PPID ?? string.Empty,
                NextFurnace = nextFurnace ?? string.Empty,
                Location = actl.Location ?? string.Empty,
                EndTime = actl.EndTime ?? string.Empty
            });
        }

        return items.OrderBy(i => i.Lot).ToList();
    }

    private async Task<List<ProcessItem>> CreateProcessItemsFromBatch(DcBatch batch, string eqpId)
    {
        var items = new List<ProcessItem>();

        var allSteps = await _context.DcBatches
            .Where(b => b.BatchId == batch.BatchId)
            .ToListAsync();

        var batches = allSteps
            .Where(b => b.EqpId == eqpId)
            .GroupBy(b => new { b.LotId, b.CarrierId, b.Qty, b.Technology })
            .Select(g => g.OrderBy(b => b.Step).First())
            .ToList();

        foreach (var batchItem in batches)
        {
            var prevEqpId = "";
            if (batchItem.Step > 1)
            {
                var prevStep = allSteps
                    .FirstOrDefault(b => b.LotId == batchItem.LotId && b.Step == batchItem.Step - 1);
                prevEqpId = prevStep?.EqpId ?? "";
            }

            items.Add(new ProcessItem
            {
                Carrier = batchItem.CarrierId,
                Lot = batchItem.LotId ?? "",
                Qty = batchItem.Qty,
                Priority = batchItem.Priority,
                PrevEqpId = prevEqpId,
                PPID = batchItem.PPID,
                NextFurnace = batchItem.NextEqpId,
                Location = "",
                EndTime = ""
            });
        }

        return items.OrderBy(i => i.Lot).ToList();
    }
}
