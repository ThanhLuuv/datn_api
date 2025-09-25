using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IRoleService
{
	Task<ApiResponse<List<RoleDto>>> GetRolesAsync();
	Task<ApiResponse<List<PermissionDto>>> GetPermissionsAsync();
	Task<ApiResponse<RoleDto>> GetRoleWithPermissionsAsync(long roleId);
	Task<ApiResponse<bool>> AssignPermissionAsync(long roleId, long permissionId);
	Task<ApiResponse<bool>> RemovePermissionAsync(long roleId, long permissionId);
}


