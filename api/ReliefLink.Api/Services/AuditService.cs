using System.Text.Json;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

public interface IAuditService
{
    /// <summary>
    /// Adds an audit row to the current DbContext unit of work. The caller's SaveChanges commits it
    /// in the same transaction as the business change. Never pass passwords, tokens or secrets in details.
    /// </summary>
    void Record(Guid? userId, string action, string entityType, string? entityId, object? details = null);
}

public class AuditService(AppDbContext db) : IAuditService
{
    public void Record(Guid? userId, string action, string entityType, string? entityId, object? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details)
        });
    }
}
