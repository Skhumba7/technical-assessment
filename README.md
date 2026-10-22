# Zapper Payments Integration Service

A production-ready **Payments Integration Service** built with **C# / .NET 8**, **Domain-Driven Design (DDD)**, and **SQL persistence**, designed for deployment to **AWS**.

The service operates as a **Payment Facilitator** (under acquiring bank **FNB**), enabling customers to pay sub-merchants by scanning QR codes and charging tokenized cards via the **Acme Pay** payment gateway.

---

## Architecture & System Overview

- **`src/Zapper.Payments.Domain`**: Pure domain aggregate (`Payment` with complete finite state machine), entities (`SubMerchant`, `CustomerCard`), and value objects (`CardAcceptorName` enforcing FNB's 40-character positional specification, `Money`). Zero external dependencies.
- **`src/Zapper.Payments.Application`**: Use cases, CQS commands & queries, DTOs, and gateway interfaces (`IAcmePayClient`).
- **`src/Zapper.Payments.Infrastructure`**: EF Core persistence (dual-provider: PostgreSQL for Docker/AWS and SQLite for zero-dependency local runs), Polly v8 resilience (`Microsoft.Extensions.Http.Resilience`), HMAC-SHA256 signature verification, and HTTP Basic Auth.
- **`src/Zapper.Payments.Api`**: ASP.NET Core 8 Web API, RFC 9457 `ProblemDetails` middleware, correlation ID tracing, health checks (`/health/live`, `/health/ready`), and OpenAPI/Swagger documentation.
- **`src/Zapper.AcmeStub`**: Standalone mock payment gateway simulating Acme Pay API v2 (Appendix A), including latency, timeouts, RTTQ query, and webhook/advice simulation dispatchers.
- **`tests/`**: Comprehensive Unit & Integration test suites covering all required assignment lifecycle scenarios.
- **`ai/`**: AI workflow artifacts, architecture specifications, ADRs, and code review logs.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for containerized runs)

---

## Running Locally

You can run the solution either via **Docker Compose** (with PostgreSQL) or directly via the **.NET CLI** (with zero setup using SQLite).

### Option A: Run via Docker Compose (Recommended)

Starts the **PostgreSQL database**, the **Acme Pay Stub**, and the **Zapper Payments API**:

```bash
docker compose up --build
```

Services will be accessible at:
- **Zapper Payments API**: `http://localhost:5000` (Swagger UI: [http://localhost:5000/swagger](http://localhost:5000/swagger))
- **Acme Pay Stub**: `http://localhost:5001` (Swagger UI: [http://localhost:5001/swagger](http://localhost:5001/swagger))
- **PostgreSQL Database**: `localhost:5432` (`zapper_payments` / `zapper` / `zapper_password_123`)

---

### Option B: Run via .NET CLI (Zero Dependencies / Instant Run)

If you don't have Docker running, you can run both services natively. The API automatically falls back to an embedded SQLite database and seeds test data:

1. **Start the Acme Pay Mock Stub** (in Terminal 1):
   ```bash
   dotnet run --project src/Zapper.AcmeStub --urls "http://localhost:5001"
   ```

2. **Start the Zapper Payments API** (in Terminal 2):
   ```bash
   dotnet run --project src/Zapper.Payments.Api --urls "http://localhost:5000"
   ```

---

## Seed Data for Testing

On startup, the system automatically seeds:
- **Sub-Merchants**:
  - `ZAP0004182` — *CAFE DEL SOL* (Cape Town, WC, ZA, MCC: 5812)
  - `ZAP0004183` — *THE GRILL HOUSE* (Johannesburg, GP, ZA, MCC: 5812)
  - `ZAP0004184` — *DURBAN BEACH DINER* (Durban, KZN, ZA, MCC: 5812)
- **Customer Stored Cards**:
  - `cust-001` — `alice@example.com` (Token: `4f2a8c9e1b7d3560` — Approves)
  - `cust-002` — `bob@example.com` (Token: `tok_decline_funds` — Declines)
  - `cust-003` — `charlie@example.com` (Token: `tok_timeout_test` — Triggers Timeout/RTTQ)

---

## Running the Automated Tests

Execute the 24 automated unit and integration tests across the solution:

```bash
dotnet test
```

Tests cover all 8 mandatory edge-case scenarios:
1. Payment approved by acquirer.
2. Payment declined by acquirer.
3. Idempotent duplicate submission (same `AppPaymentId`).
4. Idempotent duplicate notification delivery.
5. Out-of-order notification arrival (terminal state protection).
6. Post-approval reversal via Acme Switch.
7. Post-approval refund & partial refund via Acme Switch.
8. Acme timeout handling with immediate Real Time Transaction Query (RTTQ) resolution.
9. Daily merchant settlement summary reporting.

---

## Quick API Walkthrough

### 1. Submit a Payment

```bash
curl -X POST "http://localhost:5000/api/v1/payments" \
     -H "Content-Type: application/json" \
     -d '{
       "appPaymentId": "app-pay-demo-001",
       "customerId": "cust-001",
       "subMerchantId": "ZAP0004182",
       "amountInMinorUnits": 1999,
       "currency": "ZAR"
     }'
```

### 2. Query Payment Status

```bash
curl -X GET "http://localhost:5000/api/v1/payments/by-app-id/app-pay-demo-001"
```

### 3. Query Daily Settlement Summary

```bash
curl -X GET "http://localhost:5000/api/v1/settlement/daily-summary?date=2026-10-22"
```

---

## Documentation & AI Workflow

Detailed architectural decisions, AWS deployment designs, trade-offs, and Task 4 reflections are provided in:
- **[SOLUTION.md](file:///D:/source/projects/technical-assessment/SOLUTION.md)** (Full architectural submission report)
- **[ai/prompts-history.md](file:///D:/source/projects/technical-assessment/ai/prompts-history.md)** (Complete chronological user prompts & iterative refinement history)
- **[ai/high-level-architecture.md](file:///D:/source/projects/technical-assessment/ai/high-level-architecture.md)** (UML 2.5 + C4 models)
- **[ai/task-breakdown.md](file:///D:/source/projects/technical-assessment/ai/task-breakdown.md)** (Phase-by-phase execution tracking)
- **[ai/smoke-test-results.md](file:///D:/source/projects/technical-assessment/ai/smoke-test-results.md)** (Live localhost smoke test verification and HTTP traces)
- **[ai/security-and-performance-audit.md](file:///D:/source/projects/technical-assessment/ai/security-and-performance-audit.md)** (Security vulnerability remediation and query optimization report)
- **[ai/csharp-expert-review.md](file:///D:/source/projects/technical-assessment/ai/csharp-expert-review.md)** (C# Expert code review audit)

