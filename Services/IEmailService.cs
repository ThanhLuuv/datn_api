using System.Threading.Tasks;

namespace BookStore.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}
