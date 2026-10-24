using Microsoft.EntityFrameworkCore;
using OrderSystem.Application.Sagas;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Adapters.Postgres;

public class PostgresOrderFulfillmentSagaRepository(OrderDbContext dbContext) : IOrderFulfillmentSagaRepository
{
    public Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        return dbContext.OrderFulfillmentSagas.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
    }

    public async Task AddAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default)
    {
        dbContext.OrderFulfillmentSagas.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default)
    {
        dbContext.OrderFulfillmentSagas.Update(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
