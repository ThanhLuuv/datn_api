using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IReturnService
{
    Task<ApiResponse<ReturnDto>> CreateReturnAsync(CreateReturnDto request);
    Task<ApiResponse<ReturnListResponse>> GetReturnsAsync(ReturnSearchRequest request);
    Task<ApiResponse<ReturnDto>> GetReturnByIdAsync(long returnId);
    Task<ApiResponse<ReturnDto>> UpdateReturnStatusAsync(long returnId, UpdateReturnStatusRequest request, string processorEmail);
}


