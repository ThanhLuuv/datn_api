using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("employee_area")]
public class EmployeeArea
{
    [Key]
    [Column("employee_area_id")]
    public long EmployeeAreaId { get; set; }

    [Required]
    [Column("employee_id")]
    public long EmployeeId { get; set; }

    [Required]
    [Column("area_id")]
    public long AreaId { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey("EmployeeId")]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey("AreaId")]
    public virtual Area Area { get; set; } = null!;
}












