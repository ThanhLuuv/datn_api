using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly BookStoreDbContext _context;

    public PurchaseOrderService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PurchaseOrderListResponse>> GetPurchaseOrdersAsync(PurchaseOrderSearchRequest searchRequest)
    {
        try
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .AsQueryable();

            // Apply search filters
            if (searchRequest.PublisherId.HasValue)
            {
                query = query.Where(po => po.PublisherId == searchRequest.PublisherId.Value);
            }

            if (searchRequest.CreatedBy.HasValue)
            {
                query = query.Where(po => po.CreatedBy == searchRequest.CreatedBy.Value);
            }

            if (searchRequest.FromDate.HasValue)
            {
                query = query.Where(po => po.OrderedAt >= searchRequest.FromDate.Value);
            }

            if (searchRequest.ToDate.HasValue)
            {
                query = query.Where(po => po.OrderedAt <= searchRequest.ToDate.Value);
            }

            // Apply sorting
            query = searchRequest.SortBy?.ToLower() switch
            {
                "orderedat" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(po => po.OrderedAt) 
                    : query.OrderBy(po => po.OrderedAt),
                "publisher" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(po => po.Publisher.Name) 
                    : query.OrderBy(po => po.Publisher.Name),
                "total" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(po => po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice)) 
                    : query.OrderBy(po => po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice)),
                _ => query.OrderByDescending(po => po.OrderedAt)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / searchRequest.PageSize);

            var purchaseOrders = await query
                .Skip((searchRequest.PageNumber - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(po => new PurchaseOrderDto
                {
                    PoId = po.PoId,
                    PublisherId = po.PublisherId,
                    PublisherName = po.Publisher.Name,
                    OrderedAt = po.OrderedAt,
                    CreatedBy = po.CreatedBy,
                    CreatedByName = po.CreatedByEmployee.FirstName + " " + po.CreatedByEmployee.LastName,
                    Note = po.Note,
                    TotalAmount = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice),
                    TotalQuantity = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered),
                    Lines = po.PurchaseOrderLines.Select(pol => new PurchaseOrderLineDto
                    {
                        PoLineId = pol.PoLineId,
                        Isbn = pol.Isbn,
                        BookTitle = pol.Book.Title,
                        QtyOrdered = pol.QtyOrdered,
                        UnitPrice = pol.UnitPrice,
                        LineTotal = pol.QtyOrdered * pol.UnitPrice
                    }).ToList()
                })
                .ToListAsync();

            var response = new PurchaseOrderListResponse
            {
                PurchaseOrders = purchaseOrders,
                TotalCount = totalCount,
                PageNumber = searchRequest.PageNumber,
                PageSize = searchRequest.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<PurchaseOrderListResponse>
            {
                Success = true,
                Message = "Lấy danh sách đơn đặt mua thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PurchaseOrderListResponse>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(long poId)
    {
        try
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .Where(po => po.PoId == poId)
                .Select(po => new PurchaseOrderDto
                {
                    PoId = po.PoId,
                    PublisherId = po.PublisherId,
                    PublisherName = po.Publisher.Name,
                    OrderedAt = po.OrderedAt,
                    CreatedBy = po.CreatedBy,
                    CreatedByName = po.CreatedByEmployee.FirstName + " " + po.CreatedByEmployee.LastName,
                    Note = po.Note,
                    TotalAmount = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice),
                    TotalQuantity = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered),
                    Lines = po.PurchaseOrderLines.Select(pol => new PurchaseOrderLineDto
                    {
                        PoLineId = pol.PoLineId,
                        Isbn = pol.Isbn,
                        BookTitle = pol.Book.Title,
                        QtyOrdered = pol.QtyOrdered,
                        UnitPrice = pol.UnitPrice,
                        LineTotal = pol.QtyOrdered * pol.UnitPrice
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (purchaseOrder == null)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Không tìm thấy đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua không tồn tại" }
                };
            }

            return new ApiResponse<PurchaseOrderDto>
            {
                Success = true,
                Message = "Lấy thông tin đơn đặt mua thành công",
                Data = purchaseOrder
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy thông tin đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto createPurchaseOrderDto, long createdBy)
    {
        try
        {
            // Validate publisher exists
            var publisher = await _context.Publishers.FindAsync(createPurchaseOrderDto.PublisherId);
            if (publisher == null)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Nhà xuất bản không tồn tại",
                    Errors = new List<string> { "Nhà xuất bản được chọn không tồn tại" }
                };
            }

            // Validate employee exists
            var employee = await _context.Employees.FindAsync(createdBy);
            if (employee == null)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại",
                    Errors = new List<string> { "Nhân viên không tồn tại" }
                };
            }

            // Validate books exist
            var isbns = createPurchaseOrderDto.Lines.Select(l => l.Isbn).Distinct().ToList();
            var existingBooks = await _context.Books
                .Where(b => isbns.Contains(b.Isbn))
                .Select(b => b.Isbn)
                .ToListAsync();

            var missingIsbns = isbns.Except(existingBooks).ToList();
            if (missingIsbns.Any())
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Sách không tồn tại",
                    Errors = new List<string> { $"Các sách với ISBN {string.Join(", ", missingIsbns)} không tồn tại" }
                };
            }

            var purchaseOrder = new PurchaseOrder
            {
                PublisherId = createPurchaseOrderDto.PublisherId,
                OrderedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = createPurchaseOrderDto.Note
            };

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            // Add purchase order lines
            foreach (var lineDto in createPurchaseOrderDto.Lines)
            {
                var purchaseOrderLine = new PurchaseOrderLine
                {
                    PoId = purchaseOrder.PoId,
                    Isbn = lineDto.Isbn,
                    QtyOrdered = lineDto.QtyOrdered,
                    UnitPrice = lineDto.UnitPrice
                };

                _context.PurchaseOrderLines.Add(purchaseOrderLine);
            }

            await _context.SaveChangesAsync();

            // Load the created purchase order with related data
            var createdPurchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .Where(po => po.PoId == purchaseOrder.PoId)
                .Select(po => new PurchaseOrderDto
                {
                    PoId = po.PoId,
                    PublisherId = po.PublisherId,
                    PublisherName = po.Publisher.Name,
                    OrderedAt = po.OrderedAt,
                    CreatedBy = po.CreatedBy,
                    CreatedByName = po.CreatedByEmployee.FirstName + " " + po.CreatedByEmployee.LastName,
                    Note = po.Note,
                    TotalAmount = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice),
                    TotalQuantity = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered),
                    Lines = po.PurchaseOrderLines.Select(pol => new PurchaseOrderLineDto
                    {
                        PoLineId = pol.PoLineId,
                        Isbn = pol.Isbn,
                        BookTitle = pol.Book.Title,
                        QtyOrdered = pol.QtyOrdered,
                        UnitPrice = pol.UnitPrice,
                        LineTotal = pol.QtyOrdered * pol.UnitPrice
                    }).ToList()
                })
                .FirstAsync();

            return new ApiResponse<PurchaseOrderDto>
            {
                Success = true,
                Message = "Tạo đơn đặt mua thành công",
                Data = createdPurchaseOrder
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(long poId, UpdatePurchaseOrderDto updatePurchaseOrderDto)
    {
        try
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderLines)
                .FirstOrDefaultAsync(po => po.PoId == poId);

            if (purchaseOrder == null)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Không tìm thấy đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua không tồn tại" }
                };
            }

            // Check if purchase order has goods receipt (cannot modify)
            var hasGoodsReceipt = await _context.GoodsReceipts
                .AnyAsync(gr => gr.PoId == poId);

            if (hasGoodsReceipt)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Không thể cập nhật đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua đã có phiếu nhập, không thể chỉnh sửa" }
                };
            }

            // Validate books exist
            var isbns = updatePurchaseOrderDto.Lines.Select(l => l.Isbn).Distinct().ToList();
            var existingBooks = await _context.Books
                .Where(b => isbns.Contains(b.Isbn))
                .Select(b => b.Isbn)
                .ToListAsync();

            var missingIsbns = isbns.Except(existingBooks).ToList();
            if (missingIsbns.Any())
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Sách không tồn tại",
                    Errors = new List<string> { $"Các sách với ISBN {string.Join(", ", missingIsbns)} không tồn tại" }
                };
            }

            // Update purchase order
            purchaseOrder.Note = updatePurchaseOrderDto.Note;

            // Update purchase order lines
            var existingLineIds = purchaseOrder.PurchaseOrderLines
                .Where(pol => pol.PoLineId > 0)
                .Select(pol => pol.PoLineId)
                .ToList();

            var newLineIds = updatePurchaseOrderDto.Lines
                .Where(l => l.PoLineId.HasValue)
                .Select(l => l.PoLineId.Value)
                .ToList();

            var linesToRemove = existingLineIds.Except(newLineIds).ToList();
            var linesToUpdate = updatePurchaseOrderDto.Lines
                .Where(l => l.PoLineId.HasValue && existingLineIds.Contains(l.PoLineId.Value))
                .ToList();
            var linesToAdd = updatePurchaseOrderDto.Lines
                .Where(l => !l.PoLineId.HasValue)
                .ToList();

            // Remove lines
            var linesToRemoveEntities = purchaseOrder.PurchaseOrderLines
                .Where(pol => linesToRemove.Contains(pol.PoLineId))
                .ToList();
            foreach (var line in linesToRemoveEntities)
            {
                _context.PurchaseOrderLines.Remove(line);
            }

            // Update existing lines
            foreach (var lineDto in linesToUpdate)
            {
                var line = purchaseOrder.PurchaseOrderLines
                    .First(pol => pol.PoLineId == lineDto.PoLineId.Value);
                line.Isbn = lineDto.Isbn;
                line.QtyOrdered = lineDto.QtyOrdered;
                line.UnitPrice = lineDto.UnitPrice;
            }

            // Add new lines
            foreach (var lineDto in linesToAdd)
            {
                var newLine = new PurchaseOrderLine
                {
                    PoId = purchaseOrder.PoId,
                    Isbn = lineDto.Isbn,
                    QtyOrdered = lineDto.QtyOrdered,
                    UnitPrice = lineDto.UnitPrice
                };
                _context.PurchaseOrderLines.Add(newLine);
            }

            await _context.SaveChangesAsync();

            // Load the updated purchase order with related data
            var updatedPurchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .Where(po => po.PoId == poId)
                .Select(po => new PurchaseOrderDto
                {
                    PoId = po.PoId,
                    PublisherId = po.PublisherId,
                    PublisherName = po.Publisher.Name,
                    OrderedAt = po.OrderedAt,
                    CreatedBy = po.CreatedBy,
                    CreatedByName = po.CreatedByEmployee.FirstName + " " + po.CreatedByEmployee.LastName,
                    Note = po.Note,
                    TotalAmount = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered * pol.UnitPrice),
                    TotalQuantity = po.PurchaseOrderLines.Sum(pol => pol.QtyOrdered),
                    Lines = po.PurchaseOrderLines.Select(pol => new PurchaseOrderLineDto
                    {
                        PoLineId = pol.PoLineId,
                        Isbn = pol.Isbn,
                        BookTitle = pol.Book.Title,
                        QtyOrdered = pol.QtyOrdered,
                        UnitPrice = pol.UnitPrice,
                        LineTotal = pol.QtyOrdered * pol.UnitPrice
                    }).ToList()
                })
                .FirstAsync();

            return new ApiResponse<PurchaseOrderDto>
            {
                Success = true,
                Message = "Cập nhật đơn đặt mua thành công",
                Data = updatedPurchaseOrder
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi cập nhật đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeletePurchaseOrderAsync(long poId)
    {
        try
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderLines)
                .FirstOrDefaultAsync(po => po.PoId == poId);

            if (purchaseOrder == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua không tồn tại" }
                };
            }

            // Check if purchase order has goods receipt (cannot delete)
            var hasGoodsReceipt = await _context.GoodsReceipts
                .AnyAsync(gr => gr.PoId == poId);

            if (hasGoodsReceipt)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không thể xóa đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua đã có phiếu nhập, không thể xóa" }
                };
            }

            _context.PurchaseOrders.Remove(purchaseOrder);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa đơn đặt mua thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi xóa đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
