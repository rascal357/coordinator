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

public class CreateBatchModel : PageModel
{
    private readonly CoordinatorDbContext _context;

    public CreateBatchModel(CoordinatorDbContext context)
    {
        _context = context;
    }

    public List<CarrierStepViewModel> CarrierSteps { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Carriers { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EqpName { get; set; }

    // Store all PPID/EqpId combinations for each carrier and step
    public string StepOptionsJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(Carriers))
        {
            var carrierList = Carriers.Split(',').Distinct().ToList();
            var stepOptionsDict = new Dictionary<string, Dictionary<int, List<PpidEqpOption>>>();

            // Try to get WIP data from TempData (passed from WipLotList)
            Dictionary<string, WipDataItem>? wipDataDict = null;
            if (TempData["SelectedWipData"] is string wipDataJson)
            {
                var wipDataList = JsonSerializer.Deserialize<List<WipDataItem>>(wipDataJson);
                if (wipDataList != null)
                {
                    wipDataDict = wipDataList.ToDictionary(w => w.Carrier);
                }
            }

            foreach (var carrier in carrierList)
            {
                // Get carrier steps
                var steps = await _context.DcCarrierSteps
                    .Where(cs => cs.Carrier == carrier)
                    .OrderBy(cs => cs.Step)
                    .ToListAsync();

                // TODO: 将来的にSQLクエリで取得する場合
                // 以下のようなSQLクエリでDC_CarrierStepsと同じ項目を取得し、DcCarrierStepモデルにマッピングする
                /*
                var sql = @"
                    SELECT
                        Carrier, Qty, Step, EqpId, PPID
                    FROM [外部データソース]
                    WHERE Carrier = @carrier
                    ORDER BY Step
                ";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqliteParameter("@carrier", carrier));

                    await _context.Database.OpenConnectionAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        steps = new List<DcCarrierStep>();
                        while (await reader.ReadAsync())
                        {
                            steps.Add(new DcCarrierStep
                            {
                                Carrier = reader.GetString(0),
                                Qty = reader.GetInt32(1),
                                Step = reader.GetInt32(2),
                                EqpId = reader.GetString(3),
                                PPID = reader.GetString(4)
                            });
                        }
                    }
                }
                */

                // Get WIP info: prioritize TempData, fallback to DC_Wips
                string lotId = "";
                string technology = "";
                int qty = steps.FirstOrDefault()?.Qty ?? 0;

                if (wipDataDict != null && wipDataDict.ContainsKey(carrier))
                {
                    // Use data from TempData
                    var wipData = wipDataDict[carrier];
                    lotId = wipData.LotId;
                    technology = wipData.Technology;
                    qty = wipData.Qty;
                }
                else
                {
                    // Fallback to DC_Wips
                    var wipInfo = await _context.DcWips
                        .Where(w => w.Carrier == carrier)
                        .FirstOrDefaultAsync();

                    lotId = wipInfo?.LotId ?? "";
                    technology = wipInfo?.Technology ?? "";
                }

                var viewModel = new CarrierStepViewModel
                {
                    Carrier = carrier,
                    Qty = qty,
                    LotId = lotId,
                    Technology = technology
                };

                // Store all PPID/EqpId options for each step
                var carrierStepOptions = new Dictionary<int, List<PpidEqpOption>>();

                for (int stepNum = 1; stepNum <= 4; stepNum++)
                {
                    var stepData = steps.Where(s => s.Step == stepNum).ToList();
                    var options = stepData.Select(s => new PpidEqpOption
                    {
                        PPID = s.PPID,
                        EqpId = s.EqpId
                    }).ToList();

                    carrierStepOptions[stepNum] = options;

                    // Set default step info (first option or empty)
                    var stepInfo = new StepInfo
                    {
                        EqpId = stepData.FirstOrDefault()?.EqpId ?? "",
                        PPID = stepData.FirstOrDefault()?.PPID ?? ""
                    };

                    switch (stepNum)
                    {
                        case 1:
                            viewModel.Step1 = stepInfo;
                            break;
                        case 2:
                            viewModel.Step2 = stepInfo;
                            break;
                        case 3:
                            viewModel.Step3 = stepInfo;
                            break;
                        case 4:
                            viewModel.Step4 = stepInfo;
                            break;
                    }
                }

                stepOptionsDict[carrier] = carrierStepOptions;
                CarrierSteps.Add(viewModel);
            }

            // Serialize to JSON for JavaScript
            StepOptionsJson = JsonSerializer.Serialize(stepOptionsDict);
        }
    }

