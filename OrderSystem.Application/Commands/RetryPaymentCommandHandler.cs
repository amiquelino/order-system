using OrderSystem.Application.Sagas;
using OrderSystem.Domain.Ports;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Commands;

public class RetryPaymentCommandHandler(
    IOrderRepository orderRepository,
    OrderFulfillmentSagaOrchestrator sagaOrchestrator)
{
    public async Task Handle(RetryPaymentCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null || order.Status != OrderStatus.PaymentFailed)
        {
            return;
        }

        // Charge, confirm/fail, and either schedule the next retry or
        // compensate + cancel if attempts are exhausted — see
        // OrderFulfillmentSagaOrchestrator.
        await sagaOrchestrator.ContinueAsync(order, command.Attempt, cancellationToken);
    }
}
