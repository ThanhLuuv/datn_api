using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("customer")]
public class Customer
{
    [Key]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Required]
    [Column("account_id")]
    public long AccountId { get; set; }

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

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    // Computed property
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}
