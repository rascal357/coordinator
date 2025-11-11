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
    public DbSet<DcBatch> DcBatches { get; set; } = null!;
    public DbSet<DcBatchMember> DcBatchMembers { get; set; } = null!;
    public DbSet<DcActl> DcActls { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Add indexes for performance
        modelBuilder.Entity<DcEqp>()
            .HasIndex(e => new { e.Type, e.Line });

        modelBuilder.Entity<DcWip>()
            .HasIndex(e => e.TargetEqpId);

        modelBuilder.Entity<DcCarrierStep>()
            .HasIndex(e => e.Carrier);

        modelBuilder.Entity<DcBatch>()
            .HasIndex(e => new { e.BatchId, e.EqpId });

        modelBuilder.Entity<DcBatchMember>()
            .HasIndex(e => e.BatchId);

        modelBuilder.Entity<DcActl>()
            .HasIndex(e => new { e.EqpId, e.TrackInTime });
    }
}
