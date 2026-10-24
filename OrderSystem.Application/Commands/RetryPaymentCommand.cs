using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Commands;

public record RetryPaymentCommand(OrderId OrderId, int Attempt);
