# C# Expert Code Review Report

**Agent:** C# Expert (.NET 8 / C# 12)  
**Date:** 2026-10-22  
**Target Solution:** `Zapper.Payments.sln`  
**Review Standard:** `ai/CSharpExpert.agent.md` & Microsoft .NET Engineering Guidelines

---

## 1. Executive Summary
The solution was reviewed against the modern C#/.NET engineering checklist:
- **Clean Architecture & Domain Isolation:** The Domain layer maintains 0 external package dependencies. Financial invariants (`Money`), FNB payment facilitator formatting (`CardAcceptorName`), and lifecycle states (`Payment`) are encapsulated cleanly.
- **Asynchronous & Concurrency Discipline:** All async methods adhere to the `Async` suffix convention, propagate `CancellationToken`, avoid blocking calls (`.Result`/`.Wait()`), and utilize `ConfigureAwait(false)` in class libraries.
- **Resilience & I/O:** Outbound HTTP communication is guarded via Polly v8 resilience pipelines with exponential backoff and circuit breaking. Timeouts trigger immediate RTTQ queries rather than unsafe retries.
- **Cryptographic Rigour:** Constant-time comparison (`CryptographicOperations.FixedTimeEquals`) is used for webhook verification, preventing timing attacks.
- **Compiler Strictness:** `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` are enforced across all projects via `Directory.Build.props`.

---

## 2. Detailed Findings & Quality Attributes

| Category | Assessment | Status | Notes |
|---|---|---|---|
| **Architecture** | Domain-Driven Design | PASSED | Pure Domain aggregate and value objects; Application layer mediates CQS; Infrastructure implements ports via Dependency Inversion. |
| **Language Features** | Modern C# 12 / .NET 8 | PASSED | File-scoped namespaces, record DTOs/value objects, collection expressions, pattern matching, RFC 9457 `ProblemDetails`. |
| **Resilience & Outbound I/O** | Polly v8 & Fallbacks | PASSED | Timeouts trigger Appendix A Real Time Transaction Query (`apirttq`) fallback without issuing new TransactionIDs. |
| **Security & Auth** | Dual Auth Models | PASSED | HTTP Basic Auth for Payment Advice; HMAC-SHA256 Base64 digest verification for Acme Switch webhooks. |
| **Persistence** | Dual-Provider EF Core | PASSED | Supports PostgreSQL for production containers and SQLite for instantaneous zero-friction local running and testing. |
| **Null Safety** | Nullable Reference Types | PASSED | Guarded via `ArgumentNullException.ThrowIfNull` and strict compiler warnings-as-errors. |

---

## 3. Improvements Applied During Review

1. **Global Build Discipline (`Directory.Build.props`)**:
   - Added root `Directory.Build.props` enforcing `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<Nullable>enable</Nullable>`, and `<ImplicitUsings>enable</ImplicitUsings>` across all 7 projects.
2. **Cancellation Token Propagation in Delays**:
   - Updated `Zapper.AcmeStub/Program.cs` simulated timeout to accept and pass `context.RequestAborted` to `Task.Delay(delayMs, context.RequestAborted)` to prevent hanging threadpool tasks on disconnected clients.
3. **Library Context Discipline (`ConfigureAwait(false)`)**:
   - Added `.ConfigureAwait(false)` across library awaits in `Zapper.Payments.Infrastructure.Acme.AcmePayClient`.
4. **Modern Guard Clauses**:
   - Replaced legacy `?? throw new ArgumentNullException()` with C# 11+ `ArgumentNullException.ThrowIfNull(amount)` in `Payment.cs`.
