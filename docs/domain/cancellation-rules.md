# Contract cancellation rules

This document describes the deterministic cancellation rules implemented by the Contract Operations domain. These rules are authoritative application behavior; an AI assistant may explain their result but must not recalculate or override it.

## Language

- **Requested on** is the UTC business date on which a cancellation request is submitted.
- **Earliest termination date** is the first termination date allowed by the notice period.
- **Minimum commitment end date** is the first date on which early termination no longer has a penalty.
- **Chargeable monthly period** is a monthly period that starts before the minimum commitment ends. A final partial period counts as one period.

## Assessment rules

Rules are evaluated in this order:

1. The request date cannot be before the contract start date.
2. A cancelled or expired contract cannot accept another cancellation request.
3. For an active contract, the earliest termination date is the request date plus the notice period in calendar days.
4. When the earliest termination date is on or after the minimum commitment end date, the penalty is zero.
5. Otherwise, the penalty is the configured percentage of the monthly fee for every remaining chargeable monthly period.

The calculation is:

```text
penalty = monthly fee × penalty rate × chargeable monthly periods
```

The final amount is rounded to two decimal places with `MidpointRounding.AwayFromZero`. Calculations use `decimal`; the application does not perform currency conversion.

## Boundaries and examples

- A notice period of zero makes the request date the earliest termination date.
- A request whose earliest termination date equals the commitment end date has no penalty.
- If only part of the final monthly period remains, that period is still chargeable.
- Currency codes are normalized to three uppercase ASCII letters, such as `USD` or `BRL`. The MVP validates the format, not the complete ISO 4217 catalog.
- Cancellation assessments based on current time use the UTC date supplied by `TimeProvider`, which keeps tests and demonstrations repeatable.

The `DateOnly` assessment overload is intentionally pure and can reproduce a historical business date. Application operations that mean "today" must use the `TimeProvider` overload; this prevents the domain from reading the machine clock directly.

For a monthly fee of `USD 100.00`, a penalty rate of `25%`, and two chargeable periods, the penalty is `USD 50.00`.

## Explicitly outside this increment

This first model does not cover business-day calendars, taxes, currency conversion, negotiated waivers, automatic renewals, prorated billing, or cancellation-request persistence. Those behaviors must be introduced only with explicit business rules and tests.
