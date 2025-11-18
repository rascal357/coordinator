using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;
using System.Text.Json;

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

    public List<LotStepViewModel> LotSteps { get; set; } = new();

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

                // Get lot steps from database
                var steps = await _context.DcLotSteps
                    .Where(ls => ls.LotId == lotId)
                    .OrderBy(ls => ls.Step)
                    .ToListAsync();

                var viewModel = new LotStepViewModel
                {
                    LotId = lotId,
                    Carrier = carrier,
                    Qty = wipData.Qty,
                    Technology = wipData.Technology,
                    State = wipData.State,
                    Next1 = wipData.Next1,
                    Next2 = wipData.Next2,
                    Next3 = wipData.Next3
                };

                // Store all PPID/EqpId options for each step
                var lotStepOptions = new Dictionary<int, List<PpidEqpOption>>();

                for (int stepNum = 1; stepNum <= 4; stepNum++)
                {
                    var stepData = steps.Where(s => s.Step == stepNum).ToList();
                    var options = stepData.Select(s => new PpidEqpOption
                    {
                        PPID = s.PPID,
                        EqpId = s.EqpId
                    }).ToList();

                    lotStepOptions[stepNum] = options;

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

                stepOptionsDict[lotId] = lotStepOptions;
                LotSteps.Add(viewModel);
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

        // Check for duplicate LotIds in DC_Batch
        var errorMessages = new List<string>();
        foreach (var lotId in lotIdList)
        {
            var existingBatch = await _context.DcBatches
                .Where(b => b.LotId == lotId)
                .FirstOrDefaultAsync();

            if (existingBatch != null)
            {
                // Get batch information for this BatchId
                var batchInfo = await _context.DcBatches
                    .Where(b => b.BatchId == existingBatch.BatchId)
                    .OrderBy(b => b.LotId)
                    .ThenBy(b => b.Step)
                    .ToListAsync();

                if (batchInfo.Any())
                {
                    var lotIds = batchInfo.Select(b => b.LotId).Distinct();
                    var steps = batchInfo.Select(b => b.Step).Distinct().OrderBy(s => s);
                    var errorMsg = $"{lotId}は以下のバッチに含まれています。workProgress画面で確認してください。" +
                                   $"BatchId: {existingBatch.BatchId}, LotId: {string.Join(", ", lotIds)}, Steps: {string.Join(", ", steps)}";
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

        // Validate that all required steps have PPID and EqpId selected
        var validationErrors = new List<string>();
        var carrierDataDict = CarrierData.ToDictionary(cd => cd.LotId);

        foreach (var lotId in lotIdList)
        {
            // Get all available steps for this lot from DC_LotSteps
            var availableSteps = await _context.DcLotSteps
                .Where(ls => ls.LotId == lotId)
                .Select(ls => ls.Step)
                .Distinct()
                .ToListAsync();

            // Check each available step has PPID and EqpId selected
            foreach (var stepNum in availableSteps)
            {
                var ppidKey = $"ppid_{lotId}_{stepNum}";
                var eqpIdKey = $"eqpid_{lotId}_{stepNum}";

                var ppid = Request.Form[ppidKey].ToString();
                var eqpId = Request.Form[eqpIdKey].ToString();

                if (string.IsNullOrEmpty(ppid) || ppid == "選択してください")
                {
                    validationErrors.Add($"{lotId} のStep {stepNum}のPPIDを選択してください");
                }

                if (string.IsNullOrEmpty(eqpId) || eqpId == "選択してください")
                {
                    validationErrors.Add($"{lotId} のStep {stepNum}のEqpIdを選択してください");
                }
            }
        }

        // If there are validation errors, return to page with error message
        if (validationErrors.Any())
        {
            TempData["ErrorMessage"] = string.Join("<br>", validationErrors);
            return RedirectToPage("CreateBatch", new { LotIds = LotIds, EqpName = EqpName });
        }

        // Generate unique BatchId using timestamp
        var batchId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var createdAt = DateTime.Now;

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
                var ppidKey = $"ppid_{lotId}_{stepNum}";
                var eqpIdKey = $"eqpid_{lotId}_{stepNum}";

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
                    var nextEqpIdKey = $"eqpid_{lotId}_{nextStepNum}";
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
                        LotId = lotId,
                        Qty = qty,
                        Technology = technology,
                        EqpId = eqpId,
                        PPID = ppid,
                        NextEqpId = nextEqpId,
                        IsProcessed = 0,
                        CreatedAt = createdAt
                    };
                    _context.DcBatches.Add(batch);
                }
            }
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
                .OrderBy(b => b.LotId)
                .ThenBy(b => b.Step)
                .ToListAsync();

            _logger.LogInformation("=== Batch Created Successfully ===");
            _logger.LogInformation("BatchId: {BatchId}", batchId);
            _logger.LogInformation("Created At: {CreatedAt}", createdBatches.FirstOrDefault()?.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss.fff"));
            _logger.LogInformation("");

            // Log DC_Batch records
            _logger.LogInformation("--- DC_Batch Records ({Count}) ---", createdBatches.Count);
            foreach (var batch in createdBatches)
            {
                _logger.LogInformation("  [Step {Step}] LotId: {LotId}, Carrier: {CarrierId}, Qty: {Qty}, Technology: {Technology}, EqpId: {EqpId}, PPID: {PPID}, NextEqpId: {NextEqpId}",
                    batch.Step, batch.LotId, batch.CarrierId, batch.Qty, batch.Technology, batch.EqpId, batch.PPID, batch.NextEqpId);
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
