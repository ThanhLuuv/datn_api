using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("employee")]
public class Employee
{
    [Key]
    [Column("employee_id")]
    public long EmployeeId { get; set; }

    [Required]
    [Column("account_id")]
    public long AccountId { get; set; }

    [Required]
    [Column("department_id")]
    public long DepartmentId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Column("gender")]
    public Gender Gender { get; set; }

    [Column("dob", TypeName = "date")]
    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300)]
    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(30)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(191)]
    [Column("email")]
    public string? Email { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("AccountId")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("DepartmentId")]
    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<PurchaseOrder> CreatedPurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<GoodsReceipt> CreatedGoodsReceipts { get; set; } = new List<GoodsReceipt>();
    public virtual ICollection<Order> ApprovedOrders { get; set; } = new List<Order>();
    public virtual ICollection<Order> DeliveredOrders { get; set; } = new List<Order>();

    // Computed property
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}
