using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Inventory;

public class SimulatedInventoryService : IInventoryService
{
    // No real inventory is wired up yet: reservation always succeeds, and
    // release is a no-op since this service holds no state of its own —
    // "was this order's stock already released" is OrderFulfillmentSagaState's
    // job (see research.md Decision 2).
    public Task<bool> TryReserveAsync(Order order, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task ReleaseAsync(OrderId orderId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
