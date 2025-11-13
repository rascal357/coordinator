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

            foreach (var carrier in carrierList)
            {
                // Get carrier steps
                var steps = await _context.DcCarrierSteps
                    .Where(cs => cs.Carrier == carrier)
                    .OrderBy(cs => cs.Step)
                    .ToListAsync();

                // Get WIP info for carrier
                var wipInfo = await _context.DcWips
                    .Where(w => w.Carrier == carrier)
                    .FirstOrDefaultAsync();

                var viewModel = new CarrierStepViewModel
                {
                    Carrier = carrier,
                    Qty = steps.FirstOrDefault()?.Qty ?? 0,
                    LotId = wipInfo?.LotId ?? "",
                    Technology = wipInfo?.Technology ?? ""
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

        foreach (var carrier in carrierList)
        {
            // Get WIP info for carrier
            var wipInfo = await _context.DcWips
                .Where(w => w.Carrier == carrier)
                .FirstOrDefaultAsync();

            if (wipInfo == null) continue;

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
                LotId = wipInfo.LotId,
                Qty = wipInfo.Qty,
                Technology = wipInfo.Technology
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
