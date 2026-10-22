# Architecture & UML Specification: Zapper Payments Integration Service

**Document Version:** 1.1  
**Date:** 2026-10-22  
**Owner:** Team Lead Software Engineer  
**Methodology:** 4+1 View Model & UML 2.5 + C4 Standard (Mermaid Diagrams as Code)

---

## 1. Logical View: UML Class & Domain Model Diagram

```mermaid
classDiagram
    direction TB

    class Payment {
        +Guid Id
        +string AppPaymentId
        +string CustomerId
        +string SubMerchantId
        +string CardToken
        +Money Amount
        +string OrderNumber
        +string RetrievalReferenceNumber
        +string? TransactionId
        +PaymentStatus Status
        +string? ReconId
        +string? AuthCode
        +long? AuthAmount
        +long RefundedAmount
        +string? LastResponseCode
        +string? FailureReason
        +string? ClientMessage
        +DateTime CreatedAtUtc
        +DateTime UpdatedAtUtc
        +IReadOnlyCollection~PaymentHistoryEntry~ History
        +MarkSubmittedToGateway(string transactionId)
        +ApplyGatewayResponse(bool isSuccess, string responseCode, ...)
        +ApplyAdviceNotification(string responseCode, ...)
        +ApplyReversal(string switchUuid, long amount, ...)
        +ApplyRefund(string switchUuid, long amount, ...)
        +MarkTimedOut(string reason)
        +ResolveFromRttq(string txStatus, string respCode, ...)
    }

    class SubMerchant {
        +string SubMerchantId
        +string TradingName
        +string City
        +string Region
        +string Country
        +string MerchantCategoryCode
        +DateTime CreatedAtUtc
        +GetCardAcceptorName(string facilitator) CardAcceptorName
    }

    class CustomerCard {
        +Guid Id
        +string CustomerId
        +string AcmeToken
        +string Email
        +DateTime CreatedAtUtc
    }

    class CardAcceptorName {
        <<ValueObject>>
        +string FormattedValue
        +ForFnb(name, city, region, country, facilitator)$ CardAcceptorName
    }

    class Money {
        <<ValueObject>>
        +long AmountInMinorUnits
        +string Currency
        +FromZarCents(long cents)$ Money
        +ToDecimal() decimal
    }

    class PaymentHistoryEntry {
        +long Id
        +Guid PaymentId
        +PaymentStatus FromStatus
        +PaymentStatus ToStatus
        +string? Reason
        +string Source
        +DateTime TimestampUtc
    }

    class PaymentStatus {
        <<enumeration>>
        Initiated
        SubmittedToGateway
        Approved
        Declined
        TimedOut
        Reversed
        PartiallyRefunded
        Refunded
        Failed
    }

    Payment "1" *-- "1" Money : denominated in
    Payment "1" *-- "many" PaymentHistoryEntry : tracks
    Payment --> PaymentStatus : current state
    SubMerchant ..> CardAcceptorName : generates
```

---

## 2. Process & Lifecycle View: UML State Machine Diagram

This diagram governs the `Payment` Aggregate Root lifecycle, formalizing all allowed state transitions, triggers, and guard invariants:

```mermaid
stateDiagram-v2
    [*] --> Initiated : Customer initiates QR payment (AppPaymentId)

    Initiated --> SubmittedToGateway : SubmitPaymentAsync / Generate TransactionID

    SubmittedToGateway --> Approved : Acme Sync 200 OK (Code: 00)
    SubmittedToGateway --> Declined : Acme Sync 200 OK (Code != 00)
    SubmittedToGateway --> TimedOut : Gateway Timeout (>30s)

    %% Timeout Resolution via RTTQ
    TimedOut --> Approved : RTTQ query returns Code 00
    TimedOut --> Declined : RTTQ query returns Code != 00
    TimedOut --> Approved : Payment Advice Notification (Code: 00)
    TimedOut --> Declined : Payment Advice Notification (Code != 00)

    %% Asynchronous Post-Approval Transitions
    Approved --> Reversed : Switch Webhook (Reversal, txResultCode 00)
    Approved --> PartiallyRefunded : Switch Webhook (Refund < AuthAmount)
    Approved --> Refunded : Switch Webhook (Refund == AuthAmount)

    PartiallyRefunded --> Refunded : Cumulative Refunds == AuthAmount
    PartiallyRefunded --> PartiallyRefunded : Additional partial refund

    %% Invariant Guards & Idempotency
    Approved --> Approved : Duplicate Advice / Duplicate Submit [Idempotent No-op]
    Declined --> Declined : Duplicate Advice / Duplicate Submit [Idempotent No-op]
    Reversed --> Reversed : Duplicate Switch Webhook UUID [Idempotent No-op]
    Refunded --> Refunded : Duplicate Switch Webhook UUID [Idempotent No-op]

    Approved --> [*] : Settlement finalized
    Declined --> [*] : Terminal
    Reversed --> [*] : Terminal
    Refunded --> [*] : Terminal
```

