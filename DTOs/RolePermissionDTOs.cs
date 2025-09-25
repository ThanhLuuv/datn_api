using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class PermissionDto
{
	public long PermissionId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
}

public class RoleDto
{
	public long RoleId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<PermissionDto> Permissions { get; set; } = new();
}

public class AssignPermissionRequest
{
	[Required]
	public long RoleId { get; set; }
	[Required]
	public long PermissionId { get; set; }
}


