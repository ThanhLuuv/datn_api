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

	public PaymentService(BookStoreDbContext context, IConfiguration config)
	{
		_context = context;
		_config = config;
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
			Status = "PENDING"
		};
		_context.PaymentTransactions.Add(txn);
		await _context.SaveChangesAsync();

		// Build PayOS payload
		var clientId = _config["PayOS:ClientId"] ?? "";
		var apiKey = _config["PayOS:ApiKey"] ?? "";
		var checksumKey = _config["PayOS:ChecksumKey"] ?? "";

		var orderCode = txn.TransactionId; // unique
		var amount = (long)decimal.Round(request.Amount, 0, MidpointRounding.AwayFromZero);
		var description = $"Thanh toan don hang #{request.OrderId}";
		var returnUrl = request.ReturnUrl ?? _config["PayOS:ReturnUrl"] ?? "";
		var cancelUrl = _config["PayOS:CancelUrl"] ?? returnUrl;

		var payload = new
		{
			orderCode,
			amount,
			description,
			returnUrl,
			cancelUrl
		};

		var bodyJson = System.Text.Json.JsonSerializer.Serialize(payload);
		txn.RawRequest = bodyJson;
		await _context.SaveChangesAsync();

		using var http = new HttpClient();
		http.DefaultRequestHeaders.Add("x-client-id", clientId);
		http.DefaultRequestHeaders.Add("x-api-key", apiKey);

		var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
		var resp = await http.PostAsync("https://api-merchant.payos.vn/v2/payment-requests", content);
		var respText = await resp.Content.ReadAsStringAsync();
		txn.RawResponse = respText;
		if (!resp.IsSuccessStatusCode)
		{
			await _context.SaveChangesAsync();
			return new ApiResponse<CreatePaymentLinkResponseDto>
			{
				Success = false,
				Message = "Tạo liên kết thanh toán thất bại",
				Errors = new List<string> { respText }
			};
		}

		using var doc = System.Text.Json.JsonDocument.Parse(respText);
		var root = doc.RootElement;
		var data = root.GetProperty("data");
		var checkoutUrl = data.GetProperty("checkoutUrl").GetString() ?? string.Empty;
		var providerTxnId = data.GetProperty("id").GetString() ?? string.Empty;
		txn.CheckoutUrl = checkoutUrl;
		txn.ProviderTxnId = providerTxnId;
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

	public async Task HandleWebhookAsync(string payload, string signature)
	{
		// Optionally validate signature with checksum key if PayOS provides
		// Parse and update transaction status
		using var doc = System.Text.Json.JsonDocument.Parse(payload);
		var root = doc.RootElement;
		var data = root.TryGetProperty("data", out var d) ? d : root;
		var orderCode = data.GetProperty("orderCode").GetInt64();
		var status = data.GetProperty("status").GetString() ?? ""; // e.g., PAID

		var txn = await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.TransactionId == orderCode);
		if (txn != null)
		{
			txn.Status = status.ToUpperInvariant();
			txn.RawResponse = payload;
			txn.UpdatedAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();
		}
	}
}


