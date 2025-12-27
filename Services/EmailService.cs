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
    public async Task SendOrderInvoiceEmailAsync(string to, BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        try
        {
            var subject = $"Hóa đơn điện tử - Đơn hàng #{order.OrderId} - BookStore";
            var invoiceHtml = GenerateInvoiceHtml(order, invoice);

            var email = new MimeMessage();
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@bookstore.com";
            var fromName = _configuration["EmailSettings:FromName"] ?? "BookStore";
            
            email.From.Add(new MailboxAddress(fromName, fromEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = invoiceHtml,
                TextBody = $"Cảm ơn bạn đã mua hàng tại BookStore. Đơn hàng #{order.OrderId} của bạn đã được thanh toán thành công. Vui lòng xem hóa đơn đính kèm."
            };

            // Attach HTML Invoice file
            var invoiceFileName = $"Invoice_Order_{order.OrderId}_{DateTime.Now:yyyyMMdd}.html";
            bodyBuilder.Attachments.Add(invoiceFileName, System.Text.Encoding.UTF8.GetBytes(invoiceHtml), new ContentType("text", "html"));

            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            
            var host = _configuration["EmailSettings:Host"] ?? _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? _configuration["Email:Smtp:Port"] ?? "587";
            var port = int.Parse(portStr);
            var username = _configuration["EmailSettings:UserName"] ?? _configuration["Email:Credentials:User"];
            var password = _configuration["EmailSettings:Password"] ?? _configuration["Email:Credentials:Password"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                 _logger.LogWarning("Email settings missing for sending invoice.");
                 return; 
            }

            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to send invoice email to {To}", to);
             // Log only, don't throw to avoid blocking the webhook process
        }
    }

    private string GenerateInvoiceHtml(BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        var sb = new System.Text.StringBuilder();
        var date = invoice.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        
        sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 800px; margin: 0 auto; padding: 20px; border: 1px solid #eee; }}
        .header {{ text-align: center; margin-bottom: 30px; border-bottom: 2px solid #28a745; padding-bottom: 10px; }}
        .company-info {{ float: left; text-align: left; }}
        .invoice-info {{ float: right; text-align: right; }}
        .clearfix::after {{ content: ''; clear: both; display: table; }}
        .customer-info {{ margin-top: 30px; background: #f9f9f9; padding: 15px; border-radius: 5px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        th, td {{ padding: 12px; border-bottom: 1px solid #ddd; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        .total-row td {{ font-weight: bold; font-size: 1.1em; }}
        .footer {{ margin-top: 40px; text-align: center; font-size: 0.9em; color: #777; }}
        .success-badge {{ color: #28a745; font-weight: bold; border: 1px solid #28a745; padding: 5px 10px; border-radius: 4px; display: inline-block; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>HÓA ĐƠN ĐIỆN TỬ / INVOICE</h1>
            <div class='success-badge'>ĐÃ THANH TOÁN / PAID</div>
        </div>

        <div class='clearfix'>
            <div class='company-info'>
                <h3>BOOKSTORE</h3>
                <p>Website: bookstore.thanhlaptrinh.online<br>Email: support@bookstore.com</p>
            </div>
            <div class='invoice-info'>
                <p><strong>Số HĐ:</strong> {invoice.InvoiceNumber}</p>
                <p><strong>Ngày:</strong> {date}</p>
                <p><strong>Mã ĐH:</strong> #{order.OrderId}</p>
                <p><strong>Ref:</strong> {invoice.PaymentReference}</p>
            </div>
        </div>

        <div class='customer-info'>
            <h4>Thông Tin Khách Hàng</h4>
            <p><strong>Tên:</strong> {order.ReceiverName}</p>
            <p><strong>SĐT:</strong> {order.ReceiverPhone}</p>
            <p><strong>Địa chỉ:</strong> {order.ShippingAddress}</p>
        </div>

        <table>
            <thead>
                <tr>
                    <th style='width: 5%'>#</th>
                    <th>Sản Phẩm</th>
                    <th style='width: 15%; text-align: right'>Đơn Giá</th>
                    <th style='width: 10%; text-align: center'>SL</th>
                    <th style='width: 20%; text-align: right'>Thành Tiền</th>
                </tr>
            </thead>
            <tbody>");

        int index = 1;
        foreach (var line in order.OrderLines)
        {
            var bookTitle = line.Book?.Title ?? "Book ISBN " + line.Isbn;
            sb.Append($@"
                <tr>
                    <td>{index++}</td>
                    <td>{bookTitle}</td>
                    <td style='text-align: right'>{line.UnitPrice:N0} ₫</td>
                    <td style='text-align: center'>{line.Qty}</td>
                    <td style='text-align: right'>{(line.UnitPrice * line.Qty):N0} ₫</td>
                </tr>");
        }

        sb.Append($@"
                <tr class='total-row'>
                    <td colspan='4' style='text-align: right'>Tổng Cộng:</td>
                    <td style='text-align: right; color: #d9534f'>{invoice.TotalAmount:N0} ₫</td>
                </tr>
            </tbody>
        </table>

        <div class='footer'>
            <p>Cảm ơn quý khách đã mua hàng tại BookStore!</p>
            <p>Đây là hóa đơn điện tử tự động. Mọi thắc mắc vui lòng liên hệ bộ phận CSKH.</p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
