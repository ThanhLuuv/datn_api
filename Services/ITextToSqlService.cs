using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface ITextToSqlService
{
    Task<ApiResponse<TextToSqlResponse>> AskAsync(TextToSqlRequest request, CancellationToken cancellationToken = default);
}


