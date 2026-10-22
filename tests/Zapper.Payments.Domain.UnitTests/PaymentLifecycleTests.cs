using FluentAssertions;
using Xunit;
using Zapper.Payments.Domain.Aggregates;
using Zapper.Payments.Domain.Common;
using Zapper.Payments.Domain.Enums;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Domain.UnitTests;

public class PaymentLifecycleTests
{
    private Payment CreateTestPayment(long amountCents = 1999)
    {
        return new Payment(
            appPaymentId: "app-pay-12345",
            customerId: "cust-001",
            subMerchantId: "ZAP0004182",
            cardToken: "tok-4f2a8c9e1b7d3560",
            amount: Money.FromZarCents(amountCents)
        );
    }

    [Fact]
    public void PaymentCreation_InitializesCorrectStateAndRrn()
    {
        // Act
        var payment = CreateTestPayment(1999);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Initiated);
        payment.OrderNumber.Should().StartWith("MR");
        payment.RetrievalReferenceNumber.Should().HaveLength(12);
        payment.RetrievalReferenceNumber.Should().Be(payment.OrderNumber.Substring(0, 12));
        payment.Amount.AmountInMinorUnits.Should().Be(1999);
        payment.History.Should().ContainSingle(h => h.ToStatus == PaymentStatus.Initiated);
    }

    [Fact]
    public void MarkSubmittedToGateway_TransitionsToSubmitted()
    {
        // Arrange
        var payment = CreateTestPayment();

        // Act
        payment.MarkSubmittedToGateway("tx-acme-999");

        // Assert
        payment.Status.Should().Be(PaymentStatus.SubmittedToGateway);
        payment.TransactionId.Should().Be("tx-acme-999");
    }

    [Fact]
    public void ApplyGatewayResponse_WithSuccessCode00_ApprovesPayment()
    {
        // Arrange
        var payment = CreateTestPayment(2500);
        payment.MarkSubmittedToGateway("tx-1");

        // Act
        payment.ApplyGatewayResponse(
            isSuccess: true,
            responseCode: "00",
            reconId: "recon-abc",
            authCode: "123456",
            authAmount: 2500,
            description: "Approved or completed successfully",
            clientMessage: "Approved."
        );

        // Assert
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.ReconId.Should().Be("recon-abc");
        payment.AuthCode.Should().Be("123456");
        payment.AuthAmount.Should().Be(2500);
    }

    [Fact]
    public void ApplyGatewayResponse_WithDeclineCode05_DeclinesPayment()
    {
        // Arrange
        var payment = CreateTestPayment();
        payment.MarkSubmittedToGateway("tx-2");

        // Act
        payment.ApplyGatewayResponse(
            isSuccess: false,
            responseCode: "05",
            reconId: null,
            authCode: null,
            authAmount: null,
            description: "Do not honour",
            clientMessage: "Your bank declined this payment."
        );

        // Assert
        payment.Status.Should().Be(PaymentStatus.Declined);
        payment.FailureReason.Should().Be("Do not honour");
        payment.ClientMessage.Should().Be("Your bank declined this payment.");
    }

    [Fact]
    public void ApplyAdviceNotification_DuplicateApprovedAdvice_IsIdempotent()
    {
        // Arrange
        var payment = CreateTestPayment(1999);
        payment.MarkSubmittedToGateway("tx-3");
        payment.ApplyGatewayResponse(true, "00", "recon-1", "999888", 1999, "Approved", "Approved.");

        var historyCountBefore = payment.History.Count;

        // Act - Duplicate Advice
        payment.ApplyAdviceNotification("00", "recon-1", "999888", 1999, "Approved");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.History.Count.Should().Be(historyCountBefore + 1);
        payment.History.Last().Reason.Should().Contain("Duplicate approval advice received (idempotent ignore)");
    }

    [Fact]
    public void ApplyReversal_WhenApproved_TransitionsToReversed()
    {
        // Arrange
        var payment = CreateTestPayment(1999);
        payment.MarkSubmittedToGateway("tx-rev");
        payment.ApplyGatewayResponse(true, "00", "recon-rev", "888777", 1999, "Approved", "Approved.");

        // Act
        payment.ApplyReversal("uuid-rev-001", 1999);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Reversed);
    }

    [Fact]
    public void ApplyReversal_WhenDuplicateUuid_IsIdempotent()
    {
        // Arrange
        var payment = CreateTestPayment(1999);
        payment.MarkSubmittedToGateway("tx-rev2");
        payment.ApplyGatewayResponse(true, "00", "recon-rev2", "888777", 1999, "Approved", "Approved.");
        payment.ApplyReversal("uuid-dup", 1999);

        // Act
        payment.ApplyReversal("uuid-dup", 1999);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Reversed);
        payment.History.Last().Reason.Should().Contain("Duplicate reversal webhook ignored");
    }

    [Fact]
    public void ApplyReversal_WhenPaymentDeclined_ThrowsDomainException()
    {
        // Arrange
        var payment = CreateTestPayment(1999);
        payment.MarkSubmittedToGateway("tx-dec");
        payment.ApplyGatewayResponse(false, "51", null, null, null, "Insufficient funds", "Not enough money.");

        // Act & Assert
        var action = () => payment.ApplyReversal("uuid-fail", 1999);
        action.Should().Throw<DomainException>()
            .WithMessage("*Payment must be Approved*");
    }

    [Fact]
    public void ApplyRefund_PartialThenFullRefund_TransitionsProperly()
    {
        // Arrange
        var payment = CreateTestPayment(2000); // R20.00
        payment.MarkSubmittedToGateway("tx-ref");
        payment.ApplyGatewayResponse(true, "00", "recon-ref", "123123", 2000, "Approved", "Approved.");

        // Act 1: Partial refund of R5.00
        payment.ApplyRefund("ref-uuid-1", 500);
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Should().Be(500);

        // Act 2: Remaining refund of R15.00
        payment.ApplyRefund("ref-uuid-2", 1500);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(2000);
    }

    [Fact]
    public void ApplyRefund_ExceedingAuthorizedAmount_ThrowsDomainException()
    {
        // Arrange
        var payment = CreateTestPayment(1000);
        payment.MarkSubmittedToGateway("tx-ref3");
        payment.ApplyGatewayResponse(true, "00", "recon-ref3", "123123", 1000, "Approved", "Approved.");

        // Act & Assert
        var action = () => payment.ApplyRefund("ref-uuid-too-much", 1500);
        action.Should().Throw<DomainException>()
            .WithMessage("*exceeds remaining refundable balance*");
    }
}
