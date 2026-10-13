using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Data.Configurations;

/// <summary>Extra mapping for the workflow tables, added by Component A.</summary>
public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> e)
    {
        // At most one live workflow per request, enforced by the database itself.
        // Finished workflows (Executed, Rejected, FailedSafe) are not counted, so a request can be planned again.
        e.HasIndex(x => x.ReliefRequestId)
            .HasDatabaseName("UX_AgentWorkflows_ActivePerRequest")
            .IsUnique()
            .HasFilter("\"Status\" IN ('Submitted','Planning','Proposed','Validating','PendingCoordinatorApproval','Approved','RevisionRequested')");
    }
}

public class ValidationResultConfiguration : IEntityTypeConfiguration<ValidationResult>
{
    public void Configure(EntityTypeBuilder<ValidationResult> e)
    {
        e.Property(x => x.Attempt).HasDefaultValue(1);
    }
}
