using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models
{
    [Table("price_change")]
    public class PriceChange
    {
        // Composite key is configured in DbContext: (Isbn, ChangedAt)

        [Required]
        [MaxLength(20)]
        [Column("isbn")]
        public string Isbn { get; set; } = string.Empty;

        [Required]
        [Column("old_price", TypeName = "decimal(12,2)")]
        public decimal OldPrice { get; set; }

        [Required]
        [Column("new_price", TypeName = "decimal(12,2)")]
        public decimal NewPrice { get; set; }

        [Required]
        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("employee_id")]
        public long EmployeeId { get; set; }

        // Reason column not present in DB; omitted

        // Navigation properties
        [ForeignKey("Isbn")]
        public virtual Book Book { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;
    }
}


