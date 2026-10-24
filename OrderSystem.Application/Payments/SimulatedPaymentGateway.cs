using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Payments;

public class SimulatedPaymentGateway : IPaymentGateway
{
    public Task<bool> ChargeAsync(Order order, int attempt, CancellationToken cancellationToken = default)
    {
        var total = order.Items.Sum(item => item.UnitPrice.Amount * item.Quantity);

        // No real gateway is wired up yet; thresholds stand in for three outcomes a
        // real processor would produce, so the retry path has something to exercise:
        // small orders clear immediately, mid-size orders recover on the first retry
        // (a transient decline), and large orders are a hard, permanent decline.
        var approved = total switch
        {
            <= 100m => true,
            <= 1000m => attempt >= 1,
            _ => false
        };

        return Task.FromResult(approved);
    }
}
