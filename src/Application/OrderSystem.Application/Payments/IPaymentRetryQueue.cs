namespace OrderSystem.Application.Payments;

public interface IPaymentRetryQueue
{
    Task ScheduleRetryAsync(PaymentRetryMessage message, CancellationToken cancellationToken = default);
}
