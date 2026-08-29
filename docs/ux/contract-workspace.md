# Contract workspace UX specification

## Purpose

This document defines the product experience for the ContractIQ v1.0 contract
workspace. The redesign must make the application feel like a focused
contract-operations product rather than a portfolio landing page while keeping
the MVP small and preserving the existing application capabilities.

The workspace must support the complete demonstrated flow:

1. Find a customer.
2. Select one of the customer's contracts.
3. Review the deterministic cancellation assessment.
4. Ask the grounded assistant for an explanation and supporting evidence.
5. Create a cancellation request manually or review an action proposed by the
   assistant.
6. Explicitly confirm the request and receive an auditable result.

The redesign does not introduce billing, multi-tenancy, document management,
authentication, or additional backend workflows.

## Product users

### Primary persona: contract operations analyst

The primary user works in contract operations, customer success, or an
equivalent back-office function. They handle multiple customer requests during
the day and need to answer cancellation questions quickly without manually
reconstructing contract rules.

They need to:

- locate the correct customer and contract;
- understand whether cancellation can be requested;
- see the effective date and financial impact;
- distinguish application-calculated facts from AI-generated explanations;
- consult the clauses and policies that support an answer;
- create a request safely and leave an auditable trail.

### Secondary persona: legal or compliance reviewer

The secondary user validates how a decision was reached. They care about the
rules applied, the supporting documents, the origin of an action, and whether a
human explicitly confirmed the operation.

## Job to be done

> When a customer asks to cancel a contract, I want to find the correct
> contract, understand the deterministic outcome and its documentary evidence,
> and create an auditable request without allowing AI to invent business rules
> or execute an operation without my confirmation.

## Experience principles

1. **Start with the work.** The initial product view is the contract workspace,
   not a marketing hero or portfolio dashboard.
2. **Keep context visible.** The selected customer and contract remain visible
   while reviewing terms, asking questions, and confirming an action.
3. **Make the decision scannable.** Eligibility, effective date, penalty, and
   primary action have the strongest hierarchy.
4. **Separate facts from explanations.** Structured data and deterministic
   results are visually distinct from AI-generated text and retrieved evidence.
5. **Use progressive disclosure.** Show the decision first and make detailed
   terms, evidence metadata, and technical context available when needed.
6. **Require deliberate writes.** Preparing an action is not the same as
   executing it. Every cancellation request requires explicit review and
   confirmation.
7. **Degrade safely.** Failure of the LLM or retrieval layer must not block the
   deterministic contract workflow.
8. **Do not imply unavailable features.** Navigation must not contain empty or
   inactive product areas merely to make the MVP appear larger.

## Information architecture

ContractIQ v1.0 is a focused workspace rather than a collection of artificial
top-level pages.

```text
ContractIQ
└── Contract workspace
    ├── Customer and contract navigator
    │   ├── Customer search
    │   ├── Customer list
    │   └── Contracts for the selected customer
    ├── Contract decision workspace
    │   ├── Contract identity and status
    │   ├── Cancellation assessment
    │   ├── Penalty breakdown
    │   ├── Contract terms
    │   └── Cancellation request result
    └── ContractIQ assistant
        ├── Suggested questions
        ├── Conversation
        ├── Evidence and citations
        └── Proposed action
```

An operations dashboard should only be added when the application can provide
real operational measures such as pending requests, contracts approaching a
commitment date, or financial exposure. Counts of seeded customers and labels
such as "Grounded AI" are not operational key performance indicators.

## Primary journey

### Find and assess

1. The user opens ContractIQ and lands in the contract workspace.
2. The customer and contract navigator is immediately available.
3. The user searches for or selects a customer.
4. Contracts for that customer appear within the same navigator.
5. The user selects a contract.
6. The workspace loads contract data and the cancellation assessment together.
7. The decision area shows eligibility, earliest termination date, chargeable
   periods, penalty, and the action available for the contract.

### Explain with evidence

1. The assistant retains the selected customer and contract as its visible
   context.
2. The user chooses a suggested question or writes a question.
3. The assistant returns a grounded explanation.
4. Citations are displayed beside the response and expose document title,
   version, section, and page.
5. Insufficient evidence is presented as an explicit limitation, not hidden by
   a confident response.

### Create a cancellation request

The flow can start from either the deterministic assessment or an assistant
action proposal. Both entry points use the same review and confirmation
experience.

1. The user chooses to create or review the cancellation request.
2. A review surface shows customer, contract, effective date, penalty, and what
   the operation will do.
