using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("purchase_order_status")]
public class PurchaseOrderStatus
{
    [Key]
    [Column("status_id")]
    public long StatusId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("status_name")]
    public string StatusName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    // Navigation
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}


