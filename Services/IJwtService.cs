using BookStore.Api.Models;
using System.Collections.Generic;

namespace BookStore.Api.Services;

public interface IJwtService
{
	string GenerateToken(Account account, IEnumerable<string>? permissions = null);
	string GenerateToken(string email, string role, long accountId = 0, IEnumerable<string>? permissions = null);
}
