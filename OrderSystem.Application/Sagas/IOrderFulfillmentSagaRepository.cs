using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Sagas;

public interface IOrderFulfillmentSagaRepository
{
    Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task AddAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default);
}
