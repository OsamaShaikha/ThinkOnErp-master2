namespace ThinkOnErp.Domain.Interfaces;

public interface ITicketEmailQueue
{
    Task EnqueueAsync(string recipient, string subject, string body);
}
