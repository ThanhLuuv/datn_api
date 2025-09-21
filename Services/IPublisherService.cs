using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IPublisherService
{
    Task<ApiResponse<PublisherListResponse>> GetPublishersAsync(int pageNumber, int pageSize, string? searchTerm = null);
    Task<ApiResponse<PublisherDto>> GetPublisherByIdAsync(long publisherId);
    Task<ApiResponse<PublisherDto>> CreatePublisherAsync(CreatePublisherDto createPublisherDto);
    Task<ApiResponse<PublisherDto>> UpdatePublisherAsync(long publisherId, UpdatePublisherDto updatePublisherDto);
    Task<ApiResponse<bool>> DeletePublisherAsync(long publisherId);
}
