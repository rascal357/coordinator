using Microsoft.EntityFrameworkCore;
using Coordinator.Models;

namespace Coordinator.Data;

public class CoordinatorDbContext : DbContext
{
    public CoordinatorDbContext(DbContextOptions<CoordinatorDbContext> options)
        : base(options)
    {
    }

    public DbSet<DcEqp> DcEqps { get; set; } = null!;
    public DbSet<DcWip> DcWips { get; set; } = null!;
    public DbSet<DcCarrierStep> DcCarrierSteps { get; set; } = null!;
    public DbSet<DcLotStep> DcLotSteps { get; set; } = null!;
    public DbSet<DcBatch> DcBatches { get; set; } = null!;
    public DbSet<DcBatchMember> DcBatchMembers { get; set; } = null!;
    public DbSet<DcActl> DcActls { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Oracle移行時の注意点:
        // 1. Oracleではテーブル名とカラム名は大文字で作成されるため、
        //    必要に応じて [Table("DC_Eqps")] や [Column("Name")] 属性でマッピングを明示すること
        // 2. Oracleでは識別子（テーブル名・カラム名）の長さ制限があるため注意（通常30文字まで、12.2以降は128文字）
        // 3. Oracleでは文字列型は NVARCHAR2 にマッピングされる
        // 4. インデックス名も自動生成される場合があり、長さ制限に注意

        // Add indexes for performance
        modelBuilder.Entity<DcEqp>()
            .HasIndex(e => new { e.Type, e.Line });
        // Oracle用のインデックス名を明示的に指定する場合（将来的に使用）
        // .HasIndex(e => new { e.Type, e.Line })
        // .HasDatabaseName("IDX_DCEQP_TYPE_LINE");

        modelBuilder.Entity<DcWip>()
            .HasIndex(e => e.TargetEqpId);
        // .HasDatabaseName("IDX_DCWIP_TARGETEQP");

        modelBuilder.Entity<DcCarrierStep>()
            .HasIndex(e => e.Carrier);
        // .HasDatabaseName("IDX_DCCARRIERSTEP_CARRIER");

        modelBuilder.Entity<DcLotStep>()
            .HasIndex(e => e.LotId);
        // .HasDatabaseName("IDX_DCLOTSTEP_LOTID");

        modelBuilder.Entity<DcBatch>()
            .HasIndex(e => new { e.BatchId, e.EqpId });
        // .HasDatabaseName("IDX_DCBATCH_BATCHID_EQPID");

        modelBuilder.Entity<DcBatchMember>()
            .HasIndex(e => e.BatchId);
        // .HasDatabaseName("IDX_DCBATCHMEMBER_BATCHID");

        modelBuilder.Entity<DcActl>()
            .HasIndex(e => new { e.EqpId, e.TrackInTime });
        // .HasDatabaseName("IDX_DCACTL_EQPID_TRACKIN");
    }
}
