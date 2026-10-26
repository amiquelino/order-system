using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Inventory;

public interface IInventoryService
{
    Task<bool> TryReserveAsync(Order order, CancellationToken cancellationToken = default);
    Task ReleaseAsync(OrderId orderId, CancellationToken cancellationToken = default);
}
