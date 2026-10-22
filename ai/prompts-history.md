# AI Collaboration Prompt History & Iterative Refinement Log

This document provides a chronological record of all user prompts, engineering contexts, AI agent roles/skills invoked, actions taken, and deliverables produced during the development of the **Zapper Payments Integration Service**.

This document fulfills the requirements of **Task 4: AI Collaboration Artifacts & Documentation** from the technical assessment specification.

---

## Prompt Summary Table

| # | Timestamp (UTC) | User Prompt Summary | Skills / Personas Invoked | Primary Deliverable / Outcome |
|---|---|---|---|---|
| **1** | 2026-10-22 18:26:30 | Ingest and analyze technical assessment brief PDF | Generalist / Analyst | Requirements matrix, scope extraction, task breakdown |
| **2** | 2026-10-22 18:41:08 | Structure repository using DDD; invoke Architecture & .NET skills | `solution-architecture`, `backend-development` | Solution structure, Onion Architecture, `Directory.Build.props` |
| **3** | 2026-10-22 18:55:23 | Complete remaining Phase 1 domain modeling tasks | `backend-development`, Domain Expert | `Payment` aggregate, FNB `CardAcceptorName`, 13 unit tests |
| **4** | 2026-10-22 19:11:47 | Draw high-level architecture diagram for validation | `solution-architecture` | Initial high-level architecture draft |
| **5** | 2026-10-22 19:12:59 | Balance architecture using UML and Architecture skills | `solution-architecture`, UML 2.5 Standards | `ai/high-level-architecture.md` (4+1 view model, state machine) |
| **6** | 2026-10-22 19:36:42 | Complete Phase 2 through Phase 4 | Full-Stack .NET Engineer | `Zapper.AcmeStub`, EF Core persistence, Polly v8, API endpoints |
| **7** | 2026-10-22 19:46:50 | Code review using `ai/CSharpExpert.agent.md` | `CSharpExpert.agent.md` | `ai/csharp-expert-review.md`, fixed Polly v8 sampling bug |
| **8** | 2026-10-22 19:53:09 | Implement Phase 5 (Docker) and Phase 6 (Tests) | DevOps & QA Engineer | `Dockerfile`, `docker-compose.yml`, 11 integration tests (24 total) |
| **9** | 2026-10-22 19:59:56 | Proceed with Phase 7 documentation | Technical Writer / Architect | [README.md](file:///D:/source/projects/technical-assessment/README.md), [SOLUTION.md](file:///D:/source/projects/technical-assessment/SOLUTION.md) |
| **10** | 2026-10-22 20:04:43 | Run application locally and execute smoke tests | Systems / Test Engineer | Live local run of Api & AcmeStub, 6 smoke test scenarios verified |
| **11** | 2026-10-22 20:11:09 | Document smoke test results | QA Engineer | `ai/smoke-test-results.md` with raw HTTP request/response traces |
| **12** | 2026-10-22 20:14:04 | Invoke Security Analyst and Performance Tester skills | `security-engineering`, `performance-testing` | `ai/security-and-performance-audit.md`, timing attack & DoS fixes |
| **13** | 2026-10-22 20:19:42 | Document all prompts into an md file | Documentation Specialist | `ai/prompts-history.md` (this file) |

---

## Detailed Prompt Breakdown

### Prompt 1: Initial Assessment Brief Analysis
- **Timestamp:** `2026-10-22T18:26:30Z`
- **User Request:**
  ```
  We have a technical assessment we need to complete. Read through the brief on this path: D:\source\projects\technical-assessment\Team_Lead_Software_Engineer_Assignment.pdf
  ```
- **Context & Intent:**
  Read and interpret the assignment requirements for a Team Lead Software Engineer position, focusing on designing and building a Payments Integration Service for Zapper that connects to a mock third-party gateway ("Acme Pay") according to Appendix A specifications.
- **AI Agent Actions:**
  - Extracted text and tables from `Team_Lead_Software_Engineer_Assignment.pdf`.
  - Identified core requirements:
    - **Language / Framework:** C# / .NET 8, SQL (PostgreSQL), AWS architecture.
    - **Four Core Tasks:** Task 1 (Architecture & Implementation), Task 2 (Resilience & Observability), Task 3 (Comprehensive Testing), Task 4 (AI Collaboration Artifacts & Documentation).
    - **Acme Pay Gateway Specification (Appendix A):** JSON/REST endpoints, 40-character FNB positional `CardAcceptorName`, Minor-unit transaction amounts, Webhook callbacks signed with HMAC-SHA256, Payment advice notifications secured with HTTP Basic Auth.
    - **Mandatory 8 Lifecycle Scenarios:** Approved, Declined, Duplicate AppPaymentId, Duplicate Notification, Out-of-Order Notification, Post-Approval Reversal, Post-Approval Partial Refund, Post-Approval Full Refund.
- **Key Artifacts Created:**
  - Initialized internal execution plan.

---

### Prompt 2: Domain-Driven Design Setup & Project Initialization
- **Timestamp:** `2026-10-22T18:41:08Z`
- **User Request:**
  ```
  I want us to follow the Domain-Design-Development Architecture when structuring our repository. Please invoke the Relvant skills (Architecture and .Net Developer) that will help you initialize the project nicely.
  ```
- **Context & Intent:**
  Establish a robust, enterprise-grade Domain-Driven Design (DDD) and Onion Architecture directory and project structure using .NET 8, enforcing separation of concerns.
- **Personas / Skills Invoked:**
  - `solution-architecture`: To design the layer topology and dependency inversion boundaries.
  - `backend-development`: To initialize .NET 8 CLI solutions, projects, and assembly references.
- **AI Agent Actions:**
  - Scaffolded segregated .NET 8 projects:
    - `src/Zapper.Payments.Domain` (Core domain model, zero dependencies)
    - `src/Zapper.Payments.Application` (CQS handlers, service interfaces, DTOs)
    - `src/Zapper.Payments.Infrastructure` (EF Core, external Acme Pay client, security validators)
    - `src/Zapper.Payments.Api` (ASP.NET Core 8 Minimal API, middleware, health probes)
    - `src/Zapper.AcmeStub` (Mock payment gateway simulating Appendix A)
    - `tests/Zapper.Payments.Domain.UnitTests`
    - `tests/Zapper.Payments.IntegrationTests`
  - Created solution root file `Zapper.Payments.sln` linking all projects.
  - Configured root `Directory.Build.props` enforcing:
    - `<TargetFramework>net8.0</TargetFramework>`
    - `<Nullable>enable</Nullable>`
    - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
    - `<ImplicitUsings>enable</ImplicitUsings>`
- **Key Artifacts Created:**
  - `ai/architecture-spec.md`
  - `ai/task-breakdown.md`
  - `ai/adr-001-architecture-and-ddd.md`

---

### Prompt 3: Phase 1 Domain Implementation
- **Timestamp:** `2026-10-22T18:55:23Z`
- **User Request:**
  ```
  Let's continue with actioning the remaining Phase 1 tasks
  ```
- **Context & Intent:**
  Implement the pure core domain entities, value objects, domain events, and finite state machine rules according to the domain specifications.
- **AI Agent Actions:**
  - Implemented `Payment` aggregate root managing payment lifecycle states:
    - `Initiated`, `PendingGateway`, `Approved`, `Declined`, `Cancelled`, `Reversed`, `PartiallyRefunded`, `Refunded`.
  - Implemented `CardAcceptorName` value object enforcing the strict FNB Payment Facilitator 40-character positional format:
    - Positions 1–23: Facilitator name and sub-merchant trading name (`ZAP*MerchantName`) with asterisk at index 4.
    - Positions 24–36: City (13 chars, space-padded).
    - Positions 37–38: Region/Province (2 chars).
    - Positions 39–40: Country code (2 chars, e.g. `ZA`).
  - Implemented `Money` value object handling minor units (cents) and ISO currency codes (`ZAR`).
  - Created domain events: `PaymentApprovedDomainEvent`, `PaymentDeclinedDomainEvent`, `PaymentReversedDomainEvent`, `PaymentRefundedDomainEvent`.
  - Wrote 13 unit tests in `Zapper.Payments.Domain.UnitTests` covering FNB formatting, minor-unit money math, and state machine transition rules. All 13 tests passed.

---

### Prompt 4: High-Level Architecture Validation
- **Timestamp:** `2026-10-22T19:11:47Z`
- **User Request:**
  ```
  please draw a high-level architecture so I can validate it.
  ```
- **Context & Intent:**
  Provide visual architecture representations for stakeholder validation before embarking on large-scale infrastructure and API code generation.
- **AI Agent Actions:**
  - Drafted an initial architectural diagram outlining the client application, API layer, domain core, infrastructure adapters, and Acme Pay external boundary.

---

### Prompt 5: Balanced Architecture via UML and Architecture Skills
- **Timestamp:** `2026-10-22T19:12:59Z`
- **User Request:**
  ```
  use the available UML skill and the architecture skill to balance you
  ```
- **Context & Intent:**
  Apply formal UML 2.5 notation and software architecture standards (Kruchten's 4+1 Architectural View Model) to deliver a comprehensive, enterprise-grade architecture specification.
- **Personas / Skills Invoked:**
  - `solution-architecture`: To structure enterprise-grade system perspectives (logical, physical, operational, and non-functional requirements).
  - UML 2.5 modeling guidelines: To generate syntactically correct, expressive Mermaid sequence, component, deployment, and statechart diagrams.
- **AI Agent Actions:**
  - Produced `ai/high-level-architecture.md` structured around the 4+1 View Model:
    1. **Logical View:** Onion Architecture component diagrams showing inward dependency flows.
    2. **Process View:** Complete UML sequence diagrams for:
       - Synchronous Payment Authorization (`POST /api/v1/payments`)
       - Asynchronous Switch Reversal Webhook (`POST /api/v1/notifications/transaction-status`) with HMAC-SHA256
       - Asynchronous Payment Advice Callback (`POST /api/v1/notifications/payment-advice`) with Basic Auth
    3. **Development View:** Assembly layout, naming conventions, and C# coding standards.
    4. **Physical / Deployment View:** AWS production architecture (CloudFront, Route 53, ALB, ECS Fargate, Aurora PostgreSQL Multi-AZ, Secrets Manager, CloudWatch).
    5. **Scenarios & State Machine View:** Formal statechart showing all valid transitions and illegal transition safeguards.
- **Key Artifacts Created:**
  - `ai/high-level-architecture.md`

---

### Prompt 6: Execution of Phases 2, 3, and 4
- **Timestamp:** `2026-10-22T19:36:42Z`
- **User Request:**
  ```
  Architecure Looks good. You can go ahead and complete phase 2 - phase 4
  ```
- **Context & Intent:**
  Implement the mock payment gateway (`Zapper.AcmeStub`), the Infrastructure persistence/resilience layer, and the ASP.NET Core API endpoints.
- **AI Agent Actions:**
  - **Phase 2 (`Zapper.AcmeStub`):**
    - Built ASP.NET Core stub on port 5001 matching Appendix A.
    - Implemented `POST /api/payment`: Validates FNB 40-character `CardAcceptorName`, amounts, and returns deterministic approval (`cust-001`, code `00`) or decline (`cust-002`, code `51`).
    - Implemented `GET /apirttq/query/Merchant/{id}` real-time transaction query endpoint.
    - Added test dispatchers for HMAC-SHA256 switch notifications and Basic Auth payment advice callbacks.
  - **Phase 3 (Infrastructure Layer):**
    - Built EF Core `AppDbContext` supporting both PostgreSQL (Npgsql) and SQLite.
    - Configured table mappings, unique indexes on `AppPaymentId` and `OrderNumber`, and foreign key relations.
    - Implemented `PaymentRepository` handling atomic transactions and audit history logging.
    - Implemented `AcmePayClient` using Polly v8 resilience pipeline (retry with exponential jitter, circuit breaker, timeout).
    - Implemented `HmacSignatureValidator` and `BasicAuthValidator`.
  - **Phase 4 (API Layer):**
    - Built Minimal API endpoints in `PaymentEndpoints`, `NotificationEndpoints`, and `SettlementEndpoints`.
    - Added global RFC 9457 `ProblemDetails` exception handler middleware.
    - Added `CorrelationIdMiddleware` propagating `X-Correlation-ID`.
    - Added ASP.NET Core health check endpoints (`/health/live`, `/health/ready`).

---

### Prompt 7: C# Expert Code Review
- **Timestamp:** `2026-10-22T19:46:50Z`
- **User Request:**
  ```
  use this agent to review the code: D:\source\projects\technical-assessment\ai\CSharpExpert.agent.md
  ```
- **Context & Intent:**
  Execute a thorough, authoritative code review against modern C# 12/.NET 8 idioms, clean code principles, performance, and concurrency safety.
- **Personas / Skills Invoked:**
  - `CSharpExpert.agent.md`: Strict senior C# evaluator checking memory allocations, async/await hygiene, cancellation tokens, thread-safety, and exception handling.
- **AI Agent Actions:**
  - Conducted full audit across all projects.
  - Identified a critical Polly v8 configuration bug in `AcmePayClient.cs`: `SamplingDuration` must be at least double the `AttemptTimeout` to avoid runtime configuration exceptions. Fixed by setting `SamplingDuration = TimeSpan.FromSeconds(timeoutSeconds * 2)`.
  - Validated primary constructors, immutable records, pattern matching, cancellation token threading, and zero compilation warnings.
- **Key Artifacts Created:**
  - `ai/csharp-expert-review.md`

---

### Prompt 8: Containerisation (Phase 5) & Automated Testing (Phase 6)
- **Timestamp:** `2026-10-22T19:53:09Z`
- **User Request:**
  ```
  Let's do phase 5 and 6
  ```
- **Context & Intent:**
  Deliver multi-stage production Docker containers, Docker Compose orchestration, and comprehensive automated integration tests covering all 8 required payment scenarios.
- **AI Agent Actions:**
  - **Phase 5 (Docker):**
    - Authored multi-stage `Dockerfile` with distinct build and production runtime stages using `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`.
    - Created dedicated non-root user `appuser` (UID 10001) for least-privilege security.
    - Authored `docker-compose.yml` spinning up PostgreSQL 16 (`db`), `Zapper.AcmeStub`, and `Zapper.Payments.Api` with internal health checks.
    - Authored `.dockerignore`.
  - **Phase 6 (Integration Tests):**
    - Implemented `CustomWebApplicationFactory` using SQLite in-memory and custom `HttpMessageHandler` stubbing.
    - Authored 11 integration tests in `Zapper.Payments.IntegrationTests` verifying:
      1. Acquirer approved payment (`cust-001`, R19.99)
      2. Acquirer declined payment (`cust-002`, insufficient funds)
      3. Idempotent duplicate `AppPaymentId` submission
      4. Duplicate payment advice notification handling
      5. Out-of-order notification handling (reversal before delayed approval)
      6. Post-approval switch reversal via HMAC-SHA256 webhook
      7. Post-approval partial refund
      8. Post-approval full refund
      9. HMAC signature validation rejection on tampering
      10. Basic Auth payment advice rejection on invalid credentials
      11. Daily merchant settlement calculation
    - Ran `dotnet test` across entire solution: **24 passed, 0 failed, 0 warnings**.

---

### Prompt 9: Final Submission Documentation (Phase 7)
- **Timestamp:** `2026-10-22T19:59:56Z`
- **User Request:**
  ```
  proceed with phase 7
  ```
- **Context & Intent:**
  Draft the executive and engineering documentation required for submission: [README.md](file:///D:/source/projects/technical-assessment/README.md) and [SOLUTION.md](file:///D:/source/projects/technical-assessment/SOLUTION.md).
- **AI Agent Actions:**
  - Authored [README.md](file:///D:/source/projects/technical-assessment/README.md) covering:
    - Quick start instructions (`docker compose up` & `dotnet run`)
    - Architectural overview and directory layout
    - API endpoint catalog with sample curl requests
    - Testing instructions and configuration reference
  - Authored [SOLUTION.md](file:///D:/source/projects/technical-assessment/SOLUTION.md) covering:
    - Design trade-offs (DDD, dual persistence, mapping overhead)
    - Database schema design, indexing, and high-volume evolution strategy
    - In-depth handling of the 8 payment lifecycle scenarios and edge cases
    - Resilience and observability (Polly v8, Correlation IDs, health probes)
    - Production AWS cloud deployment architecture (ECS Fargate, Aurora, KMS, CloudWatch)
    - Verification and test summary

---

### Prompt 10: Local Smoke Testing Execution
- **Timestamp:** `2026-10-22T20:04:43Z`
- **User Request:**
  ```
  run the application locally and run a few smoke tests so we can verify that the application indeed works as expected,
  ```
- **Context & Intent:**
  Execute real-time smoke tests by running the actual applications locally and issuing HTTP requests across the core payment lifecycle.
- **AI Agent Actions:**
  - Started `Zapper.AcmeStub` on `http://localhost:5001`.
  - Started `Zapper.Payments.Api` on `http://localhost:5000` with SQLite persistence.
  - Executed 6 live HTTP smoke test scenarios via PowerShell:
    1. **Health Probes:** Tested `/health/live` and `/health/ready` -> returned `HTTP 200 Healthy`.
    2. **Approval Scenario:** Submitted payment for `cust-001` (R19.99) -> returned `HTTP 200 Approved` (`authCode: "390127"`).
    3. **Duplicate Submission:** Re-submitted same `appPaymentId` -> returned `HTTP 200` with `isDuplicateSubmission: true` without hitting the gateway.
    4. **Decline Scenario:** Submitted payment for `cust-002` -> returned `HTTP 200 Declined` (`responseCode: "51"`, "Insufficient funds").
    5. **Reversal Webhook:** Sent HMAC-SHA256 signed reversal from Acme Stub -> returned `HTTP 200` and transitioned state to `Reversed`.
    6. **Daily Settlement Summary:** Queried `/api/v1/settlement/daily-summary` -> verified approved, declined, and reversed transaction counts and amounts.
  - Terminated background runner processes cleanly.

---

### Prompt 11: Smoke Test Documentation
- **Timestamp:** `2026-10-22T20:11:09Z`
- **User Request:**
  ```
  document these test results
  ```
- **Context & Intent:**
  Record the live smoke test evidence, HTTP headers, payloads, status codes, and database verification into a formal markdown document in `ai/`.
- **AI Agent Actions:**
  - Created `ai/smoke-test-results.md` containing:
    - Execution environment details (.NET 8.0.400, Windows, Localhost)
    - Full HTTP requests, cURL commands, headers, and JSON responses for all 6 scenarios
    - Verification of database records in `Payments`, `PaymentHistory`, and `PaymentAuditLogs`
- **Key Artifacts Created:**
  - `ai/smoke-test-results.md`

---

### Prompt 12: Security & Performance Audit
- **Timestamp:** `2026-10-22T20:14:04Z`
- **User Request:**
  ```
  now invoke the security analyst skill and the performace tester skill. Then review the code that it adheres to security standards and that it is optimized correctly.
  ```
- **Context & Intent:**
  Perform a deep-dive security and performance review, remediate any identified vulnerabilities or performance bottlenecks, and verify compliance against enterprise standards.
- **Personas / Skills Invoked:**
  - `security-engineering`: Threat modeling (STRIDE), OWASP Top 10, PCI DSS scope, cryptographic timing attacks, input validation, and DoS resilience.
  - `performance-testing`: Heap allocation profiling, EF Core query projection optimization, connection pooling, and latency analysis.
- **AI Agent Actions:**
  - **Identified Security Vulnerabilities & Applied Fixes:**
    1. *Timing Attack in Basic Auth:* `BasicAuthValidator.cs` used ordinal string comparison for passwords. Replaced with `CryptographicOperations.FixedTimeEquals` to ensure constant-time comparison.
    2. *Denial of Service (DoS) Risk on Notification Endpoints:* Public webhook endpoints lacked payload size limits. Enforced a 64 KB body limit (`ContentLength > 65536` returns HTTP 413 Payload Too Large) in `NotificationEndpoints.cs`.
  - **Identified Performance Bottlenecks & Applied Fixes:**
    1. *Full Entity Graph Loading on Settlement Query:* `PaymentRepository.GetDailySettlementSummaryAsync` loaded entire `Payment` entity graphs. Replaced with database-level scalar column projections (`.Select(...)`), drastically reducing memory allocations and network traffic.
  - Validated all 24 unit and integration tests passing with 0 errors and 0 warnings.
- **Key Artifacts Created:**
  - `ai/security-and-performance-audit.md`

---

### Prompt 13: Prompt History Documentation (Current)
- **Timestamp:** `2026-10-22T20:19:42Z`
- **User Request:**
  ```
  now document all the prompts into an md file
  ```
- **Context & Intent:**
  Collate all user prompts, execution context, decisions, and outputs into a transparent, audit-ready markdown artifact in `ai/`.
- **AI Agent Actions:**
  - Extracted conversation timeline and prompt history from system session logs.
  - Compiled `ai/prompts-history.md` with complete metadata, persona tracking, technical decisions, and artifact mappings.
  - Updated [README.md](file:///D:/source/projects/technical-assessment/README.md) and [SOLUTION.md](file:///D:/source/projects/technical-assessment/SOLUTION.md) to cross-reference this document.
- **Key Artifacts Created:**
  - `ai/prompts-history.md`

---

## Engineering Observations & AI Collaboration Insights

1. **Iterative Refinement of Invariants:**
   The use of specific domain constraints (such as the 40-character FNB `CardAcceptorName` positional requirement and Polly v8's `SamplingDuration >= 2 * AttemptTimeout` rule) demonstrated that prompt-driven development must be paired with automated testing and static compiler enforcement (`TreatWarningsAsErrors`) to prevent subtle integration failures.

2. **Persona Segregation Enhances Rigor:**
   Transitioning between distinct specialist roles (`solution-architecture`, `CSharpExpert`, `security-engineering`, `performance-testing`) provided structured checks and balances that caught potential regressions (e.g., side-channel timing attacks and unprojected database queries) before production readiness.

3. **100% Traceability:**
   Every architectural decision, code change, and test execution is backed by committed artifacts in the `ai/` folder, ensuring full auditability and reproducibility.
