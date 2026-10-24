using Bogus;
using OrderSystem.Application.Queries;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Queries;

public class GetOrderHistoryQueryHandlerTests
{
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
    public async Task Handle_ReturnsOnlyOrdersForRequestedCustomer()
    {
        var repository = new FakeOrderRepository();
        var customerId = new CustomerId(new Faker().Random.Guid());
        var otherCustomerId = new CustomerId(new Faker().Random.Guid());

        var ownOrder = Order.Create(customerId, [AnItem()]);
        var otherOrder = Order.Create(otherCustomerId, [AnItem()]);
        repository.Seed(ownOrder);
        repository.Seed(otherOrder);

        var handler = new GetOrderHistoryQueryHandler(repository);

        var history = await handler.Handle(new GetOrderHistoryQuery(customerId), CancellationToken.None);

        history.Should().ContainSingle().Which.Id.Should().Be(ownOrder.Id);
    }

    [Fact]
    public async Task Handle_NoOrdersForCustomer_ReturnsEmpty()
    {
        var handler = new GetOrderHistoryQueryHandler(new FakeOrderRepository());

        var history = await handler.Handle(new GetOrderHistoryQuery(new CustomerId(new Faker().Random.Guid())), CancellationToken.None);

        history.Should().BeEmpty();
    }
}
