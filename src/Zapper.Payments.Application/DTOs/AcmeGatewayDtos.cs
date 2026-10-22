using System.Text.Json.Serialization;

namespace Zapper.Payments.Application.DTOs;

public record AcmeMessageHeader(
    string MessageDateTime,
    string MessageID,
    string Channel
);

public record AcmeCard(
    string Token
);

public record AcmePaymentRequestPayload(
    string MerchantID,
    string TransactionID,
    string OrderNumber,
    long Amount,
    string CurrencyCode,
    string PaymentService,
    AcmeCard Card,
    string CardOnFileScenario,
    string SubMerchantID,
    string MerchantCategoryCode,
    string CardAcceptorName,
    string Email
);

public record AcmePaymentEnvelope(
    AcmeMessageHeader MessageHeader,
    AcmePaymentRequestPayload PaymentRequest
);

public record AcmeResponseDetail(
    string Source,
    string Code,
    string Description,
    string ClientMessage
);

public record AcmePaymentResponsePayload(
    string TransactionID,
    string? TransactionDateTime,
    string OrderNumber,
    string? ReconID,
    string? RetrievalReferenceNumber,
    string? AuthCode,
    string TransactionStatus,
    AcmeResponseDetail? ResponseDetail,
    long? AuthAmount
);

public record AcmeResponseEnvelope(
    AcmeMessageHeader MessageHeader,
    AcmePaymentResponsePayload PaymentResponse
);

public record PaymentAdvicePayload(
    string RequestType,
    string MerchantID,
    string TransactionID,
    string? TransactionDateTime,
    string? OrderNumber,
    string? ReconID,
    string RetrievalReferenceNumber,
    string SystemsTraceAuditNumber,
    long Amount,
    string? CurrencyCode,
    string PaymentService,
    string TransactionStatus,
    string ResponseSource,
    string ResponseCode,
    string? ResponseDescription,
    string? AuthCode,
    string? MaskedCardNumber,
    string CommProtocol
);

public record SwitchWebhookPayload(
    string transactionUUID,
    string transactionType,
    string transactionDateTime,
    string rrn,
    long transactionAmount,
    string currencyCode,
    string txResultCode,
    bool isApproved
);
