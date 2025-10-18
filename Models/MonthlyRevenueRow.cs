using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[NotMapped]
public class MonthlyRevenueRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}















