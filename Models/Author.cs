using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("author")]
public class Author
{
    [Key]
    [Column("author_id")]
    public long AuthorId { get; set; }

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

    [MaxLength(191)]
    [Column("email")]
    public string? Email { get; set; }

    // Navigation properties
    public virtual ICollection<AuthorBook> AuthorBooks { get; set; } = new List<AuthorBook>();

    // Computed property
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}

public enum Gender
{
    Male,
    Female,
    Other
}
