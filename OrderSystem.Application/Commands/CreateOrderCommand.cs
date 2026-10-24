using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Commands;

public record CreateOrderItemDto(string ProductName, int Quantity, decimal UnitPrice, string Currency);

public record CreateOrderCommand(CustomerId CustomerId, List<CreateOrderItemDto> Items);
