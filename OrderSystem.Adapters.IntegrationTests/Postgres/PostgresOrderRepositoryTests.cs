using Bogus;
using Microsoft.EntityFrameworkCore;
using OrderSystem.Adapters.Postgres;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Adapters.IntegrationTests.Postgres;

public class PostgresOrderRepositoryTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private OrderDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new OrderDbContext(options);
    }

    private static CustomerId ACustomerId() => new(new Faker().Random.Guid());

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
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsOrderIncludingJsonItems()
    {
        var item = AnItem();
        var order = Order.Create(ACustomerId(), [item]);

        await using (var writeContext = NewDbContext())
        {
            await new PostgresOrderRepository(writeContext).AddAsync(order);
        }

        // A fresh DbContext forces a real round trip through Postgres rather than
        // returning the same tracked in-memory instance from EF Core's identity map.
        await using var readContext = NewDbContext();
        var loaded = await new PostgresOrderRepository(readContext).GetByIdAsync(order.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(order.Id);
        loaded.CustomerId.Should().Be(order.CustomerId);
        loaded.Status.Should().Be(OrderStatus.Created);
        loaded.Items.Should().ContainSingle();
        loaded.Items[0].ProductName.Should().Be(item.ProductName);
        loaded.Items[0].Quantity.Should().Be(item.Quantity);
        loaded.Items[0].UnitPrice.Should().Be(item.UnitPrice);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        var order = Order.Create(ACustomerId(), [AnItem()]);

        await using (var writeContext = NewDbContext())
        {
            var repository = new PostgresOrderRepository(writeContext);
            await repository.AddAsync(order);

            order.Cancel();
            await repository.UpdateAsync(order);
        }

        await using var readContext = NewDbContext();
        var loaded = await new PostgresOrderRepository(readContext).GetByIdAsync(order.Id);

        loaded!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOnlyOrdersForRequestedCustomer()
    {
        var customerId = ACustomerId();
        var otherCustomerId = ACustomerId();

        await using (var writeContext = NewDbContext())
        {
            var repository = new PostgresOrderRepository(writeContext);
            await repository.AddAsync(Order.Create(customerId, [AnItem()]));
            await repository.AddAsync(Order.Create(otherCustomerId, [AnItem()]));
        }

        await using var readContext = NewDbContext();
        var history = await new PostgresOrderRepository(readContext).GetHistoryAsync(customerId);

        history.Should().ContainSingle().Which.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownOrder_ReturnsNull()
    {
        await using var dbContext = NewDbContext();
        var loaded = await new PostgresOrderRepository(dbContext).GetByIdAsync(OrderId.New());

        loaded.Should().BeNull();
    }
}
