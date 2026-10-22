# Local Smoke Test & Verification Report

**Date of Execution:** 2026-10-22  
**Environment:** Localhost (`Zapper.Payments.Api` on `:5000`, `Zapper.AcmeStub` on `:5001`)  
**Database:** SQLite (`zapper_payments.db`) auto-seeded on startup  
**Execution Target:** Verification of live HTTP endpoints, state machine transitions, HMAC verification, and settlement aggregation.

---

## 1. Test Summary

| # | Test Scenario | Target Endpoint | HTTP Method | Expected Outcome | Actual Outcome | Status |
|---|---|---|---|---|---|---|
| **1** | Liveness Probe | `/health/live` | `GET` | 200 OK (`Healthy`) | 200 OK (`Healthy`) | **PASSED** |
| **2** | Readiness Probe | `/health/ready` | `GET` | 200 OK (`Healthy`) | 200 OK (`Healthy`) | **PASSED** |
| **3** | Payment Submission (Approval) | `/api/v1/payments` | `POST` | 200 OK, Status: `Approved` (3), AuthCode populated | 200 OK, Status: `Approved` (3), AuthCode: `390127` | **PASSED** |
| **4** | Idempotency (Duplicate Submit) | `/api/v1/payments` | `POST` | 200 OK, `isDuplicateSubmission: true` | 200 OK, `isDuplicateSubmission: true`, same PaymentId | **PASSED** |
| **5** | Payment Inquiry & History | `/api/v1/payments/by-app-id/{id}` | `GET` | Complete transition history | Full history: Initiated -> Submitted -> Approved | **PASSED** |
| **6** | Payment Submission (Decline) | `/api/v1/payments` | `POST` | 200 OK, Status: `Declined` (4), Code: `51` | 200 OK, Status: `Declined` (4), Code: `51` | **PASSED** |
| **7** | Switch Reversal Webhook | `/api/v1/notifications/transaction-status` | `POST` | Valid HMAC -> Status: `Reversed` (6) | 200 OK, Status updated: `Approved` -> `Reversed` | **PASSED** |
| **8** | Daily Settlement Summary | `/api/v1/settlement/daily-summary` | `GET` | Grouped count & total values | Aggregated: 1 Declined (R50), 1 Reversal (R19.99) | **PASSED** |

---

## 2. Detailed Request & Response Logs

### 2.1 Health Probes
```http
GET http://localhost:5000/health/live HTTP/1.1
HTTP/1.1 200 OK
Content-Type: text/plain

Healthy
```

```http
GET http://localhost:5000/health/ready HTTP/1.1
HTTP/1.1 200 OK
Content-Type: text/plain

Healthy
```

---

### 2.2 Payment Initiation & Acquirer Approval
**Request:**
```http
POST http://localhost:5000/api/v1/payments HTTP/1.1
Content-Type: application/json

{
  "appPaymentId": "smoke-test-app-001",
  "customerId": "cust-001",
  "subMerchantId": "ZAP0004182",
  "amountInMinorUnits": 1999,
  "currency": "ZAR"
}
```

**Response:**
```json
{
  "paymentId": "eac375e2-4e9c-42c4-b650-009247daca43",
  "appPaymentId": "smoke-test-app-001",
  "orderNumber": "MRC7C3F40988",
  "retrievalReferenceNumber": "MRC7C3F40988",
  "status": 3,
  "responseCode": "00",
  "authCode": "390127",
  "authAmount": 1999,
  "clientMessage": "Approved.",
  "isDuplicateSubmission": false
}
```

---

### 2.3 Duplicate Payment Resubmission (Idempotency)
**Action:** Re-posted exact same payload with `appPaymentId: "smoke-test-app-001"`.

**Response:**
```json
{
  "paymentId": "eac375e2-4e9c-42c4-b650-009247daca43",
  "appPaymentId": "smoke-test-app-001",
  "orderNumber": "MRC7C3F40988",
  "retrievalReferenceNumber": "MRC7C3F40988",
  "status": 3,
  "responseCode": "00",
  "authCode": "390127",
  "authAmount": 1999,
  "clientMessage": "Approved.",
  "isDuplicateSubmission": true
}
```
*Note: Gateway was not called again; cached aggregate state was replayed safely.*

---

### 2.4 Payment State & Transition History Query
**Request:**
```http
GET http://localhost:5000/api/v1/payments/by-app-id/smoke-test-app-001 HTTP/1.1
```

