using OrderSystem.Application.Payments;

namespace OrderSystem.Application.Tests.Fakes;

public class FakePaymentRetryQueue : IPaymentRetryQueue
{
    private readonly List<PaymentRetryMessage> _scheduled = [];

    public IReadOnlyList<PaymentRetryMessage> ScheduledRetries => _scheduled;

    public Task ScheduleRetryAsync(PaymentRetryMessage message, CancellationToken cancellationToken = default)
    {
        _scheduled.Add(message);
        return Task.CompletedTask;
    }
}
