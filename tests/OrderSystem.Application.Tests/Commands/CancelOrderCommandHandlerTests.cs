using Bogus;
using OrderSystem.Application.Commands;
using OrderSystem.Application.Sagas;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Commands;

public class CancelOrderCommandHandlerTests
{
    private static readonly CustomerId CustomerId = new(new Faker().Random.Guid());

    private static Order ACreatedOrder() => Order.Create(CustomerId, [AnItem()]);

    private static OrderItem AnItem()
    {
        var faker = new Faker();
        return new OrderItem
        {
            ProductName = faker.Commerce.ProductName(),
            Quantity = faker.Random.Int(1, 10),
            UnitPrice = new Money(faker.Finance.Amount(1, 200), "USD")
        };
    }

    private static CancelOrderCommandHandler AHandler(
        FakeOrderRepository orderRepository,
        FakeEventPublisher eventPublisher,
        FakeInventoryService? inventoryService = null,
        FakeOrderFulfillmentSagaRepository? sagaRepository = null)
    {
        var orchestrator = new OrderFulfillmentSagaOrchestrator(
            orderRepository,
            eventPublisher,
            FakePaymentGateway.AlwaysApproves(),
            new FakePaymentRetryQueue(),
            inventoryService ?? FakeInventoryService.AlwaysReserves(),
            sagaRepository ?? new FakeOrderFulfillmentSagaRepository());

        return new CancelOrderCommandHandler(orderRepository, eventPublisher, orchestrator);
    }

    private static FakeOrderFulfillmentSagaRepository ASagaRepositoryWithReservationFor(OrderId orderId)
    {
        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var state = OrderFulfillmentSagaState.Start(orderId);
        state.MarkStockReserved();
        sagaRepository.Seed(state);
        return sagaRepository;
    }

    [Fact]
    public async Task Handle_ExistingOrder_CancelsAndPersistsIt()
    {
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        repository.Seed(order);

        var handler = AHandler(repository, new FakeEventPublisher());

        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        var persisted = await repository.GetByIdAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsKeyNotFound()
    {
        var handler = AHandler(new FakeOrderRepository(), new FakeEventPublisher());

        var act = () => handler.Handle(new CancelOrderCommand(OrderId.New()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadyCancelledOrder_ThrowsInvalidOperation()
    {
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        order.Cancel();
        repository.Seed(order);

        var handler = AHandler(repository, new FakeEventPublisher());

        var act = () => handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_PublishesOrderCancelledEventAndStatusChangedEvent()
    {
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        repository.Seed(order);

        var eventPublisher = new FakeEventPublisher();
        var handler = AHandler(repository, eventPublisher);

        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        eventPublisher.PublishedEvents.Should().HaveCount(2);
        var cancelledEvent = eventPublisher.PublishedEvents[0].Should().BeOfType<OrderCancelledEvent>().Which;
        cancelledEvent.OrderId.Should().Be(order.Id);

        var statusChangedEvent = eventPublisher.PublishedEvents[1].Should().BeOfType<OrderStatusChangedEvent>().Which;
        statusChangedEvent.NewStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_OrderWithReservedStock_ReleasesReservation()
    {
        // FR-005: manual cancellation releases anything reserved for the order.
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        repository.Seed(order);

        var sagaRepository = ASagaRepositoryWithReservationFor(order.Id);
        var inventoryService = FakeInventoryService.AlwaysReserves();
        var handler = AHandler(repository, new FakeEventPublisher(), inventoryService, sagaRepository);

        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        inventoryService.ReleasedOrderIds.Should().ContainSingle().Which.Should().Be(order.Id);
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ConfirmedOrder_RejectsCancellation_AndDoesNotReleaseStock()
    {
        // FR-008 (existing behavior, unchanged) plus a check that a rejected
        // cancellation never has the side effect of releasing stock.
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        order.BeginPaymentProcessing();
        order.ConfirmPayment();
        repository.Seed(order);

        var sagaRepository = ASagaRepositoryWithReservationFor(order.Id);
        var inventoryService = FakeInventoryService.AlwaysReserves();
        var handler = AHandler(repository, new FakeEventPublisher(), inventoryService, sagaRepository);

        var act = () => handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        inventoryService.ReleasedOrderIds.Should().BeEmpty();
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OrderWithNoReservation_CancelsWithoutError()
    {
        // FR-009: an order whose items were never reserved (no saga row at
        // all) must cancel cleanly rather than throw.
        var repository = new FakeOrderRepository();
        var order = ACreatedOrder();
        repository.Seed(order);

        var handler = AHandler(repository, new FakeEventPublisher(), sagaRepository: new FakeOrderFulfillmentSagaRepository());

        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        var persisted = await repository.GetByIdAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Cancelled);
    }
}
