using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Application.Queries;

public class GetOrderHistoryQueryHandler(IOrderRepository orderRepository)
{
    public Task<IReadOnlyList<Order>> Handle(GetOrderHistoryQuery query, CancellationToken cancellationToken)
    {
        return orderRepository.GetHistoryAsync(query.CustomerId, cancellationToken);
    }
}
