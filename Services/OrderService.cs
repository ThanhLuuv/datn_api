using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MimeKit;

namespace BookStore.Api.Services;

public class OrderService : IOrderService
{
    private readonly BookStoreDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IInvoiceService _invoiceService;
    private readonly IExpenseService _expenseService;
    private readonly ICartService _cartService;
    private readonly IConfiguration _configuration;

    public OrderService(BookStoreDbContext context, IPaymentService paymentService, IInvoiceService invoiceService, ICartService cartService, IConfiguration configuration, IExpenseService expenseService)
    {
        _context = context;
        _paymentService = paymentService;
        _invoiceService = invoiceService;
        _cartService = cartService;
        _configuration = configuration;
        _expenseService = expenseService;
    }

    public async Task<ApiResponse<OrderListResponse>> GetOrdersAsync(OrderSearchRequest request)
    {
        try
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.ApprovedByEmployee)
                .Include(o => o.DeliveredByEmployee)
                .Include(o => o.OrderLines)
                    .ThenInclude(ol => ol.Book)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(o => o.ReceiverName.Contains(request.Keyword) || o.ReceiverPhone.Contains(request.Keyword));
            }
            if (request.CustomerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == request.CustomerId);
            }
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (Enum.TryParse<OrderStatus>(request.Status, true, out var st))
                {
                    query = query.Where(o => o.Status == st);
                }
            }
            if (request.FromDate.HasValue)
            {
                query = query.Where(o => o.PlacedAt >= request.FromDate.Value);
            }
            if (request.ToDate.HasValue)
            {
                query = query.Where(o => o.PlacedAt <= request.ToDate.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var orders = await query
                .OrderByDescending(o => o.PlacedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
                    PlacedAt = o.PlacedAt,
                    ReceiverName = o.ReceiverName,
                    ReceiverPhone = o.ReceiverPhone,
                    ShippingAddress = o.ShippingAddress,
                    DeliveryDate = o.DeliveryDate,
                    DeliveryAt = o.DeliveryAt,
                    Status = o.Status.ToString(),
                    Note = o.Note,
                    ApprovedBy = o.ApprovedBy,
                    ApprovedByName = o.ApprovedByEmployee != null ? o.ApprovedByEmployee.FirstName + " " + o.ApprovedByEmployee.LastName : null,
                    DeliveredBy = o.DeliveredBy,
                    DeliveredByName = o.DeliveredByEmployee != null ? o.DeliveredByEmployee.FirstName + " " + o.DeliveredByEmployee.LastName : null,
                    TotalAmount = o.OrderLines.Sum(l => l.Qty * l.UnitPrice),
                    TotalQuantity = o.OrderLines.Sum(l => l.Qty),
                    Lines = o.OrderLines.Select(l => new OrderLineDto
                    {
                        OrderLineId = l.OrderLineId,
                        Isbn = l.Isbn,
                        BookTitle = l.Book.Title,
                        Qty = l.Qty,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.Qty * l.UnitPrice,
                        Book = new BookSummaryDto
                        {
                            Isbn = l.Book.Isbn,
                            Title = l.Book.Title,
                            PageCount = l.Book.PageCount,
                            AveragePrice = l.Book.AveragePrice,
                            PublishYear = l.Book.PublishYear,
                            CategoryId = l.Book.CategoryId,
                            CategoryName = l.Book.Category.Name,
                            PublisherId = l.Book.PublisherId,
                            PublisherName = l.Book.Publisher.Name,
                            ImageUrl = l.Book.ImageUrl,
                            Stock = l.Book.Stock,
                            Status = l.Book.Status
                        }
                    }).ToList(),
                    Invoice = o.Invoices
                        .OrderByDescending(i => i.CreatedAt)
                        .Select(i => new OrderInvoiceDto
                        {
                            InvoiceId = i.InvoiceId,
                            InvoiceNumber = i.InvoiceNumber,
                            TotalAmount = i.TotalAmount,
                            TaxAmount = i.TaxAmount,
                            PaymentStatus = i.PaymentStatus,
                            PaymentMethod = i.PaymentMethod,
                            PaymentReference = i.PaymentReference,
                            PaidAt = i.PaidAt,
                            CreatedAt = i.CreatedAt,
                            UpdatedAt = i.UpdatedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var response = new OrderListResponse
            {
                Orders = orders,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<OrderListResponse> { Success = true, Message = "Lấy danh sách đơn hàng thành công", Data = response };
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderListResponse> { Success = false, Message = "Lỗi khi lấy danh sách đơn hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderDto>> GetOrderByIdAsync(long orderId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.ApprovedByEmployee)
                .Include(o => o.DeliveredByEmployee)
                .Include(o => o.OrderLines).ThenInclude(ol => ol.Book)
                .Include(o => o.Invoices)
                .Where(o => o.OrderId == orderId)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
                    PlacedAt = o.PlacedAt,
                    ReceiverName = o.ReceiverName,
                    ReceiverPhone = o.ReceiverPhone,
                    ShippingAddress = o.ShippingAddress,
                    DeliveryDate = o.DeliveryDate,
                    DeliveryAt = o.DeliveryAt,
                    Status = o.Status.ToString(),
                    Note = o.Note,
                    ApprovedBy = o.ApprovedBy,
                    ApprovedByName = o.ApprovedByEmployee != null ? o.ApprovedByEmployee.FirstName + " " + o.ApprovedByEmployee.LastName : null,
                    DeliveredBy = o.DeliveredBy,
                    DeliveredByName = o.DeliveredByEmployee != null ? o.DeliveredByEmployee.FirstName + " " + o.DeliveredByEmployee.LastName : null,
                    TotalAmount = o.OrderLines.Sum(l => l.Qty * l.UnitPrice),
                    TotalQuantity = o.OrderLines.Sum(l => l.Qty),
                    Lines = o.OrderLines.Select(l => new OrderLineDto
                    {
                        OrderLineId = l.OrderLineId,
                        Isbn = l.Isbn,
                        BookTitle = l.Book.Title,
                        Qty = l.Qty,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.Qty * l.UnitPrice,
                        Book = new BookSummaryDto
                        {
                            Isbn = l.Book.Isbn,
                            Title = l.Book.Title,
                            PageCount = l.Book.PageCount,
                            AveragePrice = l.Book.AveragePrice,
                            PublishYear = l.Book.PublishYear,
                            CategoryId = l.Book.CategoryId,
                            CategoryName = l.Book.Category.Name,
                            PublisherId = l.Book.PublisherId,
                            PublisherName = l.Book.Publisher.Name,
                            ImageUrl = l.Book.ImageUrl,
                            Stock = l.Book.Stock,
                            Status = l.Book.Status
                        }
                    }).ToList(),
                    Invoice = o.Invoices
                        .OrderByDescending(i => i.CreatedAt)
                        .Select(i => new OrderInvoiceDto
                        {
                            InvoiceId = i.InvoiceId,
                            InvoiceNumber = i.InvoiceNumber,
                            TotalAmount = i.TotalAmount,
                            TaxAmount = i.TaxAmount,
                            PaymentStatus = i.PaymentStatus,
                            PaymentMethod = i.PaymentMethod,
                            PaymentReference = i.PaymentReference,
                            PaidAt = i.PaidAt,
                            CreatedAt = i.CreatedAt,
                            UpdatedAt = i.UpdatedAt
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            return new ApiResponse<OrderDto> { Success = true, Message = "Lấy đơn hàng thành công", Data = order };
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto> { Success = false, Message = "Lỗi khi lấy đơn hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderDto>> ApproveOrderAsync(long orderId, ApproveOrderRequest request, string approverEmail)
    {
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            var approver = await _context.Employees.Include(e => e.Account).FirstOrDefaultAsync(e => e.Account.Email == approverEmail);
            if (approver == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy thông tin nhân viên duyệt" };
            }

            if (order.Status != OrderStatus.PendingConfirmation)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Đơn hàng chưa được xác nhận, không thể duyệt" };
            }

            // Kiểm tra thanh toán qua Invoice
            var hasPaid = await _invoiceService.HasPaidInvoiceAsync(orderId);
            if (!hasPaid)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Đơn hàng chưa thanh toán, không thể duyệt" };
            }

            if (request.Approved)
            {
                order.Status = OrderStatus.Confirmed; // Sau duyệt chuyển sang trạng thái đã xác nhận
                order.ApprovedBy = approver.EmployeeId;
            }
            else
            {
                // Nếu huỷ duyệt: có thể dùng một trạng thái khác, tạm thời giữ Pending và không tiếp tục
                return new ApiResponse<OrderDto> { Success = true, Message = "Đã đánh dấu không duyệt đơn" };
            }

            await _context.SaveChangesAsync();

            return await GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto> { Success = false, Message = "Lỗi khi duyệt đơn", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderDto>> AssignDeliveryAsync(long orderId, AssignDeliveryRequest request, string assignerEmail)
    {
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            if (order.Status != OrderStatus.PendingConfirmation)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Đơn hàng không ở trạng thái có thể phân công" };
            }

            var deliveryEmp = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.EmployeeId == request.DeliveryEmployeeId);
            if (deliveryEmp == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Nhân viên giao hàng không tồn tại" };
            }

            order.DeliveredBy = request.DeliveryEmployeeId;
            // Set full delivery datetime if provided; keep legacy date in sync
            if (request.DeliveryAt.HasValue)
            {
                order.DeliveryAt = request.DeliveryAt.Value;
                order.DeliveryDate = request.DeliveryAt.Value.Date;
            }
            else
            {
                order.DeliveryDate = request.DeliveryDate;
            }
            order.Status = OrderStatus.Confirmed; // Tự động confirm khi phân công delivery

            await _context.SaveChangesAsync();

            // Gửi email thông báo cho nhân viên giao hàng
            await SendDeliveryAssignmentEmailAsync(order, deliveryEmp);

            return await GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto> { Success = false, Message = "Lỗi khi phân công giao hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderDto>> ConfirmDeliveredAsync(long orderId, ConfirmDeliveredRequest request, string confirmerEmail)
    {
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            if (order.Status != OrderStatus.Confirmed)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Đơn hàng không ở trạng thái đang giao" };
            }

            if (!request.Success)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Xác nhận giao hàng thất bại" };
            }

            order.Status = OrderStatus.Delivered;
            await _context.SaveChangesAsync();

            return await GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto> { Success = false, Message = "Lỗi khi xác nhận giao hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderDto>> CancelOrderAsync(long orderId, CancelOrderRequest request, string cancellerEmail)
    {
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new ApiResponse<OrderDto> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            // Chỉ cho phép hủy đơn ở trạng thái PendingConfirmation hoặc Confirmed
            if (order.Status != OrderStatus.PendingConfirmation && order.Status != OrderStatus.Confirmed)
            {
                return new ApiResponse<OrderDto> 
                { 
                    Success = false, 
                    Message = "Không thể hủy đơn hàng ở trạng thái này. Chỉ có thể hủy đơn đang chờ xác nhận hoặc đã xác nhận." 
                };
            }

            // Kiểm tra nếu đơn đã giao thì không thể hủy
            if (order.Status == OrderStatus.Delivered)
            {
                return new ApiResponse<OrderDto> 
                { 
                    Success = false, 
                    Message = "Không thể hủy đơn hàng đã giao thành công" 
                };
            }

            // Chuyển trạng thái sang Cancelled
            order.Status = OrderStatus.Cancelled;
            
            // Lưu thông tin người hủy
            var canceller = await _context.Employees.Include(e => e.Account).FirstOrDefaultAsync(e => e.Account.Email == cancellerEmail);
            if (canceller != null)
            {
                order.ApprovedBy = canceller.EmployeeId; // Sử dụng ApprovedBy để lưu người hủy
            }

            // Lưu ghi chú hủy đơn vào cột note
            var cancellationInfo = $"CANCELLED - Reason: {request.Reason}";
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                cancellationInfo += $" | Note: {request.Note}";
            }
            order.Note = cancellationInfo;

            await _context.SaveChangesAsync();

            // Nếu đơn đã có hóa đơn và đã thanh toán -> tạo phiếu chi hoàn tiền và cập nhật trạng thái hóa đơn
            var paidInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId && i.PaymentStatus == "PAID");
            if (paidInvoice != null)
            {
                try
                {
                    // Tạo phiếu chi hoàn tiền cho đơn hàng
                    var createVoucher = new CreateExpenseVoucherDto
                    {
                        VoucherDate = DateTime.UtcNow,
                        Description = $"Hoàn tiền do hủy đơn hàng #{orderId}",
                        ExpenseType = "ORDER_REFUND",
                        Lines = new List<CreateExpenseVoucherLineDto>
                        {
                            new CreateExpenseVoucherLineDto
                            {
                                Description = $"Hoàn tiền đơn #{orderId}",
                                Amount = paidInvoice.TotalAmount,
                                Reference = orderId.ToString(),
                                ReferenceType = "ORDER"
                            }
                        }
                    };

                    // Lấy id nhân viên nếu có, nếu không truyền 0 (sẽ lưu được vì cột cho phép null trong DB model)
                    var voucherCreator = await _context.Employees.Include(e => e.Account).FirstOrDefaultAsync(e => e.Account.Email == cancellerEmail);
                    var createdById = voucherCreator?.EmployeeId ?? 0;
                    await _expenseService.CreateExpenseVoucherAsync(createVoucher, createdById);

                    // Cập nhật trạng thái hóa đơn -> REFUNDED
                    paidInvoice.PaymentStatus = "REFUNDED";
                    paidInvoice.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch { /* tránh làm hỏng luồng hủy đơn nếu tạo phiếu chi lỗi */ }
            }

            return await GetOrderByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto> { Success = false, Message = "Lỗi khi hủy đơn hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<OrderListResponse>> GetMyAssignedOrdersAsync(long deliveryEmployeeId, int pageNumber, int pageSize)
    {
        try
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _context.Orders
                .Include(o => o.OrderLines)
                    .ThenInclude(ol => ol.Book)
                .Include(o => o.Customer)
                .Include(o => o.ApprovedByEmployee)
                .Include(o => o.DeliveredByEmployee)
                .Where(o => o.DeliveredBy == deliveryEmployeeId)
                .OrderByDescending(o => o.PlacedAt)
                .AsQueryable();

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = items.Select(o => new OrderDto
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? (o.Customer.FirstName + " " + o.Customer.LastName) : string.Empty,
                PlacedAt = o.PlacedAt,
                ReceiverName = o.ReceiverName,
                ReceiverPhone = o.ReceiverPhone,
                ShippingAddress = o.ShippingAddress,
                DeliveryDate = o.DeliveryDate,
                DeliveryAt = o.DeliveryAt,
                Status = o.Status.ToString(),
                Note = o.Note,
                ApprovedBy = o.ApprovedBy,
                ApprovedByName = o.ApprovedByEmployee != null ? (o.ApprovedByEmployee.FirstName + " " + o.ApprovedByEmployee.LastName) : null,
                DeliveredBy = o.DeliveredBy,
                DeliveredByName = o.DeliveredByEmployee != null ? (o.DeliveredByEmployee.FirstName + " " + o.DeliveredByEmployee.LastName) : null,
                TotalAmount = o.TotalAmount,
                TotalQuantity = o.TotalQuantity,
                Lines = o.OrderLines.Select(ol => new OrderLineDto
                {
                    OrderLineId = ol.OrderLineId,
                    Isbn = ol.Isbn,
                    BookTitle = ol.Book.Title,
                    Qty = ol.Qty,
                    UnitPrice = ol.UnitPrice,
                    LineTotal = ol.Qty * ol.UnitPrice
                }).ToList()
            }).ToList();

            var response = new OrderListResponse
            {
                Orders = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return new ApiResponse<OrderListResponse>
            {
                Success = true,
                Message = "Lấy danh sách đơn được phân công thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderListResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách đơn được phân công",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<SuggestedEmployeeDto>>> GetDeliveryCandidatesAsync(long orderId)
    {
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return new ApiResponse<List<SuggestedEmployeeDto>> { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            // Chỉ lấy nhân viên có role là 4 (DELIVERY_EMPLOYEE)
            var employees = await _context.Employees
                .Include(e => e.EmployeeAreas)
                    .ThenInclude(ea => ea.Area)
                .Include(e => e.Account)
                    .ThenInclude(a => a.Role)
                .Where(e => e.Account.RoleId == 4) // Role 4 = DELIVERY_EMPLOYEE
                .ToListAsync();

            // Debug: Nếu không có nhân viên role 4, lấy tất cả nhân viên để test
            if (!employees.Any())
            {
                employees = await _context.Employees
                    .Include(e => e.EmployeeAreas)
                        .ThenInclude(ea => ea.Area)
                    .Include(e => e.Account)
                        .ThenInclude(a => a.Role)
                    .ToListAsync();
            }

            // Simple area matching by keyword contains in shipping address
            string addr = order.ShippingAddress.ToLowerInvariant();
            Func<string?, bool> match = k => !string.IsNullOrWhiteSpace(k) && addr.Contains(k.Trim().ToLowerInvariant());

            // Đếm đơn hàng đã phân công (status = 1) cho mỗi nhân viên
            var activeAssignedByEmp = await _context.Orders
                .Where(o => o.Status == OrderStatus.Confirmed && o.DeliveredBy != null)
                .GroupBy(o => o.DeliveredBy!.Value)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            // Đếm đơn hàng đã giao trong tháng (nếu hôm nay là ngày 1 thì tính tháng trước) cho mỗi nhân viên
            var today = DateTime.Today;
            var targetDate = today.Day == 1 ? today.AddMonths(-1) : today;
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var totalDeliveredByEmp = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered
                            && o.DeliveredBy != null
                            && o.DeliveryDate != null
                            && o.DeliveryDate >= monthStart
                            && o.DeliveryDate <= monthEnd)
                .GroupBy(o => o.DeliveredBy!.Value)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            var suggestions = new List<SuggestedEmployeeDto>();
            foreach (var e in employees)
            {
                bool isAreaMatched = false;
                var activeAreas = e.EmployeeAreas.Where(ea => ea.IsActive).Select(ea => ea.Area);
                foreach (var area in activeAreas)
                {
                    var kws = (area.Keywords ?? area.Name).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (kws.Any(k => match(k)))
                    {
                        isAreaMatched = true;
                        break;
                    }
                }

                var activeAssigned = activeAssignedByEmp.TryGetValue(e.EmployeeId, out var a) ? a : 0;
                var delivered = totalDeliveredByEmp.TryGetValue(e.EmployeeId, out var d) ? d : 0;

                var areaNames = string.Join(", ", activeAreas.Select(a => a.Name));
                suggestions.Add(new SuggestedEmployeeDto
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FullName,
                    Phone = e.Phone,
                    Email = e.Account.Email, // Lấy email từ Account thay vì Employee
                    AreaName = areaNames,
                    IsAreaMatched = isAreaMatched,
                    ActiveAssignedOrders = activeAssigned,
                    TotalDeliveredOrders = delivered
                });
            }

            var ordered = suggestions
                .OrderByDescending(s => s.IsAreaMatched)
                .ThenBy(s => s.ActiveAssignedOrders)
                .ThenByDescending(s => s.TotalDeliveredOrders)
                .ThenBy(s => s.EmployeeId)
                .ToList();
            return new ApiResponse<List<SuggestedEmployeeDto>> { Success = true, Message = "Gợi ý nhân viên giao hàng thành công", Data = ordered };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<SuggestedEmployeeDto>> { Success = false, Message = "Lỗi khi gợi ý nhân viên giao hàng", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<long?> GetEmployeeIdByEmailAsync(string email)
    {
        var employee = await _context.Employees.Include(e => e.Account).FirstOrDefaultAsync(e => e.Account.Email == email);
        return employee?.EmployeeId;
    }
    private static System.Linq.Expressions.Expression<Func<Order, OrderDto>> MapToOrderDtoSelector()
        => o => new OrderDto
        {
            OrderId = o.OrderId,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
            PlacedAt = o.PlacedAt,
            ReceiverName = o.ReceiverName,
            ReceiverPhone = o.ReceiverPhone,
            ShippingAddress = o.ShippingAddress,
            DeliveryDate = o.DeliveryDate,
            DeliveryAt = o.DeliveryAt,
            Status = o.Status.ToString(),
            Note = o.Note,
            ApprovedBy = o.ApprovedBy,
            ApprovedByName = o.ApprovedByEmployee != null ? o.ApprovedByEmployee.FirstName + " " + o.ApprovedByEmployee.LastName : null,
            DeliveredBy = o.DeliveredBy,
            DeliveredByName = o.DeliveredByEmployee != null ? o.DeliveredByEmployee.FirstName + " " + o.DeliveredByEmployee.LastName : null,
            TotalAmount = o.OrderLines.Sum(l => l.Qty * l.UnitPrice),
            TotalQuantity = o.OrderLines.Sum(l => l.Qty),
            Lines = o.OrderLines.Select(l => new OrderLineDto
            {
                OrderLineId = l.OrderLineId,
                Isbn = l.Isbn,
                BookTitle = l.Book.Title,
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                LineTotal = l.Qty * l.UnitPrice
            }).ToList()
        };

    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(CreateOrderDto createOrderDto, long customerId)
    {
        try
        {
            // Validate customer exists by account_id
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.AccountId == customerId);
            if (customer == null)
            {
                return new ApiResponse<OrderDto>
                {
                    Success = false,
                    Message = "Customer not found",
                    Errors = new List<string> { "Customer does not exist" }
                };
            }

            // Validate books exist and get current prices
            var isbns = createOrderDto.Lines.Select(l => l.Isbn).Distinct().ToList();
            var books = await _context.Books
                .Where(b => isbns.Contains(b.Isbn))
                .ToListAsync();

            var missingIsbns = isbns.Except(books.Select(b => b.Isbn)).ToList();
            if (missingIsbns.Any())
            {
                return new ApiResponse<OrderDto>
                {
                    Success = false,
                    Message = "Books not found",
                    Errors = new List<string> { $"Books with ISBN {string.Join(", ", missingIsbns)} do not exist" }
                };
            }

            // Get discounted prices for all books (apply active promotions if any)
            var bookPrices = new Dictionary<string, decimal>();
            foreach (var book in books)
            {
                var discountedPrice = await GetDiscountedPriceAsync(book.Isbn);
                bookPrices[book.Isbn] = discountedPrice;
            }

            // Create order
            var order = new Order
            {
                CustomerId = customer.CustomerId, // Use actual customer_id, not account_id
                PlacedAt = DateTime.UtcNow,
                ReceiverName = createOrderDto.ReceiverName,
                ReceiverPhone = createOrderDto.ReceiverPhone,
                ShippingAddress = createOrderDto.ShippingAddress,
                DeliveryAt = createOrderDto.DeliveryAt,
                Status = OrderStatus.PendingConfirmation // 0 - Chờ xác nhận
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Create order lines with discounted prices
            foreach (var lineDto in createOrderDto.Lines)
            {
                var currentPrice = bookPrices[lineDto.Isbn];
                var orderLine = new OrderLine
                {
                    OrderId = order.OrderId,
                    Isbn = lineDto.Isbn,
                    Qty = lineDto.Qty,
                    UnitPrice = currentPrice // Use discounted price at order time
                };

                _context.OrderLines.Add(orderLine);
            }

            await _context.SaveChangesAsync();

            // Calculate total amount
            var totalAmount = createOrderDto.Lines.Sum(line => 
                bookPrices[line.Isbn] * line.Qty);

            // Create payment link in same transaction
            var paymentRequest = new CreatePaymentLinkRequestDto
            {
                OrderId = order.OrderId,
                Amount = totalAmount,
                Currency = "VND",
                ReturnUrl = "http://127.0.0.1:3000/#!/payment/success",
                CancelUrl = "http://127.0.0.1:3000/#!/payment/cancel"
            };

            var paymentResult = await _paymentService.CreatePaymentLinkAsync(paymentRequest);
            if (!paymentResult.Success)
            {
                // Rollback: delete payment transaction first, then order
                var paymentTxn = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(pt => pt.OrderId == order.OrderId);
                if (paymentTxn != null)
                {
                    _context.PaymentTransactions.Remove(paymentTxn);
                }
                
                // Delete order lines first
                var orderLines = await _context.OrderLines
                    .Where(ol => ol.OrderId == order.OrderId)
                    .ToListAsync();
                _context.OrderLines.RemoveRange(orderLines);
                
                // Then delete order
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                
                return new ApiResponse<OrderDto>
                {
                    Success = false,
                    Message = "Failed to create payment link",
                    Errors = paymentResult.Errors
                };
            }

            // Clear cart after successful order creation
            try
            {
                await _cartService.ClearCartAsync(customer.CustomerId);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the order creation
                // Cart clearing is not critical for order success
                Console.WriteLine($"Warning: Failed to clear cart for customer {customer.CustomerId}: {ex.Message}");
            }

            // Return the created order with payment info
            var orderResult = await GetOrderByIdAsync(order.OrderId);
            if (orderResult.Success && orderResult.Data != null)
            {
                orderResult.Data.PaymentUrl = paymentResult.Data?.CheckoutUrl;
            }
            
            return orderResult;
        }
        catch (Exception ex)
        {
            return new ApiResponse<OrderDto>
            {
                Success = false,
                Message = "Error creating order",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task<decimal> GetCurrentPriceAsync(string isbn)
    {
        var currentPriceChange = await _context.PriceChanges
            .Where(pc => pc.Isbn == isbn && pc.ChangedAt <= DateTime.UtcNow)
            .OrderByDescending(pc => pc.ChangedAt)
            .FirstOrDefaultAsync();

        if (currentPriceChange != null)
        {
            return currentPriceChange.NewPrice;
        }

        // Fallback to average price from book table
        var book = await _context.Books
            .Where(b => b.Isbn == isbn)
            .Select(b => b.AveragePrice)
            .FirstOrDefaultAsync();

        return book;
    }

    private async Task<decimal> GetDiscountedPriceAsync(string isbn)
    {
        var currentPrice = await GetCurrentPriceAsync(isbn);

        // Find active promotion with highest discount for this ISBN
        var activePromotion = await _context.BookPromotions
            .Include(bp => bp.Promotion)
            .Where(bp => bp.Isbn == isbn
                        && bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow)
                        && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderByDescending(bp => bp.Promotion.DiscountPct)
            .FirstOrDefaultAsync();

        if (activePromotion != null)
        {
            var discountPct = activePromotion.Promotion.DiscountPct;
            var discounted = currentPrice * (1m - (discountPct / 100m));
            return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
        }

        return currentPrice;
    }

    public async Task<Customer?> GetCustomerByAccountIdAsync(long accountId)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.AccountId == accountId);
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
                    Message = "Không tìm thấy hóa đơn"
                };
            }

            var invoiceDto = new InvoiceDto
            {
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                InvoiceNumber = invoice.InvoiceNumber,
                TotalAmount = invoice.TotalAmount,
                TaxAmount = invoice.TaxAmount,
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
                Message = "Lấy thông tin hóa đơn thành công",
                Data = invoiceDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceDto>
            {
                Success = false,
                Message = "Lỗi khi lấy thông tin hóa đơn",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task SendDeliveryAssignmentEmailAsync(Order order, Employee deliveryEmployee)
    {
        try
        {
            var emailTo = deliveryEmployee.Account.Email;
            if (string.IsNullOrWhiteSpace(emailTo))
            {
                return; // No email to send
            }

            var host = _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            var port = int.TryParse(_configuration["Email:Smtp:Port"], out var p) ? p : 587;
            var user = _configuration["Email:Credentials:User"];
            var pass = (_configuration["Email:Credentials:Password"] ?? string.Empty).Replace(" ", string.Empty);

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                return; // Missing configuration
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("BookStore", user));
            message.To.Add(MailboxAddress.Parse(emailTo));
            message.Subject = $"Phân công giao hàng - Đơn hàng #{order.OrderId}";

            var deliveryWhen = order.DeliveryAt.HasValue
                ? order.DeliveryAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : (order.DeliveryDate?.ToLocalTime().ToString("dd/MM/yyyy") ?? "Chưa xác định");

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $@"Kính gửi anh/chị {deliveryEmployee.FullName},

Bạn đã được phân công giao hàng cho đơn hàng #{order.OrderId}.

Thông tin đơn hàng:
- Mã đơn hàng: #{order.OrderId}
- Khách hàng: {order.ReceiverName}
- Số điện thoại: {order.ReceiverPhone}
- Địa chỉ giao hàng: {order.ShippingAddress}
- Ngày giờ giao hàng dự kiến: {deliveryWhen}
- Tổng tiền: {order.TotalAmount:N0} VND
- Số lượng sản phẩm: {order.TotalQuantity}

Vui lòng liên hệ với khách hàng để xác nhận thời gian giao hàng phù hợp.

Trân trọng,
Hệ thống BookStore"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(user, pass);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the assignment
            Console.WriteLine($"Warning: Failed to send delivery assignment email: {ex.Message}");
        }
    }
}
