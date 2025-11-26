using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;
using System.Text.Json;
// TODO: 将来的にSQLクエリで取得する場合は以下のusing文を追加
// using Microsoft.Data.Sqlite;
// using System.Data;

namespace Coordinator.Pages;

public class WipLotListModel : PageModel
{
    private readonly CoordinatorDbContext _context;

    public WipLotListModel(CoordinatorDbContext context)
    {
        _context = context;
    }

    public List<DcWip> WipLots { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? EqpName { get; set; }

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(EqpName))
        {
            // Get carriers that are already registered in DC_Batch
            var registeredCarriers = await _context.DcBatches
                .Select(b => b.CarrierId)
                .Distinct()
                .ToListAsync();

            // Get WIP lots excluding carriers already in batches
            WipLots = await _context.DcWips
                .Where(w => w.TargetEqpId == EqpName && !registeredCarriers.Contains(w.Carrier))
                .OrderBy(w => w.Priority)
                .ToListAsync();

            // TODO: 将来的にSQLクエリで取得する場合
            // 以下のようなSQLクエリでDC_Wipsと同じ項目を取得し、DcWipモデルにマッピングする
            /*
            var sql = @"
                SELECT
                    Priority, Technology, Carrier, LotId, Qty, PartName,
                    CurrentStage, CurrentStep, TargetStage, TargetStep,
                    TargetEqpId, TargetPPID
                FROM [外部データソース]
                WHERE TargetEqpId = @eqpName
                AND Carrier NOT IN (SELECT DISTINCT CarrierId FROM DC_Batch)
                ORDER BY Priority
            ";

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.Add(new SqliteParameter("@eqpName", EqpName));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    WipLots = new List<DcWip>();
                    while (await reader.ReadAsync())
                    {
                        WipLots.Add(new DcWip
                        {
                            Priority = reader.GetInt32(0),
                            Technology = reader.GetString(1),
                            Carrier = reader.GetString(2),
                            LotId = reader.GetString(3),
                            Qty = reader.GetInt32(4),
                            PartName = reader.GetString(5),
                            CurrentStage = reader.GetString(6),
                            CurrentStep = reader.GetString(7),
                            TargetStage = reader.GetString(8),
                            TargetStep = reader.GetString(9),
                            TargetEqpId = reader.GetString(10),
                            TargetPPID = reader.GetString(11)
                        });
                    }
                }
            }
            */
        }
    }

    [BindProperty]
    public List<WipDataItem> WipData { get; set; } = new();

    public IActionResult OnPost()
    {
        if (WipData == null || !WipData.Any())
        {
            Message = "キャリアを選択してください";
            return RedirectToPage(new { eqpName = EqpName });
        }

        // Store selected carrier information in TempData to pass to CreateBatch page
        TempData["SelectedWipData"] = JsonSerializer.Serialize(WipData);

        return RedirectToPage("CreateBatch");
    }
}
