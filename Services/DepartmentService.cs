using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class DepartmentService : IDepartmentService
{
    private readonly BookStoreDbContext _context;

    public DepartmentService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<DepartmentListResponse>> GetDepartmentsAsync(int pageNumber, int pageSize, string? searchTerm = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 10;

        var query = _context.Departments
            .Include(d => d.Employees)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(d => d.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var departments = await query
            .OrderBy(d => d.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = departments.Select(d => new DepartmentDto
        {
            DepartmentId = d.DepartmentId,
            Name = d.Name,
            Description = d.Description,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            EmployeeCount = d.Employees?.Count ?? 0
        }).ToList();

        return new ApiResponse<DepartmentListResponse>
        {
            Success = true,
            Message = "OK",
            Data = new DepartmentListResponse
            {
                Departments = list,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            }
        };
    }

    public async Task<ApiResponse<DepartmentDto>> GetDepartmentByIdAsync(long departmentId)
    {
        var d = await _context.Departments.Include(x => x.Employees).FirstOrDefaultAsync(x => x.DepartmentId == departmentId);
        if (d == null)
        {
            return new ApiResponse<DepartmentDto> { Success = false, Message = "Không tìm thấy phòng ban" };
        }

        var dto = new DepartmentDto
        {
            DepartmentId = d.DepartmentId,
            Name = d.Name,
            Description = d.Description,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            EmployeeCount = d.Employees?.Count ?? 0
        };
        return new ApiResponse<DepartmentDto> { Success = true, Message = "OK", Data = dto };
    }

    public async Task<ApiResponse<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentDto request)
    {
        var exists = await _context.Departments.AnyAsync(d => d.Name == request.Name);
        if (exists)
        {
            return new ApiResponse<DepartmentDto> { Success = false, Message = "Tên phòng ban đã tồn tại" };
        }

        var d = new Department
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Departments.Add(d);
        await _context.SaveChangesAsync();

        return await GetDepartmentByIdAsync(d.DepartmentId);
    }

    public async Task<ApiResponse<DepartmentDto>> UpdateDepartmentAsync(long departmentId, UpdateDepartmentDto request)
    {
        var d = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == departmentId);
        if (d == null)
        {
            return new ApiResponse<DepartmentDto> { Success = false, Message = "Không tìm thấy phòng ban" };
        }

        d.Name = request.Name;
        d.Description = request.Description;
        d.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetDepartmentByIdAsync(d.DepartmentId);
    }

    public async Task<ApiResponse<bool>> DeleteDepartmentAsync(long departmentId)
    {
        var d = await _context.Departments.Include(x => x.Employees).FirstOrDefaultAsync(x => x.DepartmentId == departmentId);
        if (d == null)
        {
            return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy phòng ban", Data = false };
        }

        if (d.Employees != null && d.Employees.Any())
        {
            return new ApiResponse<bool> { Success = false, Message = "Không thể xóa phòng ban có nhân viên", Data = false };
        }

        _context.Departments.Remove(d);
        await _context.SaveChangesAsync();
        return new ApiResponse<bool> { Success = true, Message = "Đã xóa phòng ban", Data = true };
    }
}






