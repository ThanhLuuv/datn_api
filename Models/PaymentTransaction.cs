using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("payment_transaction")]
public class PaymentTransaction
{
	[Key]
	[Column("transaction_id")]
	public long TransactionId { get; set; }

	[Required]
	[Column("order_id")]
	public long OrderId { get; set; }

	[Required]
	[Column("order_code")]
	public long OrderCode { get; set; }

	[Required]
	[Column("amount", TypeName = "decimal(18,2)")]
	public decimal Amount { get; set; }

	[Required]
	[MaxLength(10)]
	[Column("currency")]
	public string Currency { get; set; } = "VND";

	[Required]
	[MaxLength(50)]
	[Column("provider")]
	public string Provider { get; set; } = "PAYOS";

	[MaxLength(191)]
	[Column("provider_txn_id")]
	public string? ProviderTxnId { get; set; }

	[Required]
	[MaxLength(50)]
	[Column("status")]
	public string Status { get; set; } = "PENDING"; // PENDING, PAID, FAILED, CANCELLED

	[MaxLength(500)]
	[Column("return_url")]
	public string? ReturnUrl { get; set; }

	[MaxLength(500)]
	[Column("checkout_url")]
	public string? CheckoutUrl { get; set; }

	[Column("raw_request", TypeName = "json")]
	public string? RawRequest { get; set; }

	[Column("raw_response", TypeName = "json")]
	public string? RawResponse { get; set; }

	[Column("created_at")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[Column("updated_at")]
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}


