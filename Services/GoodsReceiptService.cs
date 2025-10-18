using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly BookStoreDbContext _context;

    public GoodsReceiptService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<GoodsReceiptListResponse>> GetGoodsReceiptsAsync(GoodsReceiptSearchRequest searchRequest)
    {
        try
        {
            var query = _context.GoodsReceipts
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(gr => gr.CreatedByEmployee)
                .Include(gr => gr.GoodsReceiptLines)
                .AsQueryable();

            // Apply search filters
            if (searchRequest.PoId.HasValue)
            {
                query = query.Where(gr => gr.PoId == searchRequest.PoId.Value);
            }

            if (searchRequest.CreatedBy.HasValue)
            {
                query = query.Where(gr => gr.CreatedBy == searchRequest.CreatedBy.Value);
            }

            if (searchRequest.FromDate.HasValue)
            {
                query = query.Where(gr => gr.ReceivedAt >= searchRequest.FromDate.Value);
            }

            if (searchRequest.ToDate.HasValue)
            {
                query = query.Where(gr => gr.ReceivedAt <= searchRequest.ToDate.Value);
            }

            // Apply sorting
            query = searchRequest.SortBy?.ToLower() switch
            {
                "receivedat" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(gr => gr.ReceivedAt) 
                    : query.OrderBy(gr => gr.ReceivedAt),
                "publisher" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(gr => gr.PurchaseOrder.Publisher.Name) 
                    : query.OrderBy(gr => gr.PurchaseOrder.Publisher.Name),
                "total" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(gr => gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost)) 
                    : query.OrderBy(gr => gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost)),
                _ => query.OrderByDescending(gr => gr.ReceivedAt)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / searchRequest.PageSize);

            var goodsReceipts = await query
                .Skip((searchRequest.PageNumber - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(gr => new GoodsReceiptDto
                {
                    GrId = gr.GrId,
                    PoId = gr.PoId,
                    PurchaseOrderInfo = $"PO-{gr.PoId} - {gr.PurchaseOrder.Publisher.Name}",
                    ReceivedAt = gr.ReceivedAt,
                    CreatedBy = gr.CreatedBy,
                    CreatedByName = gr.CreatedByEmployee.FirstName + " " + gr.CreatedByEmployee.LastName,
                    Note = gr.Note,
                    TotalAmount = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost),
                    TotalQuantity = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived),
                    Lines = gr.GoodsReceiptLines.Select(grl => new GoodsReceiptLineDto
                    {
                        GrLineId = grl.GrLineId,
                        Isbn = "", // Will be loaded from purchase order lines
                        BookTitle = "", // Will be loaded from purchase order lines
                        QtyReceived = grl.QtyReceived,
                        UnitCost = grl.UnitCost,
                        LineTotal = grl.QtyReceived * grl.UnitCost
                    }).ToList()
                })
                .ToListAsync();

            // Load book information from purchase order lines
            foreach (var goodsReceipt in goodsReceipts)
            {
                var purchaseOrderLines = await _context.PurchaseOrderLines
                    .Include(pol => pol.Book)
                    .Where(pol => pol.PoId == goodsReceipt.PoId)
                    .ToListAsync();

                for (int i = 0; i < goodsReceipt.Lines.Count && i < purchaseOrderLines.Count; i++)
                {
                    var line = goodsReceipt.Lines[i];
                    var poLine = purchaseOrderLines[i];
                    line.Isbn = poLine.Isbn;
                    line.BookTitle = poLine.Book.Title;
                }
            }

            var response = new GoodsReceiptListResponse
            {
                GoodsReceipts = goodsReceipts,
                TotalCount = totalCount,
                PageNumber = searchRequest.PageNumber,
                PageSize = searchRequest.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<GoodsReceiptListResponse>
            {
                Success = true,
                Message = "Lấy danh sách phiếu nhập thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<GoodsReceiptListResponse>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách phiếu nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(long grId)
    {
        try
        {
            var goodsReceipt = await _context.GoodsReceipts
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(gr => gr.CreatedByEmployee)
                .Include(gr => gr.GoodsReceiptLines)
                .Where(gr => gr.GrId == grId)
                .Select(gr => new GoodsReceiptDto
                {
                    GrId = gr.GrId,
                    PoId = gr.PoId,
                    PurchaseOrderInfo = $"PO-{gr.PoId} - {gr.PurchaseOrder.Publisher.Name}",
                    ReceivedAt = gr.ReceivedAt,
                    CreatedBy = gr.CreatedBy,
                    CreatedByName = gr.CreatedByEmployee.FirstName + " " + gr.CreatedByEmployee.LastName,
                    Note = gr.Note,
                    TotalAmount = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost),
                    TotalQuantity = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived),
                    Lines = gr.GoodsReceiptLines.Select(grl => new GoodsReceiptLineDto
                    {
                        GrLineId = grl.GrLineId,
                        Isbn = "", // Will be loaded from purchase order lines
                        BookTitle = "", // Will be loaded from purchase order lines
                        QtyReceived = grl.QtyReceived,
                        UnitCost = grl.UnitCost,
                        LineTotal = grl.QtyReceived * grl.UnitCost
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (goodsReceipt == null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu nhập",
                    Errors = new List<string> { "Phiếu nhập không tồn tại" }
                };
            }

            // Load book information from purchase order lines
            var purchaseOrderLines = await _context.PurchaseOrderLines
                .Include(pol => pol.Book)
                .Where(pol => pol.PoId == goodsReceipt.PoId)
                .ToListAsync();

            for (int i = 0; i < goodsReceipt.Lines.Count && i < purchaseOrderLines.Count; i++)
            {
                var line = goodsReceipt.Lines[i];
                var poLine = purchaseOrderLines[i];
                line.Isbn = poLine.Isbn;
                line.BookTitle = poLine.Book.Title;
            }

            return new ApiResponse<GoodsReceiptDto>
            {
                Success = true,
                Message = "Lấy thông tin phiếu nhập thành công",
                Data = goodsReceipt
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy thông tin phiếu nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<GoodsReceiptDto>> CreateGoodsReceiptAsync(CreateGoodsReceiptDto createGoodsReceiptDto, long createdBy)
    {
        try
        {
            // Validate purchase order exists
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .FirstOrDefaultAsync(po => po.PoId == createGoodsReceiptDto.PoId);

            if (purchaseOrder == null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Đơn đặt mua không tồn tại",
                    Errors = new List<string> { "Đơn đặt mua được chọn không tồn tại" }
                };
            }

            // Check if goods receipt already exists for this purchase order
            var existingGoodsReceipt = await _context.GoodsReceipts
                .FirstOrDefaultAsync(gr => gr.PoId == createGoodsReceiptDto.PoId);

            if (existingGoodsReceipt != null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Phiếu nhập đã tồn tại",
                    Errors = new List<string> { "Đơn đặt mua này đã có phiếu nhập" }
                };
            }

            // Validate employee exists
            var employee = await _context.Employees.FindAsync(createdBy);
            if (employee == null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại",
                    Errors = new List<string> { "Nhân viên không tồn tại" }
                };
            }

            // Validate books exist in purchase order
            var purchaseOrderLines = await _context.PurchaseOrderLines
                .Where(pol => pol.PoId == createGoodsReceiptDto.PoId)
                .ToListAsync();

            if (purchaseOrderLines.Count != createGoodsReceiptDto.Lines.Count)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Số lượng dòng không khớp",
                    Errors = new List<string> { "Số lượng dòng trong phiếu nhập phải khớp với số lượng dòng trong đơn đặt mua" }
                };
            }

            var goodsReceipt = new GoodsReceipt
            {
                PoId = createGoodsReceiptDto.PoId,
                ReceivedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = createGoodsReceiptDto.Note
            };

            _context.GoodsReceipts.Add(goodsReceipt);
            await _context.SaveChangesAsync();

            // Add goods receipt lines and update stock by ordered lines order
            var poLines = purchaseOrder.PurchaseOrderLines.OrderBy(l => l.PoLineId).ToList();
            var affectedIsbns = new HashSet<string>();
            for (int i = 0; i < createGoodsReceiptDto.Lines.Count && i < poLines.Count; i++)
            {
                var lineDto = createGoodsReceiptDto.Lines[i];
                var poLine = poLines[i];
                var goodsReceiptLine = new GoodsReceiptLine
                {
                    GrId = goodsReceipt.GrId,
                    QtyReceived = lineDto.QtyReceived,
                    UnitCost = lineDto.UnitCost
                };
                _context.GoodsReceiptLines.Add(goodsReceiptLine);

                // Update book stock
                var book = poLine.Book;
                book.Stock += lineDto.QtyReceived;
                affectedIsbns.Add(poLine.Isbn);
            }
            await _context.SaveChangesAsync();

            // Auto-update purchase order status to 4 (Delivered)
            purchaseOrder.StatusId = 4;
            await _context.SaveChangesAsync();

            // Load the created goods receipt with related data
            var createdGoodsReceipt = await _context.GoodsReceipts
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(gr => gr.CreatedByEmployee)
                .Include(gr => gr.GoodsReceiptLines)
                .Where(gr => gr.GrId == goodsReceipt.GrId)
                .Select(gr => new GoodsReceiptDto
                {
                    GrId = gr.GrId,
                    PoId = gr.PoId,
                    PurchaseOrderInfo = $"PO-{gr.PoId} - {gr.PurchaseOrder.Publisher.Name}",
                    ReceivedAt = gr.ReceivedAt,
                    CreatedBy = gr.CreatedBy,
                    CreatedByName = gr.CreatedByEmployee.FirstName + " " + gr.CreatedByEmployee.LastName,
                    Note = gr.Note,
                    TotalAmount = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost),
                    TotalQuantity = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived),
                    Lines = gr.GoodsReceiptLines.Select(grl => new GoodsReceiptLineDto
                    {
                        GrLineId = grl.GrLineId,
                        Isbn = "", // Will be loaded from purchase order lines
                        BookTitle = "", // Will be loaded from purchase order lines
                        QtyReceived = grl.QtyReceived,
                        UnitCost = grl.UnitCost,
                        LineTotal = grl.QtyReceived * grl.UnitCost
                    }).ToList()
                })
                .FirstAsync();

            // Load book information from purchase order lines
            var createdPurchaseOrderLines = await _context.PurchaseOrderLines
                .Include(pol => pol.Book)
                .Where(pol => pol.PoId == createdGoodsReceipt.PoId)
                .ToListAsync();

            for (int i = 0; i < createdGoodsReceipt.Lines.Count && i < createdPurchaseOrderLines.Count; i++)
            {
                var line = createdGoodsReceipt.Lines[i];
                var poLine = createdPurchaseOrderLines[i];
                line.Isbn = poLine.Isbn;
                line.BookTitle = poLine.Book.Title;
            }

            // Recompute average price for affected ISBNs (after stock and receipts saved)
            foreach (var isbn in affectedIsbns)
            {
                await _context.Database.ExecuteSqlRawAsync("CALL SP_UpdateAveragePrice_Last4Receipts({0})", isbn);
            }

            return new ApiResponse<GoodsReceiptDto>
            {
                Success = true,
                Message = "Tạo phiếu nhập thành công",
                Data = createdGoodsReceipt
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo phiếu nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<GoodsReceiptDto>> UpdateGoodsReceiptAsync(long grId, UpdateGoodsReceiptDto updateGoodsReceiptDto)
    {
        try
        {
            var goodsReceipt = await _context.GoodsReceipts
                .Include(gr => gr.GoodsReceiptLines)
                .FirstOrDefaultAsync(gr => gr.GrId == grId);

            if (goodsReceipt == null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu nhập",
                    Errors = new List<string> { "Phiếu nhập không tồn tại" }
                };
            }

            // Validate purchase order exists
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderLines)
                .FirstOrDefaultAsync(po => po.PoId == goodsReceipt.PoId);

            if (purchaseOrder == null)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Đơn đặt mua không tồn tại",
                    Errors = new List<string> { "Đơn đặt mua không tồn tại" }
                };
            }

            if (purchaseOrder.PurchaseOrderLines.Count != updateGoodsReceiptDto.Lines.Count)
            {
                return new ApiResponse<GoodsReceiptDto>
                {
                    Success = false,
                    Message = "Số lượng dòng không khớp",
                    Errors = new List<string> { "Số lượng dòng trong phiếu nhập phải khớp với số lượng dòng trong đơn đặt mua" }
                };
            }

            // Recalculate stock adjustments: remove old, add new
            // To do this accurately we need the related purchase order lines
            var poId = goodsReceipt.PoId;
            var poLines = await _context.PurchaseOrderLines
                .Include(pl => pl.Book)
                .Where(pl => pl.PoId == poId)
                .OrderBy(pl => pl.PoLineId)
                .ToListAsync();
            var affectedIsbns2 = new HashSet<string>(poLines.Select(pl => pl.Isbn));

            // Rollback previous receipt quantities from stock
            var oldLines = await _context.GoodsReceiptLines.Where(l => l.GrId == grId).ToListAsync();
            for (int i = 0; i < oldLines.Count && i < poLines.Count; i++)
            {
                poLines[i].Book.Stock -= oldLines[i].QtyReceived;
            }
            // Remove old lines
            _context.GoodsReceiptLines.RemoveRange(oldLines);

            // Add new lines and apply stock
            for (int i = 0; i < updateGoodsReceiptDto.Lines.Count && i < poLines.Count; i++)
            {
                var lineDto = updateGoodsReceiptDto.Lines[i];
                var newLine = new GoodsReceiptLine
                {
                    GrId = goodsReceipt.GrId,
                    QtyReceived = lineDto.QtyReceived,
                    UnitCost = lineDto.UnitCost
                };
                _context.GoodsReceiptLines.Add(newLine);
                poLines[i].Book.Stock += lineDto.QtyReceived;
            }

            goodsReceipt.Note = updateGoodsReceiptDto.Note;
            await _context.SaveChangesAsync();

            // Ensure purchase order is marked as 4 (Delivered) after update as well
            var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PoId == goodsReceipt.PoId);
            if (po != null)
            {
                po.StatusId = 4;
                await _context.SaveChangesAsync();
            }

            // Load the updated goods receipt with related data
            var updatedGoodsReceipt = await _context.GoodsReceipts
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(gr => gr.CreatedByEmployee)
                .Include(gr => gr.GoodsReceiptLines)
                .Where(gr => gr.GrId == grId)
                .Select(gr => new GoodsReceiptDto
                {
                    GrId = gr.GrId,
                    PoId = gr.PoId,
                    PurchaseOrderInfo = $"PO-{gr.PoId} - {gr.PurchaseOrder.Publisher.Name}",
                    ReceivedAt = gr.ReceivedAt,
                    CreatedBy = gr.CreatedBy,
                    CreatedByName = gr.CreatedByEmployee.FirstName + " " + gr.CreatedByEmployee.LastName,
                    Note = gr.Note,
                    TotalAmount = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived * grl.UnitCost),
                    TotalQuantity = gr.GoodsReceiptLines.Sum(grl => grl.QtyReceived),
                    Lines = gr.GoodsReceiptLines.Select(grl => new GoodsReceiptLineDto
                    {
                        GrLineId = grl.GrLineId,
                        Isbn = "", // Will be loaded from purchase order lines
                        BookTitle = "", // Will be loaded from purchase order lines
                        QtyReceived = grl.QtyReceived,
                        UnitCost = grl.UnitCost,
                        LineTotal = grl.QtyReceived * grl.UnitCost
                    }).ToList()
                })
                .FirstAsync();

            // Load book information from purchase order lines
            var updatedPurchaseOrderLines = await _context.PurchaseOrderLines
                .Include(pol => pol.Book)
                .Where(pol => pol.PoId == updatedGoodsReceipt.PoId)
                .ToListAsync();

            for (int i = 0; i < updatedGoodsReceipt.Lines.Count && i < updatedPurchaseOrderLines.Count; i++)
            {
                var line = updatedGoodsReceipt.Lines[i];
                var poLine = updatedPurchaseOrderLines[i];
                line.Isbn = poLine.Isbn;
                line.BookTitle = poLine.Book.Title;
            }

            // Recompute average price for affected ISBNs after update
            foreach (var isbn in affectedIsbns2)
            {
                await _context.Database.ExecuteSqlRawAsync("CALL SP_UpdateAveragePrice_Last4Receipts({0})", isbn);
            }

            return new ApiResponse<GoodsReceiptDto>
            {
                Success = true,
                Message = "Cập nhật phiếu nhập thành công",
                Data = updatedGoodsReceipt
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi cập nhật phiếu nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteGoodsReceiptAsync(long grId)
    {
        try
        {
            var goodsReceipt = await _context.GoodsReceipts
                .FirstOrDefaultAsync(gr => gr.GrId == grId);

            if (goodsReceipt == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu nhập",
                    Errors = new List<string> { "Phiếu nhập không tồn tại" }
                };
            }

            _context.GoodsReceipts.Remove(goodsReceipt);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa phiếu nhập thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi xóa phiếu nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<PurchaseOrderDto>>> GetAvailablePurchaseOrdersAsync()
    {
        try
        {
            // Get purchase orders that don't have goods receipts yet
            var availablePurchaseOrders = await _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .Where(po => !_context.GoodsReceipts.Any(gr => gr.PoId == po.PoId))
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
                .OrderByDescending(po => po.OrderedAt)
                .ToListAsync();

            return new ApiResponse<List<PurchaseOrderDto>>
            {
                Success = true,
                Message = "Lấy danh sách đơn đặt mua có thể tạo phiếu nhập thành công",
                Data = availablePurchaseOrders
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<PurchaseOrderDto>>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách đơn đặt mua",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
