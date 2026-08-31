# Delivery roadmap

ContractIQ uses milestone-based incremental delivery. Each milestone must end with an executable or demonstrable outcome. Issues may be refined as the team learns; the roadmap is a direction, not a promise of fixed scope.

## M0 - Project Foundation

Establish the repository, engineering standards, architecture records, solution structure, and continuous integration workflow.

## M1 - Deterministic Contract Operations

Model customers, contracts, termination terms, cancellation assessments, requests, persistence, and the first API use cases with automated tests.

## M2 - Demonstrable Web Experience

Deliver a bilingual three-area contract workspace that keeps customer navigation, deterministic decisions, grounded evidence, and safe actions in context.

## M3 - Local Knowledge Retrieval

Ingest fictional documents, preserve citation metadata, and implement local lexical and vector retrieval with PostgreSQL and pgvector.

## M4 - AI Assistant and Tool Calling

Generate grounded bilingual answers, expose safe application tools, require confirmation for writes, and preserve deterministic domain authority.

## M5 - Identity and Optional Azure

Add configurable Microsoft Entra ID authentication and optional Microsoft Foundry and Azure AI Search adapters without making Azure mandatory.

The Azure AI foundation is now in planning and infrastructure-validation delivery. Foundry and Search adapters, live provisioning, and end-user Entra authentication remain separate reviewable slices.

## M6 - Portfolio Release

OpenTelemetry, the optional local dashboard, the structural product redesign, and local-first AI evaluations are implemented. Complete the security review, demonstration material, and the first tagged release.

## Definition of Ready

An issue is ready when:

- the user or engineering outcome is clear;
- acceptance criteria are observable and testable;
- dependencies are completed or explicitly planned;
- security, data, AI, API, and documentation impacts are identified where relevant;
- external accounts or credentials are not an unresolved blocker;
- the issue has a type, area, priority, milestone, and size;
- it is small enough for one focused pull request.

## Definition of Done

An issue is done when:

- acceptance criteria are met;
- relevant tests are added and passing;
- formatting, build, and static analysis pass;
- affected documentation is updated;
- telemetry and failure behavior are considered;
- no secrets or sensitive content are committed;
- the pull request contains verification evidence;
- the change is merged through a linked pull request;
- the source branch is removed.
