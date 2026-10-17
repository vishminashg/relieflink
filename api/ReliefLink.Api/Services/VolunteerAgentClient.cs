using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

public record VolunteerAgentCandidate(
    Guid Id,
    string Name,
    string District,
    IReadOnlyList<string> Skills,
    bool IsVerified,
    int CompletedAssignments,
    IReadOnlyList<VolunteerAgentAvailability> AvailabilityWindows,
    IReadOnlyList<VolunteerAgentAssignment> Assignments);

public record VolunteerAgentAvailability(DateTime? SpecificStartUtc, DateTime? SpecificEndUtc);

public record VolunteerAgentAssignment(Guid Id, DateTime StartTime, DateTime EndTime);

public record VolunteerMatchingAgentInput(
    Guid RequestId,
    string District,
    IReadOnlyList<string> RequiredSkills,
    DateTime StartTime,
    DateTime EndTime,
    int VolunteersNeeded,
    IReadOnlyList<VolunteerAgentCandidate> Candidates,
    string? Notes = null,
    int Attempt = 1);

public record VolunteerAgentProposal(
    IReadOnlyList<Guid> VolunteerIds,
    IReadOnlyList<string> SkillsCovered,
    string Summary,
    IReadOnlyList<string> Rationale);

public record VolunteerAgentResponse(
    VolunteerAgentProposal Proposal,
    List<PlannerToolCall>? ToolCalls);

public interface IVolunteerAgentClient
{
    Task<VolunteerAgentResponse> MatchAsync(VolunteerMatchingAgentInput input, CancellationToken ct);
}

public sealed class HttpVolunteerAgentClient(HttpClient http, IOptions<AgentOptions> options) : IVolunteerAgentClient
{
    private const int MaxResponseBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<VolunteerAgentResponse> MatchAsync(VolunteerMatchingAgentInput input, CancellationToken ct)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl) || string.IsNullOrWhiteSpace(opts.ServiceKey))
            throw new PlannerUnavailableException("The volunteer agent service is not configured (Agent:BaseUrl / Agent:ServiceKey).");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opts.TimeoutSeconds, 1, 120)));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(opts.BaseUrl), "/agents/volunteers"))
            {
                Content = JsonContent.Create(input, options: Json)
            };
            message.Headers.Add("X-Service-Key", opts.ServiceKey);

            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new PlannerUnavailableException($"The volunteer agent service answered {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxResponseBytes)
                    throw new PlannerUnavailableException("The volunteer agent response was too large.");
            }

            buffer.Position = 0;
            var result = await JsonSerializer.DeserializeAsync<VolunteerAgentResponse>(buffer, Json, timeout.Token);
            return result ?? throw new PlannerUnavailableException("The volunteer agent returned an empty response.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PlannerUnavailableException($"The volunteer agent did not answer within {opts.TimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new PlannerUnavailableException("The volunteer agent could not be reached.", ex);
        }
        catch (JsonException ex)
        {
            throw new PlannerUnavailableException("The volunteer agent returned malformed JSON.", ex);
        }
    }
}