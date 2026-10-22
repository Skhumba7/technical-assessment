namespace Zapper.Payments.Domain.Enums;

public enum PaymentStatus
{
    Initiated = 1,
    SubmittedToGateway = 2,
    Approved = 3,
    Declined = 4,
    TimedOut = 5,
    Reversed = 6,
    PartiallyRefunded = 7,
    Refunded = 8,
    Failed = 9
}
