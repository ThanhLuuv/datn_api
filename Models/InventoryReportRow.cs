using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Models;

[Keyless]
[Table("__inventory_report_row__")] // virtual/table-less mapping for SP result
public class InventoryReportRow
{
    [Column("Category")]
    public string Category { get; set; } = string.Empty;

    [Column("ISBN")]
    public string Isbn { get; set; } = string.Empty;

    [Column("Title")]
    public string Title { get; set; } = string.Empty;

    [Column("QuantityOnHand")]
    public int QuantityOnHand { get; set; }

    [Column("AveragePrice")]
    public decimal AveragePrice { get; set; }
}


