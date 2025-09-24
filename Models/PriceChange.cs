using System;

namespace BookStore.Api.Models
{
    public class PriceChange
    {
        public string Isbn { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAt { get; set; }
        public long EmployeeId { get; set; }
    }
}


