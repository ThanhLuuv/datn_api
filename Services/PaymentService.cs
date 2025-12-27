using System.Security.Cryptography;
using System.Text;
using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class PaymentService : IPaymentService
{
	private readonly BookStoreDbContext _context;
	private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

	public PaymentService(BookStoreDbContext context, IConfiguration config, IEmailService emailService)
	{
		_context = context;
		_config = config;
        _emailService = emailService;
	}

	public async Task<ApiResponse<CreatePaymentLinkResponseDto>> CreatePaymentLinkAsync(CreatePaymentLinkRequestDto request)
	{
		// Create local transaction record
		var txn = new PaymentTransaction
		{
			OrderId = request.OrderId,
			Amount = request.Amount,
			Currency = request.Currency,
			ReturnUrl = request.ReturnUrl,
			Status = "PENDING",
			OrderCode = 0 // Will be set after we get TransactionId
		};
		_context.PaymentTransactions.Add(txn);
		await _context.SaveChangesAsync();

		// Build PayOS payload
		var clientId = _config["PayOS:ClientId"] ?? "";
		var apiKey = _config["PayOS:ApiKey"] ?? "";
		var checksumKey = _config["PayOS:ChecksumKey"] ?? "";

		// Check if this order already has a pending payment transaction
		var existingTxn = await _context.PaymentTransactions
			.FirstOrDefaultAsync(pt => pt.OrderId == request.OrderId && pt.Status == "PENDING");
		
		if (existingTxn != null)
		{
			// Return existing payment link if available
			if (!string.IsNullOrEmpty(existingTxn.CheckoutUrl))
			{
				return new ApiResponse<CreatePaymentLinkResponseDto>
				{
					Success = true,
					Message = "Liên kết thanh toán đã tồn tại",
					Data = new CreatePaymentLinkResponseDto
					{
						TransactionId = existingTxn.TransactionId,
						CheckoutUrl = existingTxn.CheckoutUrl,
						ProviderTxnId = existingTxn.ProviderTxnId ?? ""
					}
				};
			}
			
			// If no checkout URL, remove the failed transaction and create new one
			_context.PaymentTransactions.Remove(existingTxn);
			await _context.SaveChangesAsync();
			
			// Recreate transaction with new ID
			txn = new PaymentTransaction
			{
				OrderId = request.OrderId,
				Amount = request.Amount,
				Currency = request.Currency,
				ReturnUrl = request.ReturnUrl,
				Status = "PENDING",
				OrderCode = 0 // Will be set after we get TransactionId
			};
			_context.PaymentTransactions.Add(txn);
			await _context.SaveChangesAsync();
		}
		
		// Create unique orderCode using transactionId + random suffix
		var random = new Random().Next(1000, 9999);
		var orderCode = long.Parse($"{txn.TransactionId}{random}");
		
		// Save orderCode to transaction
		txn.OrderCode = orderCode;
		await _context.SaveChangesAsync();
		var amount = (long)Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero);
		var description = $"DH#{request.OrderId}";
		var returnUrl = request.ReturnUrl ?? _config["PayOS:ReturnUrl"] ?? "";
		var cancelUrl = request.CancelUrl ?? _config["PayOS:CancelUrl"] ?? returnUrl;
		
		// Validate required fields
		if (orderCode <= 0)
		{
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Order code phải là số nguyên dương",
				Errors = new List<string> { "Invalid orderCode" }
			};
		}
		
		if (amount <= 0)
		{
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Amount phải lớn hơn 0",
				Errors = new List<string> { "Invalid amount" }
			};
		}
		
		if (string.IsNullOrWhiteSpace(description))
		{
			description = $"Order #{request.OrderId}";
		}

		// Compute HMAC-SHA256 signature
		var signature = ComputeSignature(amount, cancelUrl, description, orderCode, returnUrl, checksumKey);

		var payload = new
		{
			orderCode,
			amount,
			description,
			returnUrl,
			cancelUrl,
			signature
		};

		var bodyJson = System.Text.Json.JsonSerializer.Serialize(payload);
		txn.RawRequest = bodyJson;
		await _context.SaveChangesAsync();

		// Log request for debugging
		Console.WriteLine($"PayOS Request: {bodyJson}");
		Console.WriteLine($"ClientId: {clientId}");
		Console.WriteLine($"ApiKey: {apiKey?.Substring(0, Math.Min(10, apiKey.Length))}...");

		using var http = new HttpClient();
		http.DefaultRequestHeaders.Add("x-client-id", clientId);
		http.DefaultRequestHeaders.Add("x-api-key", apiKey);

		var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
		var resp = await http.PostAsync("https://api-merchant.payos.vn/v2/payment-requests", content);
		var respText = await resp.Content.ReadAsStringAsync();
		txn.RawResponse = respText;
		await _context.SaveChangesAsync();
		
		if (!resp.IsSuccessStatusCode)
		{
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Tạo liên kết thanh toán thất bại",
				Errors = new List<string> { $"HTTP {resp.StatusCode}: {respText}" }
			};
		}

		try
		{
			using var doc = System.Text.Json.JsonDocument.Parse(respText);
			var root = doc.RootElement;
			
			// Đọc code và desc trước
			var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
			var desc = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : null;
			
			// Chỉ tiếp tục nếu code == "00" (success) và data là Object
			if (code == "00" && root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Object)
			{
				var checkoutUrl = dataEl.TryGetProperty("checkoutUrl", out var checkoutUrlProp) 
					? checkoutUrlProp.GetString() ?? string.Empty 
					: string.Empty;
				var providerTxnId = dataEl.TryGetProperty("paymentLinkId", out var idProp)
					? idProp.GetString() ?? string.Empty 
					: (dataEl.TryGetProperty("id", out var id2) ? id2.GetString() ?? string.Empty : string.Empty);
				
				txn.CheckoutUrl = checkoutUrl;
				txn.ProviderTxnId = providerTxnId;
				txn.Status = "PENDING";
				await _context.SaveChangesAsync();

				return new ApiResponse<CreatePaymentLinkResponseDto>
				{
					Success = true,
					Message = "Tạo liên kết thanh toán thành công",
					Data = new CreatePaymentLinkResponseDto
					{
						TransactionId = txn.TransactionId,
						CheckoutUrl = checkoutUrl,
						ProviderTxnId = providerTxnId
					}
				};
			}
			
			// Nếu không thành công, trả lỗi rõ ràng
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Failed to create payment link",
				Errors = new List<string> { $"code={code}, desc={desc}", respText }
			};
		}
		catch (Exception ex)
		{
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Lỗi parse PayOS response",
				Errors = new List<string> { ex.Message, respText }
			};
		}
	}

	private static string ComputeSignature(long amount, string cancelUrl, string description, long orderCode, string returnUrl, string checksumKey)
	{
		// Build signing string theo thứ tự alpha
		var signing = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
		var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signing));
		return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); // hex lowercase
	}

	public async Task HandleWebhookAsync(string payload, string signature)
	{
		try
		{
			// Log webhook payload for debugging
			Console.WriteLine($"Webhook received: {payload}");
			
			using var doc = System.Text.Json.JsonDocument.Parse(payload);
			var root = doc.RootElement;
			
			// Parse webhook data safely
			var data = root.TryGetProperty("data", out var d) ? d : root;
			
			if (!data.TryGetProperty("orderCode", out var orderCodeEl))
			{
				Console.WriteLine("Webhook missing required field: orderCode");
				return;
			}
			
			var orderCode = orderCodeEl.GetInt64();
			
			// Determine status from PayOS response
			var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : "";
			var desc = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : "";
			
			// Map PayOS code to our status
			var status = code == "00" ? "PAID" : "FAILED";
			
			Console.WriteLine($"Processing webhook: orderCode={orderCode}, code={code}, desc={desc}, status={status}");

			var txn = await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.OrderCode == orderCode);
			if (txn != null)
			{
				txn.Status = status.ToUpperInvariant();
				txn.RawResponse = payload;
				txn.UpdatedAt = DateTime.UtcNow;
				
				Invoice? invoiceToSend = null;
				Order? orderToSend = null;

				// If payment successful, create invoice and reduce stock
				if (status.ToUpperInvariant() == "PAID")
				{
					var order = await _context.Orders
						.Include(o => o.OrderLines).ThenInclude(ol => ol.Book)
						.Include(o => o.Customer)
						.FirstOrDefaultAsync(o => o.OrderId == txn.OrderId);
					
					if (order != null)
					{
						orderToSend = order;

						// Create invoice for paid order (without tax for now)
						var totalAmount = order.OrderLines.Sum(ol => ol.UnitPrice * ol.Qty);
						
						var invoice = new Invoice
						{
							OrderId = order.OrderId,
							InvoiceNumber = $"INV-{order.OrderId:D6}-{DateTime.UtcNow:yyyyMMdd}",
							TotalAmount = totalAmount,
							TaxAmount = 0, // No tax for now
							PaymentStatus = "PAID",
							PaymentMethod = "PayOS",
							PaymentReference = txn.ProviderTxnId,
							PaidAt = DateTime.UtcNow,
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow
						};
						_context.Invoices.Add(invoice);
						invoiceToSend = invoice;
						
						// Reduce stock for each book in the order
						foreach (var orderLine in order.OrderLines)
						{
							var book = await _context.Books.FirstOrDefaultAsync(b => b.Isbn == orderLine.Isbn);
							if (book != null)
							{
								// Check if enough stock
								if (book.Stock >= orderLine.Qty)
								{
									book.Stock -= orderLine.Qty;
									Console.WriteLine($"Reduced stock for {orderLine.Isbn}: {book.Stock + orderLine.Qty} -> {book.Stock}");
								}
								else
								{
									Console.WriteLine($"Insufficient stock for {orderLine.Isbn}: requested {orderLine.Qty}, available {book.Stock}");
									// Could add error handling here if needed
								}
							}
						}
					}
				}
				
				await _context.SaveChangesAsync();

				// Send email if invoice created (after successful save)
				if (invoiceToSend != null && orderToSend != null)
				{
					var emailTo = orderToSend.Customer?.Email ?? string.Empty;
					if (!string.IsNullOrEmpty(emailTo))
					{
						// Fire and forget email to avoid blocking response
						_ = Task.Run(() => _emailService.SendOrderInvoiceEmailAsync(emailTo, orderToSend, invoiceToSend));
						Console.WriteLine($"Queued invoice email to {emailTo}");
					}
					else 
					{
						Console.WriteLine($"No email found for customer of order {orderToSend.OrderId}, skipping invoice email.");
					}
				}

				Console.WriteLine($"Updated transaction {orderCode} to status {status}");
			}
			else
			{
				Console.WriteLine($"Transaction {orderCode} not found");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Webhook error: {ex.Message}");
			Console.WriteLine($"Payload: {payload}");
		}
	}
}


