using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add DbContext with SQLite
builder.Services.AddDbContext<CoordinatorDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Background Service for batch processing
builder.Services.AddHostedService<BatchProcessingBackgroundService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<CoordinatorDbContext>();
    context.Database.EnsureCreated();

    // Add Note column to DC_Eqps table if it doesn't exist
    var columnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Eqps') WHERE name = 'Note'")
        .AsEnumerable()
        .FirstOrDefault();

    if (columnExists == 0)
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Eqps ADD COLUMN Note TEXT");
    }

    DbInitializer.Initialize(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Redirect root to WorkProgress
app.MapGet("/", () => Results.Redirect("/WorkProgress"));

app.MapRazorPages();

app.Run();
