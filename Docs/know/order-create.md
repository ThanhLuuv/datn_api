## API: POST /api/order

- **Chức năng**: Tạo đơn hàng mới (kèm link thanh toán) cho khách đang đăng nhập.
- **Controller**: `Controllers/OrderController.cs` → `CreateOrder([FromBody] CreateOrderDto createOrderDto)` (policy `PERM_WRITE_ORDER`).
- **Service**: `Services/OrderService.cs` → `CreateOrderAsync(CreateOrderDto createOrderDto, long accountId)`.
- **DTO request**: `DTOs/OrderDTOs.cs` → `CreateOrderDto` gồm `ReceiverName`, `ReceiverPhone`, `ShippingAddress`, `Lines[] { Isbn, Qty }`, tùy chọn `DeliveryAt`.

### Luồng xử lý chi tiết
1. **Validate body** (`ModelState`) ở controller. Sai → 400 với danh sách lỗi.
2. **Xác định khách hàng**:
   - Controller lấy `accountId` từ claim `NameIdentifier`; thiếu → 401.
   - Service gọi `_context.Customers.FirstOrDefaultAsync(c => c.AccountId == accountId)`; không có → báo lỗi “Customer not found”.
3. **Kiểm tra sách & dữ liệu dòng**:
   - Lấy danh sách ISBN từ `Lines`, fetch tất cả `Books`. Nếu thiếu ISBN nào → trả lỗi “Books not found”.
   - Tính giá **đã áp dụng khuyến mãi** cho từng ISBN qua `GetDiscountedPriceAsync` (giá hiện tại từ `PriceChanges` + giảm % cao nhất trong promo hiệu lực).
4. **Tạo đơn**:
   - Tạo bản ghi `Order` với `CustomerId`, `PlacedAt=UtcNow`, thông tin người nhận/địa chỉ, `DeliveryAt`, trạng thái `PendingConfirmation`.
   - Lưu DB để có `OrderId`.
5. **Tạo dòng đơn**:
   - Với mỗi line: tạo `OrderLine` { Isbn, Qty, UnitPrice = discounted price tại thời điểm đặt }.
   - Lưu DB.
6. **Tính tổng tiền**: `totalAmount = Σ(discountedPrice * Qty)` dựa trên giá đã giảm ở bước 3.
7. **Tạo payment link**: gọi `_paymentService.CreatePaymentLinkAsync` với `OrderId`, `Amount`, `ReturnUrl/CancelUrl`.
   - Nếu tạo link thất bại: rollback (xóa PaymentTransaction nếu có, OrderLines, Order), trả lỗi.
8. **Dọn giỏ hàng**: cố gắng `ClearCartAsync(customer.CustomerId)`; nếu lỗi chỉ log cảnh báo, không fail đơn.
9. **Trả kết quả**:
   - Gọi `GetOrderByIdAsync` để lấy `OrderDto` đầy đủ; gắn thêm `PaymentUrl` từ payment service.
   - Thành công → `Success=true`, kèm thông tin đơn và link thanh toán; exception → `Success=false` với lỗi.

### Điểm quan trọng
- Bắt buộc đăng nhập, quyền `PERM_WRITE_ORDER`.
- Giá dùng cho đơn là **giá hiện hành sau khuyến mãi** tại thời điểm đặt; lưu vào `OrderLine.UnitPrice`.
- Xử lý rollback thủ công nếu tạo payment link lỗi.
- Sau khi tạo đơn, giỏ hàng của khách được xóa (best-effort).
- Trạng thái khởi tạo: `PendingConfirmation`; các bước duyệt/giao tiếp theo dùng API khác.