---

## 3. Development View: UML Package & Layering Diagram

```mermaid
flowchart TD
    subgraph Presentation["Presentation: Zapper.Payments.Api"]
        PayEndpoint["PaymentsEndpoints (/api/v1/payments)"]
        NotifEndpoint["NotificationEndpoints (/payment-advice, /transaction-status)"]
        SettlementEndpoint["SettlementEndpoints (/settlement/daily-summary)"]
        Middleware["RFC 9457 ProblemDetails & Correlation Middleware"]
    end

    subgraph Application["Application: Zapper.Payments.Application"]
        Svc["PaymentService (CQS Orchestrator)"]
        DTO["Application DTOs (Requests, Results, Summaries)"]
        Ports["Interfaces: IAcmePayClient, IPaymentRepository, IAuditRepository, IUnitOfWork"]
    end

    subgraph Domain["Domain: Zapper.Payments.Domain (Pure - Zero External Dependencies)"]
        Agg["Payment (Aggregate Root)"]
        VO["Value Objects: CardAcceptorName, Money"]
        Entities["Entities: SubMerchant, CustomerCard, History, Audit"]
        DomainEvents["Domain Exceptions & Status Enum"]
    end

    subgraph Infrastructure["Infrastructure: Zapper.Payments.Infrastructure"]
        DbContext["AppDbContext (EF Core Dual Provider: PostgreSQL / SQLite)"]
        Repos["Repositories: PaymentRepository, SubMerchantRepository, CustomerCardRepository"]
        HttpAdapter["AcmePayClient (Polly v8 Resilience: Timeout, Retry, Circuit Breaker)"]
        SecurityAdapters["HmacSha256Validator + BasicAuthValidator"]
    end

    Presentation -->|"Invokes use cases & models"| Application
    Application -->|"Coordinates domain models & aggregates"| Domain
    Infrastructure -->|"Implements application interfaces (Dependency Inversion)"| Application
    Infrastructure -->|"Maps domain entities to persistence"| Domain
```

---

## 4. Scenario View: UML Dynamic Sequence Diagram (+1 Scenarios)

### Scenario A: Payment Submission with Acme Timeout & Immediate RTTQ Fallback

```mermaid
sequenceDiagram
    autonumber
    actor App as Zapper Mobile App
    participant API as Payments.Api
    participant Svc as PaymentService
    participant Agg as Payment (Aggregate)
    participant DB as SQL Database
    participant Acme as Acme Pay Gateway

    App->>API: POST /api/v1/payments (AppPaymentId, CustId, MerchantId, Amount)
    API->>Svc: SubmitPaymentAsync(req)
    
    %% Idempotency check
    Svc->>DB: Query by AppPaymentId
    DB-->>Svc: null (New transaction)
    
    Svc->>Agg: new Payment(...)
    Svc->>DB: Insert (Status: Initiated)
    Svc->>Agg: MarkSubmittedToGateway(txId)
    Svc->>DB: Update (Status: SubmittedToGateway)
    
    %% Outbound call times out
    Svc->>Acme: POST /api/payment
    Note over Svc,Acme: Latency > 30s (HTTP Timeout)
    Acme--xSvc: TimeoutException (No response)

    Svc->>Agg: MarkTimedOut("Acme Pay gateway timeout")
    Svc->>DB: Update (Status: TimedOut)

    %% RTTQ Fallback per Appendix A
    Note over Svc,Acme: Rule: Do NOT resubmit with new TransactionID. Query RTTQ.
    Svc->>Acme: GET /apirttq/query/Merchant/{MerchantID}?TransactionID={txId}
    Acme-->>Svc: 200 OK (Status: Success, ResponseCode: 00, AuthCode: 238699)
    
    Svc->>Agg: ResolveFromRttq("Success", "00", ReconID, AuthCode, Amount)
    Note over Agg: Invariant: Transitions TimedOut -> Approved
    Svc->>DB: Update (Status: Approved)
    
    Svc-->>API: SubmitPaymentResult (Status: Approved, Resolved via RTTQ)
    API-->>App: 200 OK (Payment Approved)
```

