using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Tests.Unit;

public class HttpPlannerClientTests
{
    private static readonly PlannerInput Input = new(
        Guid.NewGuid(), "Kalutara", 6, 2, 2, true, null, 71, "Critical", ["Home is flooded"],
        [new PlannerNeed(NeedCategory.Food, 6, null)], "Water is rising", 1, null);

    private const string GoodJson = """
        {"plan":{"summary":"Deliver food to Kalutara.","recommendedUrgency":"Critical","targetResponseHours":6,
        "needs":[{"category":"Food","quantity":6,"action":"Send food."}],"rationale":["Flooded."],"risks":[]},
        "toolCalls":[{"name":"triage_breakdown","arguments":{"a":1},"result":{"score":71},"succeeded":true,"durationMs":2}]}
        """;

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpPlannerClient Client(StubHandler handler, string? baseUrl = "http://agents.test", string? key = "secret-key") =>
        new(new HttpClient(handler), Options.Create(new AgentOptions { BaseUrl = baseUrl, ServiceKey = key, TimeoutSeconds = 5 }));

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GoodResponse_IsParsed()
    {
        var response = await Client(new StubHandler(_ => Json(GoodJson))).PlanAsync(Input, CancellationToken.None);

        Assert.Equal("Critical", response.Plan!.RecommendedUrgency);
        Assert.Equal(6, response.Plan.TargetResponseHours);
        Assert.Single(response.Plan.Needs!);
        Assert.Equal("triage_breakdown", response.ToolCalls!.Single().Name);
    }

    [Fact]
    public async Task Request_GoesToPlanEndpoint_WithServiceKey_AndCamelCaseBody()
    {
        var handler = new StubHandler(_ => Json(GoodJson));

        await Client(handler).PlanAsync(Input, CancellationToken.None);

        Assert.Equal("http://agents.test/plan", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("secret-key", handler.LastRequest.Headers.GetValues("X-Service-Key").Single());

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("Kalutara", body.RootElement.GetProperty("district").GetString());
        Assert.Equal("Critical", body.RootElement.GetProperty("urgency").GetString());
        Assert.Equal("Food", body.RootElement.GetProperty("needs")[0].GetProperty("category").GetString());
    }

    [Theory]
    [InlineData(null, "key")]
    [InlineData("", "key")]
    [InlineData("http://agents.test", null)]
    [InlineData("http://agents.test", " ")]
    public async Task MissingConfiguration_IsReportedAsUnavailable_WithoutCallingAnyone(string? url, string? key)
    {
        var handler = new StubHandler(_ => Json(GoodJson));

        await Assert.ThrowsAsync<PlannerUnavailableException>(() =>
            Client(handler, url, key).PlanAsync(Input, CancellationToken.None));

        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task NonOkStatus_IsReportedAsUnavailable(HttpStatusCode code)
    {
        await Assert.ThrowsAsync<PlannerUnavailableException>(() =>
            Client(new StubHandler(_ => Json("{}", code))).PlanAsync(Input, CancellationToken.None));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"plan\": ")]
    [InlineData("null")]
    public async Task MalformedOrEmptyBody_IsReportedAsUnavailable(string body)
    {
        await Assert.ThrowsAsync<PlannerUnavailableException>(() =>
            Client(new StubHandler(_ => Json(body))).PlanAsync(Input, CancellationToken.None));
    }

    [Fact]
    public async Task OversizedResponse_IsRejected()
    {
        var huge = "{\"plan\":null,\"toolCalls\":[],\"padding\":\"" + new string('x', 300 * 1024) + "\"}";

        await Assert.ThrowsAsync<PlannerUnavailableException>(() =>
            Client(new StubHandler(_ => Json(huge))).PlanAsync(Input, CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailure_IsReportedAsUnavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<PlannerUnavailableException>(() => Client(handler).PlanAsync(Input, CancellationToken.None));
    }

    [Fact]
    public async Task UnknownExtraFields_AreIgnored()
    {
        var json = GoodJson.Replace("\"toolCalls\"", "\"somethingNew\":123,\"toolCalls\"");

        var response = await Client(new StubHandler(_ => Json(json))).PlanAsync(Input, CancellationToken.None);

        Assert.NotNull(response.Plan);
    }

    [Fact]
    public async Task CallerCancellation_IsNotDisguisedAsAnOutage()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(new StubHandler(_ => Json(GoodJson))).PlanAsync(Input, cts.Token));
    }
}
