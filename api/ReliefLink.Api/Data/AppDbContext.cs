using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data;

public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.District).HasMaxLength(60);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Role);
        });

        b.Entity<AgentWorkflow>(e =>
        {
            e.ToTable("AgentWorkflows");
            e.Property(x => x.Objective).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.PlanJson).HasColumnType("jsonb");
            e.Property(x => x.ProposalJson).HasColumnType("jsonb");
            e.Property(x => x.FailureReason).HasMaxLength(2000);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.ReliefRequestId);
        });

        b.Entity<WorkflowStep>(e =>
        {
            e.ToTable("WorkflowSteps");
            e.Property(x => x.AgentName).HasMaxLength(60).IsRequired();
            e.Property(x => x.Goal).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.InputJson).HasColumnType("jsonb");
            e.Property(x => x.OutputJson).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasOne(x => x.Workflow).WithMany(w => w.Steps).HasForeignKey(x => x.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.WorkflowId, x.Sequence });
        });

        b.Entity<ToolCall>(e =>
        {
            e.ToTable("ToolCalls");
            e.Property(x => x.ToolName).HasMaxLength(80).IsRequired();
            e.Property(x => x.ArgumentsJson).HasColumnType("jsonb");
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.HasOne(x => x.Step).WithMany(s => s.ToolCalls).HasForeignKey(x => x.StepId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.StepId);
        });

        b.Entity<ValidationResult>(e =>
        {
            e.ToTable("ValidationResults");
            e.Property(x => x.CheckName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(2000);
            e.HasOne(x => x.Workflow).WithMany(w => w.Validations).HasForeignKey(x => x.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.WorkflowId);
        });

        b.Entity<ApprovalDecision>(e =>
        {
            e.ToTable("ApprovalDecisions");
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne(x => x.Workflow).WithMany(w => w.Decisions).HasForeignKey(x => x.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.WorkflowId);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // Each component keeps its mapping in Data/Configurations/*.cs; they are all picked up here.
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
