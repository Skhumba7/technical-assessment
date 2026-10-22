using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Domain.Enums;

namespace Zapper.Payments.IntegrationTests;

public class PaymentScenariosIntegrationTests
{
    private const string WebhookSecret = "k7Qf2mZr9XpL4vNcA8sTgH3wYbE6dJ5u";
    private const string AdviceUser = "acme_advice_user";
    private const string AdvicePass = "acme_advice_password_123";

    private (CustomWebApplicationFactory factory, HttpClient client, TestAcmePayClient stub) CreateTestSetup()
    {
        var stub = new TestAcmePayClient();
        var factory = new CustomWebApplicationFactory
        {
            MockAcmeClient = stub
        };
        var client = factory.CreateClient();
        return (factory, client, stub);
    }

    // -------------------------------------------------------------
    // Scenario 1: A payment approved by the acquirer
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario1_PaymentApproved_ByAcquirer()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            var request = new SubmitPaymentRequest(
                AppPaymentId: $"app-approved-{Guid.NewGuid()}",
                CustomerId: "cust-001",
                SubMerchantId: "ZAP0004182",
                AmountInMinorUnits: 1999,
                Currency: "ZAR"
            );

            var response = await client.PostAsJsonAsync("/api/v1/payments", request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<SubmitPaymentResult>();
            result.Should().NotBeNull();
            result!.Status.Should().Be(PaymentStatus.Approved);
            result.AuthCode.Should().Be("238699");
            result.AuthAmount.Should().Be(1999);
            result.IsDuplicateSubmission.Should().BeFalse();
        }
    }

    // -------------------------------------------------------------
    // Scenario 2: A payment declined by the acquirer
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario2_PaymentDeclined_ByAcquirer()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            stub.DesiredTransactionStatus = "Failure";
            stub.DesiredResponseCode = "51";
            stub.DesiredDescription = "Insufficient funds";
            stub.DesiredClientMessage = "There was not enough money in the account.";

            var request = new SubmitPaymentRequest(
                AppPaymentId: $"app-declined-{Guid.NewGuid()}",
                CustomerId: "cust-001",
                SubMerchantId: "ZAP0004182",
                AmountInMinorUnits: 5000,
                Currency: "ZAR"
            );

            var response = await client.PostAsJsonAsync("/api/v1/payments", request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<SubmitPaymentResult>();
            result.Should().NotBeNull();
            result!.Status.Should().Be(PaymentStatus.Declined);
            result.ResponseCode.Should().Be("51");
            result.ClientMessage.Should().Be("There was not enough money in the account.");
        }
    }

    // -------------------------------------------------------------
    // Scenario 3: The same payment submitted twice using the same app payment identifier
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario3_SamePaymentSubmittedTwice_IsIdempotent()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            var appPaymentId = $"app-idempotent-{Guid.NewGuid()}";
            var request = new SubmitPaymentRequest(
                AppPaymentId: appPaymentId,
                CustomerId: "cust-001",
                SubMerchantId: "ZAP0004182",
                AmountInMinorUnits: 2500,
                Currency: "ZAR"
            );

            // First submission
            var response1 = await client.PostAsJsonAsync("/api/v1/payments", request);
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            var result1 = await response1.Content.ReadFromJsonAsync<SubmitPaymentResult>();
            result1!.IsDuplicateSubmission.Should().BeFalse();

            // Duplicate submission with same AppPaymentId
            var response2 = await client.PostAsJsonAsync("/api/v1/payments", request);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
            var result2 = await response2.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            // Assertions
            result2.Should().NotBeNull();
            result2!.IsDuplicateSubmission.Should().BeTrue();
            result2.PaymentId.Should().Be(result1.PaymentId);
            result2.Status.Should().Be(result1.Status);
            result2.OrderNumber.Should().Be(result1.OrderNumber);
            stub.SubmitCallsCount.Should().Be(1); // Gateway was called ONLY ONCE
        }
    }

    // -------------------------------------------------------------
    // Scenario 4: Notifications delivered more than once
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario4_NotificationsDeliveredMoreThanOnce_IsIdempotent()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            // Create payment
            var req = new SubmitPaymentRequest($"app-dup-advice-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 1500, "ZAR");
            var res = await client.PostAsJsonAsync("/api/v1/payments", req);
            var payment = await res.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            var advicePayload = new PaymentAdvicePayload(
                RequestType: "Payment",
                MerchantID: "8B67BF88-BB8D-4EB5-ADFC-0F5C97C7ED67",
                TransactionID: Guid.NewGuid().ToString("D"),
                TransactionDateTime: DateTime.UtcNow.ToString("O"),
                OrderNumber: payment!.OrderNumber,
                ReconID: "MR999888777",
                RetrievalReferenceNumber: payment.RetrievalReferenceNumber,
                SystemsTraceAuditNumber: "1234",
                Amount: 1500,
                CurrencyCode: "ZAR",
                PaymentService: "CardNotPresent",
                TransactionStatus: "Success",
                ResponseSource: "Acquirer",
                ResponseCode: "00",
                ResponseDescription: "Approved",
                AuthCode: "238699",
                MaskedCardNumber: "541333******1429",
                CommProtocol: "REST"
            );

            // Send notification 1
            var requestMsg1 = CreateAdviceHttpRequest(advicePayload);
            var adviceRes1 = await client.SendAsync(requestMsg1);
            adviceRes1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Send identical notification 2 (retry by Acme)
            var requestMsg2 = CreateAdviceHttpRequest(advicePayload);
            var adviceRes2 = await client.SendAsync(requestMsg2);
            adviceRes2.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify payment state is not corrupted
            var checkRes = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            checkRes!.Status.Should().Be(PaymentStatus.Approved);
        }
    }

    // -------------------------------------------------------------
    // Scenario 5: Notifications arriving out of order
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario5_NotificationsArrivingOutOfOrder_DoesNotCorruptState()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            // Create approved payment
            var req = new SubmitPaymentRequest($"app-ooo-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 3000, "ZAR");
            var res = await client.PostAsJsonAsync("/api/v1/payments", req);
            var payment = await res.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            // 1. Reversal webhook arrives first (e.g. applied at switch)
            var reversalPayload = new SwitchWebhookPayload(
                transactionUUID: Guid.NewGuid().ToString("D"),
                transactionType: "Reversal",
                transactionDateTime: DateTime.UtcNow.ToString("O"),
                rrn: payment!.RetrievalReferenceNumber,
                transactionAmount: 3000,
                currencyCode: "ZAR",
                txResultCode: "00",
                isApproved: true
            );
            var switchReq = CreateSwitchHttpRequest(reversalPayload);
            var switchRes = await client.SendAsync(switchReq);
            switchRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify status transitioned to Reversed
            var checkAfterRev = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            checkAfterRev!.Status.Should().Be(PaymentStatus.Reversed);

            // 2. Delayed payment advice notification arrives out of order afterwards
            var delayedAdvice = new PaymentAdvicePayload(
                RequestType: "Payment",
                MerchantID: "8B67BF88-BB8D-4EB5-ADFC-0F5C97C7ED67",
                TransactionID: Guid.NewGuid().ToString("D"),
                TransactionDateTime: DateTime.UtcNow.ToString("O"),
                OrderNumber: payment.OrderNumber,
                ReconID: "MR999888777",
                RetrievalReferenceNumber: payment.RetrievalReferenceNumber,
                SystemsTraceAuditNumber: "1234",
                Amount: 3000,
                CurrencyCode: "ZAR",
                PaymentService: "CardNotPresent",
                TransactionStatus: "Success",
                ResponseSource: "Acquirer",
                ResponseCode: "00",
                ResponseDescription: "Approved",
                AuthCode: "238699",
                MaskedCardNumber: "541333******1429",
                CommProtocol: "REST"
            );
            var adviceReq = CreateAdviceHttpRequest(delayedAdvice);
            var adviceRes = await client.SendAsync(adviceReq);
            adviceRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Terminal state protection: status MUST REMAIN Reversed, not regress to Approved!
            var finalCheck = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            finalCheck!.Status.Should().Be(PaymentStatus.Reversed);
        }
    }

    // -------------------------------------------------------------
    // Scenario 6: A payment that is reversed after being approved
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario6_PaymentReversed_AfterBeingApproved()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            var req = new SubmitPaymentRequest($"app-rev-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 1999, "ZAR");
            var res = await client.PostAsJsonAsync("/api/v1/payments", req);
            var payment = await res.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            var reversalPayload = new SwitchWebhookPayload(
                transactionUUID: Guid.NewGuid().ToString("D"),
                transactionType: "Reversal",
                transactionDateTime: DateTime.UtcNow.ToString("O"),
                rrn: payment!.RetrievalReferenceNumber,
                transactionAmount: 1999,
                currencyCode: "ZAR",
                txResultCode: "00",
                isApproved: true
            );

            var switchReq = CreateSwitchHttpRequest(reversalPayload);
            var switchRes = await client.SendAsync(switchReq);
            switchRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var updated = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            updated!.Status.Should().Be(PaymentStatus.Reversed);
        }
    }

    // -------------------------------------------------------------
    // Scenario 7: A payment that is refunded after being approved
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario7_PaymentRefunded_PartialAndFullRefund()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            var req = new SubmitPaymentRequest($"app-ref-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 2000, "ZAR"); // R20.00
            var res = await client.PostAsJsonAsync("/api/v1/payments", req);
            var payment = await res.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            // Partial refund of R5.00
            var refund1 = new SwitchWebhookPayload(
                transactionUUID: Guid.NewGuid().ToString("D"),
                transactionType: "Refund",
                transactionDateTime: DateTime.UtcNow.ToString("O"),
                rrn: payment!.RetrievalReferenceNumber,
                transactionAmount: 500,
                currencyCode: "ZAR",
                txResultCode: "00",
                isApproved: true
            );
            var refRes1 = await client.SendAsync(CreateSwitchHttpRequest(refund1));
            refRes1.StatusCode.Should().Be(HttpStatusCode.OK);

            var checkPartial = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            checkPartial!.Status.Should().Be(PaymentStatus.PartiallyRefunded);
            checkPartial.RefundedAmount.Should().Be(500);

            // Remaining refund of R15.00
            var refund2 = new SwitchWebhookPayload(
                transactionUUID: Guid.NewGuid().ToString("D"),
                transactionType: "Refund",
                transactionDateTime: DateTime.UtcNow.ToString("O"),
                rrn: payment.RetrievalReferenceNumber,
                transactionAmount: 1500,
                currencyCode: "ZAR",
                txResultCode: "00",
                isApproved: true
            );
            var refRes2 = await client.SendAsync(CreateSwitchHttpRequest(refund2));
            refRes2.StatusCode.Should().Be(HttpStatusCode.OK);

            var checkFull = await client.GetFromJsonAsync<PaymentDto>($"/api/v1/payments/{payment.PaymentId}");
            checkFull!.Status.Should().Be(PaymentStatus.Refunded);
            checkFull.RefundedAmount.Should().Be(2000);
        }
    }

    // -------------------------------------------------------------
    // Scenario 8: Acme does not respond before request times out
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario8_PaymentTimesOut_ResolvesViaRTTQ()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            // Configure stub to simulate gateway timeout on initial submit
            stub.SimulateTimeout = true;
            stub.DesiredTransactionStatus = "Success";
            stub.DesiredResponseCode = "00";
            stub.DesiredAuthCode = "998877";

            var req = new SubmitPaymentRequest($"app-timeout-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 1999, "ZAR");
            var res = await client.PostAsJsonAsync("/api/v1/payments", req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await res.Content.ReadFromJsonAsync<SubmitPaymentResult>();

            // Assert that RTTQ was queried and resolved status to Approved
            result!.Status.Should().Be(PaymentStatus.Approved);
            result.AuthCode.Should().Be("998877");
            stub.RttqCallsCount.Should().Be(1); // RTTQ query executed
        }
    }

    // -------------------------------------------------------------
    // Scenario 9: Daily settlement summary reporting
    // -------------------------------------------------------------
    [Fact]
    public async Task Scenario9_DailySettlementSummary_ReturnsAggregatedTotals()
    {
        var (factory, client, stub) = CreateTestSetup();
        using (factory)
        {
            // Submit an approved payment
            var reqApproved = new SubmitPaymentRequest($"app-settle-app-{Guid.NewGuid()}", "cust-001", "ZAP0004182", 2000, "ZAR");
            await client.PostAsJsonAsync("/api/v1/payments", reqApproved);

            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var response = await client.GetAsync($"/api/v1/settlement/daily-summary?date={today}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await response.Content.ReadFromJsonAsync<DailySettlementSummaryDto>();

            summary.Should().NotBeNull();
            summary!.SubMerchants.Should().Contain(s => s.SubMerchantId == "ZAP0004182");
            var smSummary = summary.SubMerchants.First(s => s.SubMerchantId == "ZAP0004182");
            smSummary.ApprovedCount.Should().BeGreaterThanOrEqualTo(1);
            smSummary.ApprovedTotalValue.Should().BeGreaterThanOrEqualTo(2000);
        }
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------
    private static HttpRequestMessage CreateAdviceHttpRequest(PaymentAdvicePayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications/payment-advice")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var basicCreds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AdviceUser}:{AdvicePass}"));
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);
        return msg;
    }

    private static HttpRequestMessage CreateSwitchHttpRequest(SwitchWebhookPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications/transaction-status")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
        var signature = Convert.ToBase64String(hashBytes);

        msg.Headers.Add("x-signature", signature);
        return msg;
    }
}
