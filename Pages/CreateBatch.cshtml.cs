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
    private readonly ILogger<CreateBatchModel> _logger;

    public CreateBatchModel(CoordinatorDbContext context, ILogger<CreateBatchModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<CarrierStepViewModel> CarrierSteps { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? LotIds { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EqpName { get; set; }

    // Store all PPID/EqpId combinations for each carrier and step
    public string StepOptionsJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(LotIds))
        {
            var lotIdList = LotIds.Split(',').Distinct().ToList();
            var stepOptionsDict = new Dictionary<string, Dictionary<int, List<PpidEqpOption>>>();

            // Get WIP data from TempData (passed from WipLotList)
            Dictionary<string, WipDataItem>? wipDataByLotId = null;
            if (TempData["SelectedWipData"] is string wipDataJson)
            {
                var wipDataList = JsonSerializer.Deserialize<List<WipDataItem>>(wipDataJson);
                if (wipDataList != null)
                {
                    wipDataByLotId = wipDataList.ToDictionary(w => w.LotId);
                }
            }

            // Process each LotId
            foreach (var lotId in lotIdList)
            {
                // Get carrier and WIP info from TempData
                if (wipDataByLotId == null || !wipDataByLotId.ContainsKey(lotId))
                {
                    continue; // Skip if LotId not found in WIP data
                }

                var wipData = wipDataByLotId[lotId];
                var carrier = wipData.Carrier;

                // Get carrier steps from database
                var steps = await _context.DcCarrierSteps
                    .Where(cs => cs.Carrier == carrier)
                    .OrderBy(cs => cs.Step)
                    .ToListAsync();

                // TODO: 将来的にSQLクエリで取得する場合
                // LotIdでCarrierStepsを検索する
                /*
                var sql = @"
                    SELECT
                        Carrier, Qty, Step, EqpId, PPID
                    FROM [外部データソース]
                    WHERE LotId = @lotId
                    ORDER BY Step
                ";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqliteParameter("@lotId", lotId));

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

                var viewModel = new CarrierStepViewModel
                {
                    Carrier = carrier,
                    Qty = wipData.Qty,
                    LotId = lotId,
                    Technology = wipData.Technology,
                    State = wipData.State,
                    Next1 = wipData.Next1,
                    Next2 = wipData.Next2,
                    Next3 = wipData.Next3
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
        var recipeInfo = new RecipeInfo
        {
            EqpId = eqpId,
            PPID = ppid,
            OkNg = "OK",
            SpecialNotes = "なし",
            TrenchDummy = "必要",
            DmType = "AAA",
            TwType = "BBB",
            PosA = "○",
            PosB = "ー",
            PosC = "ー",
            PosD = "○",
            PosE = "○",
            PosF = "ー"
        };

        return new JsonResult(recipeInfo);
    }

    [BindProperty]
    public List<WipDataItem> CarrierData { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(LotIds))
        {
            return RedirectToPage("Index");
        }

        var lotIdList = LotIds.Split(',').Distinct().ToList();

        // Check for duplicate LotIds in DC_BatchMembers
        var errorMessages = new List<string>();
        foreach (var lotId in lotIdList)
        {
            var existingMember = await _context.DcBatchMembers
                .Where(bm => bm.LotId == lotId)
                .FirstOrDefaultAsync();

            if (existingMember != null)
            {
                // Get batch information for this BatchId
                var batchInfo = await _context.DcBatches
                    .Where(b => b.BatchId == existingMember.BatchId)
                    .OrderBy(b => b.CarrierId)
                    .ThenBy(b => b.Step)
                    .ToListAsync();

                if (batchInfo.Any())
                {
                    var carriers = batchInfo.Select(b => b.CarrierId).Distinct();
                    var steps = batchInfo.Select(b => b.Step).Distinct().OrderBy(s => s);
                    var errorMsg = $"{lotId}は以下のバッチに含まれています。workProgress画面で確認してください。" +
                                   $"BatchId: {existingMember.BatchId}, Carrier: {string.Join(", ", carriers)}, Steps: {string.Join(", ", steps)}";
                    errorMessages.Add(errorMsg);
                }
            }
        }

        // If there are duplicate LotIds, return error
        if (errorMessages.Any())
        {
            TempData["ErrorMessage"] = string.Join("<br>", errorMessages);
            return RedirectToPage("CreateBatch", new { LotIds = LotIds, EqpName = EqpName });
        }

        // Generate unique BatchId using timestamp
        var batchId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var createdAt = DateTime.Now;

        // Create dictionary from CarrierData for quick lookup by LotId
        var carrierDataDict = CarrierData.ToDictionary(cd => cd.LotId);

        foreach (var lotId in lotIdList)
        {
            // Get WIP info from form data
            string carrier = "";
            string technology = "";
            int qty = 0;

            if (carrierDataDict.ContainsKey(lotId))
            {
                // Use data from form
                var data = carrierDataDict[lotId];
                carrier = data.Carrier;
                technology = data.Technology;
                qty = data.Qty;
            }
            else
            {
                // Fallback to DC_Wips
                var wipInfo = await _context.DcWips
                    .Where(w => w.LotId == lotId)
                    .FirstOrDefaultAsync();

                if (wipInfo == null) continue;

                carrier = wipInfo.Carrier;
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

                    // Get next step's EqpId (stepNum + 1)
                    var nextStepNum = stepNum + 1;
                    var nextEqpIdKey = $"eqpid_{carrier}_{nextStepNum}";
                    string nextEqpId = "なし";

                    if (Request.Form.ContainsKey(nextEqpIdKey))
                    {
                        var nextEqpIdValue = Request.Form[nextEqpIdKey].ToString();
                        if (!string.IsNullOrEmpty(nextEqpIdValue) && nextEqpIdValue != "選択してください")
                        {
                            nextEqpId = nextEqpIdValue;
                        }
                    }

                    // Add to DC_Batch
                    var batch = new DcBatch
                    {
                        BatchId = batchId,
                        Step = stepNum,
                        CarrierId = carrier,
                        EqpId = eqpId,
                        PPID = ppid,
                        NextEqpId = nextEqpId,
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

        // Log created batch information for maintenance
        await LogBatchCreation(batchId);

        return RedirectToPage("WorkProgress");
    }

    private async Task LogBatchCreation(string batchId)
    {
        try
        {
            // Get created batches
            var createdBatches = await _context.DcBatches
                .Where(b => b.BatchId == batchId)
                .OrderBy(b => b.CarrierId)
                .ThenBy(b => b.Step)
                .ToListAsync();

            // Get created batch members
            var createdMembers = await _context.DcBatchMembers
                .Where(bm => bm.BatchId == batchId)
                .OrderBy(bm => bm.CarrierId)
                .ToListAsync();

            _logger.LogInformation("=== Batch Created Successfully ===");
            _logger.LogInformation("BatchId: {BatchId}", batchId);
            _logger.LogInformation("Created At: {CreatedAt}", createdBatches.FirstOrDefault()?.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss.fff"));
            _logger.LogInformation("");

            // Log DC_Batch records
            _logger.LogInformation("--- DC_Batch Records ({Count}) ---", createdBatches.Count);
            foreach (var batch in createdBatches)
            {
                _logger.LogInformation("  [Step {Step}] Carrier: {CarrierId}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}",
                    batch.Step, batch.CarrierId, batch.EqpId, batch.PPID, batch.NextEqpId);
            }
            _logger.LogInformation("");

            // Log DC_BatchMembers records
            _logger.LogInformation("--- DC_BatchMembers Records ({Count}) ---", createdMembers.Count);
            foreach (var member in createdMembers)
            {
                _logger.LogInformation("  Carrier: {CarrierId}, LotId: {LotId}, Qty: {Qty}, Technology: {Technology}",
                    member.CarrierId, member.LotId, member.Qty, member.Technology);
            }
            _logger.LogInformation("=====================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging batch creation for BatchId: {BatchId}", batchId);
        }
    }
}

// Helper class for PPID/EqpId options
public class PpidEqpOption
{
    public string PPID { get; set; } = "";
    public string EqpId { get; set; } = "";
}
