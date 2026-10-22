# Architecture Specification: Zapper Payments Integration Service

## 1. Domain-Driven Design (DDD) Overview
The service is structured following Domain-Driven Design (DDD) / Onion Architecture principles:

- **Domain Layer (`Zapper.Payments.Domain`)**:
  - Independent of external frameworks, libraries, persistence concerns, or network transports.
  - Contains core Aggregates (`Payment`), Entities (`SubMerchant`, `CustomerCard`), Value Objects (`CardAcceptorName`, `Money`, `PaymentReference`), Domain Events, and Invariant Rules (Payment Lifecycle Finite State Machine).
  - Enforces Zapper as an FNB Payment Facilitator (e.g. strict 40-character positional `CardAcceptorName` encoding `FacilitatorName*SubMerchantName`).

- **Application Layer (`Zapper.Payments.Application`)**:
  - Encapsulates application use cases, orchestration, and transaction boundaries.
  - Commands: `SubmitPaymentCommand`, `ProcessAdviceNotificationCommand`, `ProcessSwitchWebhookCommand`.
  - Queries: `GetPaymentStatusQuery`, `GetDailySettlementSummaryQuery`.
  - Ports/Interfaces: `IAcmePayClient`, `IPaymentRepository`, `ISubMerchantRepository`, `ICustomerCardRepository`, `IUnitOfWork`.

- **Infrastructure Layer (`Zapper.Payments.Infrastructure`)**:
  - Adapters implementing Application layer interfaces.
  - Relational persistence: EF Core with dual-provider support (PostgreSQL for AWS production, SQLite for frictionless zero-dependency local runs & integration testing).
  - Outbound Acme Pay HTTP Client: configured via `IHttpClientFactory` with Polly v8 standard resilience (timeouts, bounded retries with exponential backoff & jitter, circuit breaker).
  - Webhook Security: HMAC-SHA256 signature verification for Acme Switch webhooks and HTTP Basic Authentication for Advice notifications.
  - Background/Outbox worker: Real Time Transaction Query (RTTQ) query polling on gateway timeouts.

- **Presentation / API Layer (`Zapper.Payments.Api`)**:
  - ASP.NET Core 8 Web API.
  - Endpoints:
    - `POST /api/v1/payments`: Submit payment (App payment initiation).
    - `GET /api/v1/payments/{paymentId}`: Query payment status.
    - `POST /api/v1/notifications/payment-advice`: Payment Advice callback (HTTP Basic Auth).
    - `POST /api/v1/notifications/transaction-status`: Acme Switch Webhook (HMAC-SHA256 signature).
    - `GET /api/v1/settlement/daily-summary`: Daily merchant settlement reporting.
  - Cross-cutting concerns: RFC 9457 `ProblemDetails` error envelope, correlation ID propagation, structured logging.

- **Mock Gateway (`Zapper.AcmeStub`)**:
  - Autonomous stub matching Acme Pay v2 specification (Appendix A).
  - Simulates payment approval/decline, idempotent replay for duplicate attempts, delayed responses/timeouts, and dispatch of Advice and Switch Webhooks (Reversals/Refunds).

---

## 2. Invariant Rules & Payment State Machine
Allowed payment status transitions:
- `Initiated` -> `SubmittedToGateway` -> `Approved` | `Declined` | `TimedOut`
- `TimedOut` -> Query RTTQ -> `Approved` | `Declined` | `NotFound` (marks Failed)
- `Approved` -> `Reversed` (via Switch webhook)
- `Approved` -> `PartiallyRefunded` | `Refunded` (via Switch webhook)
- Duplicate notifications: Idempotent acknowledgment (no conflicting mutations).
- Out-of-order notifications: Transition rules reject regressing already finalized terminal states while maintaining full audit trail.
