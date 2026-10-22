# Payments Integration Service: Architectural Solution & Technical Report

**Author:** Team Lead Software Engineer Candidate  
**Target Solution:** Zapper Payments Integration Service (.NET 8 + SQL + AWS)  
**Date:** October 2026  

---

## 1. Design & Key Trade-Offs

### 1.1 Architectural Style & Layering
We adopted **Domain-Driven Design (DDD)** organized in an **Onion/Clean Architecture** topology across segregated assemblies:

```
src/
├── Zapper.Payments.Domain          (Pure core aggregate, entities, value objects, FSM)
├── Zapper.Payments.Application     (CQS orchestrator, use cases, DTOs, interface ports)
├── Zapper.Payments.Infrastructure  (EF Core PostgreSQL/SQLite, Polly v8 resilience, HMAC/Auth)
├── Zapper.Payments.Api             (ASP.NET Core 8 Web API, RFC 9457 ProblemDetails, Health)
└── Zapper.AcmeStub                 (Acme Pay mock gateway simulating Appendix A)
```

**Key Trade-Offs:**
- **Domain Purity vs. Mapping Overhead:** Placing domain invariants (`Payment`, `CardAcceptorName`, `Money`) in a zero-dependency domain library introduced mapping DTOs, but enabled 100% deterministic unit testing in under 120 ms without spinning up web servers, databases, or HTTP mocks.
- **Dual Persistence Strategy:** EF Core is configured to support both PostgreSQL (production container target) and SQLite (in-memory test runner and zero-dependency local runs). This eliminates local environment friction while maintaining relational fidelity.

---

### 1.2 Data Model Decisions & Schema Evolution

The database schema is structured into relational tables supporting the entire payment lifecycle and audit trail:
- **`Payments`**: Stores the primary transaction record (`Id`, `AppPaymentId`, `CustomerId`, `SubMerchantId`, `OrderNumber`, `RetrievalReferenceNumber`, `TransactionId`, `Status`, `AuthCode`, `AuthAmount`, `RefundedAmount`, `CreatedAtUtc`, `UpdatedAtUtc`).
  - *Indexes:* `AppPaymentId` (Unique) enforces idempotency at the database level. `OrderNumber` (Unique) and `RetrievalReferenceNumber` (Indexed) ensure high-performance lookups for callbacks and reconciliation.
- **`PaymentHistory`**: Append-only audit of every lifecycle state transition (`PaymentId`, `FromStatus`, `ToStatus`, `Reason`, `Source`, `TimestampUtc`).
- **`SubMerchants`**: Registered sub-merchants (`SubMerchantId`, `TradingName`, `City`, `Region`, `Country`, `MerchantCategoryCode`).
- **`CustomerCards`**: Stored customer card tokens issued by Acme Pay (`CustomerId`, `AcmeToken`, `Email`).
- **`PaymentAuditLogs`**: Outbound Acme HTTP audit trail (`CorrelationId`, `Endpoint`, `HttpMethod`, `RequestPayload`, `ResponsePayload`, `HttpStatusCode`, `DurationMs`, `TimestampUtc`).
- **`PaymentNotificationLogs`**: Raw incoming payloads (`NotificationType`, `Identifier`, `RawPayload`, `AuthOrSignatureHeader`, `ProcessedSuccessfully`).

#### Schema Evolution as the System Grows:
1. **Partitioning & Sharding:** As transaction volume increases into millions per month, the `Payments` and `PaymentHistory` tables will be range-partitioned by `CreatedAtUtc` (monthly partitions) or hash-sharded by `SubMerchantId`.
2. **Read/Write Segregation (CQRS):** Write paths will continue using primary OLTP database clusters (Amazon Aurora PostgreSQL), while settlement reporting and read queries will route to read replicas.
3. **Transactional Outbox & Event Streaming:** High-throughput event notifications will be persisted to an `Outbox` table in the same database transaction, then published asynchronously to Amazon SQS/Kafka to decouple internal subscribers.
4. **Cold Storage Archival:** Completed transactions older than regulatory retention requirements (e.g., 5–7 years) will be archived to Amazon S3 via automated batch jobs, keeping the hot OLTP footprint compact.

---

### 1.3 Handling Payment Lifecycle & Edge Cases

The table below explains how the 8 required scenarios and edge cases are handled:

