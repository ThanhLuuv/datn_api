using BookStore.Api.Data;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookStore.Api.Services;

public interface IOrderQueryService
{
    Task<string> GetOrderDetailAsync(string orderId, CancellationToken cancellationToken = default);
    Task<string> SearchCustomerOrdersAsync(string customerIdentifier, CancellationToken cancellationToken = default);
    Task<string> GetInvoiceDetailAsync(string invoiceId, CancellationToken cancellationToken = default);
}

public class OrderQueryService : IOrderQueryService
{
    private readonly BookStoreDbContext _db;
    private readonly ILogger<OrderQueryService> _logger;

    public OrderQueryService(BookStoreDbContext db, ILogger<OrderQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> GetOrderDetailAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try parse as long ID first
            Order? order = null;
            if (long.TryParse(orderId, out var id))
            {
                order = await _db.Orders
                    .AsNoTracking()
                    .Include(o => o.Customer)
                    .Include(o => o.OrderLines)
                        .ThenInclude(ol => ol.Book)
                    .FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);
            }

            if (order == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Không tìm thấy đơn hàng",
                    orderId = orderId
                });
            }

            var result = new
            {
                orderId = order.OrderId,
                customerName = order.Customer?.FullName ?? order.ReceiverName,
                customerId = order.CustomerId,
                status = order.Status.ToString(),
                statusLabel = GetOrderStatusLabel(order.Status),
                placedAt = order.PlacedAt,
                deliveryAt = order.DeliveryAt,
                receiverName = order.ReceiverName,
                receiverPhone = order.ReceiverPhone,
                shippingAddress = order.ShippingAddress,
                note = order.Note,
                items = order.OrderLines.Select(ol => new
                {
                    isbn = ol.Isbn,
                    bookTitle = ol.Book?.Title ?? ol.Isbn,
                    quantity = ol.Qty,
                    unitPrice = ol.UnitPrice,
                    total = ol.Qty * ol.UnitPrice
                }).ToList(),
                totalAmount = order.OrderLines.Sum(ol => ol.Qty * ol.UnitPrice),
                totalItems = order.OrderLines.Sum(ol => ol.Qty)
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order detail for {OrderId}", orderId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    public async Task<string> SearchCustomerOrdersAsync(string customerIdentifier, CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<Order> query = _db.Orders.AsNoTracking();

            // Try to find by customer ID or name
            if (long.TryParse(customerIdentifier, out var customerId))
            {
                query = query.Where(o => o.CustomerId == customerId);
            }
            else
            {
                query = query.Where(o => o.Customer != null && 
                    (o.Customer.FullName.Contains(customerIdentifier) ||
                     o.ReceiverName.Contains(customerIdentifier)));
            }

            var orders = await query
                .Include(o => o.Customer)
                .OrderByDescending(o => o.PlacedAt)
                .Take(10) // Giảm từ 20 xuống 10 để tiết kiệm token
                .Select(o => new
                {
                    orderId = o.OrderId,
                    customerName = o.Customer != null ? o.Customer.FullName : o.ReceiverName,
                    status = o.Status.ToString(),
                    statusLabel = GetOrderStatusLabel(o.Status),
                    placedAt = o.PlacedAt,
                    totalAmount = o.OrderLines.Sum(ol => ol.Qty * ol.UnitPrice)
                })
                .ToListAsync(cancellationToken);

            return JsonSerializer.Serialize(new
            {
                customerIdentifier = customerIdentifier,
                orderCount = orders.Count,
                orders = orders
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching orders for customer {CustomerIdentifier}", customerIdentifier);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    public async Task<string> GetInvoiceDetailAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        try
        {
            Invoice? invoice = null;
            if (long.TryParse(invoiceId, out var id))
            {
                invoice = await _db.Invoices
                    .AsNoTracking()
                    .Include(i => i.Order)
                        .ThenInclude(o => o.Customer)
                    .Include(i => i.Order)
                        .ThenInclude(o => o.OrderLines)
                            .ThenInclude(ol => ol.Book)
                    .FirstOrDefaultAsync(i => i.InvoiceId == id, cancellationToken);
            }
            else
            {
                invoice = await _db.Invoices
                    .AsNoTracking()
                    .Include(i => i.Order)
                        .ThenInclude(o => o.Customer)
                    .Include(i => i.Order)
                        .ThenInclude(o => o.OrderLines)
                            .ThenInclude(ol => ol.Book)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceId, cancellationToken);
            }

            if (invoice == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Không tìm thấy hóa đơn",
                    invoiceId = invoiceId
                });
            }

            var result = new
            {
                invoiceId = invoice.InvoiceId,
                invoiceNumber = invoice.InvoiceNumber,
                orderId = invoice.OrderId,
                customerName = invoice.Order?.Customer?.FullName ?? invoice.Order?.ReceiverName,
                createdAt = invoice.CreatedAt,
                totalAmount = invoice.TotalAmount,
                taxAmount = invoice.TaxAmount,
                grandTotal = invoice.TotalAmount + invoice.TaxAmount,
                paymentStatus = invoice.PaymentStatus,
                paymentMethod = invoice.PaymentMethod,
                paidAt = invoice.PaidAt,
                paymentReference = invoice.PaymentReference
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice detail for {InvoiceId}", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetOrderStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.PendingConfirmation => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Delivered => "Đã giao",
        OrderStatus.Cancelled => "Đã huỷ",
        _ => status.ToString()
    };
}
