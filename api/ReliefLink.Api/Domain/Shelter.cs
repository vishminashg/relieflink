namespace ReliefLink.Api.Domain;

public sealed class Shelter : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<ShelterCheckIn> CheckIns { get; set; } = new List<ShelterCheckIn>();
}

public sealed class ShelterCheckIn : BaseEntity
{
    public Guid ShelterId { get; set; }
    public Shelter Shelter { get; set; } = null!;

    public Guid? ReliefRequestId { get; set; }
    public ReliefRequest? ReliefRequest { get; set; }

    public int HouseholdSize { get; set; }
    public DateTime CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public Guid CheckedInByUserId { get; set; }
    public User CheckedInByUser { get; set; } = null!;

    public bool IsActive => CheckedOutAt is null;
}

public static class ShelterCapacityRules
{
    public static int OccupiedSpaces(IEnumerable<ShelterCheckIn> checkIns) =>
        checkIns.Where(x => x.IsActive).Sum(x => x.HouseholdSize);

    public static bool CanCheckIn(int capacity, int occupiedSpaces, int householdSize) =>
        householdSize > 0 && occupiedSpaces + householdSize <= capacity;
}
