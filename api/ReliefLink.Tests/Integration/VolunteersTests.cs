using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Tests.Infrastructure;
using Xunit;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public class VolunteersTests(ApiFactory factory)
{
    private static string NewToken() => Guid.NewGuid().ToString("N");

    private async Task<(HttpClient Client, Guid UserId)> NewVolunteerUserAsync()
    {
        var client = factory.CreateClient();
        var email = $"volunteer_{NewToken()}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test Volunteer",
            email,
            password = "Passw0rdOK",
            role = "Volunteer"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<ApiFactory.AuthPayload>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.User.Id);
    }

    [Fact]
    public async Task Create_AsVolunteer_Returns201_WithProfile()
    {
        var (client, _) = await NewVolunteerUserAsync();

        var payload = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kamal Perera",
            EmergencyContactPhone: "0771234567",
            MaxHoursPerWeek: 35,
            Notes: "Boat rescue trained",
            Skills:
            [
                new VolunteerSkillInput(VolunteerSkillType.BoatOperation, SkillProficiency.Expert, "CERT-BOAT-101")
            ]);

        var response = await client.PostAsJsonAsync("/api/volunteers", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = (await response.Content.ReadFromJsonAsync<VolunteerDto>())!;
        Assert.Equal("Kalutara", dto.District);
        Assert.Equal("Kamal Perera", dto.EmergencyContactName);
        Assert.Equal(35, dto.MaxHoursPerWeek);
        Assert.Single(dto.Skills);
        Assert.Equal("BoatOperation", dto.Skills[0].Skill);
        Assert.Equal("CERT-BOAT-101", dto.Skills[0].CertificateReference);
    }

    [Fact]
    public async Task Create_WhenProfileAlreadyExists_Returns409Conflict()
    {
        var (client, _) = await NewVolunteerUserAsync();

        var payload = new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Kamal Perera",
            EmergencyContactPhone: "0771234567");

        var first = await client.PostAsJsonAsync("/api/volunteers", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/volunteers", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_AnotherVolunteersProfile_AsVolunteer_Returns404NotFound()
    {
        var (v1Client, _) = await NewVolunteerUserAsync();
        var (v2Client, _) = await NewVolunteerUserAsync();

        var createRes = await v1Client.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara", EmergencyContactName: "Contact A", EmergencyContactPhone: "0771234567"));
        var v1Dto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        // Volunteer 2 attempts to fetch Volunteer 1's profile
        var response = await v2Client.GetAsync($"/api/volunteers/{v1Dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsStaff_CanViewAnyVolunteerProfile()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        var createRes = await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Galle", EmergencyContactName: "Contact B", EmergencyContactPhone: "0777654321"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        var response = await coordinator.GetAsync($"/api/volunteers/{vDto.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loaded = (await response.Content.ReadFromJsonAsync<VolunteerDto>())!;
        Assert.Equal("Galle", loaded.District);
    }

    [Fact]
    public async Task UpdateSkills_AndAvailability_AsVolunteer_SavesCorrectly()
    {
        var (client, _) = await NewVolunteerUserAsync();
        var createRes = await client.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Colombo", EmergencyContactName: "Contact C", EmergencyContactPhone: "0771122334"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        // 1. Update skills
        var skillsRes = await client.PutAsJsonAsync($"/api/volunteers/{vDto.Id}/skills", new UpdateVolunteerSkillsInput(
        [
            new(VolunteerSkillType.FirstAid, SkillProficiency.Advanced),
            new(VolunteerSkillType.Medical, SkillProficiency.Intermediate)
        ]));
        Assert.Equal(HttpStatusCode.OK, skillsRes.StatusCode);
        var withSkills = (await skillsRes.Content.ReadFromJsonAsync<VolunteerDto>())!;
        Assert.Equal(2, withSkills.Skills.Count);

        // 2. Update availability
        var availRes = await client.PutAsJsonAsync($"/api/volunteers/{vDto.Id}/availability", new UpdateVolunteerAvailabilityInput(
        [
            new(AvailabilityType.RecurringWeekly, DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(18, 0))
        ]));
        Assert.Equal(HttpStatusCode.OK, availRes.StatusCode);
        var withAvail = (await availRes.Content.ReadFromJsonAsync<VolunteerDto>())!;
        Assert.Single(withAvail.Availabilities);
        Assert.Equal("RecurringWeekly", withAvail.Availabilities[0].Type);
    }

    [Fact]
    public async Task Match_AsStaff_FindsQualifiedVolunteers()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        // Register a volunteer with BoatOperation in Kalutara
        await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara",
            EmergencyContactName: "Contact Match",
            EmergencyContactPhone: "0771239999",
            Skills: [new VolunteerSkillInput(VolunteerSkillType.BoatOperation, SkillProficiency.Expert)]));

        var response = await coordinator.GetAsync("/api/volunteers/match?district=Kalutara&skills=BoatOperation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var matches = (await response.Content.ReadFromJsonAsync<List<VolunteerMatchResultDto>>())!;
        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.Volunteer.District == "Kalutara" && m.MatchedSkills.Contains("BoatOperation"));
    }

    [Fact]
    public async Task CreateAssignment_WhenOverlapping_Returns409Conflict_NoDoubleBooking()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        var createRes = await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara", EmergencyContactName: "Emergency Name", EmergencyContactPhone: "0771234567"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        var baseTime = DateTime.UtcNow.AddDays(2);

        // First assignment: 10:00 to 14:00
        var firstRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(10),
            EndTime: baseTime.Date.AddHours(14),
            RoleOrTask: "Flood evacuation",
            Notes: "Primary mission"));
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        // Second assignment overlapping: 13:00 to 17:00 (Double Booking attempt!)
        var secondRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(13),
            EndTime: baseTime.Date.AddHours(17),
            RoleOrTask: "Food dispatch",
            Notes: "Conflicting mission"));

        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_WhenWithin30mBuffer_Returns409Conflict()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        var createRes = await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara", EmergencyContactName: "Emergency Name", EmergencyContactPhone: "0771234567"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        var baseTime = DateTime.UtcNow.AddDays(3);

        // First assignment: 09:00 to 12:00
        var firstRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(9),
            EndTime: baseTime.Date.AddHours(12),
            RoleOrTask: "Shift 1",
            Notes: null));
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        // Second assignment starts at 12:15 (violates 30m transit buffer!)
        var secondRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(12).AddMinutes(15),
            EndTime: baseTime.Date.AddHours(15),
            RoleOrTask: "Shift 2",
            Notes: null));

        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_WhenNonOverlapping_Returns201Created()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        var createRes = await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara", EmergencyContactName: "Emergency Name", EmergencyContactPhone: "0771234567"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        var baseTime = DateTime.UtcNow.AddDays(4);

        // First assignment: 09:00 to 12:00
        var firstRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(9),
            EndTime: baseTime.Date.AddHours(12),
            RoleOrTask: "Morning shift",
            Notes: null));
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        // Second assignment starts at 13:00 (1h gap > 30m buffer -> conflict free!)
        var secondRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(13),
            EndTime: baseTime.Date.AddHours(16),
            RoleOrTask: "Afternoon shift",
            Notes: null));

        Assert.Equal(HttpStatusCode.Created, secondRes.StatusCode);
    }

    [Fact]
    public async Task AssignmentStatus_Lifecycle_AndAuditLogged()
    {
        var (vClient, _) = await NewVolunteerUserAsync();
        var coordinator = await factory.CoordinatorAsync();

        var createRes = await vClient.PostAsJsonAsync("/api/volunteers", new VolunteerProfileInput(
            District: "Kalutara", EmergencyContactName: "Emergency Name", EmergencyContactPhone: "0771234567"));
        var vDto = (await createRes.Content.ReadFromJsonAsync<VolunteerDto>())!;

        var baseTime = DateTime.UtcNow.AddDays(5);

        var assignRes = await coordinator.PostAsJsonAsync("/api/assignments", new CreateAssignmentInput(
            VolunteerId: vDto.Id,
            ReliefRequestId: null,
            StartTime: baseTime.Date.AddHours(9),
            EndTime: baseTime.Date.AddHours(12),
            RoleOrTask: "Relief mission",
            Notes: null));
        var aDto = (await assignRes.Content.ReadFromJsonAsync<VolunteerAssignmentDto>())!;
        Assert.Equal("Assigned", aDto.Status);

        // Volunteer sets InProgress
        var inProgRes = await vClient.PatchAsJsonAsync($"/api/assignments/{aDto.Id}/status", new UpdateAssignmentStatusInput(
            AssignmentStatus.InProgress, "On site with team"));
        Assert.Equal(HttpStatusCode.OK, inProgRes.StatusCode);
        var inProgDto = (await inProgRes.Content.ReadFromJsonAsync<VolunteerAssignmentDto>())!;
        Assert.Equal("InProgress", inProgDto.Status);

        // Volunteer sets Completed
        var completedRes = await vClient.PatchAsJsonAsync($"/api/assignments/{aDto.Id}/status", new UpdateAssignmentStatusInput(
            AssignmentStatus.Completed, "Mission fulfilled successfully"));
        Assert.Equal(HttpStatusCode.OK, completedRes.StatusCode);
        var compDto = (await completedRes.Content.ReadFromJsonAsync<VolunteerAssignmentDto>())!;
        Assert.Equal("Completed", compDto.Status);

        // Verify Audit Logs
        await factory.WithDbAsync(async db =>
        {
            var auditRows = await db.AuditLogs
                .Where(l => l.EntityId == aDto.Id.ToString())
                .ToListAsync();
            Assert.Contains(auditRows, r => r.Action == "assignment.created");
            Assert.Contains(auditRows, r => r.Action == "assignment.status_updated");
            return 0;
        });
    }

    [Fact]
    public async Task Citizen_CannotAccessVolunteerEndpoints_Returns403Forbidden()
    {
        var citizen = await factory.CitizenAsync();

        var listRes = await citizen.GetAsync("/api/volunteers");
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);

        var matchRes = await citizen.GetAsync("/api/volunteers/match");
        Assert.Equal(HttpStatusCode.Forbidden, matchRes.StatusCode);
    }
}