### Scenario B: Asynchronous Lifecycle Callbacks (Advice & Reversal/Refund Webhook)

```mermaid
sequenceDiagram
    autonumber
    participant AcmeSwitch as Acme Switch / Gateway
    participant API as Payments.Api
    participant Svc as PaymentService
    participant Agg as Payment (Aggregate)
    participant DB as SQL Database

    %% 1. Payment Advice Delivery
    Note over AcmeSwitch,API: Payment Advice Notification (Idempotency & Retry Safe)
    AcmeSwitch->>API: POST /api/v1/notifications/payment-advice (Basic Auth: ACME_ADVICE_*)
    API->>Svc: HandleAdviceNotificationAsync(advice)
    Svc->>DB: Query Payment by RRN
    DB-->>Svc: Payment (Status: Approved)
    Svc->>Agg: ApplyAdviceNotification("00", ...)
    Note over Agg: Already Approved: Idempotent no-op (logs receipt)
    Svc->>DB: Log Notification Audit
    API-->>AcmeSwitch: 200 OK

    %% 2. Switch Webhook Reversal / Refund
    Note over AcmeSwitch,API: Transaction Status Webhook (Reversal or Partial Refund)
    AcmeSwitch->>API: POST /api/v1/notifications/transaction-status (Header: x-signature HMAC-SHA256)
    Note over API: Verifies HMAC-SHA256 against ACME_WEBHOOK_SECRET
    API->>Svc: HandleSwitchWebhookAsync(payload)
    Svc->>DB: Query Payment by RRN
    DB-->>Svc: Payment (Status: Approved, AuthAmount: 1999)

    alt Reversal Event
        Svc->>Agg: ApplyReversal(transactionUUID, 1999)
        Note over Agg: Status: Approved -> Reversed
    else Refund Event
        Svc->>Agg: ApplyRefund(transactionUUID, 500)
        Note over Agg: Status: Approved -> PartiallyRefunded (Refunded: 500/1999)
    end
    
    Svc->>DB: Save Changes & Audit Log
    API-->>AcmeSwitch: 200 OK
```

---

## 5. Architectural Quality Attributes & Decisions Matrix

| UML Viewpoint | Quality Attribute | Architectural Mechanism | Verification / Invariant |
|---|---|---|---|
| **Class Model** | Domain Integrity | Pure Domain Aggregate + Value Objects (`Money`, `CardAcceptorName`) | Unit tested in milliseconds without database or HTTP mocks |
| **State Machine** | Consistency & Fault Tolerance | Finite State Machine with Terminal State Locks | Prevents out-of-order Advice from regressing `Reversed` or `Refunded` payments |
| **Sequence (+1)** | High Availability & Resilience | Polly v8 standard resilience + RTTQ fallback query | Outbound calls do not block threads; timeouts automatically query transaction state |
| **Package** | Maintainability & Testability | Clean Architecture / Onion Layering (DIP) | Presentation and Infrastructure depend inwards; Domain depends on nothing |
| **Security** | Integrity & Authenticity | Basic Auth (Advice) + HMAC-SHA256 (Switch Webhook) | Rejects forged callbacks before business logic is touched |
