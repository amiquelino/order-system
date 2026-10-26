using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Commands;

public record CancelOrderCommand(OrderId OrderId);
