using Bogus;
using OrderSystem.Application.Queries;
using OrderSystem.Application.Tests.Fakes;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Tests.Queries;

public class GetOrderStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingOrder_ReturnsItsStatus()
    {
        var faker = new Faker();
        var item = new OrderItem
        {
            ProductName = faker.Commerce.ProductName(),
            Quantity = faker.Random.Int(1, 10),
            UnitPrice = new Money(faker.Finance.Amount(1, 200), "USD")
        };

        var repository = new FakeOrderRepository();
        var order = Order.Create(new CustomerId(faker.Random.Guid()), [item]);
        repository.Seed(order);

        var handler = new GetOrderStatusQueryHandler(repository);

        var status = await handler.Handle(new GetOrderStatusQuery(order.Id), CancellationToken.None);

        status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsKeyNotFound()
    {
        var handler = new GetOrderStatusQueryHandler(new FakeOrderRepository());

        var act = () => handler.Handle(new GetOrderStatusQuery(OrderId.New()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
