using Microsoft.EntityFrameworkCore;
using OrderSystem.Adapters.Postgres;
using OrderSystem.Application.Sagas;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Adapters.IntegrationTests.Postgres;

public class PostgresOrderFulfillmentSagaRepositoryTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private OrderDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new OrderDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ThenGetByOrderIdAsync_RoundTripsReservedState()
    {
        var orderId = OrderId.New();
        var state = OrderFulfillmentSagaState.Start(orderId);
        state.MarkStockReserved();

        await using (var writeContext = NewDbContext())
        {
            await new PostgresOrderFulfillmentSagaRepository(writeContext).AddAsync(state);
        }

        // A fresh DbContext forces a real round trip through Postgres rather than
        // returning the same tracked in-memory instance from EF Core's identity map.
        await using var readContext = NewDbContext();
        var loaded = await new PostgresOrderFulfillmentSagaRepository(readContext).GetByOrderIdAsync(orderId);

        loaded.Should().NotBeNull();
        loaded!.OrderId.Should().Be(orderId);
        loaded.IsStockReserved.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_PersistsReleaseAndAdvancesUpdatedAt()
    {
        var orderId = OrderId.New();
        var state = OrderFulfillmentSagaState.Start(orderId);
        state.MarkStockReserved();

        DateTimeOffset reservedAt;

        await using (var writeContext = NewDbContext())
        {
            var repository = new PostgresOrderFulfillmentSagaRepository(writeContext);
            await repository.AddAsync(state);
            reservedAt = state.UpdatedAt;

            state.ReleaseStockIfReserved().Should().BeTrue();
            await repository.UpdateAsync(state);
        }

        await using var readContext = NewDbContext();
        var loaded = await new PostgresOrderFulfillmentSagaRepository(readContext).GetByOrderIdAsync(orderId);

        loaded!.IsStockReserved.Should().BeFalse();
        loaded.UpdatedAt.Should().BeAfter(reservedAt);
    }

    [Fact]
    public async Task GetByOrderIdAsync_UnknownOrder_ReturnsNull()
    {
        await using var dbContext = NewDbContext();
        var loaded = await new PostgresOrderFulfillmentSagaRepository(dbContext).GetByOrderIdAsync(OrderId.New());

        loaded.Should().BeNull();
    }
}
