using Microsoft.EntityFrameworkCore;
using OrderSystem.Application.Sagas;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Adapters.Postgres;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderFulfillmentSagaState> OrderFulfillmentSagas => Set<OrderFulfillmentSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");

            order.HasKey(o => o.Id);
            order.Property(o => o.Id)
                .HasConversion(id => id.Value, value => new OrderId(value))
                .ValueGeneratedNever();

            order.Property(o => o.CustomerId)
                .HasConversion(id => id.Value, value => new CustomerId(value));

            order.Property(o => o.Status)
                .HasConversion<string>();

            order.OwnsMany(o => o.Items, items =>
            {
                items.ToJson();
                items.OwnsOne(i => i.UnitPrice);
            });
        });

        modelBuilder.Entity<OrderFulfillmentSagaState>(saga =>
        {
            saga.ToTable("order_fulfillment_sagas");

            saga.HasKey(s => s.OrderId);
            saga.Property(s => s.OrderId)
                .HasConversion(id => id.Value, value => new OrderId(value))
                .ValueGeneratedNever();

            saga.Property(s => s.IsStockReserved);
            saga.Property(s => s.UpdatedAt);
        });
    }
}
