using OrderSystem.Domain.Ports;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Queries;

public class GetOrderStatusQueryHandler(IOrderRepository orderRepository)
{
    public async Task<OrderStatus> Handle(GetOrderStatusQuery query, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order {query.OrderId} was not found.");
        }

        return order.Status;
    }
}
