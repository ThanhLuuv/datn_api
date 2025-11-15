using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IEmployeeService
{
    Task<ApiResponse<EmployeeDto>> CreateEmployeeWithAccountAsync(CreateEmployeeWithAccountRequest request);
    Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(long employeeId);
    Task<ApiResponse<EmployeeListResponse>> GetEmployeesAsync(int pageNumber, int pageSize, string? searchTerm = null, long? departmentId = null);
    Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(long employeeId, UpdateEmployeeDto request);
    Task<ApiResponse<bool>> DeleteEmployeeAsync(long employeeId);
}