3. When the flow originated from the assistant, the interface states that the
   assistant prepared the action but did not execute it.
4. The user confirms that they reviewed the assessment.
5. The user explicitly submits the request.
6. The workspace shows the request identifier and `pending review` status and
   prevents accidental duplicate submission.

## Responsive workspace

### Desktop: three-area workspace

At desktop widths, the product uses three persistent areas:

- a 260-300 pixel customer and contract navigator;
- a flexible central contract decision workspace;
- a 360-400 pixel contextual assistant panel.

The application header contains the product identity, workspace name,
environment or API health indicator, language selector, and any future user
menu. Development telemetry must not be presented as a business KPI.

The central area begins with the selected company, contract identifier, and
status. The cancellation assessment appears above detailed terms so that its
decision, effective date, penalty, and primary action are visible without
scrolling at 1440 by 900 pixels.

The assistant panel remains associated with the selected contract. Its composer
stays available while the conversation scrolls. Evidence can expand without
replacing the deterministic decision area.

Each area may scroll independently when required, but the selected context must
remain visible.

### Tablet: focused content with drawers

At widths from 768 to 1023 pixels:

- the customer and contract navigator becomes a left drawer;
- the selected customer and contract remain in a compact context header;
- the decision workspace uses the full content width;
- the assistant opens as a right drawer or full-height overlay;
- the cancellation action remains easy to reach without covering assessment
  values.

Opening or closing a drawer must not reset the selected context, assistant
answer, or assessment.

### Mobile: sequential task flow

Below 768 pixels, the experience becomes a deliberate sequence:

1. Select customer.
2. Select contract.
3. Review the decision.
4. Expand contract terms if needed.
5. Open the assistant as a full-screen surface.
6. Review and confirm an action in a full-screen dialog or sheet.

The current customer and contract appear in the mobile header. The decision
summary prioritizes eligibility, date, and penalty. Monetary values must not be
truncated, and the layout must not require horizontal scrolling at 320 pixels.

## Domain and AI boundary

The interface must reinforce the system's architectural boundary.

### Deterministic application and domain

The following values and operations come from structured application data and
domain logic:

- customer and contract identity;
- contract status and terms;
- cancellation eligibility;
- earliest termination date;
- chargeable monthly periods;
- cancellation penalty;
- creation and state of a cancellation request;
- validation, idempotency, and transaction handling.

These values must not be described as AI-generated. A short explanation such as
"Calculated from contract terms" may be used near the assessment.

### Retrieval and language model

The AI experience is responsible for:

- understanding a natural-language question;
- retrieving relevant contract and policy evidence;
- explaining structured facts and retrieved evidence in natural language;
- citing the evidence used;
- proposing an available application capability.

The assistant may prepare a cancellation action, but preparation must be shown
as a proposal. It cannot override the domain assessment or bypass explicit
confirmation. If the assistant repeats a date or penalty, it must use the
deterministic assessment supplied by the application.

### Visual distinction

- deterministic outcomes use the primary product surface and semantic status
  colors;
- AI messages use a subtle, consistent assistant treatment;
- retrieved sources use evidence or citation components;
- an assistant proposal is labelled as prepared, not executed;
- the final confirmation repeats the deterministic values regardless of the
  action's entry point.

## Components and states

The design should be composed around these responsibilities:

- product header;
- customer and contract navigator;
- customer search;
- contract context header;
- contract summary;
- cancellation decision;
- penalty breakdown;
- contract terms;
- contextual assistant;
- conversation message;
- citation and evidence view;
- proposed action;
- cancellation review;
- cancellation request receipt;
- status badge;
- loading skeleton;
- empty state;
- inline error;
- notification or toast.

The following states must be intentionally designed:

### Selection and data

- customers loading;
- customer load failure;
- no customers;
- no customer matching the search;
- no customer selected;
- contracts loading;
- customer with no contracts;
- no contract selected;
- contract workspace loading;
- contract or assessment load failure;
- active, cancelled, and expired contracts.

### Assessment

- cancellation allowed with penalty;
- cancellation allowed without penalty;
- cancellation not allowed because the contract is cancelled;
- cancellation not allowed because the contract is expired;
- cancellation request already created.

### Assistant

- no conversation yet;
- suggested question selected;
- question being processed;
- grounded answer with citations;
- answer with insufficient evidence;
- assistant unavailable;
- action proposed and executable;
- action proposed but rejected by domain rules;
- action awaiting explicit review.

### Request operation

