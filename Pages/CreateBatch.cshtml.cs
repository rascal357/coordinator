using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

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

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(Carriers))
        {
            var carrierList = Carriers.Split(',').ToList();

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

                // Populate step information
                foreach (var step in steps)
                {
                    var stepInfo = new StepInfo
                    {
                        EqpId = step.EqpId,
                        PPID = step.PPID
                    };

                    switch (step.Step)
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

                CarrierSteps.Add(viewModel);
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Carriers))
        {
            return RedirectToPage("Index");
        }

        var carrierList = Carriers.Split(',').ToList();

        // Generate unique BatchId using timestamp
        var batchId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var createdAt = DateTime.Now;

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

            if (wipInfo == null) continue;

            // Add to DC_Batch
            foreach (var step in steps)
            {
                var batch = new DcBatch
                {
                    BatchId = batchId,
                    Step = step.Step,
                    CarrierId = carrier,
                    EqpId = step.EqpId,
                    PPID = step.PPID,
                    IsProcessed = false,
                    CreatedAt = createdAt
                };
                _context.DcBatches.Add(batch);
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
