using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Ports;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetHistoryAsync(CustomerId customerId, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
