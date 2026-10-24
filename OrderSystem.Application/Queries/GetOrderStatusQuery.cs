using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Queries;

public record GetOrderStatusQuery(OrderId OrderId);
