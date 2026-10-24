using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Ports;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Fakes;

public class FakeOrderRepository : IOrderRepository
{
    private readonly Dictionary<OrderId, Order> _orders = [];

    public IReadOnlyCollection<Order> Orders => _orders.Values;

    public void Seed(Order order) => _orders[order.Id] = order;

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        _orders.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<IReadOnlyList<Order>> GetHistoryAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Order> result = _orders.Values.Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }
}
