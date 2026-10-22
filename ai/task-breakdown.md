# Task Breakdown: Payments Integration Service

## Phase 1: Repository Structure & DDD Foundation (Completed)
- [x] Solution & multi-project setup following Domain-Driven Design (Domain, Application, Infrastructure, Api, AcmeStub, Tests).
- [x] Package installations (EF Core, Npgsql, SQLite, Polly v8 resilience, FluentValidation, xUnit, FluentAssertions).
- [x] Core Domain models:
  - `Payment` Aggregate Root with full Lifecycle state machine.
  - `SubMerchant` with FNB Positional `CardAcceptorName` Value Object.
  - `CustomerCard` entity & token value object.
  - Payment Audit Log & Raw Notification entities.
- [x] Application Layer:
  - Command & Query handlers (CQS pattern).
  - Domain Repository & Payment Gateway interfaces.
  - Invariant unit test suite (13 passing tests).

## Phase 2: Acme Pay Stub Implementation (Completed)
- [x] Mock endpoints:
  - `POST /api/payment`: Handles 200 OK, 400 validation, 401 unauth, 409 duplicate RRN, 429 rate limit, 500 error, and simulated configurable latency.
  - `GET /apirttq/query/Merchant/{merchantId}`: Real Time Transaction Query.
- [x] Notification generation:
  - Payment Advice with HTTP Basic Auth.
  - Transaction Status webhook with HMAC-SHA256 `x-signature` header.

## Phase 3: Infrastructure & Payment Integration (Completed)
- [x] EF Core DbContext, entity type configurations, and auto-seeding for SubMerchants & Customer Stored Cards.
- [x] `AcmePayClient` implementation with Polly v8 resilience:
  - Connection & read timeouts.
  - Fallback / RTTQ resolution on gateway timeout.
  - Concurrency safety & connection pool efficiency.
- [x] Webhook security services:
  - Basic Auth filter for Advice endpoint.
  - HMAC-SHA256 signature validator for Switch webhook.

## Phase 4: API Endpoints & Daily Settlement Reporting (Completed)
- [x] `POST /api/v1/payments` (App initiation, lookup sub-merchant & stored card token, invoke gateway).
- [x] `GET /api/v1/payments/{paymentId}` (Status inquiry).
- [x] Callback endpoints:
  - `POST /api/v1/notifications/payment-advice`
  - `POST /api/v1/notifications/transaction-status`
- [x] Daily settlement summary endpoint (`GET /api/v1/settlement/daily-summary?date=YYYY-MM-DD`).

## Phase 5: Containerisation & Docker Setup (Completed)
- [x] Multi-stage Dockerfile for `Zapper.Payments.Api` and `Zapper.AcmeStub`.
- [x] `docker-compose.yml` orchestrating API, AcmeStub, and PostgreSQL database with health checks.
- [x] `.dockerignore` for minimal, secure image footprints.

## Phase 6: Automated Testing & Validation (Completed)
- [x] Unit tests for Domain Invariants (State Machine transitions, FNB `CardAcceptorName` formatting, Amount handling).
- [x] Integration tests covering required scenarios (11 passing tests):
  1. Payment approved.
  2. Payment declined.
  3. Duplicate submission with identical App Payment ID (idempotency).
  4. Duplicate notification delivery.
  5. Out-of-order notifications.
  6. Reversal after approval.
  7. Refund / Partial refund after approval.
  8. Acme timeout handling with RTTQ reconciliation.
  9. Daily settlement reporting.
- [x] 24 total automated tests passing with zero errors and warnings under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

## Phase 7: Documentation & AI Workflow Deliverables (Completed)
- [x] `README.md` with exact local run instructions (Docker Compose & .NET CLI).
- [x] `SOLUTION.md` covering architecture, trade-offs, AWS readiness, operability, and AI workflow reflection.
- [x] Complete AI artifacts in `ai/` (`high-level-architecture.md`, `architecture-spec.md`, `task-breakdown.md`, `adr-001-architecture-and-ddd.md`, `csharp-expert-review.md`).