- confirmation not yet acknowledged;
- submission in progress;
- validation or dependency failure;
- conflict because an open request already exists;
- request created and pending review.

An assistant failure must be local to the assistant. It must not replace or
disable already loaded deterministic content.

## Visual direction

The product should feel like a calm enterprise operations tool:

- neutral application background and clear white work surfaces;
- one restrained brand color;
- green reserved for allowed or successful outcomes;
- amber reserved for financial impact or attention;
- red reserved for blocked or failed outcomes;
- a subtle blue or violet treatment may identify AI-generated content;
- fewer nested cards, using spacing and dividers to establish groups;
- consistent icons from one accessible icon set rather than text glyphs;
- tabular or aligned numbers for monetary values;
- clear type hierarchy with limited use of uppercase labels;
- moderate information density so the decision remains above the fold.

## Accessibility requirements

- All interactive controls must be usable with a keyboard.
- Focus indicators must meet contrast requirements and remain visible.
- Drawers and dialogs must move focus inside when opened, contain focus while
  active, close with `Escape` when safe, and return focus to their trigger.
- A submitting confirmation cannot be accidentally dismissed.
- Icon-only controls require accessible names.
- Status must not be communicated by color alone.
- Loading, error, assistant answer, and request success updates require
  appropriate live-region behavior without excessive announcements.
- Customer and contract selection must expose the selected state to assistive
  technology.
- Heading order must reflect the workspace hierarchy.
- Text and controls must meet WCAG 2.1 AA contrast requirements.
- Touch targets should be at least 44 by 44 CSS pixels on tablet and mobile.
- English and Brazilian Portuguese content must not overflow or become
  truncated at supported widths.
- Reduced-motion preferences must be respected.

## Acceptance criteria

### Core experience

1. The initial screen opens directly in the contract workspace without a
   marketing hero, portfolio release badge, or artificial KPI section.
2. A user can reach a contract cancellation assessment within three direct
   interactions after the customer data loads.
3. At 1440 by 900 pixels, eligibility, effective date, penalty, and the primary
   cancellation action are visible without page scrolling.
4. The selected customer and contract remain visible while the user reviews the
   assessment or interacts with the assistant.
5. Contract selection does not rely on a horizontally scrolling tab row.
6. Changing customer clears the previous contract, assessment, answer, and
   action proposal.
7. Changing contract clears any answer or action proposal belonging to the
   previous contract.

### Deterministic decision and action

8. Eligibility, date, periods, and penalty are labelled as application-calculated
   values rather than AI-generated content.
9. Manual and assistant-originated cancellation actions use the same review and
   confirmation component.
10. The review displays customer, contract, earliest termination date, penalty,
    and the effect of submitting the request.
11. No cancellation request is sent before explicit acknowledgement and
    confirmation.
12. After a successful request, the identifier and pending-review status are
    visible and duplicate submission is no longer offered.
13. A conflict caused by an existing open request is explained without losing
    the selected contract context.

### Assistant and evidence

14. The desktop assistant remains available as a contextual panel; on tablet
    and mobile it opens as a drawer or full-screen surface.
15. Every citation exposes document title, version, section, and page.
16. Insufficient evidence is visibly different from a grounded answer.
17. An assistant outage does not block the deterministic assessment or manual
    request flow.
18. A proposed action is labelled as prepared and never appears as completed
    before domain execution succeeds.
19. An action rejected by domain rules cannot display an enabled confirmation
    control.

### Responsive behavior and accessibility

20. No horizontal page scrolling occurs from 320 to 1920 CSS pixels.
21. At tablet widths, navigation and assistant drawers retain the current
    contract context when opened and closed.
22. Dialog and drawer focus behavior follows the accessibility requirements in
    this document.
23. All interactive controls have an accessible name and visible focus state.
24. Status remains understandable without color.
25. The complete workflow is available in English and Brazilian Portuguese.

### Verification

26. Automated frontend tests cover customer and contract selection, assessment
    rendering, assistant failure, grounded citations, proposed action, explicit
    confirmation, conflict, and successful request creation.
27. Existing API contracts and deterministic domain behavior remain unchanged.
28. Navigation contains no links to unavailable product areas.
29. Development observability is not presented as a business metric.

## Out of scope for this redesign

- changing cancellation domain rules;
- changing RAG, LLM, or embedding providers;
- adding new backend endpoints solely to populate decorative dashboards;
- user authentication or authorization;
- tenant administration;
- billing and subscriptions;
- document upload and knowledge administration;
- production observability configuration.
