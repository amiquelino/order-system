using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Payments;

public record PaymentRetryMessage(OrderId OrderId, int Attempt, string Reason);
