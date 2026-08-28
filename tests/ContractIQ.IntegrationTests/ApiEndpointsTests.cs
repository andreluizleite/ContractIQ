using System.Net;
using System.Text.Json;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ContractIQ.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ApiEndpointsTests(PostgreSqlFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset FrozenUtc =
        DateTimeOffset.Parse("2026-03-01T00:30:00Z");

    private ContractIqApiFactory? _databaseFactory;

    public async Task InitializeAsync()
    {
        _databaseFactory = CreateFactory();
        await _databaseFactory.ResetAndSeedDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_databaseFactory is not null)
        {
            await _databaseFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Customers_endpoint_returns_the_ordered_demo_customers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/customers", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement.ArrayEnumerator customers = document.RootElement.EnumerateArray();
        Assert.Equal(
            ["ACME Corporation", "Globex Corporation", "Initech"],
            customers.Select(customer => customer.GetProperty("name").GetString()!).ToArray());
    }

    [Fact]
    public async Task Customer_contracts_endpoint_returns_only_that_customers_contracts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/customers/{DemoDataIds.InitechCustomer}/contracts",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] contracts = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, contracts.Length);
        Assert.All(
            contracts,
            contract => Assert.Equal(
                DemoDataIds.InitechCustomer,
                contract.GetProperty("customerId").GetGuid()));
    }

    [Fact]
    public async Task Contract_details_endpoint_returns_the_seeded_contract()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/contracts/{DemoDataIds.AcmeActiveContract}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement contract = document.RootElement;
        Assert.Equal(DemoDataIds.AcmeActiveContract, contract.GetProperty("id").GetGuid());
        Assert.Equal(DemoDataIds.AcmeCustomer, contract.GetProperty("customerId").GetGuid());
        Assert.Equal("active", contract.GetProperty("status").GetString());
        Assert.Equal(1_200m, contract.GetProperty("monthlyFee").GetProperty("amount").GetDecimal());
        Assert.Equal("USD", contract.GetProperty("monthlyFee").GetProperty("currency").GetString());
        Assert.Equal(30, contract.GetProperty("noticePeriodDays").GetInt32());
    }

    [Fact]
    public async Task Assessment_uses_the_frozen_UTC_date_and_domain_calculation()
    {
        var utcMinusEleven = TimeZoneInfo.CreateCustomTimeZone(
            "UTC-11-integration-test",
            TimeSpan.FromHours(-11),
            "UTC-11 integration test",
            "UTC-11 integration test");
        using var factory = new ContractIqApiFactory(
            postgres.ConnectionString,
            FrozenUtc,
            utcMinusEleven);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/contracts/{DemoDataIds.AcmeActiveContract}/cancellation-assessment",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement assessment = document.RootElement;
        Assert.True(assessment.GetProperty("isAllowed").GetBoolean());
        Assert.Equal("allowed", assessment.GetProperty("reason").GetString());
        Assert.Equal("2026-03-01", assessment.GetProperty("requestedOn").GetString());
        Assert.Equal("2026-03-31", assessment.GetProperty("earliestTerminationDate").GetString());
        Assert.Equal(22, assessment.GetProperty("chargeableMonthlyPeriods").GetInt32());
        Assert.Equal(6_600m, assessment.GetProperty("penalty").GetProperty("amount").GetDecimal());
        Assert.Equal("USD", assessment.GetProperty("penalty").GetProperty("currency").GetString());
    }

    [Theory]
    [InlineData("cccccccc-cccc-4ccc-8ccc-cccccccccccc", "contractAlreadyCancelled")]
    [InlineData("dddddddd-dddd-4ddd-8ddd-dddddddddddd", "contractExpired")]
    public async Task Inactive_contract_assessment_returns_200_with_not_allowed_reason(
        string contractId,
        string expectedReason)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/contracts/{contractId}/cancellation-assessment",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.False(document.RootElement.GetProperty("isAllowed").GetBoolean());
        Assert.Equal(expectedReason, document.RootElement.GetProperty("reason").GetString());
        Assert.Equal(0m, document.RootElement.GetProperty("penalty").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Creating_a_request_returns_201_with_a_pending_review_snapshot()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "request-001");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement request = document.RootElement;
        Assert.NotEqual(Guid.Empty, request.GetProperty("id").GetGuid());
        Assert.Equal(DemoDataIds.AcmeActiveContract, request.GetProperty("contractId").GetGuid());
        Assert.Equal("2026-03-01T00:30:00+00:00", request.GetProperty("createdAtUtc").GetString());
        Assert.Equal("2026-03-01", request.GetProperty("requestedOn").GetString());
        Assert.Equal("2026-03-31", request.GetProperty("earliestTerminationDate").GetString());
        Assert.Equal(6_600m, request.GetProperty("penalty").GetProperty("amount").GetDecimal());
        Assert.Equal("pendingReview", request.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Replaying_the_same_key_returns_200_with_the_original_request()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var createdResponse = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "request-001");
        using JsonDocument created = await ReadJsonAsync(createdResponse);

        using var replayResponse = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "request-001");
        using JsonDocument replay = await ReadJsonAsync(replayResponse);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(
            created.RootElement.GetProperty("id").GetGuid(),
            replay.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(created.RootElement.GetRawText(), replay.RootElement.GetRawText());
    }

    [Fact]
    public async Task Reusing_a_key_for_another_contract_returns_idempotency_conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var created = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "shared-key");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var conflict = await PostCancellationAsync(
            client,
            DemoDataIds.GlobexActiveContract,
            "shared-key");

        await AssertProblemDetailsAsync(
            conflict,
            HttpStatusCode.Conflict,
            "idempotency_key_conflict");
    }

    [Fact]
    public async Task Different_key_for_an_open_request_returns_conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var created = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "request-001");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var conflict = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "request-002");

        await AssertProblemDetailsAsync(
            conflict,
            HttpStatusCode.Conflict,
            "cancellation_request_already_open");
    }

    [Theory]
    [InlineData("cccccccc-cccc-4ccc-8ccc-cccccccccccc")]
    [InlineData("dddddddd-dddd-4ddd-8ddd-dddddddddddd")]
    public async Task Creating_for_an_inactive_contract_returns_conflict(string contractId)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await PostCancellationAsync(
            client,
            Guid.Parse(contractId),
            "request-001");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "contract_not_cancellable");
    }

    [Theory]
    [InlineData("details")]
    [InlineData("assessment")]
    public async Task Missing_contract_get_endpoints_return_problem_details(string endpoint)
    {
        var missingId = Guid.Parse("99999999-9999-4999-8999-999999999999");
        string path = endpoint == "assessment"
            ? $"/api/v1/contracts/{missingId}/cancellation-assessment"
            : $"/api/v1/contracts/{missingId}";
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, CancellationToken.None);

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "resource_not_found");
    }

    [Fact]
    public async Task Creating_for_a_missing_contract_returns_problem_details()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var missingId = Guid.Parse("99999999-9999-4999-8999-999999999999");

        using var response = await PostCancellationAsync(client, missingId, "request-001");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "resource_not_found");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_idempotency_key_returns_validation_problem_details(string? key)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            key);

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "validation_error",
            "idempotencyKey");
    }

    [Fact]
    public async Task Oversized_idempotency_key_returns_validation_problem_details()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            new string('a', 129));

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "validation_error",
            "idempotencyKey");
    }

    [Fact]
    public async Task OpenApi_describes_the_API_paths_and_required_idempotency_header()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement paths = document.RootElement.GetProperty("paths");
        string[] expectedPaths =
        [
            "/api/v1/customers",
            "/api/v1/customers/{customerId}/contracts",
            "/api/v1/contracts/{contractId}",
            "/api/v1/contracts/{contractId}/cancellation-assessment",
            "/api/v1/contracts/{contractId}/cancellation-requests",
        ];

        Assert.Equal(expectedPaths.Length, paths.EnumerateObject().Count());
        foreach (string path in expectedPaths)
        {
            Assert.True(paths.TryGetProperty(path, out _), $"OpenAPI path '{path}' was not found.");
        }

        JsonElement post = paths
            .GetProperty("/api/v1/contracts/{contractId}/cancellation-requests")
            .GetProperty("post");
        JsonElement idempotencyHeader = post
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" &&
                parameter.GetProperty("in").GetString() == "header");

        Assert.True(
            idempotencyHeader.TryGetProperty("required", out JsonElement required) &&
            required.GetBoolean(),
            "OpenAPI must mark the Idempotency-Key header as required.");
    }

    [Fact]
    public async Task PostgreSQL_seed_is_idempotent_and_pgvector_is_installed()
    {
        using (var scope = _databaseFactory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ContractIqDbContext>();

            await DemoDataSeeder.SeedAsync(dbContext, CancellationToken.None);
            await DemoDataSeeder.SeedAsync(dbContext, CancellationToken.None);
        }

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        Assert.Equal(3, await CountRowsAsync(connection, "customers"));
        Assert.Equal(4, await CountRowsAsync(connection, "contracts"));

        await using var vectorCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector';",
            connection);
        var vectorExtensionCount = (long)(await vectorCommand.ExecuteScalarAsync(
            CancellationToken.None))!;

        Assert.Equal(1, vectorExtensionCount);
    }

    private ContractIqApiFactory CreateFactory() =>
        new(postgres.ConnectionString, FrozenUtc);

    private static async Task<long> CountRowsAsync(
        NpgsqlConnection connection,
        string tableName)
    {
        string sql = tableName switch
        {
            "customers" => "SELECT COUNT(*) FROM customers;",
            "contracts" => "SELECT COUNT(*) FROM contracts;",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName)),
        };

        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }

    private static async Task<HttpResponseMessage> PostCancellationAsync(
        HttpClient client,
        Guid contractId,
        string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/contracts/{contractId}/cancellation-requests");

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream content = await response.Content.ReadAsStreamAsync(CancellationToken.None);
        return await JsonDocument.ParseAsync(content, cancellationToken: CancellationToken.None);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string? expectedField = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement problem = document.RootElement;
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("instance").GetString()));
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal(
            $"urn:contractiq:error:{expectedCode}",
            problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        if (expectedField is not null)
        {
            Assert.Equal(expectedField, problem.GetProperty("field").GetString());
        }
    }
}
