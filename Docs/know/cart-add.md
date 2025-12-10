## API: POST /api/cart/add

- **Chức năng**: Thêm sách vào giỏ hàng của khách đang đăng nhập.
- **Controller**: `Controllers/CartController.cs` → `AddToCart([FromBody] AddToCartRequestDto request)` (policy `PERM_WRITE_CART`).
- **Service**: `Services/CartService.cs` → `AddToCartAsync(long customerId, AddToCartRequestDto request)`.
- **DTO request**: `DTOs/CartDTOs.cs` → `AddToCartRequestDto { Isbn (required, ≤20), Quantity (1–999) }`.

### Luồng xử lý chi tiết
1. **Validate body** (`ModelState` ở controller). Sai → 400 với lỗi.
2. **Lấy customerId từ token**: Controller đọc claim `NameIdentifier`, gọi `_cartService.GetCustomerByAccountIdAsync`. Nếu không có → 401.
3. **Service kiểm tra sách**:
   - Tìm book theo ISBN; không thấy → `Success=false`, báo “Sách không tồn tại”.
   - Kiểm tra tồn kho: `book.Stock < request.Quantity` → báo “Không đủ tồn kho”.
4. **Lấy/tạo giỏ hàng**: tìm `Cart` theo `CustomerId`; nếu chưa có thì tạo mới (UTC timestamps).
5. **Gộp dữ liệu cũ (defensive)**: nếu có nhiều dòng cùng ISBN, gộp vào 1 item, xóa dòng dư.
6. **Thêm/cộng dồn**:
   - Nếu đã có item cùng ISBN → cộng `Quantity`, cập nhật `UpdatedAt`, kiểm tra không vượt `book.Stock`.
   - Nếu chưa có → tạo `CartItem` mới (Quantity, Isbn, timestamps).
7. **Lưu DB** (`SaveChangesAsync`), rồi nạp lại item kèm quan hệ (Book, Category, Publisher, Promotions) để map DTO.
8. **Map DTO** (`MapToCartItemDtoAsync`):
   - Tính `CurrentPrice` (giá mới nhất từ `PriceChanges` nếu có, else `AveragePrice`).
   - Tính `DiscountedPrice` (lấy promo hiệu lực % cao nhất, nếu có).
   - `TotalPrice = DiscountedPrice * Quantity`, kèm `HasPromotion`, `ActivePromotions`, tồn kho, ảnh, danh mục, NXB.
9. **Trả về** `ApiResponse<CartItemDto>` thành công; exception → `Success=false`, thông báo lỗi.

### Điểm quan trọng
- Yêu cầu đã đăng nhập và có quyền `PERM_WRITE_CART`.
- Luôn kiểm tra tồn kho trước và sau khi cộng dồn số lượng.
- Hợp nhất dữ liệu trùng ISBN để tránh cart bị nhân bản dòng.
- Giá hiển thị trong DTO là giá hiện tại/đã giảm tại thời điểm query, không phải lúc đặt hàng.


