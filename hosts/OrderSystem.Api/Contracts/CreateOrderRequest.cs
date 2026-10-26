namespace OrderSystem.Api.Contracts;

public record CreateOrderItemRequest(string ProductName, int Quantity, decimal UnitPrice, string Currency);

public record CreateOrderRequest(Guid CustomerId, List<CreateOrderItemRequest> Items);
