using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public interface IExpenseService
{
    Task<ApiResponse<ExpenseVoucherDto>> CreateExpenseVoucherAsync(CreateExpenseVoucherDto request, long createdBy);
    Task<ApiResponse<ExpenseVoucherResponse>> GetExpenseVouchersAsync(int pageNumber = 1, int pageSize = 50, string? status = null, string? expenseType = null);
    Task<ApiResponse<ExpenseVoucherDto>> GetExpenseVoucherByIdAsync(long expenseVoucherId);
    Task<ApiResponse<ExpenseVoucherDto>> ApproveExpenseVoucherAsync(ApproveExpenseVoucherDto request, long approvedBy);
    Task<ApiResponse<ExpenseVoucherDto>> RejectExpenseVoucherAsync(RejectExpenseVoucherDto request, long rejectedBy);
    Task<ApiResponse<ExpenseVoucherDto>> CreateReturnRefundVoucherAsync(long returnId, decimal totalAmount, long createdBy);
}

public class ExpenseService : IExpenseService
{
    private readonly BookStoreDbContext _context;

    public ExpenseService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ExpenseVoucherDto>> CreateExpenseVoucherAsync(CreateExpenseVoucherDto request, long createdBy)
    {
        try
        {
            if (!request.Lines.Any())
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Phiếu chi phải có ít nhất một dòng",
                    Errors = new List<string> { "Lines cannot be empty" }
                };
            }

