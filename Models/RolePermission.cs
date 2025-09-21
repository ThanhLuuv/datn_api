using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("role_permission")]
public class RolePermission
{
    [Key]
    [Column("role_id")]
    public long RoleId { get; set; }

    [Key]
    [Column("permission_id")]
    public long PermissionId { get; set; }

    // Navigation properties
    [ForeignKey("RoleId")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("PermissionId")]
    public virtual Permission Permission { get; set; } = null!;
}
