using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("area")]
public class Area
{
    [Key]
    [Column("area_id")]
    public long AreaId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("keywords")] // các từ khóa liên quan để matching địa chỉ
    public string? Keywords { get; set; }

    // Many-to-many relationship with Employee through EmployeeArea
    public virtual ICollection<EmployeeArea> EmployeeAreas { get; set; } = new List<EmployeeArea>();
}




