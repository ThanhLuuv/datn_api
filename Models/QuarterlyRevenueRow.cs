using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[NotMapped]
public class QuarterlyRevenueRow
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal Revenue { get; set; }
}


















