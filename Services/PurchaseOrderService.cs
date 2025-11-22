using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using MailKit.Net.Smtp;
using MimeKit;
using System.IO;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BookStore.Api.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly BookStoreDbContext _context;
    private readonly IConfiguration _configuration;

    public PurchaseOrderService(BookStoreDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<ApiResponse<PurchaseOrderListResponse>> GetPurchaseOrdersAsync(PurchaseOrderSearchRequest searchRequest)
    {
        try
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.Status)
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

            if (searchRequest.StatusId.HasValue)
            {
                query = query.Where(po => po.StatusId == searchRequest.StatusId.Value);
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
                    StatusId = po.StatusId,
                    StatusName = po.Status != null ? po.Status.StatusName : null,
                    OrderFileUrl = po.OrderFileUrl,
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
                .Include(po => po.Status)
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
                    StatusId = po.StatusId,
                    StatusName = po.Status != null ? po.Status.StatusName : null,
                    OrderFileUrl = po.OrderFileUrl,
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

    public async Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto createPurchaseOrderDto, long createdByAccountId)
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
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.AccountId == createdByAccountId);
            if (employee == null)
            {
                // Auto-provision minimal employee for this account
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == createdByAccountId);
                if (account == null)
                {
                    return new ApiResponse<PurchaseOrderDto>
                    {
                        Success = false,
                        Message = "Tài khoản không tồn tại",
                        Errors = new List<string> { "Không tìm thấy tài khoản người dùng" }
                    };
                }

                var firstDeptId = await _context.Departments.Select(d => d.DepartmentId).FirstOrDefaultAsync();
                if (firstDeptId == 0)
                {
                    // create a default department if none exists
                    var dept = new Department { Name = "Kinh doanh", Description = "Phòng kinh doanh" };
                    _context.Departments.Add(dept);
                    await _context.SaveChangesAsync();
                    firstDeptId = dept.DepartmentId;
                }

                var nameParts = (account.Email ?? "user@local").Split('@')[0].Split('.', '-', '_');
                var firstName = nameParts.Length > 0 ? nameParts[0] : "User";
                var lastName = nameParts.Length > 1 ? nameParts[1] : "Admin";

                employee = new Employee
                {
                    AccountId = account.AccountId,
                    DepartmentId = firstDeptId,
                    FirstName = firstName,
                    LastName = lastName,
                    Gender = Gender.Male,
                    Email = account.Email
                };
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
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

            long? statusId = createPurchaseOrderDto.StatusId;
            if (!statusId.HasValue)
            {
                var pendingStatus = await _context.PurchaseOrderStatuses
                    .Where(s => s.StatusName == "Pending")
                    .Select(s => s.StatusId)
                    .FirstOrDefaultAsync();
                if (pendingStatus != 0)
                {
                    statusId = pendingStatus;
                }
            }

            var purchaseOrder = new PurchaseOrder
            {
                PublisherId = createPurchaseOrderDto.PublisherId,
                OrderedAt = DateTime.UtcNow,
                CreatedBy = employee.EmployeeId,
                Note = createPurchaseOrderDto.Note,
                StatusId = statusId
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
                .Include(po => po.Status)
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
                    StatusId = po.StatusId,
                    StatusName = po.Status != null ? po.Status.StatusName : null,
                    OrderFileUrl = po.OrderFileUrl,
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

    public async Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto createPurchaseOrderDto, string createdByEmail)
    {
        // Resolve account id by email then reuse existing method
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == createdByEmail);
        if (account == null)
        {
            return new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Tài khoản không tồn tại",
                Errors = new List<string> { "Không tìm thấy tài khoản người dùng" }
            };
        }
        return await CreatePurchaseOrderAsync(createPurchaseOrderDto, account.AccountId);
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
            purchaseOrder.StatusId = updatePurchaseOrderDto.StatusId ?? purchaseOrder.StatusId;

            // Update purchase order lines
            var existingLineIds = purchaseOrder.PurchaseOrderLines
                .Where(pol => pol.PoLineId > 0)
                .Select(pol => pol.PoLineId)
                .ToList();

            var newLineIds = updatePurchaseOrderDto.Lines
                .Where(l => l.PoLineId.HasValue)
                .Select(l => l.PoLineId!.Value)
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
                    .First(pol => pol.PoLineId == lineDto.PoLineId!.Value);
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
                .Include(po => po.Status)
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
                    StatusId = po.StatusId,
                    StatusName = po.Status != null ? po.Status.StatusName : null,
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

    public async Task<ApiResponse<PurchaseOrderDto>> ChangeStatusAsync(long poId, ChangePurchaseOrderStatusDto request)
    {
        try
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.Publisher)
                .Include(p => p.CreatedByEmployee)
                .Include(p => p.PurchaseOrderLines)
                    .ThenInclude(l => l.Book)
                .Include(p => p.Status)
                .FirstOrDefaultAsync(p => p.PoId == poId);

            if (po == null)
            {
                return new ApiResponse<PurchaseOrderDto>
                {
                    Success = false,
                    Message = "Không tìm thấy đơn đặt mua",
                    Errors = new List<string> { "Đơn đặt mua không tồn tại" }
                };
            }

            var oldStatusId = po.StatusId;
            po.StatusId = request.NewStatusId;
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                po.Note = request.Note;
            }

            await _context.SaveChangesAsync();

            // If transition 1 -> 2, generate Excel and email to publisher
            if (oldStatusId == 1 && request.NewStatusId == 2)
            {
                var excelBytes = GeneratePurchaseOrderExcel(po);
                var fileUrl = await UploadToCloudinaryAsync(excelBytes, $"PO_{po.PoId}.xlsx");
                po.OrderFileUrl = fileUrl;
                await _context.SaveChangesAsync();
                await SendEmailToPublisherWithAttachmentAsync(po, excelBytes);
            }

            // Return updated dto
            var dto = await _context.PurchaseOrders
                .Include(x => x.Publisher)
                .Include(x => x.CreatedByEmployee)
                .Include(x => x.Status)
                .Include(x => x.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .Where(x => x.PoId == poId)
                .Select(x => new PurchaseOrderDto
                {
                    PoId = x.PoId,
                    PublisherId = x.PublisherId,
                    PublisherName = x.Publisher.Name,
                    OrderedAt = x.OrderedAt,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByEmployee.FirstName + " " + x.CreatedByEmployee.LastName,
                    Note = x.Note,
                    StatusId = x.StatusId,
                    StatusName = x.Status != null ? x.Status.StatusName : null,
                    OrderFileUrl = x.OrderFileUrl,
                    TotalAmount = x.PurchaseOrderLines.Sum(l => l.QtyOrdered * l.UnitPrice),
                    TotalQuantity = x.PurchaseOrderLines.Sum(l => l.QtyOrdered),
                    Lines = x.PurchaseOrderLines.Select(l => new PurchaseOrderLineDto
                    {
                        PoLineId = l.PoLineId,
                        Isbn = l.Isbn,
                        BookTitle = l.Book.Title,
                        QtyOrdered = l.QtyOrdered,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.QtyOrdered * l.UnitPrice
                    }).ToList()
                })
                .FirstAsync();

            return new ApiResponse<PurchaseOrderDto>
            {
                Success = true,
                Message = "Cập nhật trạng thái thành công",
                Data = dto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi cập nhật trạng thái",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private byte[] GeneratePurchaseOrderExcel(PurchaseOrder po)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("PO");

        // Header
        ws.Cell(1, 1).Value = "CÔNG TY TNHH NHÀ SÁCH TA";
        ws.Range(1, 1, 1, 6).Merge().Style
            .Font.SetBold().Font.SetFontSize(16)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell(3, 1).Value = "PHIẾU ĐẶT HÀNG";
        ws.Range(3, 1, 3, 6).Merge().Style
            .Font.SetBold().Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Customer info (your store)
        ws.Cell(5, 1).Value = "Thông tin khách hàng:";
        ws.Cell(6, 1).Value = "Địa chỉ:";
        ws.Cell(7, 1).Value = "Mã số thuế:";
        ws.Cell(8, 1).Value = "Người lập đơn:";
        ws.Cell(9, 1).Value = "Email:";

        ws.Cell(6, 2).Value = "123 Đường Lê Lợi, Quận 1, TP.HCM";
        ws.Cell(7, 2).Value = "0301234567";
        ws.Cell(8, 2).Value = po.CreatedByEmployee.FirstName + " " + po.CreatedByEmployee.LastName;
        ws.Cell(9, 2).Value = po.CreatedByEmployee.Email ?? string.Empty;

        // Publisher info
        ws.Cell(5, 4).Value = "Thông tin nhà xuất bản:";
        ws.Cell(6, 4).Value = "Tên NCC:";
        ws.Cell(7, 4).Value = "Địa chỉ:";
        ws.Cell(8, 4).Value = "Ngày lập đơn:";
        ws.Cell(6, 5).Value = po.Publisher.Name;
        ws.Cell(7, 5).Value = po.Publisher.Address ?? string.Empty;
        ws.Cell(8, 5).Value = po.OrderedAt.ToString("d/M/yyyy");

        // Table header - compact 6 columns with no gaps
        var startRow = 11;
        ws.Cell(startRow, 1).Value = "STT";
        ws.Cell(startRow, 2).Value = "Tên sản phẩm";
        ws.Cell(startRow, 3).Value = "Đơn vị tính";
        ws.Cell(startRow, 4).Value = "Số lượng";
        ws.Cell(startRow, 5).Value = "Đơn giá (VND)";
        ws.Cell(startRow, 6).Value = "Thành tiền (VND)";

        // Style header (yellow background, bold, borders)
        var headerRange = ws.Range(startRow, 1, startRow, 6);
        headerRange.Style.Fill.SetBackgroundColor(XLColor.Yellow);
        headerRange.Style.Font.SetBold();
        headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Rows
        var row = startRow + 1;
        int index = 1;
        foreach (var l in po.PurchaseOrderLines)
        {
            ws.Cell(row, 1).Value = index++;
            ws.Cell(row, 2).Value = l.Book.Title;
            ws.Cell(row, 3).Value = "Cái"; // ĐVT mặc định
            ws.Cell(row, 4).Value = l.QtyOrdered;
            ws.Cell(row, 5).Value = l.UnitPrice;
            ws.Cell(row, 6).Value = l.QtyOrdered * l.UnitPrice;

            // Number formats and alignment
            ws.Cell(row, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";

            row++;
        }

        // Borders for data region
        var dataRange = ws.Range(startRow, 1, row - 1, 6);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Total
        var total = po.PurchaseOrderLines.Sum(l => l.QtyOrdered * l.UnitPrice);
        ws.Cell(row + 1, 5).Value = "Tổng tiền hàng:";
        ws.Cell(row + 1, 5).Style.Font.SetBold();
        ws.Cell(row + 1, 6).Value = total;
        ws.Cell(row + 1, 6).Style.NumberFormat.Format = "#,##0";
        ws.Cell(row + 1, 6).Style.Font.SetBold();

        // Payment + bank info (compact)
        var infoStart = row + 3;
        ws.Cell(infoStart, 1).Value = "Phương thức thanh toán:";
        ws.Cell(infoStart, 2).Value = "Chuyển khoản";
        ws.Cell(infoStart + 1, 1).Value = "Tài khoản ngân hàng:";
        ws.Cell(infoStart + 1, 2).Value = "123456789 - Ngân hàng ACB - CN TP.HCM";

        // Signatures
        var signStart = infoStart + 3;
        ws.Cell(signStart, 1).Value = "Người lập đơn:";
        ws.Cell(signStart, 4).Value = "Xác nhận của nhà xuất bản";
        ws.Range(signStart + 1, 1, signStart + 3, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Dashed;
        ws.Range(signStart + 1, 4, signStart + 3, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Dashed;

        // Auto size
        ws.Columns(1, 6).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task SendEmailToPublisherWithAttachmentAsync(PurchaseOrder po, byte[] attachmentBytes)
    {
        var emailTo = po.Publisher.Email;
        if (string.IsNullOrWhiteSpace(emailTo))
        {
            return; // No email to send
        }

        var host = _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(_configuration["Email:Smtp:Port"], out var p) ? p : 587;
        var user = _configuration["Email:Credentials:User"];
        var pass = (_configuration["Email:Credentials:Password"] ?? string.Empty).Replace(" ", string.Empty); // remove spaces

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            return; // Missing configuration
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("BookStore", user));
        message.To.Add(MailboxAddress.Parse(emailTo));
        message.Subject = $"Phiếu đặt hàng PO#{po.PoId}";

        var builder = new BodyBuilder
        {
            TextBody = "Kính gửi nhà xuất bản,\n\nĐính kèm phiếu đặt hàng.\n\nTrân trọng."
        };
        builder.Attachments.Add($"PO_{po.PoId}.xlsx", attachmentBytes, new ContentType("application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(user, pass);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    private async Task<string?> UploadToCloudinaryAsync(byte[] fileBytes, string fileName)
    {
        // Expect CLOUDINARY_URL in environment or appsettings
        var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL")
            ?? _configuration["Cloudinary:Url"];
        if (string.IsNullOrWhiteSpace(cloudinaryUrl))
        {
            return null;
        }

        var cloudinary = new Cloudinary(cloudinaryUrl);

        using var ms = new MemoryStream(fileBytes);
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, ms),
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false,
            Folder = "purchase_orders"
        };
        var result = await cloudinary.UploadAsync(uploadParams);
        if (result.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return result.SecureUrl?.ToString() ?? result.Url?.ToString();
        }
        return null;
    }
}
