using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Controllers.Dtos;

public record ShelterInput(
    string Name,
    string District,
    string Address,
    double Latitude,
    double Longitude,
    int Capacity,
    bool IsActive = true,
    string? Notes = null);

public record ShelterCheckInInput(int HouseholdSize, Guid? ReliefRequestId = null);

public record ShelterDto(
    Guid Id,
    string Name,
    string District,
    string Address,
    double Latitude,
    double Longitude,
    int Capacity,
    int Occupied,
    int Available,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ShelterDto From(Shelter shelter) 
    {
        var occupied = ShelterCapacityRules.OccupiedSpaces(shelter.CheckIns);
        return new ShelterDto(shelter.Id, shelter.Name, shelter.District, shelter.Address,
            shelter.Latitude, shelter.Longitude, shelter.Capacity, occupied,
            Math.Max(0, shelter.Capacity - occupied), shelter.IsActive, shelter.Notes,
            shelter.CreatedAt, shelter.UpdatedAt);
    }
}

public record ShelterCheckInDto(
    Guid Id,
    Guid ShelterId,
    Guid? ReliefRequestId,
    int HouseholdSize,
    DateTime CheckedInAt,
    DateTime? CheckedOutAt,
    bool IsActive);