            var totalAmount = request.Lines.Sum(l => l.Amount);
            if (totalAmount <= 0)
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Tổng tiền phải lớn hơn 0",
                    Errors = new List<string> { "Total amount must be greater than 0" }
                };
            }

            // Generate voucher number
            var voucherNumber = await GenerateVoucherNumberAsync();

            var expenseVoucher = new ExpenseVoucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = request.VoucherDate,
                Description = request.Description,
                TotalAmount = totalAmount,
                Status = "PENDING",
                ExpenseType = request.ExpenseType,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ExpenseVouchers.Add(expenseVoucher);
            await _context.SaveChangesAsync();

            // Add lines
            foreach (var lineDto in request.Lines)
            {
                var line = new ExpenseVoucherLine
                {
                    ExpenseVoucherId = expenseVoucher.ExpenseVoucherId,
                    Description = lineDto.Description,
                    Amount = lineDto.Amount,
                    Reference = lineDto.Reference,
                    ReferenceType = lineDto.ReferenceType,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ExpenseVoucherLines.Add(line);
            }

            await _context.SaveChangesAsync();

            // Load full data for response
            var result = await GetExpenseVoucherByIdAsync(expenseVoucher.ExpenseVoucherId);
            result.Message = "Tạo phiếu chi thành công";
            return result;
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = false,
                Message = "Lỗi khi tạo phiếu chi",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ExpenseVoucherResponse>> GetExpenseVouchersAsync(int pageNumber = 1, int pageSize = 50, string? status = null, string? expenseType = null)
    {
        try
        {
            var query = _context.ExpenseVouchers
                .Include(ev => ev.Creator)
                .Include(ev => ev.Approver)
                .Include(ev => ev.ExpenseVoucherLines)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(ev => ev.Status == status);
            }

            if (!string.IsNullOrEmpty(expenseType))
            {
                query = query.Where(ev => ev.ExpenseType == expenseType);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var expenseVouchers = await query
                .OrderByDescending(ev => ev.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(ev => new ExpenseVoucherDto
                {
                    ExpenseVoucherId = ev.ExpenseVoucherId,
                    VoucherNumber = ev.VoucherNumber,
                    VoucherDate = ev.VoucherDate,
                    Description = ev.Description,
                    TotalAmount = ev.TotalAmount,
                    Status = ev.Status,
                    ExpenseType = ev.ExpenseType,
                    CreatedBy = ev.CreatedBy,
                    CreatorName = ev.Creator != null ? ev.Creator.FirstName + " " + ev.Creator.LastName : null,
                    ApprovedBy = ev.ApprovedBy,
                    ApproverName = ev.Approver != null ? ev.Approver.FirstName + " " + ev.Approver.LastName : null,
                    ApprovedAt = ev.ApprovedAt,
                    CreatedAt = ev.CreatedAt,
                    UpdatedAt = ev.UpdatedAt,
                    Lines = ev.ExpenseVoucherLines.Select(line => new ExpenseVoucherLineDto
                    {
                        ExpenseVoucherLineId = line.ExpenseVoucherLineId,
                        ExpenseVoucherId = line.ExpenseVoucherId,
                        Description = line.Description,
                        Amount = line.Amount,
                        Reference = line.Reference,
                        ReferenceType = line.ReferenceType,
                        CreatedAt = line.CreatedAt
                    }).ToList()
                })
                .ToListAsync();

            return new ApiResponse<ExpenseVoucherResponse>
            {
                Success = true,
                Message = "Lấy danh sách phiếu chi thành công",
                Data = new ExpenseVoucherResponse
                {
                    ExpenseVouchers = expenseVouchers,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách phiếu chi",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ExpenseVoucherDto>> GetExpenseVoucherByIdAsync(long expenseVoucherId)
    {
        try
        {
            var expenseVoucher = await _context.ExpenseVouchers
                .Include(ev => ev.Creator)
                .Include(ev => ev.Approver)
                .Include(ev => ev.ExpenseVoucherLines)
                .FirstOrDefaultAsync(ev => ev.ExpenseVoucherId == expenseVoucherId);

            if (expenseVoucher == null)
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu chi",
                    Errors = new List<string> { "Expense voucher not found" }
                };
            }

            var result = new ExpenseVoucherDto
            {
                ExpenseVoucherId = expenseVoucher.ExpenseVoucherId,
                VoucherNumber = expenseVoucher.VoucherNumber,
                VoucherDate = expenseVoucher.VoucherDate,
                Description = expenseVoucher.Description,
                TotalAmount = expenseVoucher.TotalAmount,
                Status = expenseVoucher.Status,
                ExpenseType = expenseVoucher.ExpenseType,
                CreatedBy = expenseVoucher.CreatedBy,
                CreatorName = expenseVoucher.Creator != null ? expenseVoucher.Creator.FirstName + " " + expenseVoucher.Creator.LastName : null,
                ApprovedBy = expenseVoucher.ApprovedBy,
                ApproverName = expenseVoucher.Approver != null ? expenseVoucher.Approver.FirstName + " " + expenseVoucher.Approver.LastName : null,
                ApprovedAt = expenseVoucher.ApprovedAt,
                CreatedAt = expenseVoucher.CreatedAt,
                UpdatedAt = expenseVoucher.UpdatedAt,
                Lines = expenseVoucher.ExpenseVoucherLines.Select(line => new ExpenseVoucherLineDto
                {
                    ExpenseVoucherLineId = line.ExpenseVoucherLineId,
                    ExpenseVoucherId = line.ExpenseVoucherId,
                    Description = line.Description,
                    Amount = line.Amount,
                    Reference = line.Reference,
                    ReferenceType = line.ReferenceType,
                    CreatedAt = line.CreatedAt
                }).ToList()
            };

            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = true,
                Message = "Lấy thông tin phiếu chi thành công",
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = false,
                Message = "Lỗi khi lấy thông tin phiếu chi",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ExpenseVoucherDto>> ApproveExpenseVoucherAsync(ApproveExpenseVoucherDto request, long approvedBy)
    {
        try
        {
            var expenseVoucher = await _context.ExpenseVouchers
                .FirstOrDefaultAsync(ev => ev.ExpenseVoucherId == request.ExpenseVoucherId);

            if (expenseVoucher == null)
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu chi",
                    Errors = new List<string> { "Expense voucher not found" }
                };
            }

            if (expenseVoucher.Status != "PENDING")
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Chỉ có thể duyệt phiếu chi đang chờ duyệt",
                    Errors = new List<string> { "Only pending vouchers can be approved" }
                };
            }

            expenseVoucher.Status = "APPROVED";
            expenseVoucher.ApprovedBy = approvedBy;
            expenseVoucher.ApprovedAt = DateTime.UtcNow;
            expenseVoucher.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.ApprovalNote))
            {
                expenseVoucher.Description = string.IsNullOrEmpty(expenseVoucher.Description) 
                    ? request.ApprovalNote 
                    : expenseVoucher.Description + "\n" + request.ApprovalNote;
            }

            await _context.SaveChangesAsync();

            return await GetExpenseVoucherByIdAsync(expenseVoucher.ExpenseVoucherId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = false,
                Message = "Lỗi khi duyệt phiếu chi",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ExpenseVoucherDto>> RejectExpenseVoucherAsync(RejectExpenseVoucherDto request, long rejectedBy)
    {
        try
        {
            var expenseVoucher = await _context.ExpenseVouchers
                .FirstOrDefaultAsync(ev => ev.ExpenseVoucherId == request.ExpenseVoucherId);

            if (expenseVoucher == null)
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu chi",
                    Errors = new List<string> { "Expense voucher not found" }
                };
            }

            if (expenseVoucher.Status != "PENDING")
            {
                return new ApiResponse<ExpenseVoucherDto>
                {
                    Success = false,
                    Message = "Chỉ có thể từ chối phiếu chi đang chờ duyệt",
                    Errors = new List<string> { "Only pending vouchers can be rejected" }
                };
            }

            expenseVoucher.Status = "REJECTED";
            expenseVoucher.ApprovedBy = rejectedBy;
            expenseVoucher.ApprovedAt = DateTime.UtcNow;
            expenseVoucher.UpdatedAt = DateTime.UtcNow;

            expenseVoucher.Description = string.IsNullOrEmpty(expenseVoucher.Description) 
                ? $"Từ chối: {request.RejectionReason}" 
                : expenseVoucher.Description + $"\nTừ chối: {request.RejectionReason}";

            await _context.SaveChangesAsync();

            return await GetExpenseVoucherByIdAsync(expenseVoucher.ExpenseVoucherId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = false,
                Message = "Lỗi khi từ chối phiếu chi",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ExpenseVoucherDto>> CreateReturnRefundVoucherAsync(long returnId, decimal totalAmount, long createdBy)
    {
        try
        {
            var voucherNumber = await GenerateVoucherNumberAsync();

            var expenseVoucher = new ExpenseVoucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = DateTime.UtcNow,
                Description = $"Hoàn tiền cho phiếu trả #{returnId}",
                TotalAmount = totalAmount,
                Status = "APPROVED", // Auto-approve for return refunds
                ExpenseType = "RETURN_REFUND",
                CreatedBy = createdBy,
                ApprovedBy = createdBy,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ExpenseVouchers.Add(expenseVoucher);
            await _context.SaveChangesAsync();

            // Add line for return refund
            var line = new ExpenseVoucherLine
            {
                ExpenseVoucherId = expenseVoucher.ExpenseVoucherId,
                Description = $"Hoàn tiền phiếu trả #{returnId}",
                Amount = totalAmount,
                Reference = returnId.ToString(),
                ReferenceType = "RETURN",
                CreatedAt = DateTime.UtcNow
            };
            _context.ExpenseVoucherLines.Add(line);
            await _context.SaveChangesAsync();

            return await GetExpenseVoucherByIdAsync(expenseVoucher.ExpenseVoucherId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExpenseVoucherDto>
            {
                Success = false,
                Message = "Lỗi khi tạo phiếu chi hoàn tiền",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task<string> GenerateVoucherNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"PC{today:yyyyMMdd}";
        
        var lastVoucher = await _context.ExpenseVouchers
            .Where(ev => ev.VoucherNumber.StartsWith(prefix))
            .OrderByDescending(ev => ev.VoucherNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastVoucher != null)
        {
            var lastSequence = lastVoucher.VoucherNumber.Substring(prefix.Length);
            if (int.TryParse(lastSequence, out int lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}{sequence:D4}";
    }
}
