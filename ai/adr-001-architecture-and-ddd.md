# ADR 001: Domain-Driven Design (DDD) & Onion Layering Architecture

## Status
Accepted

## Context
Zapper operates as a payment facilitator (with FNB as the acquiring bank). The service must process QR-code initiated payments from mobile clients, look up sub-merchants and tokenized stored cards, coordinate with external payment gateway Acme Pay, handle out-of-order and duplicate notifications, and provide merchant settlement reporting.
Quality attributes demand:
- Strict business invariant protection (e.g. valid status transitions, positional formatting rules, financial precision).
- Resilience to slow and failing third-party networks (Acme Pay timeouts, duplicate deliveries, HMAC webhook verification).
- Decoupling of domain business rules from database technologies and HTTP transport.
- Fast, deterministic local testing alongside production-grade cloud readiness.

## Decision
We adopt **Domain-Driven Design (DDD)** organized in an **Onion/Clean Architecture** topology across separate .NET assemblies:

1. **`Zapper.Payments.Domain`**:
   - Zero external dependencies.
   - Houses the `Payment` Aggregate Root, enforcing lifecycle invariants (Finite State Machine).
   - Houses value objects: `CardAcceptorName` (strictly enforcing the 40-character FNB positional standard: `FacilitatorName*SubMerchantName` at pos 4/8/13 + City + Region + Country), `Money` (integer minor units in ZAR cents), and `PaymentStatus`.
   - Houses sub-merchant and customer stored card entities.

2. **`Zapper.Payments.Application`**:
   - Implements Command and Query Segregation (CQS).
   - Defines use cases: `SubmitPayment`, `QueryPaymentStatus`, `ProcessAdviceNotification`, `ProcessSwitchWebhook`, `GetDailySettlementSummary`.
   - Declares abstraction contracts: `IPaymentRepository`, `ISubMerchantRepository`, `ICustomerCardRepository`, `IAcmePayClient`, `IUnitOfWork`.

3. **`Zapper.Payments.Infrastructure`**:
   - Implements data persistence using EF Core with dual-database support: PostgreSQL (production container target) and SQLite (frictionless local development and in-memory test runner).
   - Integrates `AcmePayClient` leveraging `Microsoft.Extensions.Http.Resilience` (Polly v8) for granular timeout control, retries with jitter on transient failures, and circuit breaking.
   - Implements cryptographic verification (`HMAC-SHA256` for Acme Switch and HTTP Basic Auth for Advice notifications).

4. **`Zapper.Payments.Api`**:
   - ASP.NET Core 8 Minimal APIs / Controllers exposing REST endpoints.
   - Adheres to RFC 9457 `ProblemDetails` for stable machine-readable error contracts.
   - Enforces correlation ID logging and health endpoints (`/health/live`, `/health/ready`).

5. **`Zapper.AcmeStub`**:
   - Standalone mock PSP container simulating Acme Pay v2 specification (Appendix A).

## Consequences
- **Positive**:
  - Domain rules can be tested via unit tests in milliseconds without spinning up databases or HTTP mocks.
  - Complete isolation between external PSP idiosyncrasies and internal Zapper entities.
  - Flexibility to switch database providers or transport protocols without altering core logic.
- **Trade-offs**:
  - Mapping layers between Domain Aggregates, EF Core entities, and API DTOs.
  - Additional project scaffolding overhead compared to an anemic CRUD controller.
