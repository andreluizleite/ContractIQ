using System.Diagnostics;
using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Application.Assistant.Tools;
using ContractIQ.Application.Common.Exceptions;
using ContractIQ.Application.Common.Models;
using ContractIQ.Application.Common.Observability;
using ContractIQ.Application.Contracts.AssessCancellation;
using ContractIQ.Application.Knowledge;
using ContractIQ.Application.Knowledge.Search;

namespace ContractIQ.Application.Assistant;

public sealed class AskContractQuestionHandler(
    IContractRepository contracts,
    IKnowledgeSearch knowledgeSearch,
    IAssistantAnswerGenerator answerGenerator,
    GroundedAnswerPromptBuilder promptBuilder,
    TimeProvider timeProvider)
{
    private const int MaximumQuestionCharacters = 1_000;
    private const int EvidenceLimit = 8;

    public async Task<ContractAnswer> HandleAsync(
        AskContractQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        int evidenceCount = 0;
        using Activity? activity = ContractIqTelemetry.StartActivity(
            "contractiq.assistant.ask");

        try
        {
            ArgumentNullException.ThrowIfNull(command);
            Validate(command);

            AssistantLanguageParser.TryParse(command.Language, out AssistantLanguage language);
            activity?.SetTag("contractiq.assistant.language", language.ToCode());

            var contract = await contracts.GetByIdAsync(command.ContractId, cancellationToken);

            if (contract is null || contract.CustomerId != command.CustomerId)
            {
                throw new ResourceNotFoundException("Contract", command.ContractId);
            }

            var domainAssessment = contract.AssessCancellation(timeProvider);
            var assessment = new CancellationAssessmentDto(
                contract.Id,
                domainAssessment.IsAllowed,
                domainAssessment.Reason,
                domainAssessment.RequestedOn,
                domainAssessment.EarliestTerminationDate,
                domainAssessment.ChargeableMonthlyPeriods,
                MoneyDto.FromDomain(domainAssessment.Penalty),
                domainAssessment.HasPenalty);

            IReadOnlyList<KnowledgeEvidence> evidence = await knowledgeSearch.HandleAsync(
                new SearchKnowledgeQuery(
                    command.Question,
                    command.CustomerId,
                    command.ContractId,
                    domainAssessment.RequestedOn,
                    EvidenceLimit),
                cancellationToken);
            evidenceCount = evidence.Count;

            bool hasApplicableContractEvidence = evidence.Any(item =>
                item.DocumentType == KnowledgeDocumentType.Contract &&
                item.CustomerId == command.CustomerId &&
                item.ContractId == command.ContractId);

            if (!hasApplicableContractEvidence)
            {
                activity?.SetTag("contractiq.assistant.evidence.sufficient", false);
                activity?.SetTag("contractiq.assistant.evidence.count", evidenceCount);
                activity?.SetTag("contractiq.assistant.citation.count", 0);
                activity?.SetStatus(ActivityStatusCode.Ok);
                ContractIqTelemetry.RecordAssistantRequest(
                    "insufficient_evidence",
                    Stopwatch.GetElapsedTime(startedAt),
                    evidenceCount,
                    citationCount: 0);

                return new ContractAnswer(
                    InsufficientEvidenceMessage(language),
                    language.ToCode(),
                    HasSufficientEvidence: false,
                    assessment,
                    Citations: [],
                    ModelId: null,
                    ProposedAction: null);
            }

            AssistantPrompt prompt = promptBuilder.Build(
                command.Question.Trim(),
                language,
                assessment,
                evidence);
            GeneratedAssistantAnswer generated = await answerGenerator.GenerateAsync(
                prompt,
                new AssistantToolContext(
                    command.Question.Trim(),
                    command.CustomerId,
                    command.ContractId,
                    language.ToCode(),
                    domainAssessment.RequestedOn),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(generated.Text))
            {
                throw new ExternalDependencyUnavailableException(
                    "assistant_model",
                    "The assistant model returned an empty response.");
            }

            AssistantCitation[] citations = evidence
                .Select((item, index) => new AssistantCitation(
                    index + 1,
                    item.DocumentKey,
                    item.Title,
                    item.Version,
                    item.Section,
                    item.Page,
                    item.SourcePath))
                .ToArray();

            activity?.SetTag("contractiq.assistant.evidence.sufficient", true);
            activity?.SetTag("contractiq.assistant.evidence.count", evidenceCount);
            activity?.SetTag("contractiq.assistant.citation.count", citations.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);
            ContractIqTelemetry.RecordAssistantRequest(
                "succeeded",
                Stopwatch.GetElapsedTime(startedAt),
                evidenceCount,
                citations.Length);

            return new ContractAnswer(
                generated.Text.Trim(),
                language.ToCode(),
                HasSufficientEvidence: true,
                assessment,
                citations,
                generated.ModelId,
                generated.ProposedAction);
        }
        catch (Exception exception)
        {
            string outcome = exception is OperationCanceledException
                ? "cancelled"
                : "failed";

            ContractIqTelemetry.MarkError(activity, exception);
            ContractIqTelemetry.RecordAssistantRequest(
                outcome,
                Stopwatch.GetElapsedTime(startedAt),
                evidenceCount,
                citationCount: 0);
            throw;
        }
    }

    private static void Validate(AskContractQuestionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Question) || command.Question.Trim().Length < 3)
        {
            throw new ApplicationValidationException(
                nameof(command.Question),
                "Question must contain at least 3 characters.");
        }

        if (command.Question.Length > MaximumQuestionCharacters)
        {
            throw new ApplicationValidationException(
                nameof(command.Question),
                $"Question cannot exceed {MaximumQuestionCharacters} characters.");
        }

        if (command.CustomerId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                nameof(command.CustomerId),
                "Customer id is required.");
        }

        if (command.ContractId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                nameof(command.ContractId),
                "Contract id is required.");
        }

        if (!AssistantLanguageParser.TryParse(command.Language, out _))
        {
            throw new ApplicationValidationException(
                nameof(command.Language),
                "Language must be 'en' or 'pt-BR'.");
        }
    }

    private static string InsufficientEvidenceMessage(AssistantLanguage language) => language switch
    {
        AssistantLanguage.English =>
            "I cannot answer reliably because no applicable contract clause was found. Review the indexed document or ask Contract Operations for help.",
        AssistantLanguage.PortugueseBrazil =>
            "Não posso responder com segurança porque nenhuma cláusula contratual aplicável foi encontrada. Revise o documento indexado ou consulte a equipe de Operações de Contratos.",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}