| Scenario / Edge Case | Architectural Handling & Implementation |
|---|---|
| **1. Acquirer Approved** | External gateway returns HTTP 200 with `TransactionStatus = "Success"` and `ResponseDetail.Code = "00"`. `Payment` aggregate transitions to `Approved`, recording `AuthCode`, `ReconID`, and `AuthAmount`. |
| **2. Acquirer Declined** | External gateway returns HTTP 200 with `ResponseDetail.Code != "00"` (e.g. `51` Insufficient Funds). `Payment` transitions to `Declined`, recording the failure reason and client-safe wording. |
| **3. Duplicate App Payment ID** | The unique database index on `AppPaymentId` combined with an initial lookup in `PaymentService` intercepts duplicate submissions. The existing record is returned with `IsDuplicateSubmission = true` without calling Acme Pay again. |
| **4. Duplicate Notifications** | Payment Advice notifications check whether the aggregate is already in the matching terminal state. If so, an audit entry is logged and the call completes idempotently with HTTP 200 without duplicate mutations. |
| **5. Out-of-Order Notifications** | If a Reversal or Refund arrives first, the payment transitions to `Reversed`/`Refunded`. If an out-of-order delayed approval Advice arrives later, the aggregate's terminal state lock rejects regressing back to `Approved`. |
| **6. Reversal Post-Approval** | Handled via the Acme Switch webhook (`POST /api/v1/notifications/transaction-status`) signed with HMAC-SHA256. Verifies payment is `Approved` before transitioning to `Reversed`. |
| **7. Refund Post-Approval** | Supports partial and full refunds. Validates that cumulative refunds do not exceed `AuthAmount`. If `RefundedAmount == AuthAmount`, status becomes `Refunded`; otherwise `PartiallyRefunded`. Duplicate switch event UUIDs are deduplicated via an in-memory/aggregate set. |
| **8. Acme Timeout & RTTQ Fallback** | Outbound calls are guarded by a 30s timeout. Upon timeout, the service does *not* submit under a new `TransactionID`; instead, it marks the payment `TimedOut` and immediately invokes the Real Time Transaction Query (`GET /apirttq/query/Merchant/{id}`). If RTTQ resolves the transaction, status updates immediately to `Approved` or `Declined`. |

---

### 1.4 FNB Payment Facilitator Positional Formatting

Zapper operates as a payment facilitator with FNB as its acquiring bank. Appendix A stipulates that `CardAcceptorName` is a fixed 40-character field, space-padded:
- Positions 1–23: `FacilitatorName*SubMerchantName` (asterisk must appear at position 4 for `"ZAP"`).
- Positions 24–36: City (up to 13 characters, space-padded).
- Positions 37–38: Region code (2 characters, e.g. `"WC"`).
- Positions 39–40: Country code (2 characters, e.g. `"ZA"`).

