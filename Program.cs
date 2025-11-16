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

// Add Background Service for batch processing (conditionally based on configuration)
var batchProcessingEnabled = builder.Configuration.GetValue<bool>("BatchProcessing:Enabled", true);
if (batchProcessingEnabled)
{
    builder.Services.AddHostedService<BatchProcessingBackgroundService>();
}

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

    // Add State, Next1, Next2, Next3 columns to DC_Wips table if they don't exist
    // SQLite用のカラム存在チェック（現在使用中）
    var stateColumnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Wips') WHERE name = 'State'")
        .AsEnumerable()
        .FirstOrDefault();

    // Oracle用のカラム存在チェック（将来的に使用）
    // var stateColumnExists = context.Database.SqlQueryRaw<int>(
    //     "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_WIPS' AND COLUMN_NAME = 'STATE'")
    //     .AsEnumerable()
    //     .FirstOrDefault();

    if (stateColumnExists == 0)
    {
        // SQLite用のALTER TABLE（現在使用中）
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Wips ADD COLUMN State TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Wips ADD COLUMN Next1 TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Wips ADD COLUMN Next2 TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Wips ADD COLUMN Next3 TEXT");

        // Oracle用のALTER TABLE（将来的に使用）
        // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_WIPS ADD STATE NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_WIPS ADD NEXT1 NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_WIPS ADD NEXT2 NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_WIPS ADD NEXT3 NVARCHAR2(50)");
    }

    // Add Carrier, Qty, PPID, Next, Location, EndTime columns to DC_Actl table if they don't exist
    // SQLite用のカラム存在チェック（現在使用中）
    var carrierColumnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Actl') WHERE name = 'Carrier'")
        .AsEnumerable()
        .FirstOrDefault();

    // Oracle用のカラム存在チェック（将来的に使用）
    // var carrierColumnExists = context.Database.SqlQueryRaw<int>(
    //     "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_ACTL' AND COLUMN_NAME = 'CARRIER'")
    //     .AsEnumerable()
    //     .FirstOrDefault();

    if (carrierColumnExists == 0)
    {
        // SQLite用のALTER TABLE（現在使用中）
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN Carrier TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN Qty INTEGER DEFAULT 0");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN PPID TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN Next TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN Location TEXT");
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Actl ADD COLUMN EndTime TEXT");

        // Oracle用のALTER TABLE（将来的に使用）
        // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD CARRIER NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD QTY NUMBER(10) DEFAULT 0");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD PPID NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD NEXT NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD LOCATION NVARCHAR2(50)");
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_ACTL ADD ENDTIME TIMESTAMP");
    }

    // Add ProcessedAt column to DC_Batch table if it doesn't exist
    // SQLite用のカラム存在チェック（現在使用中）
    var processedAtColumnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Batch') WHERE name = 'ProcessedAt'")
        .AsEnumerable()
        .FirstOrDefault();

    // Oracle用のカラム存在チェック（将来的に使用）
    // var processedAtColumnExists = context.Database.SqlQueryRaw<int>(
    //     "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_BATCH' AND COLUMN_NAME = 'PROCESSEDAT'")
    //     .AsEnumerable()
    //     .FirstOrDefault();

    if (processedAtColumnExists == 0)
    {
        // SQLite用のALTER TABLE（現在使用中）
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Batch ADD COLUMN ProcessedAt TEXT");

        // Oracle用のALTER TABLE（将来的に使用）
        // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_BATCH ADD PROCESSEDAT TIMESTAMP");
    }

    // Add NextEqpId column to DC_Batch table if it doesn't exist
    // SQLite用のカラム存在チェック（現在使用中）
    var nextEqpIdColumnExists = context.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) as Value FROM pragma_table_info('DC_Batch') WHERE name = 'NextEqpId'")
        .AsEnumerable()
        .FirstOrDefault();

    // Oracle用のカラム存在チェック（将来的に使用）
    // var nextEqpIdColumnExists = context.Database.SqlQueryRaw<int>(
    //     "SELECT COUNT(*) as Value FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'DC_BATCH' AND COLUMN_NAME = 'NEXTEQPID'")
    //     .AsEnumerable()
    //     .FirstOrDefault();

    if (nextEqpIdColumnExists == 0)
    {
        // SQLite用のALTER TABLE（現在使用中）
        context.Database.ExecuteSqlRaw("ALTER TABLE DC_Batch ADD COLUMN NextEqpId TEXT");

        // Oracle用のALTER TABLE（将来的に使用）
        // Oracleに切り替える場合は、上記のSQLite用コードをコメントアウトし、以下のコメントを解除してください
        // context.Database.ExecuteSqlRaw("ALTER TABLE DC_BATCH ADD NEXTEQPID NVARCHAR2(50)");
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
