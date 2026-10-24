using OrderSystem.Application.Inventory;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Fakes;

public class FakeInventoryService(bool reserveResult) : IInventoryService
{
    private readonly List<OrderId> _releasedOrderIds = [];

    public IReadOnlyList<OrderId> ReleasedOrderIds => _releasedOrderIds;

    public static FakeInventoryService AlwaysReserves() => new(reserveResult: true);

    public static FakeInventoryService NeverReserves() => new(reserveResult: false);

    public Task<bool> TryReserveAsync(Order order, CancellationToken cancellationToken = default) =>
        Task.FromResult(reserveResult);

    public Task ReleaseAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        _releasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}
