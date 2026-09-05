# Second bounded Microsoft Foundry evaluation — 2026-09-03

## Outcome

The second live four-scenario evaluation completed as **FAIL** with all four
provider requests finishing successfully. Unlike the first baseline, this run
had no HTTP 429 response. The API was stopped immediately after the run, and no
third live execution was made.

This result does not indicate that the model became the source of truth. Every
scenario passed the canonical assessment, evidence decision, citation metadata,
required-source, version, immutable-path, customer/contract-scope, inline-citation,
language, eligibility, notice-period, date, and percentage gates. No cancellation
write endpoint was called.

## Versioned run metadata

| Field | Recorded value |
| --- | --- |
| Result | FAIL |
| Scenarios | 0/4 |
| Critical findings | 8 |
| Provider | `MicrosoftFoundry` |
| Deployment | `contractiq-chat` |
| Observed model id | `contractiq-chat` |
| Prompt version | `grounded-answer-v1` |
| Dataset | `ContractIQ bilingual grounding and safe-action evaluation set` |
| Dataset schema | `contract-assistant-v1` |
| Run date (UTC) | `2026-09-03T20:28:05.8080489+00:00` |
| Inter-scenario delay | 20 seconds |
| Automatic SDK retries | 0 |

The comparison used the same four scenarios as the first bounded run:

- `acme-penalty-en`;
- `acme-penalty-pt-br`;
- `acme-prepare-en`;
- `acme-prepare-pt-br`.

## Sanitized findings

| Scenario | Passed invariant areas | Failed metrics |
| --- | --- | --- |
| `acme-penalty-en` | assessment, evidence, citations, scope, language, tool safety, dates and percentage | answer signal, penalty consistency, penalty presence |
| `acme-penalty-pt-br` | assessment, evidence, citations, scope, language, tool safety, answer signal, dates, percentage and penalty presence | penalty consistency |
| `acme-prepare-en` | assessment, evidence, citations, scope, language, dates and percentage | tool preparation, answer signal, penalty consistency |
| `acme-prepare-pt-br` | assessment, evidence, citations, scope, language, tool preparation, dates, percentage and penalty consistency | answer signal |

Prompts, generated answers, document content, business identifiers,
credentials, and tokens were not persisted in the report. The remaining
failures therefore record deterministic observations of variable prose and one
missing English action preparation; they are not silently reclassified as
passes. The successful canonical assessment gates confirm that application-owned
structured values remained unchanged.

## Measured Azure consumption

Azure Monitor was queried for the exact `20:25:30–20:29:00 UTC` interval:

| Deployment and result | Requests |
| --- | ---: |
| `contractiq-chat`, HTTP 200 | 10 |
| `contractiq-embeddings`, HTTP 200 | 6 |
| **Total** | **16** |

The approved conservative ceiling was 36 requests. Azure recorded 17,483
processed prompt tokens and 1,092 generated tokens for the chat deployment.
There were no failed provider requests and no automatic retries.

Immediately before the run, the subscription month-to-date cost query reported
BRL 0.00021094008 for Foundry Models and BRL 0.00 for Azure Cognitive Search.
Cost data is delayed, so that value is a pre-run reference rather than a claim
about the final charge for this evaluation.

## Final interpretation

The 20-second pacing change removed the rate-limit failure and allowed the full
dataset slice to complete. The comparison also demonstrates why live LLM
quality is kept outside required CI: phrasing and optional tool selection vary
even when the deterministic assessment, retrieval scope, citation chain, and
write boundary remain stable.

The local 12-scenario deterministic suite remains the release gate. A future
product iteration could retain explicitly approved, redacted response samples,
run multiple samples per scenario, and add an independent semantic judge. Those
steps would add cost and evaluation complexity and are outside this portfolio
MVP. This dated run is the final live model evaluation for the ContractIQ
portfolio delivery.
