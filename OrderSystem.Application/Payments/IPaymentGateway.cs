using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Payments;

public interface IPaymentGateway
{
    Task<bool> ChargeAsync(Order order, int attempt, CancellationToken cancellationToken = default);
}
