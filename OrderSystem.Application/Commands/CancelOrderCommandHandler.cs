using OrderSystem.Application.Sagas;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Application.Commands;

public class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IEventPublisher eventPublisher,
    OrderFulfillmentSagaOrchestrator sagaOrchestrator)
{
    public async Task Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order {command.OrderId} was not found.");
        }

        order.Cancel();

        await orderRepository.UpdateAsync(order, cancellationToken);

        await eventPublisher.PublishAsync(
            new OrderCancelledEvent(order.Id, DateTimeOffset.UtcNow),
            cancellationToken);

        // The notification service only listens for OrderStatusChangedEvent, so publish
        // one here too, alongside the more specific OrderCancelledEvent for any future
        // subscriber that cares about cancellation specifically (e.g. an audit log).
        await eventPublisher.PublishAsync(
            new OrderStatusChangedEvent(order.Id, order.Status, DateTimeOffset.UtcNow),
            cancellationToken);

        // FR-005: release anything reserved for this order now that it's
        // cancelled. order.Cancel() above already guarded against cancelling
        // an order that shouldn't be cancelled, so by this point it's safe to
        // compensate; CompensateAsync is itself a no-op (FR-006/FR-009) when
        // there's nothing to release.
        await sagaOrchestrator.CompensateAsync(order.Id, cancellationToken);
    }
}
