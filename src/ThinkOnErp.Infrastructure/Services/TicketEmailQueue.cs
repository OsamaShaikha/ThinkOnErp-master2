using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Durable single-host mail spool. Atomic file replacement preserves pending
/// messages across restarts; a directory lock prevents competing dispatchers.
/// </summary>
public sealed class TicketEmailQueue : BackgroundService, ITicketEmailQueue
{
    private readonly IConfiguration configuration;
    private readonly ILogger<TicketEmailQueue> logger;
    private readonly string directory;
    public TicketEmailQueue(IConfiguration configuration, ILogger<TicketEmailQueue> logger)
    {
        this.configuration = configuration; this.logger = logger;
        directory = Path.GetFullPath(configuration["Notifications:QueueDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "ticket-mail"));
        Directory.CreateDirectory(directory);
    }

    public sealed record Message(string Recipient, string Subject, string Body, int Attempts = 0,
        DateTime? NextAttemptUtc = null, DateTime? SentUtc = null, string? LastError = null);

    public async Task EnqueueAsync(string recipient, string subject, string body)
    {
        _ = MailboxAddress.Parse(recipient);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{recipient.ToLowerInvariant()}\n{subject}\n{body}")));
        var path = Path.Combine(directory, id + ".json");
        if (File.Exists(path)) return; // Includes sent messages: deduplicate repeated escalation checks.
        var temp = Path.Combine(directory, Guid.NewGuid() + ".tmp");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new Message(recipient, subject, body)));
        try { File.Move(temp, path, false); }
        catch (IOException) when (File.Exists(path)) { File.Delete(temp); }
    }

    public (int Pending, int Failed, int Sent, bool Configured) GetStatus()
    {
        var messages = new List<Message>(); var unreadable = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try { var message = JsonSerializer.Deserialize<Message>(File.ReadAllText(path)); if (message != null) messages.Add(message); else unreadable++; }
            catch (Exception ex) when (ex is JsonException or IOException) { unreadable++; }
        }
        unreadable += Directory.EnumerateFiles(directory, "*.invalid").Count();
        return (messages.Count(m => m.SentUtc == null), messages.Count(m => m.SentUtc == null && m.Attempts > 0) + unreadable,
            messages.Count(m => m.SentUtc != null), IsConfigured());
    }

    private bool IsConfigured() => !string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Server"]) &&
        !string.IsNullOrWhiteSpace(configuration["Notifications:FromEmail"]);

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured()) { logger.LogWarning("Ticket mail delivery is pending: SMTP server/from address are not configured"); return; }
        FileStream lease;
        try { lease = new FileStream(Path.Combine(directory, ".dispatch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return; }
        using (lease)
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Message message;
            try { message = JsonSerializer.Deserialize<Message>(await File.ReadAllTextAsync(path, cancellationToken)) ?? throw new JsonException("Empty mail record"); }
            catch (JsonException ex) {
                logger.LogError(ex, "Invalid ticket mail record quarantined; other messages will continue");
                File.Move(path, path + ".invalid", true);
                continue;
            }
            if (message.SentUtc.HasValue || message.NextAttemptUtc > DateTime.UtcNow) continue;
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(configuration["Notifications:FromName"] ?? "ThinkOnERP Support", configuration["Notifications:FromEmail"]));
                email.To.Add(MailboxAddress.Parse(message.Recipient));
                email.Subject = message.Subject; email.Body = new TextPart("plain") { Text = message.Body };
                using var smtp = new SmtpClient { Timeout = 15000 };
                var security = configuration["Notifications:Smtp:Security"]?.ToUpperInvariant() switch {
                    "SSL" => SecureSocketOptions.SslOnConnect,
                    "NONE" when configuration.GetValue<bool>("Notifications:Smtp:AllowInsecureLocal", false) &&
                        configuration["Notifications:Smtp:Server"] is "localhost" or "127.0.0.1" => SecureSocketOptions.None,
                    _ => SecureSocketOptions.StartTls };
                await smtp.ConnectAsync(configuration["Notifications:Smtp:Server"], configuration.GetValue<int>("Notifications:Smtp:Port", 587), security, cancellationToken);
                if (!string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Username"]))
                    await smtp.AuthenticateAsync(configuration["Notifications:Smtp:Username"], configuration["Notifications:Smtp:Password"], cancellationToken);
                await smtp.SendAsync(email, cancellationToken);
                await smtp.DisconnectAsync(true, cancellationToken);
                message = message with { Attempts = message.Attempts + 1, SentUtc = DateTime.UtcNow, LastError = null };
                logger.LogInformation("Ticket mail accepted by SMTP for {Recipient}", message.Recipient);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                message = message with { Attempts = message.Attempts + 1,
                    NextAttemptUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(message.Attempts, 6)))), LastError = ex.GetType().Name };
                logger.LogError(ex, "Ticket mail delivery failed; message retained for retry");
            }
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(message), cancellationToken);
            File.Move(temp, path, true);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Ticket mail dispatcher failed; pending files retained"); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
