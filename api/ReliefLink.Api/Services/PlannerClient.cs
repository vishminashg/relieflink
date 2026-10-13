using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReliefLink.Api.Domain;

namespace ReliefLink.Api.Services;

/// <summary>Settings for reaching the Python agent service. Set through environment variables, never in source control.</summary>
public class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>For example http://localhost:8001. Empty means the agent service is not configured.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Shared secret sent in the X-Service-Key header. The agent service is not reachable without it.</summary>
    public string? ServiceKey { get; set; }

    public int TimeoutSeconds { get; set; } = 20;
}

/// <summary>The Planner agent, as the workflow engine sees it.</summary>
public interface IPlannerClient
{
    /// <exception cref="PlannerUnavailableException">The service is unreachable, slow, or returned something unusable.</exception>
    Task<PlannerResponse> PlanAsync(PlannerInput input, CancellationToken ct);
}

/// <summary>Calls the internal Python agent service over HTTP.</summary>
public class HttpPlannerClient(HttpClient http, IOptions<AgentOptions> options) : IPlannerClient
{
    private const int MaxResponseBytes = 256 * 1024;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PlannerResponse> PlanAsync(PlannerInput input, CancellationToken ct)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl) || string.IsNullOrWhiteSpace(opts.ServiceKey))
            throw new PlannerUnavailableException("The agent service is not configured (Agent:BaseUrl / Agent:ServiceKey).");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opts.TimeoutSeconds, 1, 120)));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(opts.BaseUrl), "/plan"))
            {
                Content = JsonContent.Create(input, options: Json)
            };
            message.Headers.Add("X-Service-Key", opts.ServiceKey);

            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new PlannerUnavailableException($"The agent service answered {(int)response.StatusCode}.");

            // Never read an unbounded body from another service.
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxResponseBytes)
                    throw new PlannerUnavailableException("The agent service response was too large.");
            }

            buffer.Position = 0;
            var result = await JsonSerializer.DeserializeAsync<PlannerResponse>(buffer, Json, timeout.Token);
            return result ?? throw new PlannerUnavailableException("The agent service returned an empty response.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PlannerUnavailableException($"The agent service did not answer within {opts.TimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new PlannerUnavailableException("The agent service could not be reached.", ex);
        }
        catch (JsonException ex)
        {
            throw new PlannerUnavailableException("The agent service returned malformed JSON.", ex);
        }
    }
}
