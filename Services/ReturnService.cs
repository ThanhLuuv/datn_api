using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class ReturnService : IReturnService
{
    private readonly BookStoreDbContext _context;
    private readonly IExpenseService _expenseService;

    public ReturnService(BookStoreDbContext context, IExpenseService expenseService)
    {
        _context = context;
        _expenseService = expenseService;
    }

    public async Task<ApiResponse<ReturnDto>> CreateReturnAsync(CreateReturnDto request)
    {
        try
        {
            var invoice = await _context.Invoices
                .Include(i => i.Order)
                .ThenInclude(o => o.OrderLines)
                    .ThenInclude(ol => ol.Book)
                .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId);
            if (invoice == null)
            {
                return new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy hóa đơn" };
            }

            // Index order lines by ISBN instead of OrderLineId
            var isbnToLine = invoice.Order.OrderLines.ToDictionary(ol => ol.Isbn);

            // Validate lines
            foreach (var line in request.Lines)
            {
                if (!isbnToLine.TryGetValue(line.Isbn, out var ol))
                {
                    return new ApiResponse<ReturnDto> { Success = false, Message = $"Sách không tồn tại trong đơn hàng: {line.Isbn}" };
                }
                if (line.QtyReturned <= 0 || line.QtyReturned > ol.Qty)
                {
                    return new ApiResponse<ReturnDto> { Success = false, Message = $"Số lượng trả không hợp lệ cho sách {line.Isbn}" };
                }
            }

            var ret = new Return
            {
                InvoiceId = invoice.InvoiceId,
                CreatedAt = DateTime.UtcNow,
                Reason = request.Reason,
                Status = ReturnStatus.Pending,
                ApplyDeduction = request.ApplyDeduction,
                DeductionPercent = request.DeductionPercent
            };
            _context.Returns.Add(ret);
            await _context.SaveChangesAsync();

            var retLines = new List<ReturnLine>();
            var totalAmount = 0m;
            foreach (var line in request.Lines)
            {
                var ol = isbnToLine[line.Isbn];
                var amount = line.QtyReturned * ol.UnitPrice; // Use UnitPrice from request
                totalAmount += amount;
                retLines.Add(new ReturnLine
                {
                    ReturnId = ret.ReturnId,
                    OrderLineId = ol.OrderLineId,
                    QtyReturned = line.QtyReturned,
                    Amount = amount
                });
            }
            _context.ReturnLines.AddRange(retLines);

            // Calculate deduction and final amount
            var deductionAmount = 0m;
            var finalAmount = totalAmount;
            if (request.ApplyDeduction && request.DeductionPercent > 0)
            {
                deductionAmount = totalAmount * (request.DeductionPercent / 100);
                finalAmount = totalAmount - deductionAmount;
            }

            ret.DeductionAmount = deductionAmount;
            ret.FinalAmount = finalAmount;
            await _context.SaveChangesAsync();

            var dto = new ReturnDto
            {
                ReturnId = ret.ReturnId,
                InvoiceId = ret.InvoiceId,
                CreatedAt = ret.CreatedAt,
                Reason = ret.Reason,
                Status = ret.Status,
                StatusText = GetStatusText(ret.Status),
                ProcessedBy = ret.ProcessedBy,
                ProcessedAt = ret.ProcessedAt,
                Notes = ret.Notes,
                TotalAmount = totalAmount,
                ApplyDeduction = ret.ApplyDeduction,
                DeductionPercent = ret.DeductionPercent,
                DeductionAmount = ret.DeductionAmount,
                FinalAmount = ret.FinalAmount,
                Lines = retLines.Select(x => new ReturnLineDto
                {
                    ReturnLineId = x.ReturnLineId,
                    OrderLineId = x.OrderLineId,
                    QtyReturned = x.QtyReturned,
                    Amount = x.Amount,
                    Isbn = isbnToLine.Values.First(ol => ol.OrderLineId == x.OrderLineId).Isbn,
                    BookTitle = isbnToLine.Values.First(ol => ol.OrderLineId == x.OrderLineId).Book.Title,
                    UnitPrice = isbnToLine.Values.First(ol => ol.OrderLineId == x.OrderLineId).UnitPrice
                }).ToList(),
                Order = new ReturnOrderDto
                {
                    OrderId = invoice.Order.OrderId,
                    PlacedAt = invoice.Order.PlacedAt,
                    ReceiverName = invoice.Order.ReceiverName,
                    ReceiverPhone = invoice.Order.ReceiverPhone,
                    ShippingAddress = invoice.Order.ShippingAddress
                }
            };

            return new ApiResponse<ReturnDto> { Success = true, Message = "Tạo phiếu trả thành công", Data = dto };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReturnDto> { Success = false, Message = "Lỗi khi tạo phiếu trả", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<ReturnListResponse>> GetReturnsAsync(ReturnSearchRequest request)
    {
        try
        {
            var query = _context.Returns
                .Include(r => r.ReturnLines)
                .ThenInclude(rl => rl.OrderLine)
                    .ThenInclude(ol => ol.Book)
                .Include(r => r.ProcessedByEmployee)
                .AsQueryable();

            if (request.InvoiceId.HasValue)
            {
                query = query.Where(r => r.InvoiceId == request.InvoiceId.Value);
            }
            if (request.Status.HasValue)
            {
                query = query.Where(r => r.Status == request.Status.Value);
            }
            if (request.FromDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= request.FromDate.Value);
            }
            if (request.ToDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= request.ToDate.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var returns = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var items = returns.Select(r => new ReturnDto
            {
                ReturnId = r.ReturnId,
                InvoiceId = r.InvoiceId,
                CreatedAt = r.CreatedAt,
                Reason = r.Reason,
                Status = r.Status,
                StatusText = GetStatusText(r.Status),
                ProcessedBy = r.ProcessedBy,
                ProcessedByEmployeeName = r.ProcessedByEmployee != null ? 
                    $"{r.ProcessedByEmployee.FirstName} {r.ProcessedByEmployee.LastName}" : null,
                ProcessedAt = r.ProcessedAt,
                Notes = r.Notes,
                TotalAmount = r.ReturnLines.Sum(rl => rl.Amount),
                Lines = r.ReturnLines.Select(rl => new ReturnLineDto
                {
                    ReturnLineId = rl.ReturnLineId,
                    OrderLineId = rl.OrderLineId,
                    QtyReturned = rl.QtyReturned,
                    Amount = rl.Amount,
                    Isbn = rl.OrderLine.Isbn,
                    BookTitle = rl.OrderLine.Book.Title,
                    UnitPrice = rl.OrderLine.UnitPrice
                }).ToList(),
                Order = r.Invoice != null ? new ReturnOrderDto
                {
                    OrderId = r.Invoice.OrderId,
                    PlacedAt = r.Invoice.Order.PlacedAt,
                    ReceiverName = r.Invoice.Order.ReceiverName,
                    ReceiverPhone = r.Invoice.Order.ReceiverPhone,
                    ShippingAddress = r.Invoice.Order.ShippingAddress
                } : null
            }).ToList();

            var resp = new ReturnListResponse
            {
                Returns = items,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<ReturnListResponse> { Success = true, Message = "Lấy danh sách phiếu trả thành công", Data = resp };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReturnListResponse> { Success = false, Message = "Lỗi khi lấy danh sách phiếu trả", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<ReturnDto>> GetReturnByIdAsync(long returnId)
    {
        try
        {
            var ret = await _context.Returns
                .Include(r => r.ReturnLines)
                    .ThenInclude(rl => rl.OrderLine)
                        .ThenInclude(ol => ol.Book)
                .Include(r => r.Invoice)
                    .ThenInclude(i => i.Order)
                .Include(r => r.ProcessedByEmployee)
                .FirstOrDefaultAsync(r => r.ReturnId == returnId);
            if (ret == null)
            {
                return new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy phiếu trả" };
            }

            var dto = new ReturnDto
            {
                ReturnId = ret.ReturnId,
                InvoiceId = ret.InvoiceId,
                CreatedAt = ret.CreatedAt,
                Reason = ret.Reason,
                Status = ret.Status,
                StatusText = GetStatusText(ret.Status),
                ProcessedBy = ret.ProcessedBy,
                ProcessedByEmployeeName = ret.ProcessedByEmployee != null ? 
                    $"{ret.ProcessedByEmployee.FirstName} {ret.ProcessedByEmployee.LastName}" : null,
                ProcessedAt = ret.ProcessedAt,
                Notes = ret.Notes,
                TotalAmount = ret.ReturnLines.Sum(rl => rl.Amount),
                ApplyDeduction = ret.ApplyDeduction,
                DeductionPercent = ret.DeductionPercent,
                DeductionAmount = ret.DeductionAmount,
                FinalAmount = ret.FinalAmount,
                Lines = ret.ReturnLines.Select(rl => new ReturnLineDto
                {
                    ReturnLineId = rl.ReturnLineId,
                    OrderLineId = rl.OrderLineId,
                    QtyReturned = rl.QtyReturned,
                    Amount = rl.Amount,
                    Isbn = rl.OrderLine.Isbn,
                    BookTitle = rl.OrderLine.Book.Title,
                    UnitPrice = rl.OrderLine.UnitPrice
                }).ToList(),
                Order = ret.Invoice != null && ret.Invoice.Order != null
                    ? new ReturnOrderDto
                    {
                        OrderId = ret.Invoice.Order.OrderId,
                        PlacedAt = ret.Invoice.Order.PlacedAt,
                        ReceiverName = ret.Invoice.Order.ReceiverName,
                        ReceiverPhone = ret.Invoice.Order.ReceiverPhone,
                        ShippingAddress = ret.Invoice.Order.ShippingAddress
                    }
                    : null
            };

            return new ApiResponse<ReturnDto> { Success = true, Message = "Lấy phiếu trả thành công", Data = dto };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReturnDto> { Success = false, Message = "Lỗi khi lấy phiếu trả", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<ReturnDto>> UpdateReturnStatusAsync(long returnId, UpdateReturnStatusRequest request, string processorEmail)
    {
        try
        {
            var ret = await _context.Returns
                .Include(r => r.ProcessedByEmployee)
                .FirstOrDefaultAsync(r => r.ReturnId == returnId);
            if (ret == null)
            {
                return new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy phiếu trả" };
            }

            // Tìm employee từ email
            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Account.Email == processorEmail);
            if (employee == null)
            {
                return new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy nhân viên xử lý" };
            }

            // Cập nhật trạng thái
            ret.Status = request.Status;
            ret.ProcessedBy = employee.EmployeeId;
            ret.ProcessedAt = DateTime.UtcNow;
            ret.Notes = request.Notes;

            await _context.SaveChangesAsync();

            // Nếu phiếu trả được duyệt (Approved), tạo phiếu chi hoàn tiền
            if (request.Status == ReturnStatus.Approved)
            {
                try
                {
                    // Sử dụng FinalAmount (đã trừ khấu trừ) thay vì tổng tiền gốc
                    var refundAmount = ret.FinalAmount;

                    Console.WriteLine($"=== TẠO PHIẾU CHI HOÀN TIỀN ===");
                    Console.WriteLine($"Return ID: {returnId}");
                    Console.WriteLine($"Refund Amount: {refundAmount}");
                    Console.WriteLine($"Employee ID: {employee.EmployeeId}");

                    if (refundAmount > 0)
                    {
                        var expenseResult = await _expenseService.CreateReturnRefundVoucherAsync(returnId, refundAmount, employee.EmployeeId);
                        if (expenseResult.Success)
                        {
                            Console.WriteLine($"✅ Tạo phiếu chi thành công: {expenseResult.Data?.VoucherNumber}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Lỗi tạo phiếu chi: {expenseResult.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Số tiền hoàn = 0, không tạo phiếu chi");
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng không làm fail việc cập nhật phiếu trả
                    Console.WriteLine($"❌ Lỗi khi tạo phiếu chi hoàn tiền: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                Console.WriteLine($"⚠️ Trạng thái không phải Approved ({request.Status}), không tạo phiếu chi");
            }

            // Trả về dữ liệu cập nhật
            return await GetReturnByIdAsync(returnId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReturnDto> { Success = false, Message = "Lỗi khi cập nhật trạng thái phiếu trả", Errors = new List<string> { ex.Message } };
        }
    }

    private static string GetStatusText(ReturnStatus status)
    {
        return status switch
        {
            ReturnStatus.Pending => "CHỜ XỬ LÝ",
            ReturnStatus.Approved => "ĐÃ DUYỆT",
            ReturnStatus.Rejected => "TỪ CHỐI",
            ReturnStatus.Processed => "ĐÃ XỬ LÝ",
            _ => "UNKNOWN"
        };
    }
}