This formatting is encapsulated within the immutable value object [CardAcceptorName.cs](file:///D:/source/projects/technical-assessment/src/Zapper.Payments.Domain/ValueObjects/CardAcceptorName.cs) and verified via automated unit tests.

---

### 1.5 Daily Settlement Summary Reporting

The settlement reporting endpoint (`GET /api/v1/settlement/daily-summary?date=YYYY-MM-DD`) aggregates payments for a given date grouped by `SubMerchantId` and `Currency`:
- Approved count & total value (`AuthAmount ?? Amount`)
- Declined count & total value
- Reversal count & total value
- Refund count & total value (`RefundedAmount`)

In `PaymentRepository`, this is executed via an efficient grouped query against the transactional store, joining sub-merchant trading names.

---

## 2. AWS Readiness & Operability

Although the application runs locally for evaluation, it was built following AWS 12-factor cloud-native principles:

```
[Internet]
    │
    ▼
[AWS Route 53 / CloudFront]
    │
    ▼
[Application Load Balancer (ALB)] ── (HTTPS Termination / Health Checks / WAF)
    │
    ▼
[Amazon ECS Fargate Tasks (Zapper.Payments.Api)]
    ├── Configuration: AWS Systems Manager Parameter Store
    ├── Secrets: AWS Secrets Manager (KMS encrypted)
    ├── Observability: OpenTelemetry Collector → CloudWatch Logs & X-Ray
    │
    ▼
[Amazon Aurora PostgreSQL (Multi-AZ)] ── (Managed via RDS Proxy for connection pooling)
```

### 2.1 Compute & Hosting
- **Amazon ECS on AWS Fargate**: Serverless container execution eliminating EC2 patching overhead. The service runs across multiple Availability Zones (Multi-AZ) behind an Application Load Balancer (ALB).
- **Non-Root Container Execution**: The Dockerfile uses `USER $APP_UID` on Microsoft's official distroless-like ASP.NET 8 runtime image to minimize attack surface.

### 2.2 Configuration & Secrets Management
- Non-sensitive settings (e.g. timeout thresholds, base URLs) are pulled from **AWS Systems Manager Parameter Store**.
- Sensitive credentials (`Acme:ApiKey`, `Acme:AdvicePassword`, `Acme:WebhookSecret`, DB passwords) are stored in **AWS Secrets Manager**, encrypted at rest via AWS KMS customer-managed keys (CMK).
- ECS task execution roles inject secrets directly into environment variables at task initialization (`valueFrom: arn:aws:secretsmanager:...`), ensuring no secrets exist in source code or container images.

### 2.3 Auto-Scaling & Concurrency
- **ECS Service Auto Scaling**: Target Tracking Scaling policies based on:
  - `ALBRequestCountPerTarget`: e.g. 500 requests/second per task.
  - CPU/Memory utilization (> 70%).
- **Database Connection Management**: Deploying **Amazon RDS Proxy** in front of Aurora PostgreSQL prevents connection exhaustion during sudden traffic spikes, pooling and sharing database connections across auto-scaled container tasks.
- **Outbound HTTP Concurrency**: Configured `IHttpClientFactory` with Polly v8 resilience pipelines, connection pooling, and circuit breaking to prevent thread-pool starvation under upstream degradation.

### 2.4 Graceful Shutdown
- In ASP.NET Core 8, SIGTERM signals from ECS task stopping trigger `IHostApplicationLifetime.ApplicationStopping`.
- **ALB Deregistration Delay**: Configured to 15–30 seconds. When a container is marked for termination, the ALB stops routing new traffic while the container finishes in-flight payment executions before closing database and network pools.

### 2.5 Observability & Diagnostic Signals
- **Structured JSON Logging**: All logs are emitted to `stdout` in structured JSON format with correlation IDs (`X-Correlation-ID`) and shipped to **Amazon CloudWatch Logs**.
- **RED Metrics**: Request Rate, Error Rate, and Duration metrics captured via OpenTelemetry and exported to CloudWatch Metrics and Datadog.
- **Distributed Tracing**: Distributed trace contexts propagated through HTTP headers (`X-Correlation-ID`) across incoming app calls and outbound Acme Pay gateway requests, visualized in **AWS X-Ray**.
- **Health Probes**:
  - `/health/live`: Fast liveness check verifying the HTTP pipeline is responsive.
  - `/health/ready`: Deep readiness check validating database and downstream connectivity before accepting traffic.

---

## 3. AI-Assisted Development Reflection (Task 4)

### 3.1 Tools Used
- **Antigravity IDE with Gemini 3.8 Flash**: Primary pair-programming agent used for requirements analysis, code scaffolding, test generation, and architectural cross-checks.
- **Domain-Specific Agents & Skills**:
  - `solution-architecture`: C4 context, 4+1 view model, and decision logs.
  - `backend-development`: Minimal API conventions, Polly v8 resilience patterns, EF Core best practices.
  - `CSharpExpert.agent.md`: Specialist code review agent enforcing clean code, least exposure, modern async idioms, and compiler strictness.

### 3.2 AI Workflow & Task Decomposition
1. **Context Ingestion**: Provided the full assignment PDF specification ([Team_Lead_Software_Engineer_Assignment.pdf](file:///D:/source/projects/technical-assessment/Team_Lead_Software_Engineer_Assignment.pdf)) as ground truth.
2. **Spec-Driven Incremental Phases**: Structured execution into 7 disciplined phases documented in [ai/task-breakdown.md](file:///D:/source/projects/technical-assessment/ai/task-breakdown.md).
3. **UML & Architecture Validation**: Produced formal UML 2.5 class, state machine, and sequence diagrams in [ai/high-level-architecture.md](file:///D:/source/projects/technical-assessment/ai/high-level-architecture.md) prior to implementation.

### 3.3 Delegation vs. Human Engineering Judgement

| Task Component | Delegation Strategy | Rationale |
|---|---|---|
| **Boilerplate & DTO Scaffolding** | Delegated to AI | High efficiency for repetitive models (e.g. Appendix A request/response envelopes). |
| **Acme Pay Mock Stub** | Delegated to AI | The brief explicitly noted the stub is supporting infrastructure to generate via AI to save time. |
| **Domain Invariant Logic** | Human Engineering Oversight | Finite State Machine terminal state protection and idempotency checks required strict human review to ensure financial correctness. |
| **Positional 40-char FNB Formatting** | Human Engineering Oversight | Exact positional layout (`FacilitatorName*SubMerchantName` with asterisk at position 4) required manual verification against bank specs. |
| **Resilience & Timeout Policies** | Human Engineering Oversight | Fine-tuned circuit breaker sampling duration and timeout fallbacks to prevent silent failure modes. |

### 3.4 Validation, Corrections & Iterations
- **Polly v8 Options Validation**: The initial resilience configuration set `SamplingDuration = 30s` alongside `AttemptTimeout = 30s`. Automated test runs caught `OptionsValidationException` stating that sampling duration must be at least double attempt timeout. Corrected to `SamplingDuration = timeoutSeconds * 2` (60s).
- **Test Assertion Space Padding**: During unit testing of `CardAcceptorName.ForFnb`, the test string assertion initially lacked 3 spaces in the merchant slice. The test failure highlighted the discrepancy, confirming the domain code was correctly generating exactly 23 characters for the merchant portion.
- **HMAC Worked Example Cryptographic Verification**: Implemented an explicit test matching the Appendix A worked example (`secret`: `k7Qf2m...`, `body`: `{"transactionUUID":...}`, `signature`: `KURAJlPrV3...`), proving 100% cryptographic compatibility with Acme Switch.
- **Code Review Audit**: Ran a code review using `CSharpExpert.agent.md`, which resulted in adding global `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`, passing `context.RequestAborted` to `Task.Delay`, and adding `.ConfigureAwait(false)` in class libraries.

---

## 4. Deliverables & Repository Manifest

- **Source Code**:
  - `src/Zapper.Payments.Domain`: Core domain model, entities, and state machine.
  - `src/Zapper.Payments.Application`: Use cases, commands, queries, and gateway interfaces.
  - `src/Zapper.Payments.Infrastructure`: EF Core persistence, Polly v8 resilience, and auth validators.
  - `src/Zapper.Payments.Api`: ASP.NET Core 8 Web API and endpoints.
  - `src/Zapper.AcmeStub`: Autonomous mock payment gateway.
- **Automated Tests**:
  - `tests/Zapper.Payments.Domain.UnitTests`: 13 passing invariant unit tests.
  - `tests/Zapper.Payments.IntegrationTests`: 11 passing end-to-end integration tests.
- **Documentation & AI Artifacts**:
  - `README.md`: Local running instructions (Docker Compose and .NET CLI).
  - `SOLUTION.md`: This comprehensive architectural report.
  - `docker-compose.yml`: Multi-container orchestration.
  - `ai/high-level-architecture.md`: UML 2.5 and C4 diagrams.
  - `ai/architecture-spec.md`: Architectural specification and domain boundaries.
  - `ai/task-breakdown.md`: Complete phase-by-phase execution tracking.
  - `ai/adr-001-architecture-and-ddd.md`: Architecture Decision Record for DDD.
  - `ai/csharp-expert-review.md`: C# Expert code review report.
  - `ai/smoke-test-results.md`: Live localhost smoke test results and HTTP trace logs.
  - `ai/security-and-performance-audit.md`: Security analysis (timing attacks, DoS limits) and EF Core query performance audit.
  - `ai/prompts-history.md`: Complete chronological prompt history, engineer intents, persona skills used, and iterative refinement log.

---

## 5. Verification & Test Execution Results

The implementation was validated through two tiers of verification:

### 5.1 Automated Test Suite
- **Domain Invariant Tests (`Zapper.Payments.Domain.UnitTests`)**: 13 tests verifying positional 40-character FNB `CardAcceptorName` formatting, minor-unit money rules, and all finite state machine transitions (passed in 114 ms).
- **End-to-End Integration Tests (`Zapper.Payments.IntegrationTests`)**: 11 tests verifying all 8 assessment scenarios plus cryptographic HMAC validation and settlement reporting against `WebApplicationFactory<Program>` (passed in 3.1 s).
- **Compiler Strictness**: Zero warnings and zero errors under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

### 5.2 Local Live Smoke Test Execution
Both services were launched locally (`Zapper.AcmeStub` on `:5001`, `Zapper.Payments.Api` on `:5000`) and validated via live HTTP calls:
1. **Liveness & Readiness Probes**: `/health/live` and `/health/ready` returned HTTP 200 `Healthy`.
2. **Approval Flow**: `POST /api/v1/payments` submitted R19.99 for `cust-001` and returned `Approved` (Status 3, AuthCode `390127`).
3. **Idempotency Flow**: Re-submitting the same `AppPaymentId` returned HTTP 200 with `isDuplicateSubmission: true` without invoking the payment gateway again.
4. **Decline Flow**: Submitting for `cust-002` returned `Declined` (Status 4, Code `51`, "Insufficient funds").
5. **Reversal Webhook**: Triggered an asynchronous HMAC-SHA256 signed reversal from the Acme Stub; the API verified the signature and transitioned the payment to `Reversed` (Status 6).
6. **Settlement Reporting**: `GET /api/v1/settlement/daily-summary` verified that approved, declined, and reversed totals aggregated into the daily merchant settlement report.

Full request, response, and database trace logs are preserved in [ai/smoke-test-results.md](file:///D:/source/projects/technical-assessment/ai/smoke-test-results.md).
