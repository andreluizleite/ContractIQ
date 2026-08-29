using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Assistant;
using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Indexing;
using ContractIQ.Infrastructure;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace ContractIQ.IntegrationTests;

internal sealed class ContractIqApiFactory(
    string connectionString,
    DateTimeOffset utcNow,
    TimeZoneInfo? localTimeZone = null) : WebApplicationFactory<Program>
{
    private Respawner? _respawner;

    public async Task ResetAndSeedDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using (var migrationScope = Services.CreateScope())
        {
            var dbContext = migrationScope.ServiceProvider
                .GetRequiredService<ContractIqDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        _respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
            });

        await _respawner.ResetAsync(connection);

        using var seedScope = Services.CreateScope();
        var seedDbContext = seedScope.ServiceProvider
            .GetRequiredService<ContractIqDbContext>();

        await DemoDataSeeder.SeedAsync(seedDbContext, cancellationToken);
    }

    public async Task<IndexKnowledgeDocumentsResult> IndexKnowledgeDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IndexKnowledgeDocumentsHandler>()
            .HandleAsync(cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ContractIqDbContext>();
            services.RemoveAll<DbContextOptions<ContractIqDbContext>>();
            services.RemoveAll<ICustomerRepository>();
            services.RemoveAll<IContractRepository>();
            services.RemoveAll<ICancellationRequestStore>();
            services.RemoveAll<IConfigureOptions<HealthCheckServiceOptions>>();
            services.AddInfrastructure(connectionString);

            services.RemoveAll<IKnowledgeEmbeddingGenerator>();
            services.RemoveAll<IKnowledgeDocumentCatalog>();
            services.RemoveAll<IAssistantAnswerGenerator>();
            services.AddSingleton<IKnowledgeEmbeddingGenerator, DeterministicEmbeddingGenerator>();
            services.AddSingleton<IKnowledgeDocumentCatalog, TestKnowledgeDocumentCatalog>();
            services.AddSingleton<IAssistantAnswerGenerator, DeterministicAnswerGenerator>();
            services.AddScoped<IndexKnowledgeDocumentsHandler>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(
                new FrozenTimeProvider(utcNow, localTimeZone ?? TimeZoneInfo.Utc));
        });
    }

    private sealed class DeterministicEmbeddingGenerator : IKnowledgeEmbeddingGenerator
    {
        public string ModelId => "integration-test-embedding-v1";

        public int Dimensions => 768;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> values,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(
                values.Select(CreateEmbedding).ToArray());
        }

        private static float[] CreateEmbedding(string value)
        {
            string normalized = value.ToLowerInvariant();
            var embedding = new float[768];

            if (normalized.Contains("penalty") ||
                normalized.Contains("charge") ||
                normalized.Contains("multa"))
            {
                embedding[0] = 1;
            }
            else if (normalized.Contains("notice") || normalized.Contains("aviso"))
            {
                embedding[1] = 1;
            }
            else
            {
                embedding[2] = 1;
            }

            return embedding;
        }
    }

    private sealed class DeterministicAnswerGenerator : IAssistantAnswerGenerator
    {
        public Task<GeneratedAssistantAnswer> GenerateAsync(
            AssistantPrompt prompt,
            AssistantToolContext toolContext,
            CancellationToken cancellationToken = default)
        {
            string answer = prompt.UserPrompt.Contains(
                "Brazilian Portuguese",
                StringComparison.Ordinal)
                ? "A ACME pode solicitar o cancelamento. A multa determinística está na avaliação [1]."
                : "ACME can request cancellation. The deterministic penalty is shown in the assessment [1].";

            AssistantActionProposal? proposal = toolContext.Question.Contains(
                "create",
                StringComparison.OrdinalIgnoreCase) ||
                toolContext.Question.Contains("crie", StringComparison.OrdinalIgnoreCase)
                ? new AssistantActionProposal(
                    AssistantToolNames.CreateCancellation,
                    AssistantToolNames.CreateCancellation,
                    RequiresConfirmation: true,
                    CanExecute: true,
                    new CancellationAssessmentDto(
                        toolContext.ContractId,
                        IsAllowed: true,
                        ContractIQ.Domain.Contracts.CancellationAssessmentReason.Allowed,
                        new DateOnly(2026, 3, 1),
                        new DateOnly(2026, 3, 31),
                        22,
                        new MoneyDto(6_600m, "USD"),
                        HasPenalty: true))
                : null;

            return Task.FromResult(new GeneratedAssistantAnswer(
                answer,
                "integration-test-chat",
                proposal));
        }
    }

    private sealed class TestKnowledgeDocumentCatalog : IKnowledgeDocumentCatalog
    {
        public Task<IReadOnlyList<KnowledgeDocumentSource>> ReadAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<KnowledgeDocumentSource> documents =
            [
                CreateAcmeDocument(
                    "1.0",
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 6, 30),
                    "A forty percent penalty applies before the commitment date."),
                CreateAcmeDocument(
                    "2.0",
                    new DateOnly(2026, 7, 1),
                    null,
                    "A twenty-five percent penalty applies before the commitment date of January 1, 2028."),
                new KnowledgeDocumentSource(
                    "contract-globex",
                    "Globex Agreement",
                    KnowledgeDocumentType.Contract,
                    "1.0",
                    "en",
                    DemoDataIds.GlobexCustomer,
                    DemoDataIds.GlobexActiveContract,
                    new DateOnly(2024, 1, 1),
                    null,
                    "contracts/globex.md",
                    "<!-- page: 4 -->\n\n## Termination\n\nGlobex has no penalty."),
                new KnowledgeDocumentSource(
                    "policy-cancellation-en",
                    "Cancellation Policy",
                    KnowledgeDocumentType.Policy,
                    "1.0",
                    "en",
                    null,
                    null,
                    new DateOnly(2026, 1, 1),
                    null,
                    "policies/cancellation.md",
                    "<!-- page: 1 -->\n\n## Review\n\nEvery request remains pending review."),
            ];

            return Task.FromResult(documents);
        }

        private static KnowledgeDocumentSource CreateAcmeDocument(
            string version,
            DateOnly effectiveFrom,
            DateOnly? effectiveTo,
            string clause) => new(
                "contract-acme",
                "ACME Agreement",
                KnowledgeDocumentType.Contract,
                version,
                "en",
                DemoDataIds.AcmeCustomer,
                DemoDataIds.AcmeActiveContract,
                effectiveFrom,
                effectiveTo,
                $"contracts/acme-v{version}.md",
                $"<!-- page: 2 -->\n\n## Termination for convenience\n\n{clause}");
    }

    private sealed class FrozenTimeProvider(
        DateTimeOffset utcNow,
        TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
    }
}
