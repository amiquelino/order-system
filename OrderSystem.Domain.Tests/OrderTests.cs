using Bogus;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Tests;

public class OrderTests
{
    private static readonly CustomerId CustomerId = new(new Faker().Random.Guid());

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

    [Fact]
    public void Create_WithItems_StartsInCreatedStatus()
    {
        var order = Order.Create(CustomerId, [AnItem()]);

        order.Status.Should().Be(OrderStatus.Created);
        order.CustomerId.Should().Be(CustomerId);
        order.Id.Should().NotBe(default);
    }

    [Fact]
    public void Create_WithNoItems_Throws()
    {
        var act = () => Order.Create(CustomerId, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_FromCreated_SetsCancelled()
    {
        var order = Order.Create(CustomerId, [AnItem()]);

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.Cancel();

        var act = () => order.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenConfirmed_Throws()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.BeginPaymentProcessing();
        order.ConfirmPayment();

        var act = () => order.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenPaymentPending_Succeeds()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.BeginPaymentProcessing();

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void BeginPaymentProcessing_FromCreated_SetsPaymentPending()
    {
        var order = Order.Create(CustomerId, [AnItem()]);

        order.BeginPaymentProcessing();

        order.Status.Should().Be(OrderStatus.PaymentPending);
    }

    [Fact]
    public void BeginPaymentProcessing_FromPaymentFailed_ReentersPaymentPending()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.BeginPaymentProcessing();
        order.FailPayment();

        order.BeginPaymentProcessing();

        order.Status.Should().Be(OrderStatus.PaymentPending);
    }

    [Theory]
    [InlineData(OrderStatus.PaymentPending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public void BeginPaymentProcessing_FromInvalidStatus_Throws(OrderStatus startingStatus)
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        MoveTo(order, startingStatus);

        var act = () => order.BeginPaymentProcessing();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConfirmPayment_FromPaymentPending_SetsConfirmed()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.BeginPaymentProcessing();

        order.ConfirmPayment();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public void ConfirmPayment_WhenNotPaymentPending_Throws(OrderStatus startingStatus)
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        MoveTo(order, startingStatus);

        var act = () => order.ConfirmPayment();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailPayment_FromPaymentPending_SetsPaymentFailed()
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        order.BeginPaymentProcessing();

        order.FailPayment();

        order.Status.Should().Be(OrderStatus.PaymentFailed);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public void FailPayment_WhenNotPaymentPending_Throws(OrderStatus startingStatus)
    {
        var order = Order.Create(CustomerId, [AnItem()]);
        MoveTo(order, startingStatus);

        var act = () => order.FailPayment();

        act.Should().Throw<InvalidOperationException>();
    }

    private static void MoveTo(Order order, OrderStatus status)
    {
        switch (status)
        {
            case OrderStatus.Created:
                break;
            case OrderStatus.PaymentPending:
                order.BeginPaymentProcessing();
                break;
            case OrderStatus.PaymentFailed:
                order.BeginPaymentProcessing();
                order.FailPayment();
                break;
            case OrderStatus.Confirmed:
                order.BeginPaymentProcessing();
                order.ConfirmPayment();
                break;
            case OrderStatus.Cancelled:
                order.Cancel();
                break;
        }
    }
}