    // API endpoint to get equipment recipe information
    public async Task<IActionResult> OnGetRecipeInfoAsync(string eqpId, string ppid)
    {
        // TODO: Replace with actual SQL query to fetch recipe information
        // For now, return dummy data
        var recipeInfo = new
        {
            eqpId = eqpId,
            ppid = ppid,
            okNg = "OK",
            specialNotes = "なし",
            trenchDummy = "必要",
            dmType = "AAA",
            twType = "BBB",
            posA = "○",
            posB = "ー",
            posC = "ー",
            posD = "○",
            posE = "○",
            posF = "ー"
        };

        return new JsonResult(recipeInfo);
    }

    [BindProperty]
    public List<WipDataItem> CarrierData { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Carriers))
        {
            return RedirectToPage("Index");
        }

        var carrierList = Carriers.Split(',').Distinct().ToList();

        // Generate unique BatchId using timestamp
        var batchId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var createdAt = DateTime.Now;

        // Create dictionary from CarrierData for quick lookup
        var carrierDataDict = CarrierData.ToDictionary(cd => cd.Carrier);

        foreach (var carrier in carrierList)
        {
            // Get WIP info: prioritize form data, fallback to DC_Wips
            string lotId = "";
            string technology = "";
            int qty = 0;

            if (carrierDataDict.ContainsKey(carrier))
            {
                // Use data from form
                var data = carrierDataDict[carrier];
                lotId = data.LotId;
                technology = data.Technology;
                qty = data.Qty;
            }
            else
            {
                // Fallback to DC_Wips
                var wipInfo = await _context.DcWips
                    .Where(w => w.Carrier == carrier)
                    .FirstOrDefaultAsync();

                if (wipInfo == null) continue;

                lotId = wipInfo.LotId;
                technology = wipInfo.Technology;
                qty = wipInfo.Qty;
            }

            // Process each step (1-4)
            for (int stepNum = 1; stepNum <= 4; stepNum++)
            {
                var ppidKey = $"ppid_{carrier}_{stepNum}";
                var eqpIdKey = $"eqpid_{carrier}_{stepNum}";

                if (Request.Form.ContainsKey(ppidKey) && Request.Form.ContainsKey(eqpIdKey))
                {
                    var ppid = Request.Form[ppidKey].ToString();
                    var eqpId = Request.Form[eqpIdKey].ToString();

                    // Skip if not selected
                    if (string.IsNullOrEmpty(ppid) || ppid == "選択してください" ||
                        string.IsNullOrEmpty(eqpId) || eqpId == "選択してください")
                    {
                        continue;
                    }

                    // Add to DC_Batch
                    var batch = new DcBatch
                    {
                        BatchId = batchId,
                        Step = stepNum,
                        CarrierId = carrier,
                        EqpId = eqpId,
                        PPID = ppid,
                        IsProcessed = false,
                        CreatedAt = createdAt
                    };
                    _context.DcBatches.Add(batch);
                }
            }

            // Add to DC_BatchMembers
            var batchMember = new DcBatchMember
            {
                BatchId = batchId,
                CarrierId = carrier,
                LotId = lotId,
                Qty = qty,
                Technology = technology
            };
            _context.DcBatchMembers.Add(batchMember);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("WorkProgress");
    }
}

// Helper class for PPID/EqpId options
public class PpidEqpOption
{
    public string PPID { get; set; } = "";
    public string EqpId { get; set; } = "";
}

// Helper class for WIP data passed from WipLotList
public class WipDataItem
{
    public string Carrier { get; set; } = "";
    public string LotId { get; set; } = "";
    public string Technology { get; set; } = "";
    public int Qty { get; set; }
}
