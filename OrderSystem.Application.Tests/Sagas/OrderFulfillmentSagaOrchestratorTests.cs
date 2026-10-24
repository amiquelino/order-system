using Bogus;
using OrderSystem.Application.Sagas;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Sagas;

public class OrderFulfillmentSagaOrchestratorTests
{
    private static readonly CustomerId CustomerId = new(new Faker().Random.Guid());

    private static Order ANewOrder()
    {
        var faker = new Faker();
        var item = new OrderItem
        {
            ProductName = faker.Commerce.ProductName(),
            Quantity = faker.Random.Int(1, 10),
            UnitPrice = new Money(faker.Finance.Amount(1, 200), "USD")
        };

        return Order.Create(CustomerId, [item]);
    }

    private static OrderFulfillmentSagaOrchestrator AnOrchestrator(
        FakeOrderRepository orderRepository,
        FakeEventPublisher eventPublisher,
        FakePaymentGateway paymentGateway,
        FakePaymentRetryQueue retryQueue,
        FakeInventoryService inventoryService,
        FakeOrderFulfillmentSagaRepository sagaRepository) =>
        new(orderRepository, eventPublisher, paymentGateway, retryQueue, inventoryService, sagaRepository);

    [Fact]
    public async Task StartAsync_ReservationSucceeds_ApprovedPayment_ConfirmsAndKeepsReservation()
    {
        var order = ANewOrder();
        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysApproves(),
            new FakePaymentRetryQueue(),
            FakeInventoryService.AlwaysReserves(),
            sagaRepository);

        await orchestrator.StartAsync(order, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Confirmed);
        // FR-007: a confirmed order's reservation is retained, not released.
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ReservationSucceeds_DeclinedPayment_SchedulesFirstRetry_KeepsReservation()
    {
        var order = ANewOrder();
        var retryQueue = new FakePaymentRetryQueue();
        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysDeclines(),
            retryQueue,
            FakeInventoryService.AlwaysReserves(),
            sagaRepository);

        await orchestrator.StartAsync(order, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        retryQueue.ScheduledRetries.Should().ContainSingle().Which.Attempt.Should().Be(1);
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ReservationFails_CancelsOrder_NeverChargesPayment()
    {
        var order = ANewOrder();
        var paymentGateway = FakePaymentGateway.AlwaysApproves();
        var eventPublisher = new FakeEventPublisher();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            eventPublisher,
            paymentGateway,
            new FakePaymentRetryQueue(),
            FakeInventoryService.NeverReserves(),
            new FakeOrderFulfillmentSagaRepository());

        await orchestrator.StartAsync(order, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        paymentGateway.Attempts.Should().BeEmpty();
        eventPublisher.PublishedEvents.OfType<OrderCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task ContinueAsync_DeclinedUnderMaxAttempts_SchedulesNextRetry()
    {
        var order = ANewOrder();
        order.BeginPaymentProcessing();
        order.FailPayment();

        var retryQueue = new FakePaymentRetryQueue();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysDeclines(),
            retryQueue,
            FakeInventoryService.AlwaysReserves(),
            new FakeOrderFulfillmentSagaRepository());

        await orchestrator.ContinueAsync(order, attempt: 2, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        retryQueue.ScheduledRetries.Should().ContainSingle().Which.Attempt.Should().Be(3);
    }

    [Fact]
    public async Task ContinueAsync_DeclinedAtMaxAttempts_CompensatesAndCancels()
    {
        var order = ANewOrder();
        order.BeginPaymentProcessing();
        order.FailPayment();

        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var state = OrderFulfillmentSagaState.Start(order.Id);
        state.MarkStockReserved();
        sagaRepository.Seed(state);

        var inventoryService = FakeInventoryService.AlwaysReserves();
        var retryQueue = new FakePaymentRetryQueue();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysDeclines(),
            retryQueue,
            inventoryService,
            sagaRepository);

        await orchestrator.ContinueAsync(order, attempt: 3, CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        retryQueue.ScheduledRetries.Should().BeEmpty();
        inventoryService.ReleasedOrderIds.Should().ContainSingle().Which.Should().Be(order.Id);
        sagaRepository.Get(order.Id)!.IsStockReserved.Should().BeFalse();
    }

    [Fact]
    public async Task CompensateAsync_CalledTwice_ReleasesInventoryOnlyOnce()
    {
        // FR-006: both the exhausted-retry path and a manual cancellation can
        // reach CompensateAsync for the same order — the second call must be
        // a no-op, not a second release.
        var order = ANewOrder();
        var sagaRepository = new FakeOrderFulfillmentSagaRepository();
        var state = OrderFulfillmentSagaState.Start(order.Id);
        state.MarkStockReserved();
        sagaRepository.Seed(state);

        var inventoryService = FakeInventoryService.AlwaysReserves();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysApproves(),
            new FakePaymentRetryQueue(),
            inventoryService,
            sagaRepository);

        await orchestrator.CompensateAsync(order.Id, CancellationToken.None);
        await orchestrator.CompensateAsync(order.Id, CancellationToken.None);

        inventoryService.ReleasedOrderIds.Should().ContainSingle();
    }

    [Fact]
    public async Task CompensateAsync_NoSagaRowForOrder_IsNoOp()
    {
        // FR-009: nothing was ever reserved for this order.
        var orderId = OrderId.New();
        var inventoryService = FakeInventoryService.AlwaysReserves();
        var orchestrator = AnOrchestrator(
            new FakeOrderRepository(),
            new FakeEventPublisher(),
            FakePaymentGateway.AlwaysApproves(),
            new FakePaymentRetryQueue(),
            inventoryService,
            new FakeOrderFulfillmentSagaRepository());

        var act = () => orchestrator.CompensateAsync(orderId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        inventoryService.ReleasedOrderIds.Should().BeEmpty();
    }
}
