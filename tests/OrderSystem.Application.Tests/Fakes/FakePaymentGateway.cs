using OrderSystem.Application.Payments;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Tests.Fakes;

public class FakePaymentGateway(Func<int, bool> decide) : IPaymentGateway
{
    private readonly List<int> _attempts = [];

    public IReadOnlyList<int> Attempts => _attempts;

    public static FakePaymentGateway AlwaysApproves() => new(_ => true);

    public static FakePaymentGateway AlwaysDeclines() => new(_ => false);

    public Task<bool> ChargeAsync(Order order, int attempt, CancellationToken cancellationToken = default)
    {
        _attempts.Add(attempt);
        return Task.FromResult(decide(attempt));
    }
}
