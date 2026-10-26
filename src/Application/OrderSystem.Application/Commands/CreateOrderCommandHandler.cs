using OrderSystem.Application.Sagas;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.Ports;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Commands;

public class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IEventPublisher eventPublisher,
    OrderFulfillmentSagaOrchestrator sagaOrchestrator)
{
    public async Task<OrderId> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var items = command.Items
            .Select(item => new OrderItem
            {
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = new Money(item.UnitPrice, item.Currency)
            })
            .ToList();

        var order = Order.Create(command.CustomerId, items);

        await orderRepository.AddAsync(order, cancellationToken);

        await eventPublisher.PublishAsync(
            new OrderCreatedEvent(order.Id, order.CustomerId, order.CreatedAt),
            cancellationToken);

        // Reserve, charge, confirm/fail, and schedule a retry if needed —
        // see OrderFulfillmentSagaOrchestrator.
        await sagaOrchestrator.StartAsync(order, cancellationToken);

        return order.Id;
    }
}
