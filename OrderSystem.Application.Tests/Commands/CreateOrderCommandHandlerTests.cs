using Bogus;
using OrderSystem.Application.Commands;
using OrderSystem.Application.Sagas;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Commands;

public class CreateOrderCommandHandlerTests
{
    private static readonly CustomerId CustomerId = new(new Faker().Random.Guid());

    private static CreateOrderCommand ACommand()
    {
        var faker = new Faker();
        return new CreateOrderCommand(
            CustomerId,
            [new CreateOrderItemDto(faker.Commerce.ProductName(), faker.Random.Int(1, 5), faker.Finance.Amount(1, 200), "USD")]);
    }

    private static CreateOrderCommandHandler AHandler(
        FakeOrderRepository orderRepository,
        FakeEventPublisher eventPublisher,
        FakePaymentGateway paymentGateway,
        FakePaymentRetryQueue retryQueue,
        FakeInventoryService? inventoryService = null,
        FakeOrderFulfillmentSagaRepository? sagaRepository = null)
    {
        var orchestrator = new OrderFulfillmentSagaOrchestrator(
            orderRepository,
            eventPublisher,
            paymentGateway,
            retryQueue,
            inventoryService ?? FakeInventoryService.AlwaysReserves(),
            sagaRepository ?? new FakeOrderFulfillmentSagaRepository());

        return new CreateOrderCommandHandler(orderRepository, eventPublisher, orchestrator);
    }

    [Fact]
    public async Task Handle_ApprovedPayment_PersistsOrderAsConfirmed()
    {
        var repository = new FakeOrderRepository();
        var handler = AHandler(repository, new FakeEventPublisher(), FakePaymentGateway.AlwaysApproves(), new FakePaymentRetryQueue());

        var orderId = await handler.Handle(ACommand(), CancellationToken.None);

        var order = await repository.GetByIdAsync(orderId);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_DeclinedPayment_PersistsOrderAsPaymentFailed_AndSchedulesRetry()
    {
        var repository = new FakeOrderRepository();
        var retryQueue = new FakePaymentRetryQueue();
        var handler = AHandler(repository, new FakeEventPublisher(), FakePaymentGateway.AlwaysDeclines(), retryQueue);

        var orderId = await handler.Handle(ACommand(), CancellationToken.None);

        var order = await repository.GetByIdAsync(orderId);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.PaymentFailed);

        retryQueue.ScheduledRetries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { OrderId = orderId, Attempt = 1 });
    }

    [Fact]
    public async Task Handle_ApprovedPayment_DoesNotScheduleRetry()
    {
        var retryQueue = new FakePaymentRetryQueue();
        var handler = AHandler(new FakeOrderRepository(), new FakeEventPublisher(), FakePaymentGateway.AlwaysApproves(), retryQueue);

        await handler.Handle(ACommand(), CancellationToken.None);

        retryQueue.ScheduledRetries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PublishesOrderCreatedEventThenStatusChangedEvents()
    {
        var eventPublisher = new FakeEventPublisher();
        var handler = AHandler(new FakeOrderRepository(), eventPublisher, FakePaymentGateway.AlwaysApproves(), new FakePaymentRetryQueue());

        await handler.Handle(ACommand(), CancellationToken.None);

        eventPublisher.PublishedEvents[0].Should().BeOfType<OrderCreatedEvent>();
        var statusChanges = eventPublisher.PublishedEvents.OfType<OrderStatusChangedEvent>();
        statusChanges.Select(e => e.NewStatus).Should().Equal(OrderStatus.PaymentPending, OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_ReservationFails_CancelsOrderWithoutAttemptingPayment()
    {
        // FR-002: payment must never be attempted without a successful reservation.
        var repository = new FakeOrderRepository();
        var paymentGateway = FakePaymentGateway.AlwaysApproves();
        var handler = AHandler(
            repository,
            new FakeEventPublisher(),
            paymentGateway,
            new FakePaymentRetryQueue(),
            inventoryService: FakeInventoryService.NeverReserves());

        var orderId = await handler.Handle(ACommand(), CancellationToken.None);

        var order = await repository.GetByIdAsync(orderId);
        order!.Status.Should().Be(OrderStatus.Cancelled);
        paymentGateway.Attempts.Should().BeEmpty();
    }
}
