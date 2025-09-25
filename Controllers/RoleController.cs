using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class RoleController : ControllerBase
{
	private readonly IRoleService _roleService;

	public RoleController(IRoleService roleService)
	{
		_roleService = roleService;
	}

	/// <summary>
	/// Danh sách role
	/// </summary>
	[HttpGet]
	public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
	{
		var result = await _roleService.GetRolesAsync();
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}

	/// <summary>
	/// Danh sách tất cả quyền
	/// </summary>
	[HttpGet("permissions")]
	public async Task<ActionResult<ApiResponse<List<PermissionDto>>>> GetPermissions()
	{
		var result = await _roleService.GetPermissionsAsync();
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}

	/// <summary>
	/// Lấy quyền theo role
	/// </summary>
	[HttpGet("{roleId}/permissions")]
	public async Task<ActionResult<ApiResponse<RoleDto>>> GetRolePermissions(long roleId)
	{
		var result = await _roleService.GetRoleWithPermissionsAsync(roleId);
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}

	/// <summary>
	/// Gán quyền cho role
	/// </summary>
	[HttpPost("assign")]
	public async Task<ActionResult<ApiResponse<bool>>> Assign([FromBody] AssignPermissionRequest req)
	{
		var result = await _roleService.AssignPermissionAsync(req.RoleId, req.PermissionId);
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}

	/// <summary>
	/// Bỏ quyền khỏi role
	/// </summary>
	[HttpPost("remove")]
	public async Task<ActionResult<ApiResponse<bool>>> Remove([FromBody] AssignPermissionRequest req)
	{
		var result = await _roleService.RemovePermissionAsync(req.RoleId, req.PermissionId);
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}
}


