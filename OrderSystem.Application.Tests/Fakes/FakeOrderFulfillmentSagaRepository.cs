using OrderSystem.Application.Sagas;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Fakes;

public class FakeOrderFulfillmentSagaRepository : IOrderFulfillmentSagaRepository
{
    private readonly Dictionary<OrderId, OrderFulfillmentSagaState> _states = [];

    public void Seed(OrderFulfillmentSagaState state) => _states[state.OrderId] = state;

    public OrderFulfillmentSagaState? Get(OrderId orderId) =>
        _states.TryGetValue(orderId, out var state) ? state : null;

    public Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(orderId, out var state);
        return Task.FromResult(state);
    }

    public Task AddAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default)
    {
        _states[state.OrderId] = state;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default)
    {
        _states[state.OrderId] = state;
        return Task.CompletedTask;
    }
}
