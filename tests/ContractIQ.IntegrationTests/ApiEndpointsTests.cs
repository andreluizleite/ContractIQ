using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContractIQ.Application.Knowledge.Indexing;
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
    public async Task API_responses_include_local_demo_security_headers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/customers",
            CancellationToken.None);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("no-store", Assert.Single(response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task Write_endpoints_return_problem_details_after_the_local_rate_limit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage allowed = await PostCancellationAsync(
                client,
                DemoDataIds.AcmeActiveContract,
                idempotencyKey: null);
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        using HttpResponseMessage limited = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            idempotencyKey: null);

        await AssertProblemDetailsAsync(
            limited,
            HttpStatusCode.TooManyRequests,
            "rate_limit_exceeded");
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
            "/api/v1/knowledge/search",
            "/api/v1/assistant/answers",
            "/api/v1/assistant/actions/cancellation-requests",
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

        JsonElement assistantToolPost = paths
            .GetProperty("/api/v1/assistant/actions/cancellation-requests")
            .GetProperty("post");
        JsonElement assistantToolIdempotencyHeader = assistantToolPost
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" &&
                parameter.GetProperty("in").GetString() == "header");
        Assert.True(
            assistantToolIdempotencyHeader
                .GetProperty("required")
                .GetBoolean(),
            "The assistant write tool must require the Idempotency-Key header.");
    }

    [Fact]
    public async Task Knowledge_search_returns_current_scoped_evidence_with_citation_metadata()
    {
        IndexKnowledgeDocumentsResult indexResult =
            await _databaseFactory!.IndexKnowledgeDocumentsAsync();
        IndexKnowledgeDocumentsResult reindexResult =
            await _databaseFactory.IndexKnowledgeDocumentsAsync();
        using var client = _databaseFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/knowledge/search",
            new
            {
                Query = "penalty before commitment",
                CustomerId = DemoDataIds.AcmeCustomer,
                ContractId = DemoDataIds.AcmeActiveContract,
                AsOf = "2026-08-28",
                Limit = 5,
            },
            CancellationToken.None);

        Assert.Equal(4, indexResult.IndexedDocuments);
        Assert.Equal(0, reindexResult.IndexedDocuments);
        Assert.Equal(4, reindexResult.SkippedDocuments);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] evidence = document.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(evidence);
        Assert.DoesNotContain(
            evidence,
            item => item.GetProperty("documentKey").GetString() == "contract-globex");

        JsonElement contractEvidence = evidence.Single(item =>
            item.GetProperty("documentKey").GetString() == "contract-acme");
        Assert.Equal("2.0", contractEvidence.GetProperty("version").GetString());
        Assert.Equal("Termination for convenience", contractEvidence.GetProperty("section").GetString());
        Assert.Equal(2, contractEvidence.GetProperty("page").GetInt32());
        Assert.Equal("contracts/acme-v2.0.md", contractEvidence.GetProperty("sourcePath").GetString());
        Assert.True(contractEvidence.GetProperty("score").GetDouble() > 0);
        Assert.NotEqual(JsonValueKind.Null, contractEvidence.GetProperty("lexicalScore").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, contractEvidence.GetProperty("vectorScore").ValueKind);
    }

    [Fact]
    public async Task Knowledge_search_selects_the_version_effective_as_of_the_requested_date()
    {
        await _databaseFactory!.IndexKnowledgeDocumentsAsync();
        using var client = _databaseFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/knowledge/search",
            new
            {
                Query = "penalty before commitment",
                CustomerId = DemoDataIds.AcmeCustomer,
                ContractId = DemoDataIds.AcmeActiveContract,
                AsOf = "2026-03-01",
                Limit = 5,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement contractEvidence = document.RootElement
            .EnumerateArray()
            .Single(item => item.GetProperty("documentKey").GetString() == "contract-acme");
        Assert.Equal("1.0", contractEvidence.GetProperty("version").GetString());
        Assert.Contains(
            "forty percent",
            contractEvidence.GetProperty("content").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("en", "ACME can request cancellation.")]
    [InlineData("pt-BR", "A ACME pode solicitar o cancelamento.")]
    public async Task Assistant_returns_bilingual_grounded_answer_assessment_and_citations(
        string language,
        string expectedAnswer)
    {
        await _databaseFactory!.IndexKnowledgeDocumentsAsync();
        using var client = _databaseFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/assistant/answers",
            new
            {
                Question = language == "en"
                    ? "Can ACME cancel now and what penalty applies?"
                    : "A ACME pode cancelar agora e qual multa se aplica?",
                CustomerId = DemoDataIds.AcmeCustomer,
                ContractId = DemoDataIds.AcmeActiveContract,
                Language = language,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement answer = document.RootElement;
        Assert.True(answer.GetProperty("hasSufficientEvidence").GetBoolean());
        Assert.Equal(language, answer.GetProperty("language").GetString());
        Assert.Contains(expectedAnswer, answer.GetProperty("answer").GetString());
        Assert.Equal("integration-test-chat", answer.GetProperty("modelId").GetString());
        Assert.Equal(
            6_600m,
            answer.GetProperty("assessment").GetProperty("penalty").GetProperty("amount").GetDecimal());

        JsonElement citation = answer.GetProperty("citations").EnumerateArray().First();
        Assert.Equal(1, citation.GetProperty("number").GetInt32());
        Assert.Equal("contract-acme", citation.GetProperty("documentKey").GetString());
        Assert.Equal("1.0", citation.GetProperty("version").GetString());
        Assert.Equal("Termination for convenience", citation.GetProperty("section").GetString());
        Assert.Equal(2, citation.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Assistant_states_when_contract_evidence_is_insufficient()
    {
        await _databaseFactory!.IndexKnowledgeDocumentsAsync();
        using var client = _databaseFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/assistant/answers",
            new
            {
                Question = "A Initech pode cancelar este contrato?",
                CustomerId = DemoDataIds.InitechCustomer,
                ContractId = DemoDataIds.InitechCancelledContract,
                Language = "pt-BR",
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement answer = document.RootElement;
        Assert.False(answer.GetProperty("hasSufficientEvidence").GetBoolean());
        Assert.Contains(
            "Não posso responder com segurança",
            answer.GetProperty("answer").GetString());
        Assert.Empty(answer.GetProperty("citations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("modelId").ValueKind);
    }

    [Fact]
    public async Task Assistant_outage_does_not_block_deterministic_contract_operations()
    {
        using var factory = new ContractIqApiFactory(
            postgres.ConnectionString,
            FrozenUtc,
            assistantUnavailable: true);
        await factory.ResetAndSeedDatabaseAsync();
        await factory.IndexKnowledgeDocumentsAsync();
        using var client = factory.CreateClient();

        using HttpResponseMessage unavailableAssistant = await client.PostAsJsonAsync(
            "/api/v1/assistant/answers",
            new
            {
                Question = "Can ACME cancel now?",
                CustomerId = DemoDataIds.AcmeCustomer,
                ContractId = DemoDataIds.AcmeActiveContract,
                Language = "en",
            },
            CancellationToken.None);

        await AssertProblemDetailsAsync(
            unavailableAssistant,
            HttpStatusCode.ServiceUnavailable,
            "assistant_model_unavailable");

        using HttpResponseMessage assessment = await client.GetAsync(
            $"/api/v1/contracts/{DemoDataIds.AcmeActiveContract}/cancellation-assessment",
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, assessment.StatusCode);

        using HttpResponseMessage cancellation = await PostCancellationAsync(
            client,
            DemoDataIds.AcmeActiveContract,
            "fallback-request-001");
        Assert.Equal(HttpStatusCode.Created, cancellation.StatusCode);
    }

    [Fact]
    public async Task Assistant_prepares_a_cancellation_action_without_changing_state()
    {
        await _databaseFactory!.IndexKnowledgeDocumentsAsync();
        using var client = _databaseFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/assistant/answers",
            new
            {
                Question = "Create the cancellation request.",
                CustomerId = DemoDataIds.AcmeCustomer,
                ContractId = DemoDataIds.AcmeActiveContract,
                Language = "en",
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement proposal = document.RootElement.GetProperty("proposedAction");
        Assert.Equal("create_cancellation_request", proposal.GetProperty("name").GetString());
        Assert.True(proposal.GetProperty("requiresConfirmation").GetBoolean());
        Assert.True(proposal.GetProperty("canExecute").GetBoolean());

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        Assert.Equal(0, await CountCancellationRequestsAsync(connection));
    }

    [Fact]
    public async Task Assistant_write_tool_requires_explicit_confirmation()
    {
        using var client = _databaseFactory!.CreateClient();

        using var response = await PostAssistantCancellationAsync(
            client,
            DemoDataIds.AcmeCustomer,
            DemoDataIds.AcmeActiveContract,
            confirmed: false,
            "agent-request-001");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "validation_error",
            "Confirmed");
    }

    [Fact]
    public async Task Assistant_write_tool_creates_and_idempotently_replays_the_request()
    {
        using var client = _databaseFactory!.CreateClient();

        using var created = await PostAssistantCancellationAsync(
            client,
            DemoDataIds.AcmeCustomer,
            DemoDataIds.AcmeActiveContract,
            confirmed: true,
            "agent-request-001");
        using var replayed = await PostAssistantCancellationAsync(
            client,
            DemoDataIds.AcmeCustomer,
            DemoDataIds.AcmeActiveContract,
            confirmed: true,
            "agent-request-001");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        using JsonDocument createdDocument = await ReadJsonAsync(created);
        using JsonDocument replayedDocument = await ReadJsonAsync(replayed);
        Assert.Equal(
            createdDocument.RootElement.GetProperty("id").GetGuid(),
            replayedDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(
            6_600m,
            createdDocument.RootElement.GetProperty("penalty").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Assistant_write_tool_hides_a_contract_outside_customer_scope()
    {
        using var client = _databaseFactory!.CreateClient();

        using var response = await PostAssistantCancellationAsync(
            client,
            DemoDataIds.GlobexCustomer,
            DemoDataIds.AcmeActiveContract,
            confirmed: true,
            "agent-request-001");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "resource_not_found");
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

    private static async Task<HttpResponseMessage> PostAssistantCancellationAsync(
        HttpClient client,
        Guid customerId,
        Guid contractId,
        bool confirmed,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/assistant/actions/cancellation-requests")
        {
            Content = JsonContent.Create(new
            {
                CustomerId = customerId,
                ContractId = contractId,
                Intent = "create_cancellation_request",
                Confirmed = confirmed,
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<long> CountCancellationRequestsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM cancellation_requests;",
            connection);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
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
