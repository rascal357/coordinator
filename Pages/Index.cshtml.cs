using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;

namespace Coordinator.Pages;

public class IndexModel : PageModel
{
    private readonly CoordinatorDbContext _context;

    public IndexModel(CoordinatorDbContext context)
    {
        _context = context;
    }

    public List<DcEqp> Equipments { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<string> Lines { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string>? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LineFilter { get; set; }

    public async Task OnGetAsync()
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

        Equipments = await query.OrderBy(e => e.Type).ThenBy(e => e.Name).ToListAsync();

        // Get distinct types and lines for filters
        Types = await _context.DcEqps.Select(e => e.Type).Distinct().OrderBy(t => t).ToListAsync();
        Lines = await _context.DcEqps.Select(e => e.Line).Distinct().OrderBy(l => l).ToListAsync();
    }
}
