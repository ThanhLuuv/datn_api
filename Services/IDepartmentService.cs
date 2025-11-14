using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IDepartmentService
{
    Task<ApiResponse<DepartmentListResponse>> GetDepartmentsAsync(int pageNumber, int pageSize, string? searchTerm = null);
    Task<ApiResponse<DepartmentDto>> GetDepartmentByIdAsync(long departmentId);
    Task<ApiResponse<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentDto request);
    Task<ApiResponse<DepartmentDto>> UpdateDepartmentAsync(long departmentId, UpdateDepartmentDto request);
    Task<ApiResponse<bool>> DeleteDepartmentAsync(long departmentId);
}






