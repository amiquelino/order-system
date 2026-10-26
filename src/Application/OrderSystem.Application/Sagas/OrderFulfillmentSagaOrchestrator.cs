using OrderSystem.Application.Inventory;
using OrderSystem.Application.Payments;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.Ports;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Sagas;

// Hand-rolled rather than a Wolverine Saga: see Constitution Principle VI
// and research.md Decision 1 for the tradeoff. Owns the reserve -> charge ->
// confirm flow and the compensation path shared by exhausted retries and
// manual cancellation.
public class OrderFulfillmentSagaOrchestrator(
    IOrderRepository orderRepository,
    IEventPublisher eventPublisher,
    IPaymentGateway paymentGateway,
    IPaymentRetryQueue paymentRetryQueue,
    IInventoryService inventoryService,
    IOrderFulfillmentSagaRepository sagaRepository)
{
    private const int MaxAttempts = 3;

    public async Task StartAsync(Order order, CancellationToken cancellationToken = default)
    {
        var reserved = await inventoryService.TryReserveAsync(order, cancellationToken);

        var sagaState = OrderFulfillmentSagaState.Start(order.Id);
        if (reserved)
        {
            sagaState.MarkStockReserved();
        }

        await sagaRepository.AddAsync(sagaState, cancellationToken);

        if (!reserved)
        {
            // FR-002: never attempt payment without a successful reservation.
            order.Cancel();
            await AnnounceCancellationAsync(order, cancellationToken);
            return;
        }

        await ChargeAndAnnounceAsync(order, attempt: 0, cancellationToken);
    }

    public Task ContinueAsync(Order order, int attempt, CancellationToken cancellationToken = default) =>
        ChargeAndAnnounceAsync(order, attempt, cancellationToken);

    public async Task CompensateAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        var sagaState = await sagaRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (sagaState is null)
        {
            // FR-009: nothing was ever reserved for this order — safe no-op.
            return;
        }

        // FR-006: ReleaseStockIfReserved() is itself idempotent, so a second
        // CompensateAsync for the same order (automatic then manual, or vice
        // versa) never calls IInventoryService.ReleaseAsync twice.
        if (sagaState.ReleaseStockIfReserved())
        {
            await inventoryService.ReleaseAsync(orderId, cancellationToken);
            await sagaRepository.UpdateAsync(sagaState, cancellationToken);
        }
    }

    private async Task ChargeAndAnnounceAsync(Order order, int attempt, CancellationToken cancellationToken)
    {
        order.BeginPaymentProcessing();
        await AnnounceAsync(order, cancellationToken);

        var approved = await paymentGateway.ChargeAsync(order, attempt, cancellationToken);

        if (approved)
        {
            order.ConfirmPayment();
            await AnnounceAsync(order, cancellationToken);
            return;
        }

        order.FailPayment();
        await AnnounceAsync(order, cancellationToken);

        if (attempt < MaxAttempts)
        {
            await paymentRetryQueue.ScheduleRetryAsync(
                new PaymentRetryMessage(order.Id, attempt + 1, "Payment declined"),
                cancellationToken);
            return;
        }

        // Every retry is exhausted: compensate and resolve to a terminal
        // state instead of leaving the order stuck in PaymentFailed forever
        // (the bug this feature fixes — FR-003/FR-004).
        await CompensateAsync(order.Id, cancellationToken);
        order.Cancel();
        await AnnounceCancellationAsync(order, cancellationToken);
    }

    private async Task AnnounceAsync(Order order, CancellationToken cancellationToken)
    {
        await orderRepository.UpdateAsync(order, cancellationToken);

        await eventPublisher.PublishAsync(
            new OrderStatusChangedEvent(order.Id, order.Status, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private async Task AnnounceCancellationAsync(Order order, CancellationToken cancellationToken)
    {
        await orderRepository.UpdateAsync(order, cancellationToken);

        await eventPublisher.PublishAsync(
            new OrderCancelledEvent(order.Id, DateTimeOffset.UtcNow),
            cancellationToken);

        await eventPublisher.PublishAsync(
            new OrderStatusChangedEvent(order.Id, order.Status, DateTimeOffset.UtcNow),
            cancellationToken);
    }
}
