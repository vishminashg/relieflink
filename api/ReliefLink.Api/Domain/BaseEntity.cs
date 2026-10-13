namespace ReliefLink.Api.Domain;

/// <summary>
/// Every table in ReliefLink carries an Id plus CreatedAt/UpdatedAt.
/// Timestamps are stamped centrally in AppDbContext.SaveChanges.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