**Response:**
```json
{
  "paymentId": "eac375e2-4e9c-42c4-b650-009247daca43",
  "appPaymentId": "smoke-test-app-001",
  "customerId": "cust-001",
  "subMerchantId": "ZAP0004182",
  "amount": 1999,
  "currency": "ZAR",
  "orderNumber": "MRC7C3F40988",
  "retrievalReferenceNumber": "MRC7C3F40988",
  "transactionId": "13f9d8e2-dbcd-4263-a237-8819bed3a97e",
  "status": 3,
  "authCode": "390127",
  "authAmount": 1999,
  "refundedAmount": 0,
  "lastResponseCode": "00",
  "failureReason": "Approved or completed successfully",
  "clientMessage": "Approved.",
  "createdAtUtc": "2026-10-22T20:06:13.4577098",
  "updatedAtUtc": "2026-10-22T20:06:14.9351214",
  "history": [
    {
      "fromStatus": 1,
      "toStatus": 1,
      "reason": "Payment initiated",
      "source": "Client",
      "timestampUtc": "2026-10-22T20:06:13.4582897"
    },
    {
      "fromStatus": 1,
      "toStatus": 2,
      "reason": "Submitted to Acme Pay with TransactionID 13f9d8e2-dbcd-4263-a237-8819bed3a97e",
      "source": "System",
      "timestampUtc": "2026-10-22T20:06:13.6302758"
    },
    {
      "fromStatus": 2,
      "toStatus": 3,
      "reason": "Approved or completed successfully",
      "source": "AcmeApi",
      "timestampUtc": "2026-10-22T20:06:14.9351235"
    }
  ]
}
```

---

### 2.5 Acquirer Decline Flow
**Request:**
```http
POST http://localhost:5000/api/v1/payments HTTP/1.1
Content-Type: application/json

{
  "appPaymentId": "smoke-test-decline-002",
  "customerId": "cust-002",
  "subMerchantId": "ZAP0004182",
  "amountInMinorUnits": 5000,
  "currency": "ZAR"
}
```

**Response:**
```json
{
  "paymentId": "426749df-b9b1-4269-a654-bd781a09c223",
  "appPaymentId": "smoke-test-decline-002",
  "orderNumber": "MRA926943E9F",
  "retrievalReferenceNumber": "MRA926943E9F",
  "status": 4,
  "responseCode": "51",
  "authCode": null,
  "authAmount": null,
  "clientMessage": "There was not enough money in the account.",
  "isDuplicateSubmission": false
}
```

---

### 2.6 Asynchronous Reversal Webhook via Acme Stub
**Action:** Dispatched from Acme Stub to Zapper API:
- Endpoint: `POST http://localhost:5000/api/v1/notifications/transaction-status`
- Header: `x-signature: rSMRn/w9WZUzocuvaTddKX/3dzD+3Oc7MdlLgGzQEB8=`
- Payload:
  ```json
  {
    "transactionUUID": "fa84da72-5dd4-482b-8592-94d254f5933c",
    "transactionType": "Reversal",
    "transactionDateTime": "2026-10-22T22:06:54.058",
    "rrn": "MRC7C3F40988",
    "transactionAmount": 1999,
    "currencyCode": "ZAR",
    "txResultCode": "00",
    "isApproved": true
  }
  ```

**Outcome:**
Zapper API returned HTTP 200. Re-querying payment `smoke-test-app-001` confirmed state transition:
- Status: `6` (`Reversed`)
- New History Entry: `Reversed via Switch webhook fa84da72-5dd4-482b-8592-94d254f5933c for amount 1999` (Source: `SwitchWebhook`).

---

### 2.7 Daily Settlement Summary Reporting
**Request:**
```http
GET http://localhost:5000/api/v1/settlement/daily-summary HTTP/1.1
```

**Response:**
```json
{
  "date": "2026-10-22",
  "subMerchants": [
    {
      "subMerchantId": "ZAP0004182",
      "tradingName": "CAFE DEL SOL",
      "currency": "ZAR",
      "approvedCount": 0,
      "approvedTotalValue": 0,
      "declinedCount": 1,
      "declinedTotalValue": 5000,
      "reversalCount": 1,
      "reversalTotalValue": 1999,
      "refundCount": 0,
      "refundTotalValue": 0
    }
  ]
}
```

---

## 3. Automated Test Suite Execution

In addition to live HTTP smoke testing, the full automated test suite was executed:
- **Unit Tests:** 13 passed in 114 ms (`Zapper.Payments.Domain.UnitTests`)
- **Integration Tests:** 11 passed in 3.1 s (`Zapper.Payments.IntegrationTests`)
- **Total:** 24 passed, 0 failed, 0 warnings (under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
