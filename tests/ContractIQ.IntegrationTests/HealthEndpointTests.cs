using System.Net;
using System.Text.Json;
using ContractIQ.Api.Observability;
using Xunit;

namespace ContractIQ.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly ContractIqApiFactory _factory;
    private HttpClient? _client;

    public HealthEndpointTests(PostgreSqlFixture postgres)
    {
        _factory = new ContractIqApiFactory(
            postgres.ConnectionString,
            DateTimeOffset.Parse("2026-03-01T00:30:00Z"));
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetAndSeedDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Liveness_endpoint_does_not_depend_on_the_database_check()
    {
        using var response = await _client!.GetAsync(
            "/health/live",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCorrelationHeader(response);
    }

    [Fact]
    public async Task Readiness_endpoint_reports_PostgreSQL_as_healthy()
    {
        using var response = await _client!.GetAsync(
            "/health/ready",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCorrelationHeader(response);

        await using Stream content = await response.Content.ReadAsStreamAsync(
            CancellationToken.None);
        using JsonDocument document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: CancellationToken.None);

        JsonElement postgresql = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgresql");

        Assert.Equal("Healthy", postgresql.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Problem_details_and_response_header_share_the_trace_identifier()
    {
        using var response = await _client!.GetAsync(
            "/api/v1/contracts/99999999-9999-4999-8999-999999999999",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string correlationId = AssertCorrelationHeader(response);

        await using Stream content = await response.Content.ReadAsStreamAsync(
            CancellationToken.None);
        using JsonDocument document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            correlationId,
            document.RootElement.GetProperty("traceId").GetString());
    }

    private static string AssertCorrelationHeader(HttpResponseMessage response)
    {
        string correlationId = Assert.Single(
            response.Headers.GetValues(RequestCorrelationMiddleware.HeaderName));

        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        return correlationId;
    }
}
