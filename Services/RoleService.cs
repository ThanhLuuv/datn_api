using BookStore.Api.Data;
using BookStore.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class RoleService : IRoleService
{
	private readonly BookStoreDbContext _context;

	public RoleService(BookStoreDbContext context)
	{
		_context = context;
	}

	public async Task<ApiResponse<List<RoleDto>>> GetRolesAsync()
	{
		var roles = await _context.Roles
			.Select(r => new RoleDto { RoleId = r.RoleId, Name = r.Name, Description = r.Description })
			.ToListAsync();
		return new ApiResponse<List<RoleDto>> { Success = true, Message = "OK", Data = roles };
	}

	public async Task<ApiResponse<List<PermissionDto>>> GetPermissionsAsync()
	{
		var perms = await _context.Permissions
			.Select(p => new PermissionDto { PermissionId = p.PermissionId, Code = p.Code, Name = p.Name, Description = p.Description })
			.ToListAsync();
		return new ApiResponse<List<PermissionDto>> { Success = true, Message = "OK", Data = perms };
	}

	public async Task<ApiResponse<RoleDto>> GetRoleWithPermissionsAsync(long roleId)
	{
		var role = await _context.Roles
			.Where(r => r.RoleId == roleId)
			.Select(r => new RoleDto
			{
				RoleId = r.RoleId,
				Name = r.Name,
				Description = r.Description,
				Permissions = r.RolePermissions
					.Select(rp => new PermissionDto
					{
						PermissionId = rp.PermissionId,
						Code = rp.Permission.Code,
						Name = rp.Permission.Name,
						Description = rp.Permission.Description
					}).ToList()
			})
			.FirstOrDefaultAsync();

		if (role == null)
		{
			return new ApiResponse<RoleDto> { Success = false, Message = "Không tìm thấy role", Errors = new List<string> { "Role không tồn tại" } };
		}

		return new ApiResponse<RoleDto> { Success = true, Message = "OK", Data = role };
	}

	public async Task<ApiResponse<bool>> AssignPermissionAsync(long roleId, long permissionId)
	{
		var exists = await _context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
		if (!exists)
		{
			_context.RolePermissions.Add(new Models.RolePermission { RoleId = roleId, PermissionId = permissionId });
			await _context.SaveChangesAsync();
		}
		return new ApiResponse<bool> { Success = true, Message = "Đã gán quyền", Data = true };
	}

	public async Task<ApiResponse<bool>> RemovePermissionAsync(long roleId, long permissionId)
	{
		var rp = await _context.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);
		if (rp == null)
		{
			return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy mapping", Errors = new List<string> { "Mapping không tồn tại" } };
		}
		_context.RolePermissions.Remove(rp);
		await _context.SaveChangesAsync();
		return new ApiResponse<bool> { Success = true, Message = "Đã bỏ quyền", Data = true };
	}
}


