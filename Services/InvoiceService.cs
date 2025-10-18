using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public interface IInvoiceService
{
    Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId);
    Task<ApiResponse<InvoiceListResponse>> GetInvoicesAsync(InvoiceSearchRequest request);
    Task<ApiResponse<InvoiceDto>> GetInvoiceByOrderIdAsync(long orderId);
    Task<bool> HasPaidInvoiceAsync(long orderId);
    Task<ApiResponse<InvoiceWithOrderListResponse>> GetInvoicesWithOrdersAsync(InvoiceSearchRequest request);
}

public class InvoiceService : IInvoiceService
{
    private readonly BookStoreDbContext _context;

    public InvoiceService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId)
    {
        try
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
            {
                return new ApiResponse<InvoiceDto>
                {
                    Success = false,
                    Message = "Hóa đơn không tồn tại",
                    Errors = new List<string> { $"Invoice {invoiceId} not found" }
                };
            }

            var invoiceDto = new InvoiceDto
            {
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                InvoiceNumber = invoice.InvoiceNumber,
                TotalAmount = invoice.TotalAmount,
                PaymentStatus = invoice.PaymentStatus,
                PaymentMethod = invoice.PaymentMethod,
                PaymentReference = invoice.PaymentReference,
                PaidAt = invoice.PaidAt,
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };

            return new ApiResponse<InvoiceDto>
            {
                Success = true,
                Message = "Lấy hóa đơn thành công",
                Data = invoiceDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceDto>
            {
                Success = false,
                Message = "Lỗi khi lấy hóa đơn",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<InvoiceListResponse>> GetInvoicesAsync(InvoiceSearchRequest request)
    {
        try
        {
            var query = _context.Invoices.AsQueryable();

            // Apply filters
            if (request.OrderId.HasValue)
            {
                query = query.Where(i => i.OrderId == request.OrderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentStatus))
            {
                query = query.Where(i => i.PaymentStatus == request.PaymentStatus);
            }

            if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(request.InvoiceNumber));
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt <= request.ToDate.Value);
            }

            var totalCount = await query.CountAsync();

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new InvoiceDto
                {
                    InvoiceId = i.InvoiceId,
                    OrderId = i.OrderId,
                    InvoiceNumber = i.InvoiceNumber,
                    TotalAmount = i.TotalAmount,
                    PaymentStatus = i.PaymentStatus,
                    PaymentMethod = i.PaymentMethod,
                    PaymentReference = i.PaymentReference,
                    PaidAt = i.PaidAt,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt
                })
                .ToListAsync();

            var response = new InvoiceListResponse
            {
                Invoices = invoices,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };

            return new ApiResponse<InvoiceListResponse>
            {
                Success = true,
                Message = "Lấy danh sách hóa đơn thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceListResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách hóa đơn",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<InvoiceWithOrderListResponse>> GetInvoicesWithOrdersAsync(InvoiceSearchRequest request)
    {
        try
        {
            var query = _context.Invoices
                .Include(i => i.Order)
                .AsQueryable();

            // Chỉ lấy hóa đơn chưa có phiếu trả
            query = query.Where(i => !_context.Returns.Any(r => r.InvoiceId == i.InvoiceId));

            if (request.OrderId.HasValue)
            {
                query = query.Where(i => i.OrderId == request.OrderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentStatus))
            {
                query = query.Where(i => i.PaymentStatus == request.PaymentStatus);
            }

            if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(request.InvoiceNumber));
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt <= request.ToDate.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new InvoiceWithOrderDto
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceNumber = i.InvoiceNumber,
                    TotalAmount = i.TotalAmount,
                    TaxAmount = i.TaxAmount,
                    PaymentStatus = i.PaymentStatus,
                    CreatedAt = i.CreatedAt,
                    OrderId = i.OrderId,
                    PlacedAt = i.Order.PlacedAt,
                    ReceiverName = i.Order.ReceiverName,
                    ReceiverPhone = i.Order.ReceiverPhone,
                    ShippingAddress = i.Order.ShippingAddress,
                    OrderStatus = i.Order.Status.ToString()
                })
                .ToListAsync();

            var response = new InvoiceWithOrderListResponse
            {
                Invoices = items,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };

            return new ApiResponse<InvoiceWithOrderListResponse>
            {
                Success = true,
                Message = "Lấy danh sách hóa đơn chưa có phiếu trả thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceWithOrderListResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách hóa đơn kèm đơn hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByOrderIdAsync(long orderId)
    {
        try
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.OrderId == orderId);

            if (invoice == null)
            {
                return new ApiResponse<InvoiceDto>
                {
                    Success = false,
                    Message = "Không tìm thấy hóa đơn cho đơn hàng này",
                    Errors = new List<string> { $"No invoice found for order {orderId}" }
                };
            }

            var invoiceDto = new InvoiceDto
            {
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                InvoiceNumber = invoice.InvoiceNumber,
                TotalAmount = invoice.TotalAmount,
                PaymentStatus = invoice.PaymentStatus,
                PaymentMethod = invoice.PaymentMethod,
                PaymentReference = invoice.PaymentReference,
                PaidAt = invoice.PaidAt,
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };

            return new ApiResponse<InvoiceDto>
            {
                Success = true,
                Message = "Lấy hóa đơn thành công",
                Data = invoiceDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceDto>
            {
                Success = false,
                Message = "Lỗi khi lấy hóa đơn",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<bool> HasPaidInvoiceAsync(long orderId)
    {
        try
        {
            return await _context.Invoices
                .AnyAsync(i => i.OrderId == orderId && i.PaymentStatus == "PAID");
        }
        catch
        {
            return false;
        }
    }
}
