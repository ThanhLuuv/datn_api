using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class EmployeeService : IEmployeeService
{
    private readonly BookStoreDbContext _context;

    public EmployeeService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<EmployeeDto>> CreateEmployeeWithAccountAsync(CreateEmployeeWithAccountRequest request)
    {
        if (request == null)
        {
            return new ApiResponse<EmployeeDto> { Success = false, Message = "Dữ liệu không hợp lệ" };
        }

        // Validate role
        var role = await _context.Roles.FindAsync(request.RoleId);
        if (role == null)
        {
            return new ApiResponse<EmployeeDto>
            {
                Success = false,
                Message = "Role không tồn tại",
                Errors = new List<string> { "RoleId không hợp lệ" }
            };
        }

        // Validate department
        var department = await _context.Departments.FindAsync(request.DepartmentId);
        if (department == null)
        {
            return new ApiResponse<EmployeeDto>
            {
                Success = false,
                Message = "Phòng ban không tồn tại",
                Errors = new List<string> { "DepartmentId không hợp lệ" }
            };
        }

        // Validate account email unique
        var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == request.AccountEmail);
        if (existingAccount != null)
        {
            return new ApiResponse<EmployeeDto>
            {
                Success = false,
                Message = "Email tài khoản đã tồn tại",
                Errors = new List<string> { "AccountEmail đã được sử dụng" }
            };
        }

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var account = new Account
            {
                Email = request.AccountEmail,
                PasswordHash = passwordHash,
                RoleId = request.RoleId,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var genderEnum = ParseGender(request.Gender);

            var employee = new Employee
            {
                AccountId = account.AccountId,
                DepartmentId = request.DepartmentId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Gender = genderEnum,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                Phone = request.Phone,
                Email = request.EmployeeEmail,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // Assign areas if provided
            if (request.AreaIds != null && request.AreaIds.Any())
            {
                var validAreaIds = await _context.Areas
                    .Where(a => request.AreaIds.Contains(a.AreaId))
                    .Select(a => a.AreaId)
                    .ToListAsync();

                foreach (var areaId in validAreaIds)
                {
                    var employeeArea = new EmployeeArea
                    {
                        EmployeeId = employee.EmployeeId,
                        AreaId = areaId,
                        IsActive = true,
                        AssignedAt = DateTime.UtcNow
                    };
                    _context.EmployeeAreas.Add(employeeArea);
                }
                await _context.SaveChangesAsync();
            }

            await _context.Entry(employee).Reference(e => e.Department).LoadAsync();
            await _context.Entry(account).Reference(a => a.Role).LoadAsync();

            await tx.CommitAsync();

            return new ApiResponse<EmployeeDto>
            {
                Success = true,
                Message = "Tạo nhân viên và tài khoản thành công",
                Data = new EmployeeDto
                {
                    EmployeeId = employee.EmployeeId,
                    AccountId = employee.AccountId,
                    DepartmentId = employee.DepartmentId,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Gender = employee.Gender.ToString(),
                    DateOfBirth = employee.DateOfBirth,
                    Address = employee.Address,
                    Phone = employee.Phone,
                    Email = employee.Email,
                    DepartmentName = department.Name,
                    AccountEmail = account.Email,
                    RoleId = account.RoleId,
                    CreatedAt = employee.CreatedAt,
                    UpdatedAt = employee.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return new ApiResponse<EmployeeDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo nhân viên",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(long employeeId)
    {
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Account)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee == null)
        {
            return new ApiResponse<EmployeeDto> { Success = false, Message = "Không tìm thấy nhân viên" };
        }

        var dto = new EmployeeDto
        {
            EmployeeId = employee.EmployeeId,
            AccountId = employee.AccountId,
            DepartmentId = employee.DepartmentId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Gender = employee.Gender.ToString(),
            DateOfBirth = employee.DateOfBirth,
            Address = employee.Address,
            Phone = employee.Phone,
            Email = employee.Email,
            DepartmentName = employee.Department?.Name ?? string.Empty,
            AccountEmail = employee.Account?.Email ?? string.Empty,
            RoleId = employee.Account?.RoleId ?? 0,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };

        return new ApiResponse<EmployeeDto> { Success = true, Message = "OK", Data = dto };
    }

    public async Task<ApiResponse<EmployeeListResponse>> GetEmployeesAsync(int pageNumber, int pageSize, string? searchTerm = null, long? departmentId = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 10;

        var query = _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Account)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) ||
                e.LastName.ToLower().Contains(term) ||
                (e.Email != null && e.Email.ToLower().Contains(term)) ||
                (e.Account != null && e.Account.Email.ToLower().Contains(term))
            );
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var employees = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = employees.Select(e => new EmployeeDto
        {
            EmployeeId = e.EmployeeId,
            AccountId = e.AccountId,
            DepartmentId = e.DepartmentId,
            FirstName = e.FirstName,
            LastName = e.LastName,
            Gender = e.Gender.ToString(),
            DateOfBirth = e.DateOfBirth,
            Address = e.Address,
            Phone = e.Phone,
            Email = e.Email,
            DepartmentName = e.Department?.Name ?? string.Empty,
            AccountEmail = e.Account?.Email ?? string.Empty,
            RoleId = e.Account?.RoleId ?? 0,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        }).ToList();

        return new ApiResponse<EmployeeListResponse>
        {
            Success = true,
            Message = "OK",
            Data = new EmployeeListResponse
            {
                Employees = list,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            }
        };
    }

    public async Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(long employeeId, UpdateEmployeeDto request)
    {
        var employee = await _context.Employees.Include(e => e.Department).Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee == null)
        {
            return new ApiResponse<EmployeeDto> { Success = false, Message = "Không tìm thấy nhân viên" };
        }

        var department = await _context.Departments.FindAsync(request.DepartmentId);
        if (department == null)
        {
            return new ApiResponse<EmployeeDto> { Success = false, Message = "Phòng ban không tồn tại" };
        }

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.Gender = ParseGender(request.Gender);
        employee.DateOfBirth = request.DateOfBirth;
        employee.Address = request.Address;
        employee.Phone = request.Phone;
        employee.Email = request.EmployeeEmail;
        employee.DepartmentId = request.DepartmentId;
        employee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new EmployeeDto
        {
            EmployeeId = employee.EmployeeId,
            AccountId = employee.AccountId,
            DepartmentId = employee.DepartmentId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Gender = employee.Gender.ToString(),
            DateOfBirth = employee.DateOfBirth,
            Address = employee.Address,
            Phone = employee.Phone,
            Email = employee.Email,
            DepartmentName = department.Name,
            AccountEmail = employee.Account?.Email ?? string.Empty,
            RoleId = employee.Account?.RoleId ?? 0,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };

        return new ApiResponse<EmployeeDto> { Success = true, Message = "Cập nhật thành công", Data = dto };
    }

    public async Task<ApiResponse<bool>> DeleteEmployeeAsync(long employeeId)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee == null)
        {
            return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy nhân viên", Data = false };
        }

        // Optionally also delete or deactivate the account. For safety, only remove employee and keep account.
        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();
        return new ApiResponse<bool> { Success = true, Message = "Đã xóa nhân viên", Data = true };
    }

    private static Gender ParseGender(string gender)
    {
        if (Enum.TryParse<Gender>(gender, true, out var g)) return g;
        return Gender.Other;
    }
}






