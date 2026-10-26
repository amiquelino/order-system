using Bogus;
using OrderSystem.Application.Commands;
using OrderSystem.Application.Sagas;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Commands;

public class RetryPaymentCommandHandlerTests
{
    private static readonly CustomerId CustomerId = new(new Faker().Random.Guid());

    private static Order APaymentFailedOrder()
    {
        var faker = new Faker();
        var item = new OrderItem
        {
            ProductName = faker.Commerce.ProductName(),
            Quantity = faker.Random.Int(1, 10),
            UnitPrice = new Money(faker.Finance.Amount(1, 200), "USD")
        };

        var order = Order.Create(CustomerId, [item]);
        order.BeginPaymentProcessing();
        order.FailPayment();
        return order;
    }

    private static RetryPaymentCommandHandler AHandler(
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

        return new RetryPaymentCommandHandler(orderRepository, orchestrator);
    }

    // An order whose stock was reserved when it was created — mirrors the
    // saga row StartAsync would have written. Tests exercising the
    // exhausted-retries compensation path need this to exist already.
    private static FakeOrderFulfillmentSagaRepository ASagaRepositoryWithReservationFor(OrderId orderId)
    {
        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var state = OrderFulfillmentSagaState.Start(orderId);
        state.MarkStockReserved();
        sagaRepository.Seed(state);
        return sagaRepository;
    }

    [Fact]
    public async Task Handle_UnknownOrder_NoOps()
    {
        var handler = AHandler(new FakeOrderRepository(), new FakeEventPublisher(), FakePaymentGateway.AlwaysApproves(), new FakePaymentRetryQueue());

        var act = () => handler.Handle(new RetryPaymentCommand(OrderId.New(), 1), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_OrderNoLongerPaymentFailed_NoOps()
    {
        var repository = new FakeOrderRepository();
        var order = APaymentFailedOrder();
        order.BeginPaymentProcessing();
        order.ConfirmPayment(); // e.g. a previous retry already resolved it
        repository.Seed(order);

        var gateway = FakePaymentGateway.AlwaysDeclines();
        var handler = AHandler(repository, new FakeEventPublisher(), gateway, new FakePaymentRetryQueue());

        await handler.Handle(new RetryPaymentCommand(order.Id, 1), CancellationToken.None);

        gateway.Attempts.Should().BeEmpty();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_ApprovedOnRetry_ConfirmsOrder()
    {
        var repository = new FakeOrderRepository();
        var order = APaymentFailedOrder();
        repository.Seed(order);

        var handler = AHandler(repository, new FakeEventPublisher(), FakePaymentGateway.AlwaysApproves(), new FakePaymentRetryQueue());

        await handler.Handle(new RetryPaymentCommand(order.Id, 1), CancellationToken.None);

        var persisted = await repository.GetByIdAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_DeclinedUnderMaxAttempts_SchedulesAnotherRetry()
    {
        var repository = new FakeOrderRepository();
        var order = APaymentFailedOrder();
        repository.Seed(order);

        var retryQueue = new FakePaymentRetryQueue();
        var handler = AHandler(repository, new FakeEventPublisher(), FakePaymentGateway.AlwaysDeclines(), retryQueue);

        await handler.Handle(new RetryPaymentCommand(order.Id, 1), CancellationToken.None);

        var persisted = await repository.GetByIdAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.PaymentFailed);

        retryQueue.ScheduledRetries.Should().ContainSingle().Which.Attempt.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DeclinedAtMaxAttempts_CompensatesAndCancelsOrder()
    {
        // FR-003/FR-004: exhausted retries must release the reservation and
        // resolve the order to Cancelled automatically — not leave it stuck
        // in PaymentFailed forever (the bug this feature fixes).
        var repository = new FakeOrderRepository();
        var order = APaymentFailedOrder();
        repository.Seed(order);

        var sagaRepository = ASagaRepositoryWithReservationFor(order.Id);
        var inventoryService = FakeInventoryService.AlwaysReserves();
        var retryQueue = new FakePaymentRetryQueue();
        var eventPublisher = new FakeEventPublisher();
        var handler = AHandler(
            repository,
            eventPublisher,
            FakePaymentGateway.AlwaysDeclines(),
            retryQueue,
            inventoryService,
            sagaRepository);

        await handler.Handle(new RetryPaymentCommand(order.Id, 3), CancellationToken.None);

        var persisted = await repository.GetByIdAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Cancelled);
        retryQueue.ScheduledRetries.Should().BeEmpty();

        inventoryService.ReleasedOrderIds.Should().ContainSingle().Which.Should().Be(order.Id);
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeFalse();

        eventPublisher.PublishedEvents.OfType<OrderCancelledEvent>().Should().ContainSingle();
        eventPublisher.PublishedEvents.Last().Should().BeOfType<OrderStatusChangedEvent>()
            .Which.NewStatus.Should().Be(OrderStatus.Cancelled);
    }
}
