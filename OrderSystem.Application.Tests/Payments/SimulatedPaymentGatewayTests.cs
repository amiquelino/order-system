using Bogus;
using OrderSystem.Application.Payments;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Payments;

public class SimulatedPaymentGatewayTests
{
    // The order total is the one thing every test here deliberately controls, since
    // it's what drives the gateway's decision — everything else about the order is
    // incidental, so that part alone is faked.
    private static Order AnOrderTotaling(decimal amount)
    {
        var faker = new Faker();
        var item = new OrderItem
        {
            ProductName = faker.Commerce.ProductName(),
            Quantity = 1,
            UnitPrice = new Money(amount, "USD")
        };

        return Order.Create(new CustomerId(faker.Random.Guid()), [item]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ChargeAsync_LowTotal_ApprovesRegardlessOfAttempt(int attempt)
    {
        var gateway = new SimulatedPaymentGateway();
        var order = AnOrderTotaling(100m);

        var approved = await gateway.ChargeAsync(order, attempt);

        approved.Should().BeTrue();
    }

    [Fact]
    public async Task ChargeAsync_MidTotal_FirstAttempt_IsDeclined()
    {
        var gateway = new SimulatedPaymentGateway();
        var order = AnOrderTotaling(500m);

        var approved = await gateway.ChargeAsync(order, attempt: 0);

        approved.Should().BeFalse();
    }

    [Fact]
    public async Task ChargeAsync_MidTotal_OnRetry_IsApproved()
    {
        var gateway = new SimulatedPaymentGateway();
        var order = AnOrderTotaling(500m);

        var approved = await gateway.ChargeAsync(order, attempt: 1);

        approved.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task ChargeAsync_HighTotal_AlwaysDeclined(int attempt)
    {
        var gateway = new SimulatedPaymentGateway();
        var order = AnOrderTotaling(5000m);

        var approved = await gateway.ChargeAsync(order, attempt);

        approved.Should().BeFalse();
    }
}
