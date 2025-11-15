using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add DbContext with SQLite（現在使用中）
builder.Services.AddDbContext<CoordinatorDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Oracle用のDbContext設定（将来的に使用）
// Oracleに切り替える場合は、上記のUseSqliteをコメントアウトし、以下のコメントを解除してください
// builder.Services.AddDbContext<CoordinatorDbContext>(options =>
//     options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    // SQLite用のカラム存在チェック（現在使用中）
    var columnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Eqps') WHERE name = 'Note'")
        .AsEnumerable()
        .FirstOrDefault();

    // Oracle用のカラム存在チェック（将来的に使用）
    // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
    // var columnExists = context.Database.SqlQueryRaw<int>(
    //     "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_EQPS' AND COLUMN_NAME = 'NOTE'")
    //     .AsEnumerable()
    //     .FirstOrDefault();

    if (columnExists == 0)
    {
        // SQLite用のALTER TABLE（現在使用中）
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Eqps ADD COLUMN Note TEXT");

        // Oracle用のALTER TABLE（将来的に使用）
        // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_EQPS ADD NOTE NVARCHAR2(2000)");
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
