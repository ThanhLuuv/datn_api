using BookStore.Api.Models;

namespace BookStore.Api.Services;

public interface IJwtService
{
    string GenerateToken(Account account);
    string GenerateToken(string email, string role);
}
