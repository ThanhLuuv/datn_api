using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookStore.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
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
            
            var host = _configuration["EmailSettings:Host"] ?? _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? _configuration["Email:Smtp:Port"] ?? "587";
            var port = int.Parse(portStr);
            var username = _configuration["EmailSettings:UserName"] ?? _configuration["Email:Credentials:User"];
            var password = _configuration["EmailSettings:Password"] ?? _configuration["Email:Credentials:Password"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                 _logger.LogWarning("Email settings missing.");
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
            throw;
        }
    }

    public async Task SendOrderInvoiceEmailAsync(string to, BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        try
        {
            var subject = $"Hóa đơn điện tử - Đơn hàng #{order.OrderId} - BookStore";
            
            // Generate content
            var invoiceHtml = GenerateInvoiceHtmlBody(order, invoice);
            var invoicePdf = GenerateInvoicePdf(order, invoice);

            var email = new MimeMessage();
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@bookstore.com";
            var fromName = _configuration["EmailSettings:FromName"] ?? "BookStore";
            
            email.From.Add(new MailboxAddress(fromName, fromEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = invoiceHtml,
                TextBody = $"Cảm ơn bạn đã mua hàng tại BookStore. Đơn hàng #{order.OrderId} của bạn đã được thanh toán thành công. Vui lòng xem hóa đơn PDF đính kèm."
            };

            // Attach PDF Invoice file
            var invoiceFileName = $"Invoice_Order_{order.OrderId}_{DateTime.Now:yyyyMMdd}.pdf";
            bodyBuilder.Attachments.Add(invoiceFileName, invoicePdf, new ContentType("application", "pdf"));

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
        }
    }

    private string GenerateInvoiceHtmlBody(BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        // Simple HTML body focusing on notification
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .header {{ color: #28a745; }}
        .info {{ background: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ font-size: 0.9em; color: #777; }}
    </style>
</head>
<body>
    <h2 class='header'>Thanh Toán Thành Công!</h2>
    <p>Xin chào <strong>{order.ReceiverName}</strong>,</p>
    <p>Cảm ơn bạn đã mua hàng tại BookStore. Đơn hàng <strong>#{order.OrderId}</strong> của bạn đã được thanh toán thành công.</p>
    
    <div class='info'>
        <p><strong>Mã Hóa Đơn:</strong> {invoice.InvoiceNumber}</p>
        <p><strong>Tổng Tiền:</strong> {invoice.TotalAmount:N0} ₫</p>
        <p><strong>Trạng Thái:</strong> ĐÃ THANH TOÁN (PAID)</p>
    </div>

    <p>Chi tiết hóa đơn đầy đủ đã được đính kèm trong file PDF theo email này.</p>
    
    <div class='footer'>
        <hr>
        <p>BookStore - support@bookstore.com</p>
    </div>
</body>
</html>";
    }

    private byte[] GenerateInvoicePdf(BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, order, invoice));
                page.Footer().Element(ComposeFooter);
            });
        })
        .GeneratePdf();
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("BOOKSTORE").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                column.Item().Text("Website: bookstore.thanhlaptrinh.online");
                column.Item().Text("Email: support@bookstore.com");
            });

            row.ConstantItem(150).Column(column =>
            {
                column.Item().Text("HÓA ĐƠN GTGT").FontSize(16).SemiBold().AlignRight();
                column.Item().Text("INVOICE").FontSize(12).AlignRight();
                
                column.Item().PaddingTop(5).Text("ĐÃ THANH TOÁN").FontSize(12).FontColor(Colors.Green.Medium).Bold().AlignRight();
            });
        });
    }

    void ComposeContent(IContainer container, BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        container.PaddingVertical(20).Column(column =>
        {
            // Info Row
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Thông Tin Khách Hàng").Bold();
                    col.Item().Text(order.ReceiverName);
                    col.Item().Text(order.ReceiverPhone);
                    col.Item().Text(order.ShippingAddress);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("Thông Tin Hóa Đơn").Bold();
                    col.Item().Text($"Số: {invoice.InvoiceNumber}");
                    col.Item().Text($"Ngày: {invoice.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}");
                    col.Item().Text($"Đơn hàng: #{order.OrderId}");
                    col.Item().Text($"Ref: {invoice.PaymentReference}");
                });
            });

            column.Item().PaddingTop(20).Element(c => ComposeTable(c, order, invoice));
            
            column.Item().PaddingTop(25).Row(row => 
            {
                 row.RelativeItem().Column(c => c.Item().Text("Người mua hàng").AlignCenter());
                 row.RelativeItem().Column(c => c.Item().Text("Người bán hàng").AlignCenter());
            });
        });
    }

    void ComposeTable(IContainer container, BookStore.Api.Models.Order order, BookStore.Api.Models.Invoice invoice)
    {
        container.Table(table =>
        {
            // Definition
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(25);
                columns.RelativeColumn(3);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            // Header
            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("#");
                header.Cell().Element(CellStyle).Text("Sản Phẩm");
                header.Cell().Element(CellStyle).AlignRight().Text("Đơn Giá");
                header.Cell().Element(CellStyle).AlignCenter().Text("SL");
                header.Cell().Element(CellStyle).AlignRight().Text("Thành Tiền");

                static IContainer CellStyle(IContainer container)
                {
                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                }
            });

            // Content
            int index = 1;
            foreach (var line in order.OrderLines)
            {
                var bookTitle = line.Book?.Title ?? "Book ISBN " + line.Isbn;
                
                table.Cell().Element(CellStyle).Text($"{index++}");
                table.Cell().Element(CellStyle).Text(bookTitle);
                table.Cell().Element(CellStyle).AlignRight().Text($"{line.UnitPrice:N0} ₫");
                table.Cell().Element(CellStyle).AlignCenter().Text($"{line.Qty}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{(line.UnitPrice * line.Qty):N0} ₫");

                static IContainer CellStyle(IContainer container)
                {
                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            }
            
             // Total
            table.Cell().ColumnSpan(4).AlignRight().PaddingTop(10).Text("Tổng Cộng:").Bold().FontSize(12);
            table.Cell().AlignRight().PaddingTop(10).Text($"{invoice.TotalAmount:N0} ₫").Bold().FontSize(12).FontColor(Colors.Red.Medium);
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2);
            column.Item().AlignCenter().Text(x =>
            {
                x.Span("BookStore System - Generated via QuestPDF");
            });
        });
    }
    public async Task SendDeliverySuccessEmailAsync(string to, BookStore.Api.Models.Order order)
    {
        try 
        {
            var subject = $"Giao Hàng Thành Công - Đơn hàng #{order.OrderId} - BookStore";
            var email = new MimeMessage();
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@bookstore.com";
            var fromName = _configuration["EmailSettings:FromName"] ?? "BookStore";
            
            email.From.Add(new MailboxAddress(fromName, fromEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder
            {
                TextBody = $"Đơn hàng #{order.OrderId} đã giao thành công. Cảm ơn bạn đã mua hàng."
            };
            
            var imageUrl = order.DeliveryProofImageUrl;
            string? imageCid = null;

            if (!string.IsNullOrEmpty(imageUrl))
            {
                 try 
                 {
                    using (var client = new HttpClient())
                    {
                        var imageBytes = await client.GetByteArrayAsync(imageUrl);
                        var attachment = builder.Attachments.Add("delivery_proof.jpg", imageBytes, new ContentType("image", "jpeg"));
                        attachment.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
                        attachment.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                        imageCid = attachment.ContentId;
                    }
                 }
                 catch(Exception ex)
                 {
                     _logger.LogWarning($"Failed to download proof image: {ex.Message}");
                 }
            }
            
            builder.HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .header {{ color: #28a745; text-align: center; }}
        .content {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .image-container {{ text-align: center; margin-top: 20px; }}
        .image-container img {{ max-width: 100%; border: 1px solid #ddd; padding: 5px; border-radius: 5px; }}
        .btn {{ display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='content'>
        <h2 class='header'>Giao Hàng Thành Công!</h2>
        <p>Xin chào <strong>{order.ReceiverName}</strong>,</p>
        <p>Kiện hàng thuộc đơn hàng <strong>#{order.OrderId}</strong> đã được giao thành công đến địa chỉ:</p>
        <p><i>{order.ShippingAddress}</i></p>
        
        {(imageCid != null ? 
            $"<div class='image-container'><p><strong>Ảnh xác thực giao hàng:</strong></p><img src='cid:{imageCid}' alt='Delivery Proof' /></div>" : 
            (!string.IsNullOrEmpty(imageUrl) ? $"<p>Xem ảnh xác thực: <a href='{imageUrl}'>Tại đây</a></p>" : "")
        )}

        <p>Cảm ơn bạn đã tin tưởng và mua sắm tại BookStore!</p>
        <div style='text-align: center;'>
            <a href='https://bookstore.thanhlaptrinh.online' class='btn'>Tiếp tục mua sắm</a>
        </div>
    </div>
</body>
</html>";

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            var host = _configuration["EmailSettings:Host"] ?? _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? _configuration["Email:Smtp:Port"] ?? "587";
            var port = int.Parse(portStr);
            var username = _configuration["EmailSettings:UserName"] ?? _configuration["Email:Credentials:User"];
            var password = _configuration["EmailSettings:Password"] ?? _configuration["Email:Credentials:Password"];

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(username, password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to send delivery success email to {To}", to);
        }
    }
}
