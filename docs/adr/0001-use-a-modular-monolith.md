# ADR 0001: Use a modular monolith

- Status: Accepted
- Date: 2026-08-27

## Context

ContractIQ must demonstrate credible enterprise architecture while remaining small, understandable, and easy to run as a portfolio project.

## Decision

Build a modular monolith with separate Domain, Application, Infrastructure, API, and Web projects. Keep Contract Operations and Knowledge as logical boundaries within one deployable backend.

## Consequences

- Business and infrastructure boundaries remain explicit.
- Local development, testing, debugging, and deployment stay simple.
- Cross-module calls remain in-process and transactional when required.
- The solution does not demonstrate distributed systems patterns that the use case does not need.
- A module can be extracted later only if an independently scalable or deployable boundary becomes real.
