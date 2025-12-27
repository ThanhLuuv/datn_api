using BookStore.Api.Models;

namespace BookStore.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendOrderInvoiceEmailAsync(string to, Order order, Invoice invoice);
}
