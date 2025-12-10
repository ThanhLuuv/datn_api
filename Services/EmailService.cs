using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace BookStore.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var email = new MimeMessage();
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@bookstore.com";
            var fromName = _configuration["EmailSettings:FromName"] ?? "BookStore";
            
            email.From.Add(new MailboxAddress(fromName, fromEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = body };

            using var smtp = new SmtpClient();
            
            // Try EmailSettings first (standard), then Email (legacy from OrderService)
            var host = _configuration["EmailSettings:Host"] ?? _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? _configuration["Email:Smtp:Port"] ?? "587";
            var port = int.Parse(portStr);
            var username = _configuration["EmailSettings:UserName"] ?? _configuration["Email:Credentials:User"];
            var password = _configuration["EmailSettings:Password"] ?? _configuration["Email:Credentials:Password"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                 // Log warning and fallback to simulation
                 _logger.LogWarning("Email settings missing (checked both EmailSettings and Email:Credentials).");
                 _logger.LogInformation("=== SIMULATED EMAIL ===");
                 _logger.LogInformation("To: {0}", to);
                 _logger.LogInformation("Subject: {0}", subject);
                 _logger.LogInformation("Body: {0}", body);
                 _logger.LogInformation("=======================");
                 return; 
            }

            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            // Don't throw to avoid breaking the user flow, just log. 
            // Or should we throw? For forgot password, if email fails, user can't reset.
            // I'll throw so AuthService can handle it and tell user.
            throw;
        }
    }
}
