## API: GET /api/book/latest

- **Chức năng**: Trả về danh sách sách mới nhất còn hàng, đang bật bán.
- **Controller**: `BookController.GetLatestBooks` gọi `_bookService.GetLatestBooksAsync(limit)`.

### Vị trí code
- Controller: `Controllers/BookController.cs` – `GetLatestBooks([FromQuery] int limit = 10)`, cho phép anonymous.
- Service: `Services/BookService.cs` – `GetLatestBooksAsync(int limit = 10)`.
- Hàm phụ tính giá: `GetCurrentPriceAsync(string isbn, DateTime? asOfDate = null)` lấy giá mới nhất từ `PriceChanges`, fallback `AveragePrice`.

### Luồng xử lý chính
1. Nhận query `limit` (mặc định 10) để giới hạn số sách.
2. Service truy vấn `Books` kèm `Category`, `Publisher`, `AuthorBooks/Author`, `BookPromotions/Promotion`.
3. Lọc chỉ sách `Stock > 0` và `Status == true`.
4. Sắp xếp theo `CreatedAt` giảm dần, `Take(limit)`.
5. Tính `CurrentPrice` từ bảng `PriceChanges` (nếu có giá mới) hoặc `AveragePrice`.
6. Nếu có khuyến mãi đang hiệu lực (`StartDate <= hôm nay <= EndDate`), áp dụng giảm tối đa để tính `DiscountedPrice`.
7. Trả về `ApiResponse<List<BookDto>>` (thông điệp thành công hoặc lỗi).

### Giải thích code (service)
- `Include` các bảng liên quan để dựng DTO đầy đủ (category, publisher, authors, promotions).
- `Where(b => b.Stock > 0 && b.Status == true)` đảm bảo còn hàng và đang kinh doanh.
- `OrderByDescending(b => b.CreatedAt).Take(limit)` lấy sách mới nhất.
- Mỗi bản ghi:
  - `currentPrice = GetCurrentPriceAsync(isbn)` lấy giá hiện tại theo lịch sử đổi giá.
  - Nếu có promo hiệu lực, lấy `maxDiscount` và tính `discountedPrice`.
  - Map sang `BookDto` gồm giá, thông tin sách, danh mục, NXB, tác giả, tồn kho, trạng thái, danh sách khuyến mãi.
- Nếu lỗi ném exception, bao `ApiResponse` với `Success=false`, `Errors`.

### Dữ liệu trả về (BookDto)
- Giá gốc (`AveragePrice`), giá hiện tại (`CurrentPrice`), giá sau giảm (`DiscountedPrice`), tồn kho, trạng thái.
- Thông tin danh mục, nhà XB, tác giả, danh sách khuyến mãi đang hoạt động.

### Mã liên quan
- `Controllers/BookController.cs` – action `GetLatestBooks`.
- `Services/BookService.cs` – method `GetLatestBooksAsync`.

