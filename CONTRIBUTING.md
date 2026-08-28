# Contributing to ContractIQ

ContractIQ demonstrates pragmatic enterprise .NET architecture and responsible AI engineering. Contributions should preserve its main goals: architectural credibility, clarity, testability, and ease of local demonstration.

## Development principles

- Keep deterministic business rules inside the domain and application code.
- Do not delegate validation, calculations, authorization, or state transitions to an LLM.
- Prefer established .NET and Microsoft patterns over custom frameworks.
- Keep changes focused and avoid abstractions without a concrete need.
- Optimize for readability and normal code formatting.
- Never commit credentials, tokens, connection strings, or customer data.
- Use comments to explain why a constraint exists, not to narrate obvious code.

## Development workflow

1. Select or create an issue with testable acceptance criteria.
2. Assign the issue to a milestone and add appropriate labels.
3. Create a short-lived branch from `main`.
4. Implement the smallest complete and demonstrable change.
5. Add or update automated tests.
6. Update documentation when behavior or architecture changes.
7. Open a pull request and link its issue.
8. Complete the pull request checklist and perform a self-review.
9. Merge using **Squash and merge** after required checks pass.
10. Delete the merged branch.

`main` must remain buildable and demonstrable. The informal work-in-progress limit is one primary issue at a time.

## Branch naming

Use a prefix, the issue number, and a concise description:

- `feat/12-cancellation-assessment`
- `fix/27-duplicate-request`
- `docs/8-domain-glossary`
- `chore/3-ci-pipeline`

Supported prefixes are `feat`, `fix`, `docs`, `test`, `refactor`, and `chore`.

## Commit messages

Use Conventional Commits:

```text
<type>(<scope>): <description>
```

Examples:

```text
feat(domain): implement cancellation assessment
test(domain): cover commitment end date boundary
docs(architecture): record CQRS decision
fix(api): reject duplicate cancellation requests
chore(ci): add backend test workflow
```

## Pull requests

A pull request should:

- address one primary concern;
- link its issue using `Closes #<issue-number>`;
- explain why the change is valuable;
- describe important technical decisions;
- include reproducible verification evidence;
- identify risks, limitations, and follow-up work;
- remain reasonably small and reviewable.

## Testing

Choose tests according to the behavior being changed:

- Domain tests for business rules and edge cases.
- Application tests for command and query behavior.
- Integration tests for PostgreSQL, API boundaries, and infrastructure adapters.
- Retrieval evaluations for search relevance and citations.
- Frontend tests for important user interactions.

Tests should verify observable behavior rather than implementation details.

## Documentation

Update documentation when a change affects public API behavior, local setup, business terminology, architecture, AI behavior, security, privacy, observability, or cost. Record decisions with long-term consequences as Architecture Decision Records under `docs/adr`.

## Responsible AI and data handling

Treat retrieved documents and user prompts as untrusted input.

Do not:

- allow model output to bypass domain validation;
- accept model-calculated penalties as authoritative;
- log complete prompts, contracts, tokens, or sensitive data by default;
- execute a state-changing tool without explicit user confirmation;
- present an answer as grounded when supporting evidence is unavailable.

AI-assisted code receives the same review, testing, and security checks as manually written code.

## Definition of Done

A change is complete when:

- acceptance criteria are satisfied;
- relevant tests pass;
- formatting and static analysis pass;
- documentation is current;
- no secrets or sensitive data are exposed;
- telemetry and failure behavior were considered;
- the pull request contains reproducible evidence;
- all required checks pass.
