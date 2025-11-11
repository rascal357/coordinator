using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

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
        }
    }

    public async Task<IActionResult> OnPostAsync(List<string> selectedCarriers)
    {
        if (selectedCarriers == null || !selectedCarriers.Any())
        {
            Message = "キャリアを選択してください";
            return RedirectToPage(new { eqpName = EqpName });
        }

        // Remove duplicates
        var uniqueCarriers = selectedCarriers.Distinct().ToList();

        // Get carrier information
        var wipData = await _context.DcWips
            .Where(w => uniqueCarriers.Contains(w.Carrier))
            .Select(w => new
            {
                w.Carrier,
                w.LotId,
                w.Technology,
                w.Qty
            })
            .ToListAsync();

        // Serialize data to pass to CreateBatch page
        var carriersParam = string.Join(",", uniqueCarriers);

        return RedirectToPage("CreateBatch", new { carriers = carriersParam });
    }
}
